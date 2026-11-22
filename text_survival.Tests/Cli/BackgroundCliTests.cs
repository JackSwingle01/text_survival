using System.Diagnostics;
using System.Text.Json;
using text_survival.GameCli;

namespace text_survival.Tests.Cli;

public sealed class BackgroundCliTests
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, CliProtocol.Json);
    private static string Status(object value) => Json(value).GetProperty("status").GetString()!;
    // A request is given up on by what the worker has spent, not by the wall clock: on a loaded runner,
    // generating the world stretches past a minute of wall time while costing the worker the same few
    // seconds of processor. The wall-clock backstop catches a worker that hangs without spending any.
    private static readonly TimeSpan WorkerCpuBudget = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan WallBackstop = TimeSpan.FromMinutes(5);

    private static async Task<JsonElement> Result(BackgroundCli cli, string session, string id)
    {
        using var worker = Find(Json(cli.Peek(session)).GetProperty("pid").GetInt32());
        var cpuAtStart = CpuSpent(worker);
        var wall = Stopwatch.StartNew();
        while (true)
        {
            var value = Json(cli.Poll(session, id));
            if (value.GetProperty("status").GetString() is "completed" or "failed" or "interrupted") return value;
            if (CpuSpent(worker) - cpuAtStart > WorkerCpuBudget)
                throw new TimeoutException($"Background CLI request did not complete in {WorkerCpuBudget.TotalSeconds:0} s of worker CPU.");
            if (wall.Elapsed > WallBackstop)
                throw new TimeoutException($"Background CLI request did not complete in {WallBackstop.TotalMinutes:0} min.");
            await Task.Delay(25);
        }
    }

    /// The worker process, or null once it has exited.
    private static Process? Find(int pid)
    {
        try { return Process.GetProcessById(pid); }
        catch (ArgumentException) { return null; }
    }

    private static TimeSpan CpuSpent(Process? worker)
    {
        if (worker == null) return TimeSpan.Zero;
        try { worker.Refresh(); return worker.TotalProcessorTime; }
        // Exited: the next poll reports how it ended.
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return TimeSpan.Zero; }
    }

    private static async Task WaitForExit(int pid)
    {
        using var worker = Find(pid);
        using var backstop = new CancellationTokenSource(WallBackstop);
        if (worker != null) await worker.WaitForExitAsync(backstop.Token);
    }

    [Fact]
    public async Task DetachedWorker_PreservesPrompts_DeduplicatesRequests_RejectsStaleDecisions_Stops()
    {
        string root = Path.Combine(Path.GetTempPath(), "gamecli-test-" + Guid.NewGuid().ToString("N"));
        string session = "cli_bg_" + Guid.NewGuid().ToString("N");
        var cli = new BackgroundCli(root);
        CliWorker? worker = null;
        try
        {
            worker = cli.Start(new(session, 42));
            Assert.True(Json(cli.Peek(session)).GetProperty("alive").GetBoolean());
            Assert.Throws<ArgumentException>(() => cli.Start(new(session, 42)));
            cli.Send(session, "click Continue", requestId: "intro"); // a new game opens on the intro notice
            Assert.True((await Result(cli, session, "intro")).GetProperty("response").GetProperty("ok").GetBoolean());
            var watch = Stopwatch.StartNew();
            Assert.Equal("queued", Status(cli.Send(session, "act Inventory", requestId: "inventory")));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), "Submission waited for world generation or gameplay.");
            var opened = await Result(cli, session, "inventory");
            Assert.True(opened.GetProperty("response").GetProperty("ok").GetBoolean());
            Assert.Equal("inventory", opened.GetProperty("response").GetProperty("result").GetProperty("screen").GetString());
            var time = opened.GetProperty("response").GetProperty("result").GetProperty("status").GetProperty("time").GetString();
            var before = Json(cli.Peek(session)).GetProperty("state");
            long revision = before.GetProperty("revision").GetInt64();
            Assert.Equal("completed", Status(cli.Send(session, "act Inventory", requestId: "inventory")));
            Assert.Equal(revision, Json(cli.Peek(session)).GetProperty("state").GetProperty("revision").GetInt64());
            Assert.Throws<ArgumentException>(() => cli.Send(session, "close", requestId: "inventory"));
            cli.Send(session, "act Wait", requestId: "invalid");
            Assert.False((await Result(cli, session, "invalid")).GetProperty("response").GetProperty("ok").GetBoolean());
            cli.Send(session, "close", requestId: "stale", revision: 0);
            Assert.Contains("Stale revision", (await Result(cli, session, "stale")).GetProperty("response").GetProperty("error").GetString());
            Assert.Equal("inventory", Json(cli.Peek(session)).GetProperty("state").GetProperty("snapshot").GetProperty("screen").GetString());
            // Submit both before polling: FIFO keeps close ahead of the map action.
            cli.Send(session, "close", requestId: "close");
            cli.Send(session, "act Wait", requestId: "wait");
            var waited = await Result(cli, session, "wait");
            Assert.True(waited.GetProperty("response").GetProperty("ok").GetBoolean());
            var newTime = waited.GetProperty("response").GetProperty("result").GetProperty("status").GetProperty("time").GetString();
            Assert.Equal(TimeSpan.FromMinutes(5), TimeSpan.Parse(newTime!.Split(' ')[^1]) - TimeSpan.Parse(time!.Split(' ')[^1]));
            var viaDo = Json(cli.Do(session, ["act Inventory", "click #0", "close"], WallBackstop));
            Assert.Equal("map", viaDo.GetProperty("screen").GetString());
            Assert.False(Json(cli.Do(session, ["click #999"], WallBackstop)).GetProperty("ok").GetBoolean());
            cli.Send(session, "act Wait", requestId: "wait");
            Assert.Equal(newTime, Json(cli.Peek(session)).GetProperty("state").GetProperty("snapshot").GetProperty("status").GetProperty("time").GetString());
            Assert.Throws<ArgumentException>(() => cli.Poll(session, "missing"));
            Assert.Throws<ArgumentException>(() => cli.Send(session, "look", requestId: "../bad"));
            cli.Dispatch(["stop", session]);
            Assert.Equal("completed", (await Result(cli, session, "stop")).GetProperty("status").GetString());
            await WaitForExit(worker.Pid);
            Assert.Equal("stopped", Status(cli.Peek(session)));
            Assert.False(Json(cli.Peek(session)).GetProperty("alive").GetBoolean());
            Assert.Equal("completed", Status(cli.Poll(session, "wait")));
            Assert.Throws<ArgumentException>(() => cli.Send(session, "look"));
            // A new process never replays the old request log.
            worker = cli.Start(new(session, 42));
            cli.Send(session, "look", requestId: "fresh");
            var fresh = await Result(cli, session, "fresh");
            Assert.Equal(time, fresh.GetProperty("response").GetProperty("result").GetProperty("status").GetProperty("time").GetString());
            Assert.Throws<ArgumentException>(() => cli.Poll(session, "wait", opened.GetProperty("run").GetString()));
        }
        finally
        {
            if (worker != null)
            {
                try
                {
                    using var process = Process.GetProcessById(worker.Pid);
                    process.Kill(); await process.WaitForExitAsync();
                }
                catch (ArgumentException) { }
            }
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartupFailureAndWorkerDeath_ReportTerminalStateWithoutHanging()
    {
        string root = Path.Combine(Path.GetTempPath(), "gamecli-test-" + Guid.NewGuid().ToString("N"));
        string session = "cli_bg_" + Guid.NewGuid().ToString("N");
        var cli = new BackgroundCli(root);
        CliWorker? worker = null;
        try
        {
            worker = cli.Start(new(session, Load: session)); // This save does not exist.
            await WaitForExit(worker.Pid); // a worker that cannot start records why, then exits
            Assert.Equal("failed", Status(cli.Peek(session)));
            Assert.Contains("does not exist", Json(cli.Peek(session)).GetProperty("state").GetProperty("error").GetString());
            worker = cli.Start(new(session, 42));
            cli.Send(session, "look", requestId: "pending");
            using var process = Process.GetProcessById(worker.Pid);
            process.Kill(); await process.WaitForExitAsync();
            Assert.Equal("interrupted", Status(cli.Peek(session)));
            Assert.False(Json(cli.Peek(session)).GetProperty("state").GetProperty("snapshotCurrent").GetBoolean());
            Assert.Equal("interrupted", Status(cli.Poll(session, "pending")));
        }
        finally
        {
            if (worker != null)
            {
                try { using var p = Process.GetProcessById(worker.Pid); p.Kill(); await p.WaitForExitAsync(); }
                catch (ArgumentException) { }
            }
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
