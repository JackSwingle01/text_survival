using Survival;
using Survival.Tests.Verification;
using Xunit;
using Xunit.Abstractions;

/// Phase 3b: the scheduler, animal minds, the populated valley and the player's hunt. Every animal is
/// its own agent; a herd is the animals of one species on one tile.
public class HerdTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;

    static HerdMind Mind(World w, AgentId id) => (HerdMind)w.Agent(id).Mind;
    static IEnumerable<Agent> Herds(World w) => w.Agents.Values.Where(a => a.Mind is HerdMind);
    /// The herds standing in the world: its animals grouped by species and tile, each in id order.
    static List<List<Agent>> DerivedHerds(World w) => [.. Herds(w).GroupBy(a => (a.Species, a.Tile)).Select(g => g.ToList())];

    /// An open tile far from camp and off every populated herd's land, so a test's own herds meet
    /// nobody else's.
    static TileId Quiet(World w, Terrain terrain = Terrain.Plain)
    {
        var m = Map.Of(w);
        var claimed = Herds(w).SelectMany(h => ((HerdMind)h.Mind).Territory).ToHashSet();
        return Enumerable.Range(3, m.Width - 6).SelectMany(x => Enumerable.Range(3, m.Height - 6).Select(y => new TileId(x, y)))
            .First(t => m.TerrainAt(t) == terrain && m.CaveAt(t) == Cave.None && !claimed.Contains(t)
                && MapGen.Manhattan(t, m.Camp) > 30 && MapGen.Neighbors(t).All(n => Map.Blocked(m, t, n) == null && !claimed.Contains(n)));
    }

    /// A hungry pack and a caribou herd on one tile, both left to their own minds.
    static (Animals Wolves, Animals Caribou, TileId Tile) PackAndPrey(World w, int wolves = 5, int caribou = 3)
    {
        var tile = Quiet(w);
        var prey = Animals.Spawn(w, Species.Caribou, tile, caribou);
        var pack = Animals.Spawn(w, Species.Wolf, tile, wolves);
        foreach (var wolf in pack.In(w))
        {
            TestKit.SetHunger(wolf, 0.9);
            Fauna.MindOf(wolf).State = HerdState.Patrolling;
        }
        return (pack, prey, tile);
    }

    /// Keeps the player alive and idle at camp while the world runs: the player is not involved.
    static List<WorldEvent> Idle(World w, int minutes)
    {
        var events = new List<WorldEvent>();
        for (int done = 0; done < minutes; done += 60)
        {
            var me = w.Agent(P).Body;
            Body.Eat(me, 3000);
            me.HydrationMl = Body.MaxHydrationMl;
            me.EnergyMinutes = Body.MaxEnergyMinutes;
            events.AddRange(Sim.Step(w, new Rest(P), Math.Min(60, minutes - done)).Events);
        }
        return events;
    }

    // ---- gate: a pack hunt resolves headless in the running world ----

    [Fact]
    public void Gate_WolvesKillACaribouInScheduledTime_AndFeedOnTheCarcass()
    {
        int kills = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var w = TestWorlds.New(seed);
            var (wolves, caribou, tile) = PackAndPrey(w);
            var events = Idle(w, 120);
            var kill = events.OfType<Killed>().FirstOrDefault(k => k.Species == Species.Caribou && k.By is AgentId by && wolves.Ids.Contains(by));
            if (kill == null) continue;
            kills++;
            tile = kill.Tile; // where the chase ended, if the caribou ran
            Assert.NotEqual(tile, w.Agent(P).Tile); // the player was at camp throughout
            Assert.Contains(events.OfType<EncounterEnded>(), f => wolves.Ids.Contains(f.Agent));
            // The carcass is on the tile and the pack has eaten into it.
            Assert.True(wolves.Living(w) > 0);
            Assert.Contains(events.OfType<Killed>(), k => caribou.Ids.Contains(k.Agent));
            var fed = wolves.In(w).MinBy(Fauna.Hunger)!;
            // Meat starts at 65% of the muscle, and a caribou is 60% muscle: the eaten one has less.
            var eaten = w.Tiles[tile].Carcasses!.Where(c => c.Animal == "Caribou")
                .MinBy(c => c.Remaining[Resource.RawMeat] / (c.BodyWeightKg * 0.60 * 0.65))!;
            double meatLeft = eaten.Remaining[Resource.RawMeat], whole = eaten.BodyWeightKg;
            output.WriteLine($"seed {seed}: kill at minute {kill.Minute}, meat left {meatLeft:F1} kg of {whole:F0} kg, best fed wolf {Fauna.MindOf(fed).State} hunger {Fauna.Hunger(fed):F2}");
            Assert.True(meatLeft < whole * 0.60 * 0.65 - 0.5, "the pack never ate");
            Assert.True(Fauna.Hunger(fed) < 0.9, "feeding did not ease the pack's hunger");
        }
        Assert.True(kills >= 3, $"only {kills} of 6 packs made a kill in two hours beside the prey");
    }

    [Fact]
    public void AFrightenedPack_LeavesPreyAlone()
    {
        var w = TestWorlds.New(1);
        var (wolves, _, _) = PackAndPrey(w);
        foreach (var wolf in wolves.In(w)) Fauna.MindOf(wolf).Fear = 1.0; // fades at 0.015 a minute: still frightened for half an hour
        var events = Idle(w, 30);
        Assert.DoesNotContain(events.OfType<EncounterEnded>(), f => wolves.Ids.Contains(f.Agent));
    }

    // ---- gate: a wounded animal that gets away is tracked ----

    [Fact]
    public void Gate_AWoundedAnimalThatGetsAway_RunsOnItsOwn_AndLeavesPrints()
    {
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        w.Agent(P).Tile = tile;
        var herd = Animals.Spawn(w, Species.Caribou, tile, 5);
        var e = Combat.Create(w, [new Fighter(P)], [new Fighter(herd[1])], tile, 5, Awareness.Engaged, Awareness.Engaged);
        var unit = e.Units.Single(u => u.Team == 1);
        var wounded = Combat.BodyOf(w, unit);
        wounded.Regions[2].Muscle.Condition = 0.6;
        Effects.Add(wounded, EffectKind.Bleeding, 0.3);
        Assert.Null(Combat.Apply(w, e, unit, CombatAction.Flee, []).Refused); // nobody presses it: it gets away
        var r = Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        var got = r.Events.OfType<WoundedEscaped>().Single();
        var escaped = got.Agent;

        var split = w.Agent(escaped);
        Assert.Equal(herd[1], escaped);
        Assert.Equal(HerdState.Fleeing, Mind(w, escaped).State);
        Assert.All(herd.Ids.Where(id => id != escaped), id => Assert.Equal(tile, w.Agent(id).Tile)); // the rest were not in the fight

        // It ran on the way it fled, a tile for every 100 m, and its trail runs that way.
        Assert.NotNull(got.Heading);
        Assert.NotEqual(tile, split.Tile);
        Assert.InRange(MapGen.Manhattan(tile, split.Tile), 1, 5);
        Assert.True(MapGen.Manhattan(Map.Step(tile, got.Heading!.Value), split.Tile) < MapGen.Manhattan(tile, split.Tile), "it ran the other way");
        Assert.Contains(Tracks.Read(w, split.Tile), s => s.Maker == TrackMaker.Hoof);

        // It lives on its own mind, through the shared travel: it bolts on.
        var events = Idle(w, 30);
        Assert.Contains(events.OfType<Moved>(), m => m.Who == escaped);
    }

    [Fact]
    public void ABleedingAnimal_DiesWhereItFalls_AsACarcass()
    {
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        var lone = Agents.Spawn(w, Species.Caribou, tile);
        Mind(w, lone).State = HerdState.Resting;
        w.Agent(lone).Members[0].BloodPct = 0.52;
        Effects.Add(w.Agent(lone).Members[0], EffectKind.Bleeding, 1.0);
        var events = Sim.Advance(w, 240);
        var death = events.OfType<Killed>().Single(k => k.Agent == lone);
        Assert.Null(death.By);
        Assert.False(w.Agents.ContainsKey(lone));
        Assert.NotNull(w.Tiles[death.Tile].Carcasses);
    }

    /// An agent's own streams, by exact name: "body:1" is agent 1's, "body:10" is not.
    static List<string> StreamsOf(World w, AgentId id) =>
        w.Rngs.Keys.Select(k => k.Value).Where(k => k.Split(':') is [_, var who] && who == id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

    /// A herd that is gone takes its streams with it (they used to stay in every save for good),
    /// both ways a herd goes: its last member bleeding out (Fauna) and its last falling in a fight
    /// (the aftermath). Its id is never made again, so nothing could roll on them.
    [Fact]
    public void AGoneHerd_TakesItsStreamsWithIt()
    {
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        var lone = Agents.Spawn(w, Species.Caribou, tile);
        Mind(w, lone).State = HerdState.Resting;
        w.Agent(lone).Members[0].BloodPct = 0.52;
        Effects.Add(w.Agent(lone).Members[0], EffectKind.Bleeding, 1.0);
        Sim.Advance(w, 1);
        Assert.True(w.Agents.ContainsKey(lone));
        Assert.NotEmpty(StreamsOf(w, lone)); // its wounds roll on its own body stream
        Sim.Advance(w, 239);
        Assert.False(w.Agents.ContainsKey(lone));
        Assert.Empty(StreamsOf(w, lone));
        Assert.True(Agents.Spawn(w, Species.Caribou, tile).Value > lone.Value, "a gone herd's id was made again");

        int pulledDown = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var v = TestWorlds.New(seed);
            var (_, caribou, _) = PackAndPrey(v, caribou: 1);
            var events = Idle(v, 120);
            if (!events.OfType<Killed>().Any(k => k.Agent == caribou)) continue;
            pulledDown++;
            Assert.False(v.Agents.ContainsKey(caribou));
            Assert.Empty(StreamsOf(v, caribou));
            Assert.Throws<InvalidOperationException>(() => v.Rng("combat", caribou));
        }
        Assert.True(pulledDown > 0, "no pack pulled its lone caribou down: the aftermath's removal is untested");
    }

    [Fact]
    public void AStreamName_IsForExactlyOneAgent()
    {
        Assert.True(StreamName.Of("travel", new AgentId(1)).IsFor(new AgentId(1)));
        Assert.False(StreamName.Of("travel", new AgentId(10)).IsFor(new AgentId(1)));
        Assert.False(StreamName.Of("travel", new AgentId(1)).IsFor(new AgentId(10)));
        Assert.False(new StreamName("herds").IsFor(new AgentId(0)));
    }

    // ---- gate: the scheduler is independent of step granularity and of saving ----

    static World Planted(int seed)
    {
        var w = TestWorlds.New(seed);
        PackAndPrey(w);
        return w;
    }

    /// The player keeps resting, in steps of the given length; when that is impossible (dead), the clock runs on.
    static World RunPlayer(int seed, int totalMinutes, int stepMinutes, int saveEverySteps = 0)
    {
        var w = Planted(seed);
        int steps = 0;
        while (w.Minute < totalMinutes)
        {
            if (saveEverySteps > 0 && ++steps % saveEverySteps == 0) w = TestWorlds.Load(Save.ToJson(w));
            int n = (int)Math.Min(stepMinutes, totalMinutes - w.Minute);
            if (Body.IsAlive(w.Agent(P).Body)) Sim.Step(w, new Wait(P), n);
            else Sim.Advance(w, n);
        }
        return w;
    }

    [Fact]
    public void Gate_OneLongStep_ManyShortOnes_AndSaveLoad_GiveTheSameWorld()
    {
        string one = Save.ToJson(RunPlayer(3, 300, 300));
        Assert.Equal(one, Save.ToJson(RunPlayer(3, 300, 1)));
        Assert.Equal(one, Save.ToJson(RunPlayer(3, 300, 7, saveEverySteps: 5)));
    }

    /// Planted worlds, each with the same baseline player, run straight and in 97-minute chunks with a
    /// save and load after each: the planted hunt happens, and both runs end in the same world.
    static void AssertIndependentOfChunkingAndSaves(Func<World> planted, int minutes)
    {
        World Make()
        {
            var w = planted();
            w.Agent(P).Members[0] = BodyState.BaselineHuman(); // the same player in both
            return w;
        }
        var a = Make();
        var events = Sim.Advance(a, minutes);
        Assert.Contains(events, e => e is EncounterEnded); // the planted hunt, at least
        string straight = Save.ToJson(a);

        var b = Make();
        while (b.Minute < minutes)
        {
            Sim.Advance(b, (int)Math.Min(97, minutes - b.Minute));
            b = TestWorlds.Load(Save.ToJson(b));
        }
        Assert.Equal(straight, Save.ToJson(b));
    }

    /// The rule of Gate_ThreePopulatedDays_AreIndependentOfChunkingAndSaves, fast: a day of the planted
    /// pack and prey in a world with nobody else in it.
    [Fact]
    public void Gate_APlantedHuntsDay_IsIndependentOfChunkingAndSaves() =>
        AssertIndependentOfChunkingAndSaves(() =>
        {
            var w = ConservationWorlds.Seeded();
            PackAndPrey(w);
            return w;
        }, 1440);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Gate_ThreePopulatedDays_AreIndependentOfChunkingAndSaves() =>
        AssertIndependentOfChunkingAndSaves(() => Planted(5), 3 * 1440);

    [Fact]
    public void HerdsAreNotTickedForMinutesAHeadlessFightAlreadyLived()
    {
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        var wolves = Animals.Spawn(w, Species.Wolf, tile, 3);
        var caribou = Animals.Spawn(w, Species.Caribou, tile, 2);
        var fight = Combat.Create(w, [new Fighter(wolves[0]), new Fighter(wolves[1])], [new Fighter(caribou[0])], tile, 5, Awareness.Engaged, Awareness.Unaware);
        var survivor = w.Agent(caribou[0]).Body;
        Effects.Add(survivor, EffectKind.Bleeding, 0.5);
        Combat.ResolveHeadless(w, fight, []);
        if (!Body.IsAlive(survivor) || fight.Rounds == 0) return;
        Assert.Equal(w.Minute + fight.Rounds, survivor.WoundsLivedThroughMinute);
        // The fight's rounds were its next minutes: living them again draws no more blood.
        double blood = survivor.BloodPct;
        Fauna.MindOf(w.Agent(caribou[0])).State = HerdState.Resting;
        Sim.Advance(w, fight.Rounds);
        Assert.Equal(blood, survivor.BloodPct);
        Sim.Advance(w, 1);
        Assert.True(survivor.BloodPct < blood, "and after them it bleeds again");
    }

    [Fact]
    public void APersonInAHeadlessFight_DoesNotBleedItsRoundsTwice()
    {
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        w.Agent(P).Tile = tile;
        var me = w.Agent(P).Body;
        Effects.Add(me, EffectKind.Bleeding, 0.5);
        var wolf = Agents.Spawn(w, Species.Wolf, tile);
        var fight = Combat.Create(w, [new Fighter(P)], [new Fighter(wolf)], tile, 20, Awareness.Engaged, Awareness.Engaged);
        fight.Units[0].InField = false; // as if the player had just fled: the rest is fought headless
        Combat.ResolveHeadless(w, fight, []);
        Assert.Equal(0, me.WoundsLivedThroughMinute); // never in the field for those rounds
    }

    // ---- gate: herds move, graze and wear trails ----

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void Gate_HerdsMoveGrazeAndWearTrails()
    {
        var w = TestWorlds.New(17);
        var events = Sim.Advance(w, 4 * 1440);
        var herdIds = Herds(w).Select(h => h.Id).ToHashSet();
        var moves = events.OfType<Moved>().Where(m => herdIds.Contains(m.Who)).ToList();
        output.WriteLine($"4 days: {moves.Count} animal moves by {moves.Select(m => m.Who).Distinct().Count()} animals, {w.Trails.Count} worn edges");
        Assert.True(moves.Select(m => m.Who).Distinct().Count() >= herdIds.Count / 2, "most animals never moved");
        var grazed = w.Tiles.Where(t => t.Value.Pools?.Values.OfType<ForageState>().Any(s => s.Grazed.Values.Any(g => g.Level > 0)) == true).ToList();
        Assert.NotEmpty(grazed);
        var camp = Map.Of(w).Camp;
        Assert.Contains(w.Trails.Keys, e => MapGen.Manhattan(e.A, camp) > Fauna.SafeZoneTiles && Tracks.TierOf(w, e.A, e.B) >= TrailTier.Trace);
        Assert.Contains(moves, m => Tracks.Read(w, m.To).Any(s => s.Maker is TrackMaker.Hoof or TrackMaker.Paw));
    }

    [Fact]
    public void GrazingThinsWhatThisDietEats_AndNotTheRest()
    {
        // The strip, all pasture, with no kin anywhere to draw them off it (FoodTests).
        var w = FoodTests.Pasture();
        var tile = new TileId(20, 1);
        var bison = Animals.Spawn(w, Species.Bison, tile, 1);
        var deer = Animals.Spawn(w, Species.Caribou, tile, 1);
        foreach (var a in bison.In(w).Concat(deer.In(w))) { TestKit.SetHunger(a, 1.0); Fauna.MindOf(a).State = HerdState.Grazing; }
        Sim.Advance(w, 30);
        var pool = new PoolId(tile, 0);
        Assert.True(Pools.GrazedLevel(w, pool, Resource.Tinder) > 0);   // bison: grass
        Assert.True(Pools.GrazedLevel(w, pool, Resource.Usnea) > 0);    // caribou: lichen
        Assert.Equal(0, Pools.GrazedLevel(w, pool, Resource.Berries));  // nobody here eats berries
    }

    // ---- old bugs fixed ----

    /// What the herd's animals hold in their calorie stores, and burn a minute.
    static double Held(World w, Animals herd) => herd.In(w).Sum(a => a.Body.CaloriesKcal);
    static double Burn(World w, Animals herd) => herd.In(w).Sum(a => Fauna.BurnKcalPerMinute(a.Body));

    [Fact]
    public void AHerdOnTheMove_StillGetsHungrier()
    {
        // old game: hunger stood still while a herd was walking (bug, fixed).
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        var herd = Animals.Spawn(w, Species.Caribou, tile, 3);
        foreach (var a in herd.In(w)) (Fauna.MindOf(a).State, Fauna.MindOf(a).FleeFrom) = (HerdState.Fleeing, tile);
        double before = Held(w, herd), burn = Burn(w, herd);
        Sim.Advance(w, 1);
        Assert.NotNull(w.Agent(herd).Crossing);
        Sim.Advance(w, 2);
        Assert.NotNull(w.Agent(herd).Crossing); // still on the way
        Assert.Equal(before - 3 * burn, Held(w, herd), 9);
    }

    [Fact]
    public void AFleeingPack_RunsOneTileTogether_ThenSettles()
    {
        // old game: a fleeing pack fled from the player's tile every minute until the map stopped it (bug, fixed).
        // Each wolf flees on its own mind; the pack runs one way because each follows the ones already going.
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        var pack = Animals.Spawn(w, Species.Wolf, tile, 4);
        foreach (var id in pack.Ids) Fauna.AfterEncounter(w, id, CombatResult.Fled, killedOne: false, tile);
        Assert.Equal(HerdState.Fleeing, Mind(w, pack).State);
        var events = Sim.Advance(w, 120);
        Assert.All(pack.Ids, id => Assert.Single(events.OfType<Moved>(), m => m.Who == id));
        Assert.Single(pack.In(w).Select(a => a.Tile).Distinct());
        Assert.All(pack.In(w), a => Assert.NotEqual(HerdState.Fleeing, Fauna.MindOf(a).State));
    }

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void APatrollingPack_RangesAcrossItsLand()
    {
        // old game: each patrol step aimed at the next tile in flood-fill order, so a pack never left its den's doorstep (bug, fixed).
        var w = TestWorlds.New(17);
        var pack = Herds(w).First(h => h.Species == Species.Wolf);
        var den = Mind(w, pack.Id).Territory[0];
        int farthest = 0;
        for (int day = 0; day < 6; day++)
        {
            for (int h = 0; h < 24; h++) { Sim.Advance(w, 60); farthest = Math.Max(farthest, MapGen.Manhattan(w.Agent(pack.Id).Tile, den)); }
        }
        output.WriteLine($"pack ranged {farthest} tiles from its den");
        Assert.True(farthest >= 4, $"the pack ranged only {farthest} tiles from its den in six days");
    }

    // ---- the populated valley ----

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(42)]
    public void TheValley_IsPopulatedLikeTheOldGame(int seed)
    {
        var w = World.New(seed);
        var m = Map.Of(w);
        // Every animal is its own agent, and the herds are where they were put together.
        Assert.All(Herds(w), a => Assert.Single(a.Members));
        var groups = DerivedHerds(w);
        var herds = groups.Select(g => g[0]).ToList();
        int Count(params Species[] s) => herds.Count(h => s.Contains(h.Species));
        output.WriteLine(string.Join(", ", groups.GroupBy(g => g[0].Species).Select(g => $"{g.Key} {g.Count()}x{string.Join("/", g.Select(h => h.Count))}")));
        Assert.InRange(Count(Species.Wolf), 1, 2);
        Assert.InRange(Count(Species.Caribou), 2, 3);
        Assert.InRange(Count(Species.Megaloceros) + Count(Species.Bison), 8, 15);
        Assert.InRange(Count(Species.SaberTooth), 1, 2);
        Assert.InRange(Count(Species.Hyena), 1, 2 + m.Width * m.Height); // the old 1-2 packs, plus cave clans
        Assert.InRange(herds.Count(h => h.Species is Species.Bear or Species.CaveBear && m.CaveAt(h.Tile) == Cave.None), 3, 5);
        foreach (var h in herds)
        {
            var land = ((HerdMind)h.Mind).Territory;
            Assert.Equal(h.Tile, land[0]);
            Assert.Equal(land.Count, land.Distinct().Count());
            Assert.All(land, t => Assert.True(m.TerrainAt(t).IsPassable()));
            if (m.CaveAt(h.Tile) != Cave.None) { Assert.All(land, t => Assert.Equal(m.CaveIdAt(h.Tile), m.CaveIdAt(t))); continue; }
            Assert.True(MapGen.Manhattan(h.Tile, m.Camp) > Fauna.SafeZoneTiles, $"{h.Species} starts {MapGen.Manhattan(h.Tile, m.Camp)} tiles from camp");
            var (min, max) = h.Species switch
            {
                Species.Wolf => (3, 80),
                Species.Bear or Species.CaveBear => (3, 128),
                Species.Caribou => (6, 192),
                Species.Megaloceros => (3, 96),
                Species.Bison => (3, 400),
                Species.SaberTooth => (4, 160),
                Species.Raven => (3, 250),
                Species.Fox => (3, 128),
                _ => (3, 112),
            };
            Assert.InRange(land.Count, min, max);
        }
        Assert.All(groups, g => Assert.InRange(g.Count, 1, 15));
        // Predators hold their land apart from each other's.
        var wolfLand = herds.Where(h => h.Species == Species.Wolf).SelectMany(h => ((HerdMind)h.Mind).Territory).ToList();
        Assert.Equal(wolfLand.Count, wolfLand.Distinct().Count());
        Assert.Equal(Save.ToJson(World.New(seed)), Save.ToJson(w)); // same seed, same herds
    }

    [Fact]
    public void PreyLand_MayOverlapAPack()
    {
        // Judgment: old prey drew their land from what wolf packs left, so no pack ever had prey living on its land.
        int overlapping = 0;
        foreach (int seed in new[] { 1, 2, 3, 17, 42 })
        {
            var w = TestWorlds.New(seed);
            var wolfLand = Herds(w).Where(h => h.Species == Species.Wolf).SelectMany(h => ((HerdMind)h.Mind).Territory).ToHashSet();
            if (Herds(w).Any(h => !Fauna.IsPredator(h.Species) && ((HerdMind)h.Mind).Territory.Any(wolfLand.Contains))) overlapping++;
        }
        Assert.True(overlapping >= 2, $"prey share a pack's land on only {overlapping} of 5 seeds");
    }

    // ---- the player's hunt ----

    /// Old RunHunt took the sighting's DetectionModifier as every stalked animal's DetectionFactor:
    /// 0.45-0.9 for wounded, grazing or resting prey, 2.0-2.5 for alert prey (old HuntRunner.Run,
    /// HuntingSightings). One sighting of the behaviour's pool was picked at random.
    [Trait("Category", TestCategories.Slow)]
    [Theory]
    [InlineData("grazing", 0.7, 0.75, 0.65)]
    [InlineData("feeding", 0.7, 0.75, 0.65)]
    [InlineData("resting", 0.9, 0.85)]
    [InlineData("wounded", 0.5, 0.45)]
    [InlineData("fleeing", 2.0, 2.5, 2.2)]
    [InlineData("fearful", 2.0, 2.5, 2.2)]
    [InlineData("patrolling", 1.2, 1.3)]
    public void Hunt_StalkedAnimalsWatchAsTheOldSightingsDid(string doing, params double[] oldPool)
    {
        var seen = new HashSet<double>();
        for (int seed = 1; seed <= 24; seed++)
        {
            var w = World.New(seed);
            var tile = Quiet(w);
            w.Agent(P).Tile = tile;
            var herd = Animals.Spawn(w, doing == "patrolling" ? Species.Wolf : Species.Caribou, tile, 4);
            foreach (var a in herd.In(w))
            {
                Fauna.MindOf(a).State = doing switch
                {
                    "grazing" => HerdState.Grazing,
                    "feeding" => HerdState.Feeding,
                    "fleeing" => HerdState.Fleeing,
                    "patrolling" => HerdState.Patrolling,
                    _ => HerdState.Resting,
                };
                if (doing == "fearful") Fauna.MindOf(a).Fear = 0.8;
            }
            if (doing == "wounded") w.Agent(herd).Body.BloodPct = 0.9;
            var enc = Fauna.BeginHunt(w, P, herd);
            var animals = enc.Units.Where(u => herd.Ids.Contains(u.Agent)).ToList();
            Assert.NotEmpty(animals);
            Assert.All(animals, u => Assert.Equal(animals[0].DetectionFactor, u.DetectionFactor));
            Assert.Contains(animals[0].DetectionFactor, oldPool);
            Assert.Equal(1.0, Combat.UnitOf(enc, P).DetectionFactor);
            seen.Add(animals[0].DetectionFactor);
        }
        Assert.Equal(oldPool.Length, seen.Count); // every sighting of the pool turns up
    }

    [Trait("Category", TestCategories.Slow)]
    [Fact]
    // The naive stalk below (advance to throwing range on open plain) kills about one hunt in fifty, so
    // this walks seeds until one does: seed 3 did with shared streams, seed 43 once each agent rolled
    // on its own, seed 15 since resting prey watches as the old sightings did (re-pinned 2026-11-21).
    // The stalk's odds are a balance question, not this test's.
    public void ThePlayer_CanHuntAHerdMember_ButcherItAndEat()
    {
        // Kills are rare (rebuild-combat-balance.md; the first one comes at seed 80 today), so this looks
        // until one comes: a fixed handful of seeds broke whenever anything else shifted an agent id or a
        // tile. How rare is the combat balance's (Tools/Tune).
        for (int seed = 1; seed <= 200; seed++)
        {
            var w = World.New(seed);
            var tile = Quiet(w);
            w.Agent(P).Tile = tile;
            var pack = Items.Pack(w, P);
            var spear = Items.Make("wooden_spear");
            pack.Gear.Add(spear);
            pack.Gear.Add(Items.Make("flint_knife"));
            var herd = Animals.Spawn(w, Species.Caribou, tile, 4);
            foreach (var a in herd.In(w)) Fauna.MindOf(a).State = HerdState.Resting;
            Fauna.BeginHunt(w, P, herd, spear);
            CombatResult? result = null;
            for (int round = 0; round < 200 && result == null; round++)
            {
                var enc = Combat.EncounterOf(w, P)!;
                var me = Combat.UnitOf(enc, P);
                var acts = Combat.Actions(w, enc, me);
                var target = Combat.NearestEnemy(enc, me);
                // Advance to throwing range, throw, then close and strike.
                double gap = target == null ? 0 : me.Position.DistanceTo(target.Position);
                var act = acts.Contains(CombatAction.Attack) ? CombatAction.Attack
                    : gap <= 10 && acts.Contains(CombatAction.Throw) ? CombatAction.Throw
                    : acts.Contains(CombatAction.Advance) ? CombatAction.Advance : CombatAction.Wait;
                var r = Sim.Step(w, new Fight(P, act, target == null ? null : enc.Units.IndexOf(target)), 1);
                result = r.Events.OfType<EncounterEnded>().FirstOrDefault(f => f.Agent == P)?.Result;
            }
            output.WriteLine($"seed {seed}: {result}");
            // A kill is a carcass, even when the rest of the herd ran (AnimalFled).
            if (w.Tiles.GetValueOrDefault(tile)?.Carcasses is not [var carcass, ..]) continue;

            var butchered = Sim.Step(w, new Butcher(P, carcass.Id), 240);
            double meat = Items.Have(pack, Resource.RawMeat);
            Assert.True(meat > 1, $"butchering a caribou gave {meat:F2} kg of meat ({butchered.InterruptedReason})");
            var ate = Sim.Step(w, new Eat(P, Resource.RawMeat, 0.5), 10).Events.OfType<Ate>().Single();
            Assert.True(ate.Kcal > 0);
            output.WriteLine($"seed {seed}: killed, butchered {meat:F1} kg of meat, ate {ate.Kcal:F0} kcal");
            return;
        }
        Assert.Fail("two hundred stalks with a spear and never a kill");
    }

    /// A well-placed stalk of a resting caribou with a wooden spear can kill it: the odds are the combat
    /// balance's (Tools/Tune), this only holds that a careful hunt is not hopeless.
    [Fact]
    public void AWellPlacedStalk_CanKillACaribou()
    {
        var w = TestWorlds.New(17);
        var tile = Quiet(w);
        w.Agent(P).Tile = tile;
        for (int stalk = 1; stalk <= 1000; stalk++)
        {
            var player = w.Agent(P);
            player.Members[0] = BodyState.BaselineHuman(); // whole again for every stalk
            var pack = Items.Pack(w, P);
            pack.Gear.RemoveAll(g => g.Kind == "wooden_spear");
            var spear = Items.Make("wooden_spear");
            pack.Gear.Add(spear);
            var herd = Animals.Spawn(w, Species.Caribou, tile, 4);
            foreach (var a in herd.In(w)) Fauna.MindOf(a).State = HerdState.Resting;
            Fauna.BeginHunt(w, P, herd, spear);
            for (int round = 0; round < 200 && Combat.EncounterOf(w, P) is { } enc; round++)
            {
                var me = Combat.UnitOf(enc, P);
                var acts = Combat.Actions(w, enc, me);
                var target = Combat.NearestEnemy(enc, me);
                double gap = target == null ? 0 : me.Position.DistanceTo(target.Position);
                var act = acts.Contains(CombatAction.Attack) ? CombatAction.Attack
                    : gap <= 10 && acts.Contains(CombatAction.Throw) ? CombatAction.Throw
                    : acts.Contains(CombatAction.Advance) ? CombatAction.Advance : CombatAction.Wait;
                Sim.Step(w, new Fight(P, act, target == null ? null : enc.Units.IndexOf(target)), 1);
            }
            if (w.Tiles.GetValueOrDefault(tile)?.Carcasses is { Count: > 0 })
            {
                output.WriteLine($"a kill on stalk {stalk}");
                return;
            }
            foreach (var id in Herds(w).Select(h => h.Id).ToList()) Agents.Remove(w, id);
        }
        Assert.Fail("1,000 careful stalks killed no caribou");
    }

    /// Every agent rolls on its own streams: a pack pulling down caribou across the valley, bleeding
    /// and fighting, leaves the player's injuries and body as they were without it. (Shared streams
    /// let any herd's fight move the player's next injury.)
    [Fact]
    public void AFarFight_LeavesThePlayersRollsAlone()
    {
        (string Rolls, int Mine, int HerdInjuries) Run(bool farFight)
        {
            var w = TestWorlds.New(17);
            var m = Map.Of(w);
            if (farFight)
            {
                var far = Enumerable.Range(0, m.Width / 2).Select(dx => m.Width - 30 - dx).SelectMany(x => Enumerable.Range(10, m.Height - 20).Select(y => new TileId(x, y)))
                    .First(t => m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && !m.OnPass(t));
                Animals.Spawn(w, Species.Caribou, far, 3);
                foreach (var wolf in Animals.Spawn(w, Species.Wolf, far, 5).In(w))
                {
                    TestKit.SetHunger(wolf, 0.9);
                    Fauna.MindOf(wolf).State = HerdState.Patrolling;
                }
            }
            // Seed 17: forest at (2,33), rock below it, so every quick step south rolls for an injury.
            w.Agent(P).Tile = new TileId(2, 33);
            var events = new List<WorldEvent>();
            for (int i = 0; i < 30 && Body.IsAlive(w.Agent(P).Body); i++)
            {
                events.AddRange(Sim.Step(w, new Travel(P, Direction.South), 60).Events);
                events.AddRange(Sim.Step(w, new Travel(P, Direction.North), 60).Events);
                events.AddRange(Sim.Step(w, new Wait(P), 30).Events);
            }
            var mine = events.OfType<Injured>().Where(e => e.Who == P).Select(e => $"{e.Minute} {e.Region} {e.Type} {e.Amount}");
            // The player's own streams, by exact name ("travel:0" and not "travel:10").
            var streams = w.Rngs.Where(kv => kv.Key == StreamName.Of(kv.Key.Value.Split(':')[0], P)).Select(kv => $"{kv.Key}={kv.Value.State}");
            string body = System.Text.Json.JsonSerializer.Serialize(w.Agent(P).Body);
            return (string.Join("\n", mine.Concat(streams)) + "\n" + body, mine.Count(), events.OfType<Injured>().Count(e => e.Who != P));
        }
        var alone = Run(false);
        var beside = Run(true);
        Assert.True(beside.HerdInjuries > 0, "the far pack never drew blood: nothing tested");
        Assert.True(alone.Mine > 0, "thirty quick steps onto rock and never hurt: nothing tested");
        Assert.Equal(alone.Rolls, beside.Rolls);
    }

    // ---- saves ----

    [Fact]
    public void AWorkSessionUnderWay_RoundTripsThroughASave()
    {
        var w = TestWorlds.New(1);
        var herd = Herds(w).First();
        herd.Doing = new Doing { Work = new WorkSession { Activity = new Gather(herd.Id, 0, ForageFocus.Food), Minutes = 3 }, MinutesLeft = 7 };
        Items.Add(herd.Doing.Work!.Got, Resource.Berries, 0.2);
        string json = Save.ToJson(w);
        var back = TestWorlds.Load(json).Agent(herd.Id).Doing!;
        Assert.Equal(new Gather(herd.Id, 0, ForageFocus.Food), back.Activity);
        Assert.Equal(0.2, Items.Have(back.Work!.Got, Resource.Berries));
        Assert.Equal(json, Save.ToJson(TestWorlds.Load(json)));
    }

    [Fact]
    [Trait("Category", TestCategories.Slow)] // a long-run simulation: about 43 s in Release
    public void ThirtyPopulatedDays_StayCheap()
    {
        var w = TestWorlds.New(17);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var events = Sim.Advance(w, 30 * 1440);
        clock.Stop();
        long allocatedMB = (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / (1024 * 1024);
        int json = Save.ToJson(w).Length;
        output.WriteLine($"30 days, {Herds(w).Count()} herds: {clock.Elapsed.TotalSeconds:F1}s, {allocatedMB} MB allocated, {events.OfType<Moved>().Count()} moves, " +
                         $"{events.OfType<EncounterEnded>().Count()} fight sides, {events.OfType<Killed>().Count()} killed; save {json / 1024} KB");
        // Cost is budgeted in bytes allocated, not seconds: the sim is single-threaded, so this thread's
        // allocation is deterministic for a seed and blind to machine load, and it tracks the work done.
        // Re-baselined 2026-11-23 for one agent per animal (#81), Release: herds as agents 4,064 MB in 5.1 s,
        // individuals 4,332 MB in about 10 s (about 110 animals). The ticket's ceiling is 3x the herd baseline,
        // 12,192 MB; the cap below stays tighter. Wall clock flaked at 13-21 s under parallel suites (37 s in Debug
        // alone with individuals); the 120 s limit is only a backstop for a blow-up that does not allocate.
        // These budgets are tripwires, not design limits: 6000 MB is about three times what a populated month
        // allocated when it was set, and the 1 MB save was the phase 1 exit criterion. Crossing one calls for
        // Jack's judgement on whether the cost buys something the player sees (then re-baseline). An agent does
        // not redesign a feature to get back under one, nor raise the number: it reports the measurement in its PR.
        // Re-baselined on purpose 2026-11-23 for thirst, water and rest on the planner (#82, whose criteria allow it with
        // the number stated), Release, seed 17: 9,423 MB in about 50 s (main 5,948 MB on the same probe), save 860 KB.
        // Animals now drink and sleep: route searches for water and dens, sensing, and predators up in their hours.
        // Under #81's 3x ceiling (12,192 MB); the cap leaves the usual margin over the measurement.
        // Lowered 2026-11-24 for the pooled route search (#133), Release, seed 17: 5,676 MB in about 25 s (main 8,106 MB
        // on the same probe), the same 39,161 moves.
        Assert.True(allocatedMB < 6_600, $"allocated {allocatedMB} MB");
        Assert.True(clock.Elapsed.TotalSeconds < 120, $"took {clock.Elapsed.TotalSeconds:F1}s");
        Assert.True(json < 1_000_000, $"save is {json} bytes");
        Assert.DoesNotContain("\"Layers\"", Save.ToJson(w)); // everyone read the ground; no tile stored any
    }

    // ---- wounds mend; the lame stay behind ----

    /// In forest, where a caribou's lichen grows: a lame animal with nothing to eat where it lies is never fed enough to mend.
    static Animals HerdWithALameMember(World w, int members)
    {
        var tile = Quiet(w, Terrain.Forest);
        var land = MapGen.Neighbors(tile).Prepend(tile).SelectMany(t => MapGen.Neighbors(t).Prepend(t)).Distinct()
            .Where(t => Map.Of(w).TerrainAt(t).IsPassable()).ToList();
        land.Remove(tile);
        land.Insert(0, tile);
        var id = Animals.Spawn(w, Species.Caribou, tile, members, land);
        var lame = w.Agent(id).Body;
        var rng = Rng.ForSystem(1, "test");
        while (Body.GetCapacities(lame).Moving > 0.1)
        {
            Body.Damage(lame, new DamageInfo(8, DamageType.Blunt, BodyTarget.AnyLeg), rng);
            lame.Effects.Clear(); // lame from its legs, not from pain that fades
        }
        // A hurt back as well: four broken legs alone leave exactly the tenth of Moving the rest of the
        // body gives, which is Fauna.Mobile's line, and a minute's mending crosses it.
        lame.Regions.Single(r => r.Name == "Torso").Muscle.Condition = 0.5;
        Assert.True(Body.IsAlive(lame));
        return id;
    }

    [Fact]
    public void ALameAnimal_StaysBehind_AndTheHerdMovesOn()
    {
        // Review P11: one member that could not walk held a herd of five still for ten days. Each animal
        // walks on its own legs now: the lame one stays, the rest drift on together.
        var w = TestWorlds.New(17);
        var herd = HerdWithALameMember(w, 5);
        var tile = w.Agent(herd).Tile;
        var events = new List<WorldEvent>();
        for (int i = 0; i < 1440 && !events.OfType<Moved>().Any(m => herd.Ids.Skip(1).Contains(m.Who)); i++) events.AddRange(Sim.Advance(w, 1));
        Assert.Contains(events.OfType<Moved>(), m => herd.Ids.Skip(1).Contains(m.Who));
        Assert.Equal(tile, w.Agent(herd[0]).Tile); // it could not follow
    }

    [Fact]
    public void AnimalWounds_MendAtTheOldRate()
    {
        // Old PreyBehavior: WoundSeverity fell 0.0002 a minute. Here the tissue and the blood do.
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        var id = Agents.Spawn(w, Species.Caribou, tile);
        Mind(w, id).State = HerdState.Resting;
        var body = w.Agent(id).Members[0];
        body.Regions[2].Muscle.Condition = 0.6;
        body.BloodPct = 0.8;
        double before = Combat.WoundLevel(body);
        body.Regions[3].Bone.Condition = 0.99999;
        Sim.Advance(w, 500);
        Assert.Equal(before - 500 * Fauna.MendPctPerMinute, Combat.WoundLevel(body), 9);
        Assert.Equal(0.8 + 500 * Fauna.MendPctPerMinute, body.BloodPct, 9);
        Assert.Equal(1.0, body.Regions[3].Bone.Condition); // mended whole, and no further
    }

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void ALoneLameAnimal_MendsAndWalksAgain()
    {
        var w = TestWorlds.New(17);
        var lone = HerdWithALameMember(w, 1);
        var body = w.Agent(lone).Body;
        var events = Sim.Advance(w, 6 * 1440);
        Assert.True(Body.GetCapacities(body).Moving > 0.9, $"moving {Body.GetCapacities(body).Moving:F2} after six days");
        Assert.Contains(events.OfType<Moved>(), m => m.Who == lone);
    }

    // ---- every body lives every minute exactly once ----

    /// A tracer: an effect nothing in an ice-age world gives or takes, fading at a fixed rate on each
    /// minute a body's wounds live. How far it has faded counts those minutes.
    /// Lasts 60 minutes at Hyperthermia's fading: longer than any fight's rounds run ahead plus the window.
    const double TracerPct = 0.5;
    static void Trace(BodyState b) => Effects.Add(b, EffectKind.Hyperthermia, TracerPct);
    static int MinutesLived(BodyState b) => (int)Math.Round((TracerPct - Effects.Severity(b, EffectKind.Hyperthermia)) / (0.5 / 60));

    static void AssertEachLivedOnce(IEnumerable<BodyState> bodies, int minutes, string what)
    {
        foreach (var b in bodies.Where(Body.IsAlive)) Assert.True(MinutesLived(b) == minutes, $"{what}: a body lived {MinutesLived(b)} of {minutes} minutes");
    }

    [Fact]
    public void EveryBody_LivesEveryMinuteOnce_InThePlayersHunt()
    {
        // Review P9: a herd with one member in the player's fight was frozen, every member of it.
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        w.Agent(P).Tile = tile;
        var herd = Animals.Spawn(w, Species.Caribou, tile, 4);
        foreach (var a in herd.In(w)) Fauna.MindOf(a).State = HerdState.Resting;
        var enc = Combat.Create(w, [new Fighter(P)], [new Fighter(herd)], tile, 40, Awareness.Engaged, Awareness.Unaware);
        var bodies = herd.In(w).Select(a => a.Body).Append(w.Agent(P).Body).ToList();
        foreach (var b in bodies) Trace(b);
        double held = Held(w, herd), burn = Burn(w, herd);
        for (int i = 0; i < 10; i++) Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Contains(enc, w.Encounters);
        AssertEachLivedOnce(bodies, 10, "player's hunt");
        // The herd lives on beside its animal's fight, every animal burning each minute once; the one in it
        // also pays for the steps it wandered.
        Assert.InRange(Held(w, herd), held - 10 * burn - 0.5, held - 10 * burn + 1e-6);
    }

    [Fact]
    public void AFledFighter_LivesOnItsOwnMind_WhileThePlayerFightsOn()
    {
        // Review P9: an NPC that fled the player's fight was frozen until it ended.
        var w = TestWorlds.New(17);
        var camp = Map.Of(w).Camp;
        var wolves = Agents.Spawn(w, Species.Wolf, camp);
        var enc = Combat.Create(w, [new Fighter(P)], [new Fighter(wolves, 0)], camp, 40, Awareness.Engaged, Awareness.Unaware);
        var npc = Survival.Npc.Spawn(w, camp);
        Combat.Join(w, enc, new Fighter(npc), 0); // it came to help
        Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Combat.UnitOf(enc, npc).InField = false; // and ran off the grid
        var b = w.Agent(npc).Body;
        Trace(b);
        double kcal = b.CaloriesKcal;
        for (int i = 0; i < 10; i++) Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Contains(enc, w.Encounters);
        Assert.True(b.CaloriesKcal < kcal, "the fled NPC never burned a calorie");
        AssertEachLivedOnce([b], 10, "fled NPC");
    }

    [Fact]
    public void AnNpcJoiningThePlayersFight_LivesItsJoiningMinuteOnce()
    {
        // Review P8b: the NPC lived its joining minute on the scheduler and again in the player's round.
        var w = TestWorlds.New(17);
        var camp = Map.Of(w).Camp;
        var wolves = Agents.Spawn(w, Species.Wolf, camp);
        var enc = Combat.Create(w, [new Fighter(P)], [new Fighter(wolves, 0)], camp, 40, Awareness.Engaged, Awareness.Unaware);
        var npc = Survival.Npc.Spawn(w, camp);
        Combat.Join(w, enc, new Fighter(npc), 0);
        var bodies = new[] { w.Agent(npc).Body, w.Agent(wolves).Body, w.Agent(P).Body };
        foreach (var b in bodies) Trace(b);
        for (int i = 0; i < 5; i++) Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Contains(enc.Units, u => u.Agent == npc);
        AssertEachLivedOnce(bodies, 5, "NPC joining");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryBody_LivesEveryMinuteOnce_ThroughAPackHunt(bool playerSteps)
    {
        // Review 7: the headless rounds were the next minutes, so a fighter not yet scheduled this
        // minute (the pack that started it, higher ids) skipped one, by id order.
        int hunts = 0;
        for (int seed = 1; seed <= 4; seed++)
        {
            var w = TestWorlds.New(seed);
            var (wolves, caribou, _) = PackAndPrey(w);
            var bodies = wolves.In(w).Concat(caribou.In(w)).Select(a => a.Body).Append(w.Agent(P).Body).ToList();
            foreach (var b in bodies) Trace(b);
            var events = playerSteps ? Sim.Step(w, new Wait(P), 40).Events : Sim.Advance(w, 40);
            if (!events.OfType<EncounterEnded>().Any(f => wolves.Ids.Contains(f.Agent))) continue;
            hunts++;
            AssertEachLivedOnce(bodies, 40, $"seed {seed} pack hunt");
        }
        Assert.True(hunts >= 2, $"only {hunts} hunts");
    }

    [Fact]
    public void EveryBody_LivesEveryMinuteOnce_ThroughAnNpcsOwnFight()
    {
        int fights = 0;
        for (int seed = 1; seed <= 8; seed++)
        {
            var w = TestWorlds.New(seed);
            var tile = Quiet(w);
            var npc = Survival.Npc.Spawn(w, tile);
            var wolf = Agents.Spawn(w, Species.Wolf, tile, [tile]);
            Mind(w, wolf).State = HerdState.Resting;
            var bodies = new[] { w.Agent(npc).Body, w.Agent(wolf).Body, w.Agent(P).Body };
            foreach (var b in bodies) Trace(b);
            // The NPC takes the wolf on, as a mind choosing the Hunt activity would: its fight is fought out headless.
            var events = new List<WorldEvent>();
            Sim.Start(w, new Hunt(npc, wolf), 1, events, out var doing);
            w.Agent(npc).Doing = Sim.IsAlive(w.Agent(npc)) ? doing : null;
            events.AddRange(Sim.Advance(w, 30));
            if (!events.OfType<EncounterEnded>().Any(f => f.Agent == npc)) continue;
            fights++;
            // A headless fight lives its rounds ahead of the clock (Combat.RunRounds): those minutes are lived
            // already, and the clock skips them when it gets there.
            foreach (var b in bodies.Where(Body.IsAlive))
                AssertEachLivedOnce([b], 30 + (int)Math.Max(0, b.WoundsLivedThroughMinute - w.Minute), $"seed {seed} NPC fight");
        }
        Assert.True(fights >= 1, "the NPC never fought");
    }

    [Fact]
    public void Advance_LivesThePlayersMinutes_AsWaiting()
    {
        // Review P1: the world ran on and the player stood outside time.
        var w = TestWorlds.New(1);
        var b = w.Agent(P).Body;
        Trace(b);
        double kcal = b.CaloriesKcal, water = b.HydrationMl;
        Sim.Advance(w, 30);
        AssertEachLivedOnce([b], 30, "player");
        Assert.True(b.CaloriesKcal < kcal && b.HydrationMl < water);
    }

    // ---- review 2 ----

    /// A headless fight's rounds are minutes each body lives once: an animal mends in them as it
    /// bleeds in them (Fauna.Tick skips them after), so no lived minute goes unmended.
    [Fact]
    public void AHeadlessFightsRounds_MendAnimalsAsWellAsBleedThem()
    {
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        Sim.Advance(w, 1); // off minute zero, where the first round's minute is clamped
        var a = Agents.Spawn(w, Species.Caribou, tile);
        var b = Agents.Spawn(w, Species.Caribou, tile);
        var body = w.Agent(a).Body;
        body.BloodPct = 0.8;
        var fight = Combat.Create(w, [new Fighter(a)], [new Fighter(b)], tile, 40, Awareness.Unaware, Awareness.Unaware);
        var events = new List<WorldEvent>();
        Combat.ResolveHeadless(w, fight, events);
        Assert.DoesNotContain(events, e => e is Injured);
        long lived = body.WoundsLivedThroughMinute - (w.Minute - 1);
        Assert.True(lived > 0);
        Assert.Equal(0.8 + lived * Fauna.MendPctPerMinute, body.BloodPct, 9);
    }

    /// A starving pack's foray ends when it eats, whatever it eats: a carcass it found, not only its own kill.
    [Fact]
    public void AForayEnds_WhenThePackScavenges()
    {
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        foreach (var id in w.Agents.Values.Where(x => x.Mind is HerdMind).Select(x => x.Id).ToList()) w.Agents.Remove(id);
        var wolves = Animals.Spawn(w, Species.Wolf, tile, 3);
        Combat.LeaveCarcass(w, tile, Species.Caribou, BodyState.Create(SpeciesTable.Of(Species.Caribou).Body, 100, 0.1, 0.4), new Cause(null, "a test"));
        var minds = wolves.In(w).Select(Fauna.MindOf).ToList();
        foreach (var wolf in wolves.In(w)) TestKit.SetHunger(wolf, 0.95);
        foreach (var mind in minds) (mind.Foray, mind.State) = (new TileId(tile.X + 10, tile.Y), HerdState.Patrolling);
        Sim.Advance(w, 3);
        Assert.All(minds, mind => Assert.Equal(HerdState.Feeding, mind.State));
        Assert.All(minds, mind => Assert.Null(mind.Foray));
    }

}

/// Months of the populated valley: nothing in it gets stuck, and every pack hunts (#96). Each seed is
/// its own class, so they run side by side; three seeds, because any change to behaviour reshuffles a
/// whole run, and a bug one valley happens to miss another finds.
public static class HerdLongRun
{
    /// Review 3: a herd that chased meat off its land stood behind a mountain for forty days. No animal
    /// that can walk spends more than two days wanting to move (patrolling, grazing on, fleeing) without
    /// getting more than a tile from where it last made progress, read every minute (NetProgress):
    /// resting and feeding, a pack may lie up at a big kill for a fortnight. Every pack that lives hunts.
    const int Days = 60, MaxDaysStuck = 2, RadiusTiles = 1;

    internal static void Run(int seed, ITestOutputHelper output)
    {
        var w = TestWorlds.New(seed);
        var progress = new NetProgress(RadiusTiles);
        var hunts = new Dictionary<AgentId, int>();
        // A pack is the wolves that started together on their den, named by its first.
        var packOf = w.Agents.Values.Where(a => a.Species == Species.Wolf).GroupBy(a => a.Tile)
            .SelectMany(g => g.Select(a => (a.Id, Pack: g.First().Id))).ToDictionary(x => x.Id, x => x.Pack);
        var packs = packOf.Values.Distinct().ToList();
        for (int minute = 0; minute < Days * 1440; minute++)
        {
            foreach (var f in Sim.Advance(w, 1).OfType<EncounterEnded>().Where(f => packOf.ContainsKey(f.Agent))) hunts[packOf[f.Agent]] = hunts.GetValueOrDefault(packOf[f.Agent]) + 1;
            progress.Sample(w);
        }
        var (worstMinutes, who) = progress.Worst;
        output.WriteLine($"seed {seed}: longest wanting to move within {RadiusTiles} tile {worstMinutes / 1440.0:F2} days ({who}); by state "
            + string.Join(", ", progress.WorstByState.Select(kv => $"{kv.Key} {kv.Value / 1440.0:F2}"))
            + $"; hunts {string.Join(", ", packs.Select(p => $"{p} {hunts.GetValueOrDefault(p)}"))}");
        Assert.True(worstMinutes <= MaxDaysStuck * 1440, $"seed {seed}: {who} wanted to move but got no further than {RadiusTiles} tile in {worstMinutes / 1440.0:F2} days");
        Assert.All(packs.Where(p => packOf.Any(x => x.Value == p && w.Agents.ContainsKey(x.Key))),
            p => Assert.True(hunts.GetValueOrDefault(p) > 0, $"seed {seed}: pack {p} lived {Days} days and never hunted"));
    }
}

[Trait("Category", TestCategories.Slow)]
public class HerdLongRunSeed5Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_NothingStuck_AndPacksHunt() => HerdLongRun.Run(5, output);
}

