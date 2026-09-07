using System.Diagnostics;
using System.Text.Json;
using text_survival.GameCli;

namespace text_survival.Tests.Cli;

public sealed class BackgroundCliTests
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, CliProtocol.Json);
    private static string Status(object value) => Json(value).GetProperty("status").GetString()!;
    private static async Task<JsonElement> Result(BackgroundCli cli, string session, string id)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(40))
        {
            var value = Json(cli.Poll(session, id));
            if (value.GetProperty("status").GetString() is "completed" or "failed" or "interrupted") return value;
            await Task.Delay(25);
        }
        throw new TimeoutException("Background CLI request did not complete.");
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
            Assert.Equal(DateTime.Parse(time!).AddMinutes(5), DateTime.Parse(newTime!));
            cli.Send(session, "act Wait", requestId: "wait");
            Assert.Equal(newTime, Json(cli.Peek(session)).GetProperty("state").GetProperty("snapshot").GetProperty("status").GetProperty("time").GetString());
            Assert.Throws<ArgumentException>(() => cli.Poll(session, "missing"));
            Assert.Throws<ArgumentException>(() => cli.Send(session, "look", requestId: "../bad"));
            cli.Dispatch(["stop", session]);
            Assert.Equal("completed", (await Result(cli, session, "stop")).GetProperty("status").GetString());
            for (int i = 0; i < 100 && Json(cli.Peek(session)).GetProperty("alive").GetBoolean(); i++) await Task.Delay(25);
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
            for (int i = 0; i < 200 && Status(cli.Peek(session)) == "starting"; i++) await Task.Delay(25);
            Assert.Equal("failed", Status(cli.Peek(session)));
            Assert.Contains("does not exist", Json(cli.Peek(session)).GetProperty("state").GetProperty("error").GetString());
            for (int i = 0; i < 200 && Json(cli.Peek(session)).GetProperty("alive").GetBoolean(); i++) await Task.Delay(25);
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
