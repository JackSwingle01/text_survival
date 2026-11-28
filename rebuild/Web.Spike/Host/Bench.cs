using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Survival;

namespace WebSpike;

/// The spike's measurements, in steps, so a browser can yield between them (one simulated day per call).
/// The native bench and the wasm page run this same code.
public sealed class Bench
{
    World? _world;
    World W => _world ?? throw new InvalidOperationException("Call NewWorld first");
    public readonly BenchResult Result = new();

    public void NewWorld(int seed)
    {
        var sw = Stopwatch.StartNew();
        _world = World.New(seed);
        Result.NewWorldMs = sw.Elapsed.TotalMilliseconds;
    }

    /// Step(Wait, 60) once cold, then `reps` times; and Step(Wait, 1) sixty times. Milliseconds per simulated minute.
    public StepTiming Steps(int reps = 3)
    {
        var sw = Stopwatch.StartNew();
        Sim.Step(W, new Wait(AgentId.Player), 60);
        double cold = sw.Elapsed.TotalMilliseconds / 60;
        sw.Restart();
        for (int i = 0; i < reps; i++) Sim.Step(W, new Wait(AgentId.Player), 60);
        double warm = sw.Elapsed.TotalMilliseconds / (60.0 * reps);
        sw.Restart();
        for (int i = 0; i < 60; i++) Sim.Step(W, new Wait(AgentId.Player), 1);
        double single = sw.Elapsed.TotalMilliseconds / 60;
        return new StepTiming(cold, warm, single, W.Agents.Count);
    }

    public void StepsAtStart() => Result.AtStart = Steps();
    public void StepsAfter() => Result.AfterAdvance = Steps();

    /// One simulated day of Sim.Advance, in hour chunks. Between hours (outside the timing) the
    /// player's needs are topped up: a player who waits 30 days in the open dies, and the "after 30
    /// days" Step needs a live one. The world around them runs untouched.
    public double AdvanceDay()
    {
        double ms = 0;
        var sw = new Stopwatch();
        for (int h = 0; h < 24; h++)
        {
            sw.Restart();
            Sim.Advance(W, 60);
            ms += sw.Elapsed.TotalMilliseconds;
            KeepPlayerAlive();
        }
        Result.AdvanceDayMs.Add(ms);
        return ms;
    }

    void KeepPlayerAlive()
    {
        var b = W.Agent(AgentId.Player).Body;
        if (b.DiedMinute != null) throw new InvalidOperationException($"The bench player died: {b.DeathCause}");
        b.CaloriesKcal = Body.MaxCaloriesKcal;
        b.HydrationMl = Body.MaxHydrationMl;
        b.SleepDebtMinutes = 0;
        b.CoreTempF = Body.BaseCoreTempF;
    }

    public void SaveRoundTrip()
    {
        var sw = Stopwatch.StartNew();
        string json = Save.ToJson(W);
        Result.SaveMs = sw.Elapsed.TotalMilliseconds;
        byte[] raw = Encoding.UTF8.GetBytes(json);
        Result.SaveBytes = raw.Length;
        using (var ms = new MemoryStream())
        {
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(raw);
            Result.SaveGzipBytes = (int)ms.Length;
        }
        sw.Restart();
        var loaded = Save.FromJson(json);
        Result.LoadMs = sw.Elapsed.TotalMilliseconds;
        Result.RoundTripIdentical = Save.ToJson(loaded) == json;
        // A loaded world must also run on: the same next hour from both copies.
        Sim.Step(W, new Wait(AgentId.Player), 60);
        Sim.Step(loaded, new Wait(AgentId.Player), 60);
        Result.RunOnIdentical = Save.ToJson(loaded) == Save.ToJson(W);
    }

    /// The world after the bench, for comparing a wasm run against a native one byte for byte.
    public string WorldJson() => Save.ToJson(W);

    public string ResultJson() => System.Text.Json.JsonSerializer.Serialize(Result, HostJson.Default.BenchResult);

    public static BenchResult RunAll(int seed, int days, Action<string>? progress = null) => Run(seed, days, progress).Result;

    public static Bench Run(int seed, int days, Action<string>? progress = null)
    {
        var b = new Bench();
        b.NewWorld(seed);
        b.StepsAtStart();
        for (int d = 0; d < days; d++)
        {
            double ms = b.AdvanceDay();
            progress?.Invoke($"day {d + 1}: {ms:0} ms");
        }
        b.StepsAfter();
        b.SaveRoundTrip();
        return b;
    }
}

public sealed record StepTiming(double ColdMsPerMin, double WarmMsPerMin, double OneMinuteStepMs, int Agents);

public sealed class BenchResult
{
    public string Runtime { get; set; } = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        + " " + System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
    public double NewWorldMs { get; set; }
    public StepTiming? AtStart { get; set; }
    public StepTiming? AfterAdvance { get; set; }
    public List<double> AdvanceDayMs { get; set; } = [];
    public double AdvanceTotalMs => AdvanceDayMs.Sum();
    public double SaveMs { get; set; }
    public double LoadMs { get; set; }
    public int SaveBytes { get; set; }
    public int SaveGzipBytes { get; set; }
    public bool RoundTripIdentical { get; set; }
    public bool RunOnIdentical { get; set; }
}