[Trait("Category", TestCategories.Slow)]
public class HerdLongRunSeed17Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_NothingStuck_AndPacksHunt() => HerdLongRun.Run(17, output);
}

[Trait("Category", TestCategories.Slow)]
public class HerdLongRunSeed42Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_NothingStuck_AndPacksHunt() => HerdLongRun.Run(42, output);
}

/// Den-anchored herds (old HerdPopulator's Wolf Den, Bear Cave and Bone Hollow).
public class DenTests
{
    static IEnumerable<Agent> Herds(World w, Species s) => w.Agents.Values.Where(a => a.Mind is HerdMind && a.Species == s);
    static List<TileId> Sites(World w, string id) => [.. Map.Of(w).Sites.Where(s => s.Value.Id == id).Select(s => s.Key)];

    [Theory]
    [InlineData(17)] // a Wolf Den at (42,53), as the old generator put it
    [InlineData(30)] // three dens
    [InlineData(19)] // a den nine tiles from camp, inside the safe zone
    public void EveryWolfDen_HasAPackLivingOnIt(int seed)
    {
        var w = TestWorlds.New(seed);
        var dens = Sites(w, "wolf_den");
        Assert.NotEmpty(dens);
        foreach (var den in dens)
        {
            // A pack: three to eight wolves on the den, each knowing all the others.
            var pack = Herds(w, Species.Wolf).Where(a => ((HerdMind)a.Mind).Territory[0] == den).ToList();
            Assert.InRange(pack.Count, 3, 8);
            Assert.All(pack, wolf => Assert.Equal(den, wolf.Tile));
            Assert.All(pack, wolf => Assert.Equal(pack.Count, Combat.Side(w, wolf).Count));
            Assert.All(pack, wolf => Assert.All(((HerdMind)wolf.Mind).Territory, t => Assert.True(Math.Abs(t.X - den.X) <= 10 && Math.Abs(t.Y - den.Y) <= 10)));
        }
    }

