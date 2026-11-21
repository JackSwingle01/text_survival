using Survival;
using Xunit;
using Xunit.Abstractions;

/// Phase 3b: the scheduler, herd minds, the populated valley and the player's hunt.
public class HerdTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;

    static HerdMind Mind(World w, AgentId id) => (HerdMind)w.Agent(id).Mind;
    static IEnumerable<Agent> Herds(World w) => w.Agents.Values.Where(a => a.Mind is HerdMind);

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
    static (AgentId Wolves, AgentId Caribou, TileId Tile) PackAndPrey(World w, int wolves = 5, int caribou = 3)
    {
        var tile = Quiet(w);
        var prey = Agents.Spawn(w, Species.Caribou, tile, caribou);
        var pack = Agents.Spawn(w, Species.Wolf, tile, wolves);
        var mind = Mind(w, pack);
        mind.Hunger = 0.9;
        mind.State = HerdState.Patrolling;
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
            var w = World.New(seed);
            var (wolves, caribou, tile) = PackAndPrey(w);
            var events = Idle(w, 120);
            var kill = events.OfType<Killed>().FirstOrDefault(k => k.Species == Species.Caribou && k.By is AgentId by && by == wolves);
            if (kill == null) continue;
            kills++;
            Assert.Equal(tile, kill.Tile);
            Assert.NotEqual(tile, w.Agent(P).Tile); // the player was at camp throughout
            Assert.Contains(events.OfType<FightEnded>(), f => f.Agent == wolves);
            // The carcass is on the tile and the pack has eaten into it.
            Assert.True(w.Agents.ContainsKey(wolves));
            Assert.Contains(events.OfType<Killed>(), k => k.Agent == caribou);
            // Meat starts at 65% of the muscle, and a caribou is 60% muscle: the eaten one has less.
            var eaten = w.Tiles[tile].Carcasses!.Where(c => c.Animal == "Caribou")
                .MinBy(c => c.Remaining[Resource.RawMeat] / (c.BodyWeightKg * 0.60 * 0.65))!;
            double meatLeft = eaten.Remaining[Resource.RawMeat], whole = eaten.BodyWeightKg;
            output.WriteLine($"seed {seed}: kill at minute {kill.Minute}, meat left {meatLeft:F1} kg of {whole:F0} kg, pack {Mind(w, wolves).State} hunger {Mind(w, wolves).Hunger:F2}");
            Assert.True(meatLeft < whole * 0.60 * 0.65 - 0.5, "the pack never ate");
            Assert.True(Mind(w, wolves).Hunger < 0.9, "feeding did not ease the pack's hunger");
        }
        Assert.True(kills >= 3, $"only {kills} of 6 packs made a kill in two hours beside the prey");
    }

    [Fact]
    public void AFrightenedPack_LeavesPreyAlone()
    {
        var w = World.New(1);
        var (wolves, _, _) = PackAndPrey(w);
        Mind(w, wolves).Fear = 1.0; // fades at 0.015 a minute: still frightened for half an hour
        var events = Idle(w, 30);
        Assert.DoesNotContain(events.OfType<FightEnded>(), f => f.Agent == wolves);
    }

    // ---- gate: a wounded animal splits into its own agent and is tracked ----

    [Fact]
    public void Gate_AWoundedAnimalThatGetsAway_IsItsOwnHerd_AndLeavesPrints()
    {
        var w = World.New(1);
        var tile = Quiet(w);
        w.Agent(P).Tile = tile;
        var herd = Agents.Spawn(w, Species.Caribou, tile, 5);
        var e = Combat.Create(w, [new Fighter(P)], [new Fighter(herd, 1)], tile, 5, Awareness.Engaged, Awareness.Engaged);
        var unit = e.Units.Single(u => u.Team == 1);
        var wounded = Combat.BodyOf(w, unit);
        wounded.Regions[2].Muscle.Condition = 0.6;
        Effects.Add(wounded, EffectKind.Bleeding, 0.3);
        unit.Position = new Cell(unit.Position.X, Combat.MapSize - 1);
        Combat.Move(w, e, unit, new Cell(unit.Position.X, Combat.MapSize)); // off the edge
        var r = Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        var escaped = r.Events.OfType<WoundedEscaped>().Single().Agent;

        var split = w.Agent(escaped);
        Assert.NotEqual(herd, escaped);
        Assert.Single(split.Members);
        Assert.Equal(HerdState.Fleeing, Mind(w, escaped).State);
        Assert.NotSame(w.Agent(herd).Mind, split.Mind);

        // It runs on its own mind, through the shared travel, and the ground remembers it.
        var events = Idle(w, 30);
        var fled = events.OfType<Moved>().First(m => m.Who == escaped);
        Assert.Equal(tile, fled.From);
        var prints = Tracks.Read(w, fled.To);
        Assert.Contains(prints, s => s.Maker == TrackMaker.Hoof);
        Assert.True(w.Agent(escaped).Tile != tile, "the wounded one never left");
        // The rest of the herd ran too, on its own way.
        Assert.Contains(events.OfType<Moved>(), m => m.Who == herd);
    }

    [Fact]
    public void ABleedingAnimal_DiesWhereItFalls_AsACarcass()
    {
        var w = World.New(1);
        var tile = Quiet(w);
        var lone = Agents.Spawn(w, Species.Caribou, tile, 1);
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
        var w = World.New(1);
        var tile = Quiet(w);
        var lone = Agents.Spawn(w, Species.Caribou, tile, 1);
        Mind(w, lone).State = HerdState.Resting;
        w.Agent(lone).Members[0].BloodPct = 0.52;
        Effects.Add(w.Agent(lone).Members[0], EffectKind.Bleeding, 1.0);
        Sim.Advance(w, 1);
        Assert.True(w.Agents.ContainsKey(lone));
        Assert.NotEmpty(StreamsOf(w, lone)); // its wounds roll on its own body stream
        Sim.Advance(w, 239);
        Assert.False(w.Agents.ContainsKey(lone));
        Assert.Empty(StreamsOf(w, lone));
        Assert.True(Agents.Spawn(w, Species.Caribou, tile, 1).Value > lone.Value, "a gone herd's id was made again");

        int pulledDown = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var v = World.New(seed);
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
        var w = World.New(seed);
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
            if (saveEverySteps > 0 && ++steps % saveEverySteps == 0) w = Save.FromJson(Save.ToJson(w));
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

    [Fact]
    public void Gate_ThreePopulatedDays_AreIndependentOfChunkingAndSaves()
    {
        var a = Planted(5);
        a.Agent(P).Members[0] = BodyState.BaselineHuman();
        var events = Sim.Advance(a, 3 * 1440);
        Assert.Contains(events, e => e is FightEnded); // the planted hunt, at least
        string straight = Save.ToJson(a);

        var b = Planted(5);
        while (b.Minute < 3 * 1440)
        {
            Sim.Advance(b, (int)Math.Min(97, 3 * 1440 - b.Minute));
            b = Save.FromJson(Save.ToJson(b));
        }
        Assert.Equal(straight, Save.ToJson(b));
    }

    [Fact]
    public void HerdsAreNotTickedForMinutesAHeadlessFightAlreadyLived()
    {
        var w = World.New(1);
        var tile = Quiet(w);
        var wolves = Agents.Spawn(w, Species.Wolf, tile, 3);
        var caribou = Agents.Spawn(w, Species.Caribou, tile, 2);
        var fight = Combat.Create(w, [new Fighter(wolves, 0), new Fighter(wolves, 1)], [new Fighter(caribou, 0)], tile, 5, Awareness.Engaged, Awareness.Unaware);
        var survivor = w.Agent(caribou).Members[0];
        Effects.Add(survivor, EffectKind.Bleeding, 0.5);
        Combat.ResolveHeadless(w, fight, []);
        if (!Body.IsAlive(survivor) || fight.Rounds == 0) return;
        Assert.Equal(w.Minute + fight.Rounds, survivor.WoundsLivedThroughMinute);
        // The fight's rounds were its next minutes: living them again draws no more blood.
        double blood = survivor.BloodPct;
        var holder = w.Agents.Values.First(a => a.Members.Contains(survivor));
        Fauna.MindOf(holder).State = HerdState.Resting;
        Sim.Advance(w, fight.Rounds);
        Assert.Equal(blood, survivor.BloodPct);
        Sim.Advance(w, 1);
        Assert.True(survivor.BloodPct < blood, "and after them it bleeds again");
    }

    [Fact]
    public void APersonInAHeadlessFight_DoesNotBleedItsRoundsTwice()
    {
        var w = World.New(1);
        var tile = Quiet(w);
        w.Agent(P).Tile = tile;
        var me = w.Agent(P).Body;
        Effects.Add(me, EffectKind.Bleeding, 0.5);
        var wolf = Agents.Spawn(w, Species.Wolf, tile, 1);
        var fight = Combat.Create(w, [new Fighter(P)], [new Fighter(wolf, 0)], tile, 20, Awareness.Engaged, Awareness.Engaged);
        fight.Units[0].InField = false; // as if the player had just fled: the rest is fought headless
        Combat.ResolveHeadless(w, fight, []);
        Assert.Equal(0, me.WoundsLivedThroughMinute); // never in the field for those rounds
    }

    // ---- gate: herds move, graze and wear trails ----

    [Fact]
    public void Gate_HerdsMoveGrazeAndWearTrails()
    {
        var w = World.New(17);
        var events = Sim.Advance(w, 4 * 1440);
        var herdIds = Herds(w).Select(h => h.Id).ToHashSet();
        var moves = events.OfType<Moved>().Where(m => herdIds.Contains(m.Who)).ToList();
        output.WriteLine($"4 days: {moves.Count} herd moves by {moves.Select(m => m.Who).Distinct().Count()} herds, {w.Trails.Count} worn edges");
        Assert.True(moves.Select(m => m.Who).Distinct().Count() >= herdIds.Count / 2, "most herds never moved");
        var grazed = w.Tiles.Where(t => t.Value.Pools?.Values.OfType<ForageState>().Any(s => s.Grazed.Values.Any(g => g.Level > 0)) == true).ToList();
        Assert.NotEmpty(grazed);
        var camp = Map.Of(w).Camp;
        Assert.Contains(w.Trails.Keys, e => MapGen.Manhattan(e.A, camp) > Fauna.SafeZoneTiles && Tracks.TierOf(w, e.A, e.B) >= TrailTier.Trace);
        Assert.Contains(moves, m => Tracks.Read(w, m.To).Any(s => s.Maker is TrackMaker.Hoof or TrackMaker.Paw));
    }

    [Fact]
    public void GrazingThinsWhatThisDietEats_AndNotTheRest()
    {
        var w = World.New(1);
        var tile = Quiet(w, Terrain.Forest);
        var bison = Agents.Spawn(w, Species.Bison, tile, 6);
        var deer = Agents.Spawn(w, Species.Caribou, tile, 6);
        foreach (var id in new[] { bison, deer }) { Mind(w, id).Hunger = 1.0; Mind(w, id).State = HerdState.Grazing; }
        Sim.Advance(w, 30);
        var pool = new PoolId(tile, 0);
        Assert.True(Pools.GrazedLevel(w, pool, Resource.Tinder) > 0);   // bison: grass
        Assert.True(Pools.GrazedLevel(w, pool, Resource.Usnea) > 0);    // caribou: lichen
        Assert.Equal(0, Pools.GrazedLevel(w, pool, Resource.Berries));  // nobody here eats berries
    }

    // ---- old bugs fixed ----

    [Fact]
    public void AHerdOnTheMove_StillGetsHungrier()
    {
        // old game: hunger stood still while a herd was walking (bug, fixed).
        var w = World.New(1);
        var tile = Quiet(w);
        var herd = Agents.Spawn(w, Species.Caribou, tile, 3);
        Mind(w, herd).State = HerdState.Fleeing;
        Mind(w, herd).FleeFrom = tile;
        double before = Mind(w, herd).Hunger;
        Sim.Advance(w, 1);
        Assert.NotNull(w.Agent(herd).Crossing);
        Sim.Advance(w, 2);
        Assert.NotNull(w.Agent(herd).Crossing); // still on the way
        Assert.Equal(before + 3 * 0.003, Mind(w, herd).Hunger, 9);
    }

    [Fact]
    public void AFleeingPack_RunsOneTile_ThenSettles()
    {
        // old game: a fleeing pack fled from the player's tile every minute until the map stopped it (bug, fixed).
        var w = World.New(1);
        var tile = Quiet(w);
        var pack = Agents.Spawn(w, Species.Wolf, tile, 4);
        Fauna.AfterFight(w, pack, CombatResult.Fled, killedOne: false, tile);
        Assert.Equal(HerdState.Fleeing, Mind(w, pack).State);
        var events = Sim.Advance(w, 120);
        Assert.Single(events.OfType<Moved>(), m => m.Who == pack);
        Assert.NotEqual(HerdState.Fleeing, Mind(w, pack).State);
    }

    [Fact]
    public void APatrollingPack_RangesAcrossItsLand()
    {
        // old game: each patrol step aimed at the next tile in flood-fill order, so a pack never left its den's doorstep (bug, fixed).
        var w = World.New(17);
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
        var herds = Herds(w).ToList();
        int Count(params Species[] s) => herds.Count(h => s.Contains(h.Species));
        output.WriteLine(string.Join(", ", herds.GroupBy(h => h.Species).Select(g => $"{g.Key} {g.Count()}x{string.Join("/", g.Select(h => h.Members.Count))}")));
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
                Species.Wolf => (3, 80), Species.Bear or Species.CaveBear => (3, 128), Species.Caribou => (6, 192),
                Species.Megaloceros => (3, 96), Species.Bison => (3, 400), Species.SaberTooth => (4, 160), _ => (3, 112),
            };
            Assert.InRange(land.Count, min, max);
            Assert.InRange(h.Members.Count, 1, 15);
        }
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
            var w = World.New(seed);
            var wolfLand = Herds(w).Where(h => h.Species == Species.Wolf).SelectMany(h => ((HerdMind)h.Mind).Territory).ToHashSet();
            if (Herds(w).Any(h => !Fauna.IsPredator(h.Species) && ((HerdMind)h.Mind).Territory.Any(wolfLand.Contains))) overlapping++;
        }
        Assert.True(overlapping >= 2, $"prey share a pack's land on only {overlapping} of 5 seeds");
    }

    // ---- the player's hunt ----

    /// Old RunHunt took the sighting's DetectionModifier as every stalked animal's DetectionFactor:
    /// 0.45-0.9 for wounded, grazing or resting prey, 2.0-2.5 for alert prey (old HuntRunner.Run,
    /// HuntingSightings). One sighting of the behaviour's pool was picked at random.
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
            var herd = Agents.Spawn(w, doing == "patrolling" ? Species.Wolf : Species.Caribou, tile, 4);
            Mind(w, herd).State = doing switch
            {
                "grazing" => HerdState.Grazing, "feeding" => HerdState.Feeding, "fleeing" => HerdState.Fleeing,
                "patrolling" => HerdState.Patrolling, _ => HerdState.Resting,
            };
            if (doing == "fearful") Mind(w, herd).Fear = 0.8;
            if (doing == "wounded") w.Agent(herd).Members[0].BloodPct = 0.9;
            var enc = Fauna.BeginHunt(w, P, herd, 0);
            var animals = enc.Units.Where(u => u.Agent == herd).ToList();
            Assert.NotEmpty(animals);
            Assert.All(animals, u => Assert.Equal(animals[0].DetectionFactor, u.DetectionFactor));
            Assert.Contains(animals[0].DetectionFactor, oldPool);
            Assert.Equal(1.0, Combat.UnitOf(enc, P).DetectionFactor);
            seen.Add(animals[0].DetectionFactor);
        }
        Assert.Equal(oldPool.Length, seen.Count); // every sighting of the pool turns up
    }

    [Fact]
    // The naive stalk below (advance to throwing range on open plain) kills about one hunt in fifty, so
    // this walks seeds until one does: seed 3 did with shared streams, seed 43 once each agent rolled
    // on its own, seed 15 since resting prey watches as the old sightings did (re-pinned 2026-11-21).
    // The stalk's odds are a balance question, not this test's.
    public void ThePlayer_CanHuntAHerdMember_ButcherItAndEat()
    {
        for (int seed = 1; seed <= 60; seed++)
        {
            var w = World.New(seed);
            var tile = Quiet(w);
            w.Agent(P).Tile = tile;
            var pack = Items.Pack(w, P);
            var spear = Items.Make("wooden_spear");
            pack.Gear.Add(spear);
            pack.Gear.Add(Items.Make("flint_knife"));
            var herd = Agents.Spawn(w, Species.Caribou, tile, 4);
            Mind(w, herd).State = HerdState.Resting;
            Fauna.BeginHunt(w, P, herd, 0, spear);
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
                result = r.Events.OfType<FightEnded>().FirstOrDefault(f => f.Agent == P)?.Result;
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
        Assert.Fail("sixty stalks with a spear and never a kill");
    }

    /// Every agent rolls on its own streams: a pack pulling down caribou across the valley, bleeding
    /// and fighting, leaves the player's injuries and body as they were without it. (Shared streams
    /// let any herd's fight move the player's next injury.)
    [Fact]
    public void AFarFight_LeavesThePlayersRollsAlone()
    {
        (string Rolls, int Mine, int HerdInjuries) Run(bool farFight)
        {
            var w = World.New(17);
            var m = Map.Of(w);
            if (farFight)
            {
                var far = Enumerable.Range(0, m.Width / 2).Select(dx => m.Width - 30 - dx).SelectMany(x => Enumerable.Range(10, m.Height - 20).Select(y => new TileId(x, y)))
                    .First(t => m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && !m.OnPass(t));
                Agents.Spawn(w, Species.Caribou, far, 3);
                var pack = Agents.Spawn(w, Species.Wolf, far, 5);
                Mind(w, pack).Hunger = 0.9;
                Mind(w, pack).State = HerdState.Patrolling;
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
        var w = World.New(1);
        var herd = Herds(w).First();
        herd.Doing = new Doing { Work = new WorkSession { Activity = new Gather(herd.Id, 0, ForageFocus.Food), Minutes = 3 }, MinutesLeft = 7 };
        Items.Add(herd.Doing.Work!.Got, Resource.Berries, 0.2);
        string json = Save.ToJson(w);
        var back = Save.FromJson(json).Agent(herd.Id).Doing!;
        Assert.Equal(new Gather(herd.Id, 0, ForageFocus.Food), back.Activity);
        Assert.Equal(0.2, Items.Have(back.Work!.Got, Resource.Berries));
        Assert.Equal(json, Save.ToJson(Save.FromJson(json)));
    }

    [Fact]
    public void ThirtyPopulatedDays_StayCheap()
    {
        var w = World.New(17);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var events = Sim.Advance(w, 30 * 1440);
        clock.Stop();
        int json = Save.ToJson(w).Length;
        output.WriteLine($"30 days, {Herds(w).Count()} herds: {clock.Elapsed.TotalSeconds:F1}s, {events.OfType<Moved>().Count()} moves, " +
                         $"{events.OfType<FightEnded>().Count()} fight sides, {events.OfType<Killed>().Count()} killed; save {json / 1024} KB");
        Assert.True(clock.Elapsed.TotalSeconds < 10, $"took {clock.Elapsed.TotalSeconds:F1}s");
        Assert.True(json < 1_000_000, $"save is {json} bytes");
    }

    // ---- wounds mend; the lame are left behind ----

    static AgentId HerdWithALameMember(World w, int members)
    {
        var tile = Quiet(w);
        var land = MapGen.Neighbors(tile).Prepend(tile).SelectMany(t => MapGen.Neighbors(t).Prepend(t)).Distinct()
            .Where(t => Map.Of(w).TerrainAt(t).IsPassable()).ToList();
        land.Remove(tile);
        land.Insert(0, tile);
        var id = Agents.Spawn(w, Species.Caribou, tile, members, land);
        var lame = w.Agent(id).Members[0];
        var rng = Rng.ForSystem(1, "test");
        while (Body.GetCapacities(lame).Moving > 0.1)
        {
            Body.Damage(lame, new DamageInfo(8, DamageType.Blunt, BodyTarget.LeftLeg), rng);
            Body.Damage(lame, new DamageInfo(8, DamageType.Blunt, BodyTarget.RightLeg), rng);
        }
        lame.Effects.Clear();
        Assert.True(Body.IsAlive(lame));
        return id;
    }

    [Fact]
    public void ALameMember_IsLeftBehind_AndTheHerdMovesOn()
    {
        // Review P11: one member that could not walk held a herd of five still for ten days.
        var w = World.New(17);
        var herd = HerdWithALameMember(w, 5);
        var lame = w.Agent(herd).Members[0];
        var events = Sim.Advance(w, 1440);
        var alone = w.Agents.Values.Single(a => a.Members.Contains(lame));
        Assert.NotEqual(herd, alone.Id);
        Assert.Single(alone.Members);
        Assert.Equal(4, w.Agent(herd).Members.Count);
        Assert.Contains(events.OfType<Moved>(), m => m.Who == herd);
    }

    [Fact]
    public void AnimalWounds_MendAtTheOldRate()
    {
        // Old PreyBehavior: WoundSeverity fell 0.0002 a minute. Here the tissue and the blood do.
        var w = World.New(1);
        var tile = Quiet(w);
        var id = Agents.Spawn(w, Species.Caribou, tile, 1);
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
    public void ALoneLameAnimal_MendsAndWalksAgain()
    {
        var w = World.New(17);
        var lone = HerdWithALameMember(w, 1);
        var body = w.Agent(lone).Members[0];
        var events = Sim.Advance(w, 6 * 1440);
        Assert.True(Body.GetCapacities(body).Moving > 0.9, $"moving {Body.GetCapacities(body).Moving:F2} after six days");
        Assert.Contains(events.OfType<Moved>(), m => m.Who == lone);
    }

    // ---- every body lives every minute exactly once ----

    /// A tracer: an effect nothing in an ice-age world gives or takes, fading at a fixed rate on each
    /// minute a body's wounds live. How far it has faded counts those minutes.
    const double TracerPct = 0.4;
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
        var w = World.New(1);
        var tile = Quiet(w);
        w.Agent(P).Tile = tile;
        var herd = Agents.Spawn(w, Species.Caribou, tile, 4);
        Mind(w, herd).State = HerdState.Resting;
        var enc = Combat.Create(w, [new Fighter(P)], [new Fighter(herd, 0)], tile, 40, Awareness.Engaged, Awareness.Unaware);
        var bodies = w.Agent(herd).Members.Append(w.Agent(P).Body).ToList();
        foreach (var b in bodies) Trace(b);
        double hunger = Mind(w, herd).Hunger;
        for (int i = 0; i < 10; i++) Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Contains(enc, w.Encounters);
        AssertEachLivedOnce(bodies, 10, "player's hunt");
        Assert.Equal(hunger + 10 * 0.003, Mind(w, herd).Hunger, 9); // the herd lives on beside its member's fight
    }

    [Fact]
    public void AFledFighter_LivesOnItsOwnMind_WhileThePlayerFightsOn()
    {
        // Review P9: an NPC that fled the player's fight was frozen until it ended.
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var wolves = Agents.Spawn(w, Species.Wolf, camp, 1);
        var enc = Combat.Create(w, [new Fighter(P)], [new Fighter(wolves, 0)], camp, 40, Awareness.Engaged, Awareness.Unaware);
        var npc = Survival.Npc.Spawn(w, camp, boldness: 1.0);
        Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Contains(enc.Units, u => u.Agent == npc); // it came to help
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
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var wolves = Agents.Spawn(w, Species.Wolf, camp, 1);
        var enc = Combat.Create(w, [new Fighter(P)], [new Fighter(wolves, 0)], camp, 40, Awareness.Engaged, Awareness.Unaware);
        var npc = Survival.Npc.Spawn(w, camp, boldness: 1.0);
        var bodies = new[] { w.Agent(npc).Body, w.Agent(wolves).Members[0], w.Agent(P).Body };
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
            var w = World.New(seed);
            var (wolves, caribou, _) = PackAndPrey(w);
            var bodies = w.Agent(wolves).Members.Concat(w.Agent(caribou).Members).Append(w.Agent(P).Body).ToList();
            foreach (var b in bodies) Trace(b);
            var events = playerSteps ? Sim.Step(w, new Wait(P), 40).Events : Sim.Advance(w, 40);
            if (!events.OfType<FightEnded>().Any(f => f.Agent == wolves)) continue;
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
            var w = World.New(seed);
            var tile = Quiet(w);
            var npc = Survival.Npc.Spawn(w, tile, boldness: 1.0);
            var wolf = Agents.Spawn(w, Species.Wolf, tile, 1, [tile]);
            Mind(w, wolf).State = HerdState.Resting;
            var bodies = new[] { w.Agent(npc).Body, w.Agent(wolf).Members[0], w.Agent(P).Body };
            foreach (var b in bodies) Trace(b);
            var events = Sim.Advance(w, 30);
            if (!events.OfType<FightEnded>().Any(f => f.Agent == npc)) continue;
            fights++;
            AssertEachLivedOnce(bodies, 30, $"seed {seed} NPC fight");
        }
        Assert.True(fights >= 1, "the NPC never fought");
    }

    [Fact]
    public void Advance_LivesThePlayersMinutes_AsWaiting()
    {
        // Review P1: the world ran on and the player stood outside time.
        var w = World.New(1);
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
        var w = World.New(1);
        var tile = Quiet(w);
        Sim.Advance(w, 1); // off minute zero, where the first round's minute is clamped
        var a = Agents.Spawn(w, Species.Caribou, tile, 1);
        var b = Agents.Spawn(w, Species.Caribou, tile, 1);
        var body = w.Agent(a).Members[0];
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
        var w = World.New(1);
        var tile = Quiet(w);
        foreach (var id in w.Agents.Values.Where(x => x.Mind is HerdMind).Select(x => x.Id).ToList()) w.Agents.Remove(id);
        var wolves = Agents.Spawn(w, Species.Wolf, tile, 3);
        var mind = Fauna.MindOf(w.Agent(wolves));
        Combat.LeaveCarcass(w, tile, Species.Caribou, BodyState.Create(SpeciesTable.Of(Species.Caribou).Body, 100, 0.1, 0.4));
        mind.Hunger = 0.95;
        mind.Foray = new TileId(tile.X + 10, tile.Y);
        mind.State = HerdState.Patrolling;
        Sim.Advance(w, 3);
        Assert.Equal(HerdState.Feeding, mind.State);
        Assert.Null(mind.Foray);
    }

}

/// Months of the populated valley: nothing in it gets stuck. One class per seed, so they run side by side.
public class HerdLongRunSeed5Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_NoHerdStaysPut_AndPacksHunt() => HerdLongRunTests.Run(5, output);
}

