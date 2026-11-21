using System.Diagnostics;
using System.Reflection;
using Survival;
using Xunit;
using Xunit.Abstractions;

/// Phase 2 gate (documentation/clean-room-architecture.md): a 30-day headless run completes, and the
/// same seed + actions gives the same world. Since phase 3a the camper lives off the land: it
/// forages its wood, tinder and what food there is, melts snow for water, keeps its fire with what
/// it carried home, and sleeps. Only the food foraging cannot supply is handed over, and counted.
public class Phase2GateTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;
    const int Days = 30;
    /// What a day of this routine burns, roughly: the hand-feeding tops the day's gathered food up to it.
    const double DailyKcal = 2400;

    internal sealed class Tally { public double GatheredKcal, HandFedKcal, WaterLiters, WoodKg; public int Injuries; }

    /// The script reads only world state, so it is deterministic and resumable from any save.
    /// A day: a quick step onto the rock west of camp (seed 17: hazardous, so the injury roll is
    /// live) and a careful step back; a tree felled and hauled home, more while the camp's wood is
    /// short of a long storm's worth; foraging walks out three tiles and back, working whatever it
    /// has found; home, the wood onto the stash; eat what it found; drink; the fire; sleep.
    /// It lives by a few rules a careful player would: never leave camp wet or thirsty, turn home
    /// when cold, wet, thirsty or the weather turns, and keep water and wood laid by.
    static void PlayDay(World w, Tally t)
    {
        var camp = Map.Of(w).Camp;
        int day = Weather.DayNumber(w.Minute);
        void Note(string what) => Log?.Invoke($"day {day} {what} @{Weather.MinuteOfDay(w.Minute) / 60:00}h: core {w.Agent(P).Body.CoreTempF:0.0} wet {Effects.Severity(w.Agent(P).Body, EffectKind.Wet):0.00} energy {w.Agent(P).Body.EnergyPct:0.00} water {w.Agent(P).Body.HydrationPct:0.00} stash {Items.Describe(w.Tiles.GetValueOrDefault(camp)?.Stash ?? new Inventory())} pack {Items.Describe(Items.Pack(w, P))} fire {(Fire.PitAt(w, camp) is FireId f ? Fire.Phase(w.Fires[f]) + " " + Fire.TotalKg(w.Fires[f]).ToString("0.0") : "none")} sky {Weather.At(w, w.Minute).Condition} {Weather.At(w, w.Minute).TemperatureC:0}C");
        Note("wake");
        for (int i = 0; i < 6 && Alive(w) && w.Agent(P).Body.CoreTempF < 98.0; i++) { TendFire(w); S(w, new Rest(P), 30); }
        // Days run short of 24 h, so some start in the dark, when felling is refused: it waits for
        // light by the fire, fed as through the evening.
        for (int i = 0; i < 5 && Alive(w) && Weather.MinuteOfDay(w.Minute) / 60 is < 5 or >= 21; i++) { TendFire(w); S(w, new Rest(P), 120); }
        Drink(w, t);
        // A foul morning with wood at camp is spent by the fire: one cold excursion below 95F core is
        // enough to kill (hypothermia works on the brain). With the wood short it goes to the trees
        // anyway, one step from camp, coming home to warm and dry between spells of chopping.
        bool stormDay = Foul(w);
        if (stormDay && CampWoodKg(w) >= LowWoodKg)
            for (int i = 0; i < 2 && Alive(w); i++) { TendFire(w); S(w, new Rest(P), 120); }
        else
        {
            if (!stormDay)
            {
                t.Injuries += Walk(w, Direction.West, 1, Pace.Quick); // one hazardous segment a day, risk 0.3-0.5
                t.Injuries += Walk(w, Direction.East, 1);
            }
            // Firewood: fell a tree in the wood east of camp, carry it home in loads (what the pack
            // cannot hold drops on that tile's stash and is fetched on the next load). Another while
            // the camp's wood is short of what a two-day blizzard burns, light and energy allowing.
            for (int tree = 0; tree < 3 && Alive(w) && (tree == 0 || CampWoodKg(w) < ReserveWoodKg && !Foul(w)); tree++)
            {
                if (w.Agent(P).Body.EnergyPct < 0.5 || !FellATree(w, t)) break;
                HaulWood(w, t);
            }
        }

        for (int trip = 0; trip < 2 && Alive(w) && w.Agent(P).Body.EnergyPct > 0.45 && !TurnForHome(w); trip++)
        {
            Ready(w, t);
            if (TurnForHome(w)) break;
            // From the clock, not a counter: the script reads only world state, so a load resumes it exactly.
            var (lead, route) = Routes[(int)(w.Minute / 240 % Routes.Length)];
            var path = Enumerable.Repeat(route[0], lead).Concat(route).ToList();
            int walked = 0;
            for (; walked < path.Count && !TurnForHome(w); walked++)
            {
                var from = w.Agent(P).Tile;
                t.Injuries += Walk(w, path[walked], 1);
                if (w.Agent(P).Tile == from) break; // refused (water, a cliff): this route ends here
                if (walked >= lead) ForageHere(w, 45);
            }
            for (int i = walked - 1; i >= 0; i--) t.Injuries += Walk(w, Opposite(path[i]), 1);
            if (!Alive(w)) return;
            Assert.Equal(camp, w.Agent(P).Tile); // walked back the same way
            StashWood(w, t);
        }
        Note("home");

        var pack = Items.Pack(w, P);
        double gathered = 0;
        foreach (var food in pack.Kg.Keys.Where(r => Items.Resources[r].Category == ResourceCategory.Food).ToList())
            gathered += S(w, new Eat(P, food, 99), Work.EatMinutes).Events.OfType<Ate>().Sum(a => a.Kcal);
        t.GatheredKcal += gathered;
        // Topped up to a full store, not past it: surplus became fat once food could (Body.Eat), and this
        // camper is fed to live, not to fatten - as before that fix, when the surplus was thrown away.
        double handFed = Math.Clamp(DailyKcal - gathered, 0, Body.MaxCaloriesKcal - w.Agent(P).Body.CaloriesKcal);
        t.HandFedKcal += handFed;
        Body.Eat(w.Agent(P).Body, handFed);

        TendFire(w);
        Drink(w, t);
        LayByWater(w, t);
        while (w.Agent(P).Body.EnergyPct > 0.2 && Alive(w))
        {
            TendFire(w);
            if (w.Agent(P).Body.HydrationPct < 0.8) Drink(w, t); // a fire that would not light earlier may now
            // A storm eating the wood: another tree while there is light to fell it by.
            if (CampWoodKg(w) < LowWoodKg && Daylight(w) && w.Agent(P).Body.EnergyPct > 0.3 && FellATree(w, t)) HaulWood(w, t);
            S(w, new Rest(P), 120);
        }
        TendFire(w);
        Drink(w, t);
        if (Fire.PitAt(w, camp) is FireId night) Feed(w, 12.0 - Fire.TotalKg(w.Fires[night])); // bank it for the night
        Note("bed");
        // Sleeps in two-hour stretches, waking to feed the fire, until rested.
        for (int i = 0; i < 6 && Alive(w); i++)
        {
            if (S(w, new Sleep(P), 120).InterruptedReason == "fully rested") break;
            TendFire(w);
        }
    }

    /// Wood the camp holds, on the stash and in the fire.
    static double CampWoodKg(World w)
    {
        var camp = Map.Of(w).Camp;
        double kg = new[] { Resource.Pine, Resource.Birch, Resource.Oak, Resource.Kindling }.Sum(r => Items.Have(Items.StashAt(w, camp), r));
        return kg + (Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f]) ? Fire.TotalKg(w.Fires[f]) : 0);
    }
    /// About what two days of blizzard by the fire burn (seed 17 has one of 36 h, then 12 h of whiteout).
    const double ReserveWoodKg = 80;
    /// Below this, sitting a storm out ends at a dead fire: better to go for wood.
    const double LowWoodKg = 30;
    /// Felling needs light (Work.TooDarkToWork: night is before 05:00 and from 21:00), and a tree takes a while.
    static bool Daylight(World w) => Weather.MinuteOfDay(w.Minute) / 60 is >= 5 and < 19;

    static bool Foul(World w) => Weather.At(w, w.Minute) is var sky && (sky.Precipitation != Precipitation.None || sky.TemperatureC < -8);
    static double Wet(World w) => Effects.Severity(w.Agent(P).Body, EffectKind.Wet);
    /// Cold, wet or thirsty: time to be at the fire.
    static bool Spent(World w) => w.Agent(P).Body.CoreTempF < 97.5 || Wet(w) > 0.5 || w.Agent(P).Body.HydrationPct < 0.45;
    /// On a foraging walk, the weather turning sends it home too.
    static bool TurnForHome(World w) => Spent(w) || Foul(w);

    /// Before leaving camp: dry and warm by the fire, and drunk.
    static void Ready(World w, Tally t)
    {
        for (int i = 0; i < 8 && Alive(w) && (Wet(w) > 0.05 || w.Agent(P).Body.CoreTempF < 98.0) && TendFire(w); i++) S(w, new Rest(P), 30);
        Drink(w, t);
    }

    /// Fells one tree on the wood tile east of camp, going home to the fire whenever the cold or the
    /// wet gets into it (the cut so far stays cut). With no fire at camp to go home to, it keeps at it
    /// until the cold is serious: the tree is the only way back to a fire. False when no tree came
    /// down (dark, too spent).
    static bool FellATree(World w, Tally t)
    {
        var camp = Map.Of(w).Camp;
        bool FireAtCamp() => Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f]);
        bool Done() => FireAtCamp()
            ? w.Agent(P).Body.CoreTempF < 97.0 || Wet(w) > 0.6 || w.Agent(P).Body.HydrationPct < 0.35
            : w.Agent(P).Body.CoreTempF < 95.5;
        for (int spell = 0; spell < 8 && Alive(w); spell++)
        {
            Ready(w, t);
            t.Injuries += Walk(w, Direction.East, 1);
            var wood = w.Agent(P).Tile;
            int trees = Pools.At(w, wood).ToList().FindIndex(p => p is TreePool);
            for (int i = 0; i < 8 && Alive(w) && !Done(); i++)
            {
                var r = S(w, new Gather(P, trees), 30);
                if (r.InterruptedReason == "tree felled") return true;
                if (r.MinutesElapsed == 0) { t.Injuries += Walk(w, Direction.West, 1); return false; } // refused: dark
            }
            t.Injuries += Walk(w, Direction.West, 1);
            if (w.Agent(P).Body.EnergyPct < 0.3) return false;
        }
        return false;
    }

    /// Carries a felled tree home in loads, the pack's worth at a time.
    static void HaulWood(World w, Tally t)
    {
        var camp = Map.Of(w).Camp;
        var wood = new TileId(camp.X + 1, camp.Y);
        for (int load = 0; load < 3 && Alive(w); load++)
        {
            if (load > 0)
            {
                t.Injuries += Walk(w, Direction.East, 1);
                var pile = Items.StashAt(w, wood);
                foreach (var r in pile.Kg.Keys.ToList()) S(w, new Unstash(P, r, 99), 1);
            }
            t.Injuries += Walk(w, Direction.West, 1);
            StashWood(w, t);
            if (Items.StashAt(w, wood).Kg.Count == 0) break;
        }
    }

    /// Drinks to 0.9: the water laid by first, then snow melted on the fire, lighting it if it is
    /// out. Nothing to melt it on and nothing stored: tries again later (the evening's rests).
    static void Drink(World w, Tally t)
    {
        var camp = Map.Of(w).Camp;
        if (w.Agent(P).Tile != camp) return;
        for (int i = 0; i < 12 && Alive(w) && w.Agent(P).Body.HydrationPct < 0.9; i++)
        {
            if (Items.Have(Items.Pack(w, P), Resource.Water) <= 0 && Items.Have(Items.StashAt(w, camp), Resource.Water) > 0)
                S(w, new Unstash(P, Resource.Water, 1), 1);
            if (Items.Have(Items.Pack(w, P), Resource.Water) > 0) { t.WaterLiters += Sip(w); continue; }
            if (!TendFire(w)) return;
            if (S(w, new MeltSnow(P), Work.MeltSnowMinutes).InterruptedReason == "melted") t.WaterLiters += Sip(w);
        }
    }
    static double Sip(World w) => S(w, new Drink(P), Work.DrinkMinutes).Events.OfType<Drank>().Sum(d => d.Liters);

    /// With the fire going, melts snow into the stash until three litres are laid by, against a
    /// day the fire will not light.
    static void LayByWater(World w, Tally t)
    {
        var camp = Map.Of(w).Camp;
        for (int i = 0; i < 4 && Alive(w) && Items.Have(Items.StashAt(w, camp), Resource.Water) + Items.Have(Items.Pack(w, P), Resource.Water) < 3; i++)
        {
            if (!TendFire(w) || S(w, new MeltSnow(P), Work.MeltSnowMinutes).InterruptedReason != "melted") return;
            S(w, new Stash(P, Resource.Water, 99), 1);
        }
    }

    static readonly Direction[][] BaseRoutes =
    [
        [Direction.East, Direction.East, Direction.East], [Direction.North, Direction.North, Direction.North],
        [Direction.South, Direction.South, Direction.South], [Direction.North, Direction.East, Direction.East],
        [Direction.South, Direction.East, Direction.East], [Direction.North, Direction.North, Direction.East],
    ];
    /// Each base route from camp, then again starting three tiles further out.
    static readonly (int Lead, Direction[] Route)[] Routes = [.. BaseRoutes.Select(r => (0, r)), .. BaseRoutes.Select(r => (3, r))];

    static Direction Opposite(Direction d) => (Direction)(((int)d + 2) % 4);

    /// Everything but food and a fire-lighting reserve goes on the camp stash, so the next walk
    /// starts with room in the pack.
    static void StashWood(World w, Tally t)
    {
        var pack = Items.Pack(w, P);
        foreach (var (r, kg) in pack.Kg.ToList())
        {
            if (Items.Resources[r].Category == ResourceCategory.Food) continue;
            double keep = r switch { Resource.Kindling => 2.0, Resource.Tinder or Resource.BirchBark or Resource.Amadou => 0.3, _ => 0 };
            if (r is Resource.Pine or Resource.Birch or Resource.Oak) t.WoodKg += kg;
            if (kg - keep > 1e-6) S(w, new Stash(P, r, kg - keep), 1);
        }
    }

    internal static bool Alive(World w) => Body.IsAlive(w.Agent(P).Body);

    [ThreadStatic] internal static Action<string>? Log;
    static StepResult S(World w, Survival.Activity a, int minutes)
    {
        if (!Alive(w)) return new StepResult([], 0, StepOutcome.Refused, "dead");
        var r = Sim.Step(w, a, minutes);
        if (r.InterruptedReason == "died" || Log != null && r.Events.Any(e => e is Died))
            Log?.Invoke($"died at {w.Minute} during {a}: core {w.Agent(P).Body.CoreTempF:0.0} kcal {w.Agent(P).Body.CaloriesKcal:0} water {w.Agent(P).Body.HydrationMl:0} blood {w.Agent(P).Body.BloodPct:0.00} caps {Body.GetCapacities(w.Agent(P).Body)} effects {string.Join(",", w.Agent(P).Body.Effects.Select(e => $"{e.Kind}:{e.Severity:0.00}"))}");
        return r;
    }

    /// Forages, and works out any stock it has already found here.
    static void ForageHere(World w, int minutes)
    {
        if (!Alive(w)) return;
        S(w, new Gather(P, 0), minutes);
        var tile = w.Agent(P).Tile;
        if (Work.HasOpenWater(w, tile))
            for (int i = 0; i < 3 && Alive(w) && w.Agent(P).Body.HydrationPct < 0.9; i++) Sip(w);
        var pools = Pools.At(w, tile);
        for (int i = 0; i < pools.Count && Alive(w); i++)
            if (pools[i] is StockPool && Pools.Knows(w, P, new PoolId(tile, i)) && Pools.HasStock(w, new PoolId(tile, i)))
                S(w, new Gather(P, i), 60);
    }

    static int Walk(World w, Direction dir, int legs, Pace pace = Pace.Careful)
    {
        int injuries = 0;
        for (int i = 0; i < legs && Alive(w); i++)
        {
            var leg = new Travel(P, dir, pace);
            for (int tries = 0; tries < 10; tries++)
            {
                var r = S(w, leg, 180);
                injuries += r.Events.OfType<Injured>().Count();
                // Too hurt to walk: rest it off, and try again.
                if (r.InterruptedReason == "you can barely move") { S(w, new Rest(P), 120); continue; }
                if (r.InterruptedReason is "arrived" or "died" || r.MinutesElapsed == 0) break;
            }
        }
        return injuries;
    }

    /// Wood comes off the camp stash, a load at a time; refusals (full, too cold for it) cost nothing.
    static void Feed(World w, double kg)
    {
        var stash = Items.StashAt(w, Map.Of(w).Camp);
        foreach (var wood in new[] { Resource.Pine, Resource.Birch, Resource.Oak, Resource.Kindling })
        {
            if (kg <= 0) break;
            double spare = wood == Resource.Kindling ? Items.Have(stash, wood) - 3 : Items.Have(stash, wood); // kindling kept for lighting
            double take = Math.Min(kg, spare);
            if (take <= 0) continue;
            S(w, new Unstash(P, wood, take), 1);
            double before = Items.Have(Items.Pack(w, P), wood);
            S(w, new AddFuel(P, wood, before), 1);
            kg -= before - Items.Have(Items.Pack(w, P), wood);
            S(w, new Stash(P, wood, 99), 1);
        }
    }

    /// Lights the fire if it is out (with what the pack and stash hold) and feeds it up to a working
    /// size, bigger in foul weather: a fire fed blind burns a tree a day. Returns whether it burns.
    static bool TendFire(World w)
    {
        if (!Alive(w)) return false;
        var camp = Map.Of(w).Camp;
        var pack = Items.Pack(w, P);
        var stash = Items.StashAt(w, camp);
        foreach (var (r, want) in new[] { (Resource.Kindling, 2.0), (Resource.Tinder, 0.3), (Resource.BirchBark, 0.3), (Resource.Amadou, 0.3) })
            if (Items.Have(pack, r) < want && Items.Have(stash, r) > 0) S(w, new Unstash(P, r, want - Items.Have(pack, r)), 1);
        bool Burning() => Fire.PitAt(w, camp) is FireId id && Fire.IsActive(w.Fires[id]);
        for (int i = 0; i < 20 && !Burning(); i++)
        {
            var tinder = new[] { Resource.BirchBark, Resource.Amadou, Resource.Tinder, Resource.Usnea, Resource.Chaga }
                .FirstOrDefault(r => Items.Have(pack, r) >= 0.05, Resource.Kindling);
            double kindling = Math.Min(0.3, Items.Have(pack, Resource.Kindling) - (tinder == Resource.Kindling ? 0.05 : 0));
            if (kindling <= 0 || S(w, new StartFire(P, ToolType.HandDrill, tinder, 0.05, kindling), 1).MinutesElapsed == 0) break;
        }
        if (!Burning()) return false;
        Feed(w, (Foul(w) ? 8.0 : 5.0) - Fire.TotalKg(w.Fires[Fire.PitAt(w, camp)!.Value]));
        return true;
    }

    /// The one thing handed over: an axe. Without one (there is no crafting) foraged
    /// deadwood alone does not keep a fire through a month of nights; see the report.
    /// A salt re-seeds every stream the player rolls on, so one map can be lived many ways: the
    /// script has to survive the dice, not one lucky draw of them. Null leaves the seed's own streams.
    static World NewWorld(int seed, int? salt = null)
    {
        var w = World.New(seed);
        Items.Pack(w, P).Gear.Add(Items.Make("stone_axe"));
        if (salt is int s)
            foreach (var sys in PlayerStreams) w.Rngs[StreamName.Of(sys, P)] = Rng.ForSystem(s * 1000003 + seed, sys + ":salt");
        return w;
    }
    internal static readonly string[] PlayerStreams = ["body", "travel", "work", "fire", "combat"];

    internal static (World World, Tally Tally) Run(int seed, int fromDay = 0, World? start = null, int? salt = null)
    {
        var w = start ?? NewWorld(seed, salt);
        var t = new Tally();
        for (int day = fromDay; day < Days && Alive(w); day++) PlayDay(w, t);
        while (w.Minute < Days * 1440L && Alive(w)) PlayDay(w, t); // days run short of 24 h
        return (w, t);
    }

    /// What a month lived means, for any run of the script.
    internal static void AssertLivedTheMonth(World w, Tally t)
    {
        Assert.True(Alive(w), $"a camper living off the land with a fire should live 30 days ({t.Injuries} injuries)");
        Assert.True(w.Minute >= Days * 1440L, $"only reached minute {w.Minute}");
        Assert.True(t.Injuries > 0, "thirty quick steps onto hazardous rock and never a scratch: the injury roll is not live");
        Assert.True(t.WaterLiters > Days, $"drank only {t.WaterLiters:0.0} L of melted snow in {Days} days");
        Assert.True(t.WoodKg > Days * 5, $"carried home only {t.WoodKg:0} kg of wood");
        Assert.True(t.GatheredKcal > 0, "a month of foraging found no food at all");
        // Walked to the wood and back several times a day for a month: that first leg out of camp
        // has worn into a Trail.
        var camp = Map.Of(w).Camp;
        var tier = Tracks.TierOf(w, camp, new TileId(camp.X + 1, camp.Y));
        Assert.True(tier >= TrailTier.Trail, $"daily route is only {tier}");
    }

    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void ThirtyDays_Headless_CompletesAliveAndSmall()
    {
        Log = output.WriteLine;
        var clock = Stopwatch.StartNew();
        var (w, t) = Run(17);
        Log = null;
        clock.Stop();

        output.WriteLine($"30 days: {t.Injuries} injuries, {clock.Elapsed.TotalSeconds:F1}s; gathered {t.GatheredKcal:0} kcal of food, " +
                         $"hand-fed {t.HandFedKcal:0} kcal ({t.HandFedKcal / (t.HandFedKcal + t.GatheredKcal):P0}); " +
                         $"melted and drank {t.WaterLiters:0.0} L; carried home {t.WoodKg:0} kg of wood");
        var camp = Map.Of(w).Camp;
        output.WriteLine($"camp to the wood: {Tracks.TierOf(w, camp, new TileId(camp.X + 1, camp.Y))}");
        AssertLivedTheMonth(w, t);
        string json = Save.ToJson(w);
        output.WriteLine($"save {json.Length} bytes");
        Assert.True(json.Length < 1_000_000, $"save is {json.Length} bytes");
        Assert.True(clock.Elapsed.TotalSeconds < 60, $"took {clock.Elapsed.TotalSeconds:F1}s");
    }

    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void ThirtyDays_SameSeedAndActions_SameWorld()
    {
        Assert.Equal(Save.ToJson(Run(17).World), Save.ToJson(Run(17).World));
        Assert.NotEqual(Save.ToJson(Run(17).World), Save.ToJson(Run(18).World));
    }

    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void ThirtyDays_SaveAndLoadAtDay15_MatchesAStraightRun()
    {
        var straight = Run(17).World;
        var half = NewWorld(17);
        var tally = new Tally();
        for (int day = 0; day < 15; day++) PlayDay(half, tally);
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

/// Its own class so its runs go alongside the gate's others (xUnit runs classes in parallel).
[Trait("Category", TestCategories.Slow)]
public class Phase2GateReseededTests
{
    /// The same map lived with the player's dice re-seeded: the script survives the dice, not one
    /// lucky draw. These salts each killed the script before it was hardened (third review: 9-13 of
    /// 48 salts died) - 4 and 33 of hypothermia in the day-16 blizzard, 38 and 43 of thirst when no
    /// fire would light to melt snow on. All 48 of the review's salts, and 144 more, now live.
    [Theory]
    [InlineData(4)]
    [InlineData(33)]
    [InlineData(38)]
    [InlineData(43)]
    public void ThirtyDays_ReseededDice_StillLiveTheMonth(int salt)
    {
        var (w, t) = Phase2GateTests.Run(17, salt: salt);
        Phase2GateTests.AssertLivedTheMonth(w, t);
    }
}