    [Fact]
    public void TheBearCave_HasItsBear()
    {
        var w = TestWorlds.New(53); // the first seed whose valley has a Bear Cave (elite: far east only)
        var cave = Assert.Single(Sites(w, "bear_cave"));
        var bear = Assert.Single(w.Agents.Values, a => a.Mind is HerdMind m && m.Territory[0] == cave);
        Assert.Contains(bear.Species, new[] { Species.Bear, Species.CaveBear });
        Assert.Single(bear.Members);
    }

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void BoneHollow_HasTheMammoths_AndAKilledHerdStaysDead()
    {
        var w = TestWorlds.New(91237);
        var hollow = Sites(w, "bone_hollow")[0];
        var herd = Herds(w, Species.Mammoth).ToList();
        Assert.All(herd, m => Assert.Equal(hollow, m.Tile));
        Assert.InRange(herd.Count, 8, 12);
        Assert.InRange(((HerdMind)herd[0].Mind).Territory.Count, 8, 288);

        // Hunted out: every one dies of its wounds and leaves the world the way the dead do (Fauna.Tick),
        // and days on nothing has come back to the hollow.
        foreach (var m in herd) m.Body.BloodPct = 0;
        Sim.Advance(w, 60);
        Assert.All(herd, m => Assert.False(w.Agents.ContainsKey(m.Id)));
        Sim.Advance(w, 3 * 1440);
        Assert.DoesNotContain(w.Agents.Values, a => a.Species == Species.Mammoth);
        Assert.Empty(Herds(TestWorlds.New(1), Species.Mammoth)); // and a valley with no Bone Hollow has none at all
    }

