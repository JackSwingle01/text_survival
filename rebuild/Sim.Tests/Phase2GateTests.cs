using System.Diagnostics;
using Survival;
using Xunit;

/// Phase 2 gate (documentation/clean-room-architecture.md): a 30-day headless run completes, and the
/// same seed + actions gives the same world. A scripted camper walks out and back each day under real
/// seeded weather, keeps a fire, sleeps, and is fed and watered by hand.
// ponytail: food and water are handed over directly; inventory and foraging arrive in later phases.
public class Phase2GateTests
{
    static readonly AgentId P = AgentId.Player;
    const int Days = 30;

    /// The script reads only world state, so it is deterministic and resumable from any save.
    /// A day: walk out and back, eat and drink, sit by the fire until tired, bank the fire, sleep.
    static void PlayDay(World w)
    {
        var camp = w.CampTile;
        Walk(w, Direction.East, 4);
        Walk(w, Direction.West, 4);
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
    }

    static void Walk(World w, Direction dir, int legs)
    {
        for (int i = 0; i < legs && Body.IsAlive(w.Agent(P).Body); i++)
        {
            var leg = new Travel(P, dir, Pace.Careful);
            for (int tries = 0; tries < 10; tries++)
            {
                var r = Sim.Step(w, leg, 180);
                if (r.InterruptedReason is "arrived" or "died" || r.MinutesElapsed == 0) break;
            }
        }
    }

    static void TendFire(World w)
    {
        var pit = Fire.PitAt(w, w.CampTile);
        if (pit == null || !Fire.IsActive(w.Fires[pit.Value]))
            for (int i = 0; i < 20; i++)
            {
                Sim.Step(w, new StartFire(P, FireStarter.FireStriker, Fuel.BirchBark, 0.05, 1.0), 1);
                pit = Fire.PitAt(w, w.CampTile);
                if (pit != null && Fire.IsActive(w.Fires[pit.Value])) break;
            }
        Sim.Step(w, new AddFuel(P, Fuel.Kindling, 1.0), 1); // refused when full: costs nothing
        Sim.Step(w, new AddFuel(P, Fuel.Birch, 3.0), 1);
    }

    static World Run(int seed, int fromDay = 0, World? start = null)
    {
        var w = start ?? World.New(seed);
        for (int day = fromDay; day < Days && Body.IsAlive(w.Agent(P).Body); day++) PlayDay(w);
        while (w.Minute < Days * 1440L && Body.IsAlive(w.Agent(P).Body)) PlayDay(w); // days run short of 24 h
        return w;
    }

    [Fact]
    public void ThirtyDays_Headless_CompletesAliveAndSmall()
    {
        var clock = Stopwatch.StartNew();
        var w = Run(17);
        clock.Stop();

        Assert.True(Body.IsAlive(w.Agent(P).Body), "a fed camper with a fire should live 30 days");
        Assert.True(w.Minute >= Days * 1440L, $"only reached minute {w.Minute}");
        string json = Save.ToJson(w);
        Assert.True(json.Length < 1_000_000, $"save is {json.Length} bytes");
        Assert.True(clock.Elapsed.TotalSeconds < 60, $"took {clock.Elapsed.TotalSeconds:F1}s");
        // Walked out and back daily for a month: the first leg out of camp has worn into at least a Path.
        var camp = w.CampTile;
        var tier = Tracks.TierOf(w, camp, new TileId(camp.X + 1, camp.Y));
        Assert.True(tier >= TrailTier.Path, $"daily route is only {tier}");
    }

    [Fact]
    public void ThirtyDays_SameSeedAndActions_SameWorld()
    {
        Assert.Equal(Save.ToJson(Run(17)), Save.ToJson(Run(17)));
        Assert.NotEqual(Save.ToJson(Run(17)), Save.ToJson(Run(18)));
    }

    [Fact]
    public void ThirtyDays_SaveAndLoadAtDay15_MatchesAStraightRun()
    {
        var straight = Run(17);
        var half = World.New(17);
        for (int day = 0; day < 15; day++) PlayDay(half);
        var resumed = Run(17, fromDay: 15, start: Save.FromJson(Save.ToJson(half)));
        Assert.Equal(Save.ToJson(straight), Save.ToJson(resumed));
    }
}
