using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace text_survival.GameCli;

public sealed record CliWorker(string Session, string Run, int Pid, long StartedTicks);
public sealed record CliWorkerState(string Status, long Revision, bool SnapshotCurrent, JsonElement? Snapshot = null, string? RequestId = null, string? Error = null);
public sealed record CliRequest(string Id, long Sequence, string Command, string? Value, long? Revision);

/// <summary>File-backed command transport. Clients only read/write atomic JSON files and
/// exit; one background process owns the game's single-threaded scheduler and call stack.</summary>
public sealed class BackgroundCli(string root)
{
    [DllImport("libc", SetLastError = true)]
    private static extern int setsid();

    private readonly string _root = Path.GetFullPath(root);
    public static string DefaultRoot => Environment.GetEnvironmentVariable("GAMECLI_STATE_DIR") ?? Path.Combine(Directory.GetCurrentDirectory(), ".gamecli");
    private string SessionPath(string session) { CliLaunchOptions.ValidateSession(session); return Path.Combine(_root, session); }
    private string RunPath(CliWorker worker) => Path.Combine(SessionPath(worker.Session), worker.Run);
    private static void ValidateRequestId(string id)
    {
        if (id.Length is < 1 or > 100 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-'))
            throw new ArgumentException("Request IDs must contain 1–100 ASCII letters, digits, underscores or hyphens.");
    }
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), CliProtocol.Json)
        ?? throw new InvalidOperationException($"Empty transport file: {path}");
    private static void Write(string path, object value)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, CliProtocol.Json));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static FileStream Lease(string path)
    {
        try { return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new ArgumentException("Another client is updating this session. Retry the same request ID."); }
    }
    private CliWorker Worker(string session)
    {
        string path = Path.Combine(SessionPath(session), "worker.json");
        if (!File.Exists(path)) throw new ArgumentException("No background session found. Use start first.");
        return Read<CliWorker>(path);
    }
    private static bool IsAlive(CliWorker worker)
    {
        try
        {
            using var process = Process.GetProcessById(worker.Pid);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == worker.StartedTicks;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    public CliWorker Start(CliLaunchOptions options)
    {
        string directory = SessionPath(options.SessionId);
        Directory.CreateDirectory(directory);
        using var lease = Lease(Path.Combine(directory, "start.lock"));
        string descriptor = Path.Combine(directory, "worker.json");
        if (File.Exists(descriptor) && IsAlive(Read<CliWorker>(descriptor)))
            throw new ArgumentException("This session already has a live worker. Use peek or send.");
        string run = Guid.NewGuid().ToString("N");
        string runPath = Path.Combine(directory, run);
        Directory.CreateDirectory(runPath);
        Write(Path.Combine(runPath, "options.json"), options);
        Write(Path.Combine(runPath, "state.json"), new { status = "starting", revision = 0, snapshotCurrent = false });
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet") {
            UseShellExecute = false, CreateNoWindow = true,
            // Separate handles let the launching tool return without waiting for the child.
            // The worker immediately redirects its diagnostics to its own log file.
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };
        start.ArgumentList.Add(typeof(BackgroundCli).Assembly.Location);
        start.ArgumentList.Add("--worker"); start.ArgumentList.Add(_root);
        start.ArgumentList.Add(options.SessionId); start.ArgumentList.Add(run);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start CLI worker.");
        var worker = new CliWorker(options.SessionId, run, process.Id, process.StartTime.ToUniversalTime().Ticks);
        // A short startup handshake detaches the child before this command exits.
        // This waits for process setup only, never world generation or gameplay.
        var startup = Stopwatch.StartNew();
        string detached = Path.Combine(runPath, "detached");
        while (!File.Exists(detached))
        {
            if (process.HasExited) throw new InvalidOperationException($"Worker exited during startup. See {Path.Combine(runPath, "worker.log")}");
            if (startup.Elapsed > TimeSpan.FromSeconds(3))
            {
                process.Kill();
                throw new InvalidOperationException("Worker did not finish process setup within three seconds.");
            }
            Thread.Sleep(10);
        }
        Write(descriptor, worker);
        return worker;
    }

    public object Peek(string session)
    {
        var worker = Worker(session);
        var state = Read<CliWorkerState>(Path.Combine(RunPath(worker), "state.json"));
        bool alive = IsAlive(worker);
        string status = !alive && state.Status is not "stopped" and not "failed" ? "interrupted" : state.Status;
        return new { worker.Session, worker.Run, worker.Pid, alive, status,
            state = state with { Status = status, SnapshotCurrent = status is "ready" or "stopped" }, logPath = Path.Combine(RunPath(worker), "worker.log") };
    }

    public object Send(string session, string command, string? value = null, string? requestId = null, long? revision = null)
    {
        var worker = Worker(session);
        string directory = RunPath(worker);
        string id = requestId ?? Guid.NewGuid().ToString("N");
        ValidateRequestId(id);
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("A command is required.");
        using var lease = Lease(Path.Combine(directory, "submit.lock"));
        string requestPath = Path.Combine(directory, id + ".request.json");
        if (File.Exists(requestPath))
        {
            var previous = Read<CliRequest>(requestPath);
            if (previous.Command != command || previous.Value != value || previous.Revision != revision)
                throw new ArgumentException("That request ID was already used for a different command.");
            return Poll(session, id, worker.Run);
        }
        if (!IsAlive(worker)) throw new ArgumentException("Worker is not running. Its last snapshot and results remain available through peek/poll.");
        var state = Read<JsonElement>(Path.Combine(directory, "state.json"));
        if (state.GetProperty("status").GetString() is "stopped" or "failed")
            throw new ArgumentException("Worker has ended. Start a new session or load its last save.");
        string counterPath = Path.Combine(directory, "sequence.json");
        long sequence = File.Exists(counterPath) ? Read<long>(counterPath) + 1 : 1;
        Write(counterPath, sequence);
        Write(requestPath, new CliRequest(id, sequence, command, value, revision));
        return new { worker.Session, worker.Run, requestId = id, status = "queued" };
    }

    public object Poll(string session, string requestId, string? run = null)
    {
        ValidateRequestId(requestId);
        var worker = Worker(session);
        if (run != null && run != worker.Run) throw new ArgumentException("This session has restarted; this request belongs to an earlier run. Archived files remain on disk.");
        string directory = RunPath(worker);
        string resultPath = Path.Combine(directory, requestId + ".result.json");
        if (File.Exists(resultPath)) return Read<JsonElement>(resultPath);
        if (!File.Exists(Path.Combine(directory, requestId + ".request.json"))) throw new ArgumentException("Unknown request ID.");
        var state = Read<JsonElement>(Path.Combine(directory, "state.json"));
        bool running = state.TryGetProperty("requestId", out var current) && current.GetString() == requestId;
        bool ended = !IsAlive(worker) || state.GetProperty("status").GetString() is "stopped" or "failed";
        return new { worker.Session, worker.Run, requestId, status = ended ? "interrupted" : running ? "running" : "queued",
            outcomeUnknown = ended && running };
    }

    // Called only by the detached worker. All simulation and JSON snapshot serialization
    // stay on this thread; clients never touch the live context or its synchronization queue.
    public int RunWorker(string sessionId, string run)
    {
        if (!Guid.TryParseExact(run, "N", out _)) throw new ArgumentException("Invalid worker run ID.");
        string directory = Path.Combine(SessionPath(sessionId), run);
        using var log = new StreamWriter(Path.Combine(directory, "worker.log"), append: true) { AutoFlush = true };
        Console.SetOut(log); Console.SetError(log);
        // Separate the worker from the launcher's terminal/process group. Agent tool
        // runners commonly clean up that group as soon as the one-shot command exits.
        if (!OperatingSystem.IsWindows() && setsid() < 0)
            throw new InvalidOperationException($"Could not detach CLI worker: errno {Marshal.GetLastPInvokeError()}.");
        using var lease = Lease(Path.Combine(SessionPath(sessionId), "worker.lock"));
        File.WriteAllText(Path.Combine(directory, "detached"), "ready");
        string statePath = Path.Combine(directory, "state.json");
        object? snapshot = null;
        long revision = 0;
        CliRequest? active = null;
        var completed = new HashSet<string>();
        void State(string status, string? error = null) => Write(statePath, new {
            status, revision, requestId = active?.Id, snapshotCurrent = status is "ready" or "stopped", snapshot, error
        });
        try
        {
            var options = Read<CliLaunchOptions>(Path.Combine(directory, "options.json"));
            using var session = new CliSession(options.CreateContext(), sessionId);
            snapshot = JsonSerializer.SerializeToElement(session.Look(), CliProtocol.Json);
            State("ready");
            while (!session.Exiting)
            {
                active = Directory.EnumerateFiles(directory, "*.request.json")
                    .Where(p => !completed.Contains(Path.GetFileName(p)))
                    .Select(Read<CliRequest>).OrderBy(r => r.Sequence).FirstOrDefault();
                if (active == null) { Thread.Sleep(25); continue; }
                State("running");
                object response;
                try
                {
                    if (active.Revision.HasValue && active.Revision != revision)
                        throw new ArgumentException($"Stale revision {active.Revision}; current revision is {revision}. Read peek before deciding again.");
                    object result = session.Execute(active.Command, active.Value);
                    while (session.IsBusy && !session.Exiting)
                    {
                        snapshot = JsonSerializer.SerializeToElement(session.Look(), CliProtocol.Json);
                        State("running");
                        result = session.Execute("step");
                    }
                    response = new { ok = true, result };
                }
                catch (Exception ex) when (CliProtocol.IsInputError(ex))
                { response = new { ok = false, error = ex.Message, screen = session.Ui.Screen }; }
                // Freeze lazy enumerables before processing any subsequent commands.
                var serialized = JsonSerializer.SerializeToElement(response, CliProtocol.Json);
                revision++;
                snapshot = JsonSerializer.SerializeToElement(session.Look(), CliProtocol.Json);
                Write(Path.Combine(directory, active.Id + ".result.json"), new {
                    session = sessionId, run, requestId = active.Id, status = "completed", revision, response = serialized
                });
                completed.Add(active.Id + ".request.json");
                active = null;
                State(session.Exiting ? "stopped" : "ready");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            if (active != null)
                Write(Path.Combine(directory, active.Id + ".result.json"), new {
                    session = sessionId, run, requestId = active.Id, status = "failed", revision,
                    response = new { ok = false, fatal = true, error = ex.Message }
                });
            State("failed", ex.Message);
            return 1;
        }
    }

    public object Dispatch(string[] args)
    {
        if (args[0] == "start") return new { status = "starting", worker = Start(CliLaunchOptions.Parse(args[1..])) };
        if (args.Length < 2) throw new ArgumentException("A session ID is required.");
        string session = args[1];
        switch (args[0])
        {
            case "peek" when args.Length == 2: return Peek(session);
            case "poll" when args.Length is 3 or 4: return Poll(session, args[2], args.Length == 4 ? args[3] : null);
            case "stop" when args.Length == 2: return Send(session, "quit", requestId: "stop");
            case "send" when args.Length >= 3:
                var (command, value) = CliProtocol.Parse(args[2]);
                string? id = null; long? revision = null;
                for (int i = 3; i < args.Length; i++)
                {
                    if (i + 1 >= args.Length) throw new ArgumentException("Missing option value.");
                    switch (args[i])
                    {
                        case "--request": id = args[++i]; break;
                        case "--value": value = args[++i]; break;
                        case "--revision": revision = long.Parse(args[++i]); break;
                        default: throw new ArgumentException($"Unknown option: {args[i]}");
                    }
                }
                return Send(session, command, value, id, revision);
            default: throw new ArgumentException("Usage: start [options] | send SESSION \"command\" [--value TEXT] [--request ID] [--revision N] | poll SESSION ID [RUN] | peek SESSION | stop SESSION");
        }
    }
}