    /// old game: den land was any open tile in a square round the den bar the far side, grown by
    /// adjacency alone, so a pack could hold pass stages and land across a ravine or cliff it could
    /// never walk to (bug, fixed). Every herd's land is walkable from its start, and off the whole
    /// pass corridor: the rock between the stages too, which once held herds penned in 3-tile pockets.
    [Theory]
    [InlineData(24)] // a wolf den under the mountains: its land took three pass stages
    [InlineData(67)] // land beyond a blocking edge
    [InlineData(80)]
    [InlineData(63)] // six bison penned between two stages
    [InlineData(1)] // megaloceros between stages
    public void EveryTerritory_IsWalkableFromItsStart_AndOffThePass(int seed)
    {
        var w = TestWorlds.New(seed);
        var m = Map.Of(w);
        // The corridor read off the terrain alone: from the exit back to the Pass Approach, every tile
        // reached through a passage one tile wide. (West of the approach is the valley's own.)
        int Open(TileId t) => MapGen.Neighbors(t).Count(n => m.InBounds(n) && m.TerrainAt(n).IsPassable());
        int approachX = m.PassStageIndex.Single(s => s.Value == 0).Key.X;
        var corridor = new HashSet<TileId> { m.Exit };
        var walk = new Queue<TileId>(corridor);
        while (walk.Count > 0)
            foreach (var n in MapGen.Neighbors(walk.Dequeue()))
                if (m.InBounds(n) && n.X >= approachX && m.TerrainAt(n).IsPassable() && Open(n) <= 2 && corridor.Add(n)) walk.Enqueue(n);
        Assert.True(corridor.Count >= 12, $"corridor of only {corridor.Count} tiles");
        Assert.All(corridor, t => Assert.True(m.OnPass(t), $"{t} is in the corridor but not on the map's pass"));
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind))
        {
            var land = ((HerdMind)h.Mind).Territory;
            var inLand = land.ToHashSet();
            var reached = new HashSet<TileId> { land[0] };
            var queue = new Queue<TileId>(reached);
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                foreach (var n in MapGen.Neighbors(t))
                    if (inLand.Contains(n) && Map.Blocked(m, t, n) == null && reached.Add(n)) queue.Enqueue(n);
            }
            Assert.True(reached.Count == land.Count, $"{h.Species} {h.Id}: {land.Count - reached.Count} of {land.Count} tiles cut off from its start");
            Assert.DoesNotContain(land, t => m.StageAt(t) != null);
            Assert.DoesNotContain(land, m.OnPass);
            Assert.DoesNotContain(land, corridor.Contains);
        }
    }
}

