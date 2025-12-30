using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using text_survival.Web.Dto;

namespace text_survival.Web;

/// <summary>
/// Manages a single WebSocket connection for one game session.
/// Handles sending frames and receiving player responses with reconnection support.
/// </summary>
public class WebGameSession : IDisposable
{
    private WebSocket _socket;
    private readonly BlockingCollection<PlayerResponse> _responses = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly object _socketLock = new();
    private readonly ManualResetEventSlim _reconnectEvent = new(true);
    private WebFrame? _lastSentFrame = null;  // Cache for reconnection
    private int _nextInputId = 1;  // Sequential input ID counter
    private int _currentInputId = 0;  // The ID we're currently waiting for

    // Separate JSON options for web API - no ReferenceHandler needed
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public WebGameSession(WebSocket socket)
    {
        _socket = socket;
    }

    public bool IsConnected
    {
        get
        {
            lock (_socketLock)
            {
                return _socket.State == WebSocketState.Open;
            }
        }
    }

    public CancellationToken CancellationToken => _cts.Token;

    /// <summary>
    /// Generate a unique input ID for the next input request.
    /// </summary>
    public int GenerateInputId()
    {
        return Interlocked.Increment(ref _nextInputId);
    }

    /// <summary>
    /// Send a frame to the client. Blocks until send completes.
    /// </summary>
    public void Send(WebFrame frame)
    {
        var json = JsonSerializer.Serialize(frame, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Cache frame for potential resend on reconnection
        lock (_socketLock)
        {
            _lastSentFrame = frame;

            if (_socket.State != WebSocketState.Open)
            {
                // Wait for reconnection (up to 30 seconds)
                _reconnectEvent.Reset();
            }
        }

        // Wait for reconnection if disconnected
        if (!_reconnectEvent.Wait(TimeSpan.FromSeconds(30), _cts.Token))
        {
            throw new OperationCanceledException("Connection lost and reconnection timed out");
        }

        lock (_socketLock)
        {
            if (_socket.State == WebSocketState.Open)
            {
                _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token
                ).GetAwaiter().GetResult();
            }
        }
    }

    /// <summary>
    /// Wait for a player response. Blocks until response received or timeout.
    /// Validates that the response's input ID matches the expected ID.
    /// </summary>
    public PlayerResponse WaitForResponse(int expectedInputId, TimeSpan timeout)
    {
        _currentInputId = expectedInputId;

        // Drain stale responses AND responses with wrong input IDs
        while (_responses.TryTake(out var staleResponse))
        {
            // Log discarded responses for debugging
            if (staleResponse.InputId != expectedInputId)
            {
                Console.WriteLine($"[WebGameSession] Discarded stale response with inputId={staleResponse.InputId}, expected={expectedInputId}");
            }
        }

        try
        {
            // Wait for response with matching input ID
            while (true)
            {
                if (_responses.TryTake(out var response, (int)timeout.TotalMilliseconds, _cts.Token))
                {
                    if (response.InputId == expectedInputId)
                    {
                        return response;  // Valid response
                    }
                    else
                    {
                        Console.WriteLine($"[WebGameSession] Discarded late response with inputId={response.InputId}, expected={expectedInputId}");
                        continue;  // Discard and keep waiting
                    }
                }

                // Timeout occurred
                lock (_socketLock)
                {
                    // If disconnected, throw instead of returning default
                    // This prevents silent continuation while player is disconnected
                    if (_socket.State != WebSocketState.Open)
                    {
                        throw new OperationCanceledException("Response timeout during disconnection");
                    }
                }

                // Connected timeout - return default selection (player chose not to respond)
                return new PlayerResponse(null, expectedInputId);
            }
        }
        catch (OperationCanceledException)
        {
            throw;  // Propagate cancellation to stop the game loop
        }
    }

    /// <summary>
    /// Enqueue a response from the client (called by receive loop).
    /// </summary>
    public void EnqueueResponse(PlayerResponse response)
    {
        _responses.Add(response, _cts.Token);
    }

    /// <summary>
    /// Handle socket reconnection (new socket from reconnecting client).
    /// </summary>
    public void Reconnect(WebSocket newSocket)
    {
        lock (_socketLock)
        {
            _socket = newSocket;
            _reconnectEvent.Set();

            // Resend last frame to sync reconnected client
            if (_lastSentFrame != null && _socket.State == WebSocketState.Open)
            {
                var json = JsonSerializer.Serialize(_lastSentFrame, JsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);

                _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                ).GetAwaiter().GetResult();
            }
        }
    }

    /// <summary>
    /// Mark connection as disconnected (triggers reconnect wait in Send).
    /// Only resets the event if the socket is actually disconnected - prevents
    /// race condition where reconnect is undone by stale disconnect notification.
    /// </summary>
    public void MarkDisconnected()
    {
        lock (_socketLock)
        {
            // Only reset if actually disconnected - if we've reconnected, don't reset
            if (_socket.State != WebSocketState.Open)
            {
                _reconnectEvent.Reset();
            }
        }
    }

    /// <summary>
    /// Notify that the socket has disconnected (alias for MarkDisconnected).
    /// </summary>
    public void NotifyDisconnect() => MarkDisconnected();

    /// <summary>
    /// Receive loop - reads messages and enqueues responses. Run on background task.
    /// </summary>
    public async Task ReceiveLoopAsync()
    {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                WebSocket socket;
                lock (_socketLock)
                {
                    socket = _socket;
                }

                if (socket.State != WebSocketState.Open)
                {
                    await Task.Delay(100, _cts.Token);
                    continue;
                }

                try
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        MarkDisconnected();
                        continue;
                    }

                    messageBuffer.AddRange(buffer.Take(result.Count));

                    if (result.EndOfMessage)
                    {
                        var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        messageBuffer.Clear();

                        var response = JsonSerializer.Deserialize<PlayerResponse>(json, JsonOptions);
                        if (response != null)
                        {
                            EnqueueResponse(response);
                        }
                    }
                }
                catch (WebSocketException)
                {
                    MarkDisconnected();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    /// <summary>
    /// Cancel the session (game over or disconnect timeout).
    /// </summary>
    public void Cancel()
    {
        _cts.Cancel();
        _reconnectEvent.Set(); // Unblock any waiting Send calls
    }

    /// <summary>
    /// Wait until the session completes (game over or session cancelled).
    /// Used by reconnecting handlers to keep the WebSocket connection alive.
    /// </summary>
    public async Task WaitForCompletionAsync()
    {
        try
        {
            // Wait indefinitely until the session is cancelled
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Session was cancelled (game over or timeout) - this is expected
        }
    }

    /// <summary>
    /// Close the WebSocket connection gracefully.
    /// </summary>
    public void CloseAsync()
    {
        Cancel();

        lock (_socketLock)
        {
            if (_socket.State == WebSocketState.Open)
            {
                try
                {
                    _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Game ended",
                        CancellationToken.None
                    ).GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore close errors
                }
            }
        }
    }

    /// <summary>
    /// Dispose the session, clearing response queue and canceling operations.
    /// </summary>
    public void Dispose()
    {
        _responses.CompleteAdding();
        while (_responses.TryTake(out _)) { }  // Clear queue
        _cts.Cancel();
        _reconnectEvent.Dispose();
        _responses.Dispose();
        _cts.Dispose();
    }
}
