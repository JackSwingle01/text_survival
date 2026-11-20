using System.Diagnostics;
using System.Reflection;
using Survival;
using Xunit;
using Xunit.Abstractions;

/// Phase 2 gate (documentation/clean-room-architecture.md): a 30-day headless run completes, and the
/// same seed + actions gives the same world. A scripted camper walks out and back each day under real
/// seeded weather, keeps a fire, sleeps, and is fed and watered by hand.
// ponytail: food and water are handed over directly; inventory and foraging arrive in later phases.
public class Phase2GateTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;
    const int Days = 30;

    /// The script reads only world state, so it is deterministic and resumable from any save.
    /// A day: a quick step onto the rock west of camp (seed 17: hazardous, so the injury roll is
    /// live) and a careful step back, a walk out and back, eat and drink, sit by the fire until
    /// tired, bank the fire, sleep. Returns the injuries taken.
    static int PlayDay(World w)
    {
        var camp = Map.Of(w).Camp;
        int injuries = Walk(w, Direction.West, 1, Pace.Quick); // one hazardous segment a day, risk 0.3-0.5
        injuries += Walk(w, Direction.East, 1);
        injuries += Walk(w, Direction.East, 4);
        injuries += Walk(w, Direction.West, 4);
        Assert.Equal(camp, w.Agent(P).Tile); // walked back the same way

        Body.Eat(w.Agent(P).Body, 2500);
        Body.Drink(w.Agent(P).Body, 3000);

        while (w.Agent(P).Body.EnergyPct > 0.2 && Body.IsAlive(w.Agent(P).Body))
        {
            TendFire(w);
            Sim.Step(w, new Rest(P), 120);
        }
        TendFire(w);
        for (int i = 0; i < 3; i++) Sim.Step(w, new AddFuel(P, Fuel.Birch, 3.0), 1); // bank it for the night
        if (Body.IsAlive(w.Agent(P).Body)) Sim.Step(w, new Sleep(P), 10 * 60); // stops when fully rested
        return injuries;
    }

    static int Walk(World w, Direction dir, int legs, Pace pace = Pace.Careful)
    {
        int injuries = 0;
        for (int i = 0; i < legs && Body.IsAlive(w.Agent(P).Body); i++)
        {
            var leg = new Travel(P, dir, pace);
            for (int tries = 0; tries < 10; tries++)
            {
                var r = Sim.Step(w, leg, 180);
                injuries += r.Events.OfType<Injured>().Count();
                if (r.InterruptedReason is "arrived" or "died" || r.MinutesElapsed == 0) break;
            }
        }
        return injuries;
    }

    static void TendFire(World w)
    {
        var camp = Map.Of(w).Camp;
        var pit = Fire.PitAt(w, camp);
        if (pit == null || !Fire.IsActive(w.Fires[pit.Value]))
            for (int i = 0; i < 20; i++)
            {
                Sim.Step(w, new StartFire(P, FireStarter.FireStriker, Fuel.BirchBark, 0.05, 1.0), 1);
                pit = Fire.PitAt(w, camp);
                if (pit != null && Fire.IsActive(w.Fires[pit.Value])) break;
            }
        Sim.Step(w, new AddFuel(P, Fuel.Kindling, 1.0), 1); // refused when full: costs nothing
        Sim.Step(w, new AddFuel(P, Fuel.Birch, 3.0), 1);
    }

    static (World World, int Injuries) Run(int seed, int fromDay = 0, World? start = null)
    {
        var w = start ?? World.New(seed);
        int injuries = 0;
        for (int day = fromDay; day < Days && Body.IsAlive(w.Agent(P).Body); day++) injuries += PlayDay(w);
        while (w.Minute < Days * 1440L && Body.IsAlive(w.Agent(P).Body)) injuries += PlayDay(w); // days run short of 24 h
        return (w, injuries);
    }

    [Fact]
    public void ThirtyDays_Headless_CompletesAliveAndSmall()
    {
        var clock = Stopwatch.StartNew();
        var (w, injuries) = Run(17);
        clock.Stop();

        Assert.True(Body.IsAlive(w.Agent(P).Body), $"a fed camper with a fire should live 30 days ({injuries} injuries)");
        Assert.True(w.Minute >= Days * 1440L, $"only reached minute {w.Minute}");
        Assert.True(injuries > 0, "thirty quick steps onto hazardous rock and never a scratch: the injury roll is not live");
        output.WriteLine($"30 days: {injuries} injuries, {clock.Elapsed.TotalSeconds:F1}s");
        string json = Save.ToJson(w);
        Assert.True(json.Length < 1_000_000, $"save is {json.Length} bytes");
        Assert.True(clock.Elapsed.TotalSeconds < 60, $"took {clock.Elapsed.TotalSeconds:F1}s");
        // Walked out and back daily for a month: the first leg out of camp has worn into at least a Path.
        var camp = Map.Of(w).Camp;
        var tier = Tracks.TierOf(w, camp, new TileId(camp.X + 1, camp.Y));
        Assert.True(tier >= TrailTier.Path, $"daily route is only {tier}");
    }

    [Fact]
    public void ThirtyDays_SameSeedAndActions_SameWorld()
    {
        Assert.Equal(Save.ToJson(Run(17).World), Save.ToJson(Run(17).World));
        Assert.NotEqual(Save.ToJson(Run(17).World), Save.ToJson(Run(18).World));
    }

    [Fact]
    public void ThirtyDays_SaveAndLoadAtDay15_MatchesAStraightRun()
    {
        var straight = Run(17).World;
        var half = World.New(17);
        for (int day = 0; day < 15; day++) PlayDay(half);
        var resumed = Run(17, fromDay: 15, start: Save.FromJson(Save.ToJson(half))).World;
        Assert.Equal(Save.ToJson(straight), Save.ToJson(resumed));
    }

    /// Two separate processes, same seed and commands, byte-identical saves: nothing in the run
    /// depends on hash seeds, the clock, or anything else a process brings with it.
    [Fact]
    public void Cli_SameSeedAndCommandsInTwoProcesses_SaveTheSameWorld()
    {
        // Sim.Tests builds the CLI (a ProjectReference that is not referenced), so no build here.
        string config = typeof(Phase2GateTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Cli", "Cli.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        string cli = Path.Combine(dir.FullName, "Cli", "bin", config, "net10.0", "Cli.dll");
        Assert.True(File.Exists(cli), $"no CLI build at {cli}");
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

        // A fresh directory and bare file names: the CLI splits commands on spaces, so a temp path
        // with a space in it would save somewhere else and leave nothing to compare.
        string work = Path.Combine(Path.GetTempPath(), "cli-determinism-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);

        string? RunCli(string save)
        {
            var psi = new ProcessStartInfo(dotnet) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = work };
            foreach (string arg in new[] { cli, "--seed", "17", "wait 30", "fire", "fuel birch 3", "w", "e careful",
                         "e", "rest 120", "w careful", "sleep 480", $"save {save}" })
                psi.ArgumentList.Add(arg);
            Process p;
            try { p = Process.Start(psi)!; }
            catch (System.ComponentModel.Win32Exception e)
            {
                output.WriteLine($"SKIPPED: cannot start '{dotnet}' ({e.Message}); cross-process determinism not checked");
                return null;
            }
            string stdout = p.StandardOutput.ReadToEnd(), stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            Assert.True(p.ExitCode == 0, $"CLI failed ({p.ExitCode}): {stderr}\n{stdout}");
            string path = Path.Combine(work, save);
            Assert.True(File.Exists(path), $"the CLI wrote no save at {path}");
            return File.ReadAllText(path);
        }

        try
        {
            string? first = RunCli("a.json");
            if (first == null) return; // only when the process cannot be started at all
            string? second = RunCli("b.json");
            Assert.True(first.Length > 1000, "the save should hold a world");
            Assert.Equal(first, second);
        }
        finally { Directory.Delete(work, recursive: true); }
    }
}
