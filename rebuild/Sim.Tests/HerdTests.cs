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

    [Fact]
    public void ThePlayer_CanHuntAHerdMember_ButcherItAndEat()
    {
        for (int seed = 1; seed <= 10; seed++)
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
                // Stalk in short steps to throwing range, throw, then close and strike.
                double gap = target == null ? 0 : me.Position.DistanceTo(target.Position);
                var (act, to) = acts.Contains(CombatAction.Attack) ? (CombatAction.Attack, (Cell?)null)
                    : gap <= 10 && acts.Contains(CombatAction.Throw) ? (CombatAction.Throw, null)
                    : acts.Contains(CombatAction.Move) && target != null ? (CombatAction.Move, StepToward(me.Position, target.Position))
                    : acts.Contains(CombatAction.Advance) ? (CombatAction.Advance, null) : (CombatAction.Wait, null);
                var r = Sim.Step(w, new Fight(P, act, target == null ? null : enc.Units.IndexOf(target), to), 1);
                result = r.Events.OfType<FightEnded>().FirstOrDefault(f => f.Agent == P)?.Result;
            }
            output.WriteLine($"seed {seed}: {result}");
            if (result != CombatResult.Victory) continue;

            var carcass = w.Tiles[tile].Carcasses!.First();
            var butchered = Sim.Step(w, new Butcher(P, carcass.Id), 240);
            double meat = Items.Have(pack, Resource.RawMeat);
            Assert.True(meat > 1, $"butchering a caribou gave {meat:F2} kg of meat ({butchered.InterruptedReason})");
            var ate = Sim.Step(w, new Eat(P, Resource.RawMeat, 0.5), 10).Events.OfType<Ate>().Single();
            Assert.True(ate.Kcal > 0);
            output.WriteLine($"seed {seed}: killed, butchered {meat:F1} kg of meat, ate {ate.Kcal:F0} kcal");
            return;
        }
        Assert.Fail("ten stalks with a spear and never a kill");
    }

    static Cell StepToward(Cell from, Cell to) =>
        new(from.X + Math.Sign(to.X - from.X), from.Y + Math.Sign(to.Y - from.Y));

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
}