/// Herd land across many valleys: every species' smallest territory is real range, not a pocket.
/// The floors sit under the smallest seen in seeds 0-59 with the whole pass corridor left out
/// (land grown through walkable edges is cut short by ravines and cliffs, so they are below the
/// target sizes). Cave dens range only where their cave connects, and hyena packs start hard against
/// a wolf pack's land, so those two keep the generator's own floor of three tiles.
[Trait("Category", TestCategories.Slow)]
public class TerritorySweepTests(ITestOutputHelper output)
{
    static readonly IReadOnlyDictionary<Species, int> Floor = new Dictionary<Species, int>
    {
        [Species.Bison] = 80,
        [Species.Megaloceros] = 40,
        [Species.Caribou] = 100,
        [Species.SaberTooth] = 80,
        [Species.Wolf] = 20,
        [Species.Bear] = 40,
        [Species.Mammoth] = 150,
        [Species.CaveBear] = 3,
        [Species.Hyena] = 3,
        [Species.Raven] = 100,
        [Species.Fox] = 20,
    };

    [Fact]
    public void SmallestTerritory_PerSpecies_IsRealRange()
    {
        var smallest = new SortedDictionary<Species, (int Tiles, int Seed)>();
        for (int seed = 0; seed < 60; seed++)
            foreach (var h in World.New(seed).Agents.Values.Where(a => a.Mind is HerdMind))
            {
                int tiles = ((HerdMind)h.Mind).Territory.Count;
                if (!smallest.TryGetValue(h.Species, out var s) || tiles < s.Tiles) smallest[h.Species] = (tiles, seed);
            }
        output.WriteLine(string.Join(", ", smallest.Select(s => $"{s.Key} {s.Value.Tiles} (seed {s.Value.Seed})")));
        foreach (var (species, (tiles, seed)) in smallest)
            Assert.True(tiles >= Floor[species], $"{species}: seed {seed} gave a territory of {tiles} tiles, under {Floor[species]}");
    }
}
