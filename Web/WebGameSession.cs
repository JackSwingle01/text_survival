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
public class WebGameSession
{
    private WebSocket _socket;
    private readonly BlockingCollection<PlayerResponse> _responses = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly object _socketLock = new();
    private readonly ManualResetEventSlim _reconnectEvent = new(true);
    private WebFrame? _lastSentFrame = null;  // Cache for reconnection

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
    /// </summary>
    public PlayerResponse WaitForResponse(TimeSpan timeout)
    {
        // Drain any stale responses that arrived before this input was requested
        // This prevents old clicks from being consumed by new input prompts
        while (_responses.TryTake(out _)) { }

        try
        {
            if (_responses.TryTake(out var response, (int)timeout.TotalMilliseconds, _cts.Token))
            {
                return response;
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
            return new PlayerResponse(null);
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
    /// </summary>
    public void MarkDisconnected()
    {
        lock (_socketLock)
        {
            _reconnectEvent.Reset();
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
}