public class HerdLongRunTests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_NoHerdStaysPut_AndPacksHunt() => Run(17, output);

    /// Review 3: a herd that chased meat off its land stood behind a mountain for forty days. No herd
    /// that can walk spends more than a few days on one tile wanting to move (patrolling, grazing on,
    /// fleeing): resting and feeding, a pack may lie up at a big kill for a fortnight. Every pack that lives hunts.
    internal static void Run(int seed, ITestOutputHelper output)
    {
        const int Days = 60, MaxDaysPut = 2;
        var w = World.New(seed);
        var put = new Dictionary<AgentId, (TileId Tile, long Minutes)>();
        var hunts = new Dictionary<AgentId, int>();
        var packs = w.Agents.Values.Where(a => a.Species == Species.Wolf).Select(a => a.Id).ToList();
        long worst = 0;
        string? worstWho = null;
        for (int hour = 0; hour < Days * 24; hour++)
        {
            foreach (var f in Sim.Advance(w, 60).OfType<FightEnded>().Where(f => packs.Contains(f.Agent))) hunts[f.Agent] = hunts.GetValueOrDefault(f.Agent) + 1;
            foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind))
            {
                bool wantsToMove = ((HerdMind)h.Mind).State is HerdState.Patrolling or HerdState.Grazing or HerdState.Fleeing;
                if (!put.TryGetValue(h.Id, out var p) || p.Tile != h.Tile || !h.Members.All(b => Body.GetCapacities(b).Moving > 0.1))
                    p = (h.Tile, 0);
                put[h.Id] = p = (p.Tile, p.Minutes + (wantsToMove ? 60 : 0));
                if (p.Minutes > worst) (worst, worstWho) = (p.Minutes, $"{h.Id} {h.Species} at {h.Tile}");
            }
        }
        output.WriteLine($"seed {seed}: longest put wanting to move {worst / 1440.0:F1} days ({worstWho}); hunts {string.Join(", ", packs.Select(p => $"{p} {hunts.GetValueOrDefault(p)}"))}");
        Assert.True(worst <= MaxDaysPut * 1440, $"{worstWho} wanted to move but stayed on one tile {worst / 1440.0:F1} days");
        Assert.All(packs.Where(w.Agents.ContainsKey), p => Assert.True(hunts.GetValueOrDefault(p) > 0, $"pack {p} lived {Days} days and never hunted"));
    }
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
        var w = World.New(seed);
        var dens = Sites(w, "wolf_den");
        Assert.NotEmpty(dens);
        foreach (var den in dens)
        {
            var pack = Assert.Single(Herds(w, Species.Wolf), a => ((HerdMind)a.Mind).Territory[0] == den);
            Assert.Equal(den, pack.Tile);
            Assert.InRange(pack.Members.Count, 3, 8);
            Assert.All(((HerdMind)pack.Mind).Territory, t => Assert.True(Math.Abs(t.X - den.X) <= 10 && Math.Abs(t.Y - den.Y) <= 10));
        }
    }

    [Fact]
    public void TheBearCave_HasItsBear()
    {
        var w = World.New(53); // the first seed whose valley has a Bear Cave (elite: far east only)
        var cave = Assert.Single(Sites(w, "bear_cave"));
        var bear = Assert.Single(w.Agents.Values, a => a.Mind is HerdMind m && m.Territory[0] == cave);
        Assert.Contains(bear.Species, new[] { Species.Bear, Species.CaveBear });
        Assert.Single(bear.Members);
    }

    [Fact]
    public void BoneHollow_HasTheMammoths_AndAKilledHerdStaysDead()
    {
        var w = World.New(91237);
        var hollow = Sites(w, "bone_hollow")[0];
        var herd = Assert.Single(Herds(w, Species.Mammoth));
        Assert.Equal(hollow, herd.Tile);
        Assert.InRange(herd.Members.Count, 8, 12);
        Assert.InRange(((HerdMind)herd.Mind).Territory.Count, 8, 288);

        // Hunted out: every member dies of its wounds, the herd leaves the world the way the dead do
        // (Fauna.Tick), and days on nothing has come back to the hollow.
        foreach (var body in herd.Members) body.BloodPct = 0;
        Sim.Advance(w, 60);
        Assert.False(w.Agents.ContainsKey(herd.Id));
        Sim.Advance(w, 3 * 1440);
        Assert.DoesNotContain(w.Agents.Values, a => a.Species == Species.Mammoth);
        Assert.Empty(Herds(World.New(1), Species.Mammoth)); // and a valley with no Bone Hollow has none at all
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
        var w = World.New(seed);
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
public class TerritorySweepTests(ITestOutputHelper output)
{
    static readonly IReadOnlyDictionary<Species, int> Floor = new Dictionary<Species, int>
    {
        [Species.Bison] = 80, [Species.Megaloceros] = 40, [Species.Caribou] = 100, [Species.SaberTooth] = 80,
        [Species.Wolf] = 20, [Species.Bear] = 40, [Species.Mammoth] = 150, [Species.CaveBear] = 3, [Species.Hyena] = 3,
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
