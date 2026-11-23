using Survival;
using Xunit;
using Xunit.Abstractions;

/// Every animal is its own agent; a herd is the animals of one species on one tile (decision record
/// "Animals are individuals; a herd is derived"). Driven through the world: spawn, advance, read.
public class IndividualsTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;
    static IEnumerable<Agent> Animals(World w) => w.Agents.Values.Where(a => a.Mind is HerdMind);

    /// An open plain far from camp with nobody on it or next to it.
    static TileId Quiet(World w)
    {
        var m = Map.Of(w);
        var taken = w.Agents.Values.Select(a => a.Tile).ToHashSet();
        return Enumerable.Range(3, m.Width - 6).SelectMany(x => Enumerable.Range(3, m.Height - 6).Select(y => new TileId(x, y)))
            .First(t => m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && MapGen.Manhattan(t, m.Camp) > 30
                && !taken.Contains(t) && MapGen.Neighbors(t).All(n => !taken.Contains(n) && m.TerrainAt(n) == Terrain.Plain && Map.Blocked(m, t, n) == null));
    }

    [Theory]
    [InlineData(17)]
    [InlineData(42)]
    public void APopulatedWorld_HasOneAgentPerAnimal(int seed)
    {
        var w = World.New(seed);
        var animals = Animals(w).ToList();
        output.WriteLine($"{animals.Count} animals");
        Assert.All(animals, a => Assert.Single(a.Members));
        // The valley still holds herds: caribou stand together where they were put.
        Assert.Contains(animals.Where(a => a.Species == Species.Caribou).GroupBy(a => a.Tile), g => g.Count() >= 5);
    }

    /// A pack's hunt draws its side from the wolves that know each other: a rival pack on the same tile is
    /// strangers and never fights beside them.
    [Fact]
    public void APackHunt_DrawsItsSide_FromTheFamiliarWolvesOnTheTile()
    {
        int hunts = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var w = World.New(seed);
            var tile = Quiet(w);
            var ours = Agents.SpawnGroup(w, Species.Wolf, tile, 3);
            var theirs = Agents.SpawnGroup(w, Species.Wolf, tile, 3);
            Agents.Spawn(w, Species.Caribou, tile);
            foreach (var wolf in ours.Concat(theirs).Select(w.Agent))
            {
                TestKit.SetHunger(wolf, 0.9);
                Fauna.MindOf(wolf).State = HerdState.Patrolling;
            }
            var events = new List<WorldEvent>();
            for (int i = 0; i < 120 && !events.OfType<EncounterEnded>().Any(e => ours.Contains(e.Agent) || theirs.Contains(e.Agent)); i++)
                events.AddRange(Sim.Advance(w, 1));
            var fought = events.OfType<EncounterEnded>().Select(e => e.Agent).Where(id => ours.Contains(id) || theirs.Contains(id)).ToHashSet();
            if (fought.Count == 0) continue;
            hunts++;
            Assert.True(fought.IsSubsetOf(ours) || fought.IsSubsetOf(theirs), "wolves of two packs fought on one side");
            Assert.True(fought.Count > 1, "a wolf hunted alone beside its own pack");
        }
        Assert.True(hunts >= 3, $"only {hunts} of 6 packs hunted the caribou beside them");
    }

    /// A fight's outcome reaches the whole herd, not only the animals drawn into it: when a pack takes
    /// one caribou of ten, the nine that never fought run too.
    [Fact]
    public void APackHunt_SendsTheRestOfThePreyHerdRunning()
    {
        int hunts = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var w = World.New(seed);
            var tile = Quiet(w);
            var pack = Agents.SpawnGroup(w, Species.Wolf, tile, 3);
            var herd = Agents.SpawnGroup(w, Species.Caribou, tile, 10);
            foreach (var wolf in pack.Select(w.Agent))
            {
                TestKit.SetHunger(wolf, 0.9);
                Fauna.MindOf(wolf).State = HerdState.Patrolling;
            }
            for (int i = 0; i < 120; i++)
            {
                var ended = Sim.Advance(w, 1).OfType<EncounterEnded>().Where(e => herd.Contains(e.Agent)).ToList();
                if (ended.Count == 0) continue;
                var fought = ended.Select(e => e.Agent).ToHashSet();
                var bystanders = herd.Where(id => !fought.Contains(id) && w.Agents.TryGetValue(id, out var a) && a.Tile == ended[0].Tile && !Sim.Held(w, id))
                    .Select(w.Agent).ToList();
                Assert.NotEmpty(bystanders);
                hunts++;
                Assert.All(bystanders, c =>
                {
                    Assert.Equal(HerdState.Fleeing, Fauna.MindOf(c).State);
                    Assert.True(Fauna.MindOf(c).Fear > 0, $"caribou {c.Id} saw its herd attacked and is not afraid");
                });
                break;
            }
        }
        Assert.True(hunts >= 3, $"only {hunts} of 6 packs hunted the caribou beside them");
    }

    /// A pack that catches a scent goes to where it smelled the prey, even once it is out of range: it does
    /// not forget and turn back the moment the scent fades (which, on the edge of its range, walked a
    /// starving pack to and fro between two tiles for days). A valley emptied of animals, so the pack has
    /// only the one scent to follow.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void APack_KeepsOnToWhereItLastSmelledPrey(int seed)
    {
        var w = World.New(seed);
        foreach (var other in Animals(w).Select(a => a.Id).ToList()) Agents.Remove(w, other);
        var m = Map.Of(w);
        int range = Fauna.DetectionRangeTiles(Species.Wolf);
        // A straight run of open plain, the pack at one end and the scent at the other.
        bool Open(TileId t) => m.InBounds(t) && m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && m.SiteAt(t) == null;
        var tile = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(t => MapGen.Manhattan(t, m.Camp) > 30 && Enumerable.Range(0, range + 1).All(i => Open(new TileId(t.X + i, t.Y))
                && (i == 0 || Map.Blocked(m, new TileId(t.X + i - 1, t.Y), new TileId(t.X + i, t.Y)) == null)));
        var scent = new TileId(tile.X + range, tile.Y);
        var pack = Agents.SpawnGroup(w, Species.Wolf, tile, 3);
        foreach (var wolf in pack.Select(w.Agent))
        {
            TestKit.SetHunger(wolf, 0.95);
            Fauna.MindOf(wolf).State = HerdState.Patrolling;
        }
        var caribou = Agents.Spawn(w, Species.Caribou, scent);
        Fauna.MindOf(w.Agent(caribou)).State = HerdState.Resting;
        for (int i = 0; i < 120 && pack.All(id => w.Agent(id).Tile == tile); i++) Sim.Advance(w, 1);
        Assert.Contains(pack, id => MapGen.Manhattan(w.Agent(id).Tile, scent) < range); // it set out after the scent
        Agents.Remove(w, caribou); // the scent is gone: what is left is where it was
        for (int i = 0; i < 6 * 60 && !pack.Any(id => w.Agent(id).Tile == scent); i++) Sim.Advance(w, 1);
        Assert.Contains(pack, id => w.Agent(id).Tile == scent);
    }

    /// Boldness from numbers counts the derived herd: the wolves on the tile that know each other.
    [Fact]
    public void ABoldnessFromNumbers_CountsTheWolvesItKnows_OnItsTile()
    {
        var w = World.New(1);
        var tile = Quiet(w);
        var person = BodyState.BaselineHuman();
        var lone = Agents.Spawn(w, Species.Wolf, tile);
        double alone = Combat.Boldness(w, lone, person);
        Agents.SpawnGroup(w, Species.Wolf, tile, 4); // strangers
        Assert.Equal(alone, Combat.Boldness(w, lone, person), 9);
        var pack = Agents.SpawnGroup(w, Species.Wolf, Map.Step(tile, Direction.North), 5);
        double inPack = Combat.Boldness(w, pack[0], person);
        Assert.True(inPack > alone, $"a pack of five ({inPack:0.00}) no bolder than a lone wolf ({alone:0.00})");
    }

    /// A herd crossing an edge animal by animal leaves the prints and wear the same herd left as one agent.
    [Fact]
    public void AHerdCrossing_LeavesThePrintsAndWear_OfTheSameHerdCrossingAsOne()
    {
        var w = World.New(1);
        var from = Quiet(w);
        var to = Map.Step(from, Direction.East);
        var herd = Agents.SpawnGroup(w, Species.Caribou, from, 12);
        var moved = new HashSet<AgentId>();
        long start = w.Minute;
        Sim.Start(w, new Travel(herd[0], Direction.East), int.MaxValue, [], out var doing);
        w.Agent(herd[0]).Doing = doing;
        for (int i = 0; i < 120 && moved.Count < herd.Count; i++)
            foreach (var m in Sim.Advance(w, 1).OfType<Moved>().Where(m => m.From == from && m.To == to)) moved.Add(m.Who);
        Assert.Equal(herd.Count, moved.Count); // every one followed
        var together = Tracks.Read(w, to).Single(t => t.Maker == TrackMaker.Hoof);
        double wear = Tracks.WearAt(w, from, to);

        // The same herd as one agent of twelve: one stamp of twelve, at the minute the last one crossed.
        var once = World.New(1);
        once.Minute = w.Minute;
        Tracks.RecordMove(once, from, to, TrackMaker.Hoof, herd.Count, Tracks.IndividualDepth(Species.Caribou));
        var alone = Tracks.Read(once, to).Single(t => t.Maker == TrackMaker.Hoof);
        output.WriteLine($"crossed over {w.Minute - start} min: traffic {together.Traffic} vs {alone.Traffic}, wear {wear:0.00} vs {Tracks.WearAt(once, from, to):0.00}");
        Assert.Equal(alone.Traffic, together.Traffic);
        Assert.Equal(alone.Depth, together.Depth, 1);
        Assert.Equal(alone.Heading, together.Heading);
        Assert.Equal(Tracks.WearAt(once, from, to), wear, 1);
    }

    /// Twelve caribou on a tile are one herd of twelve to whoever sees them, and seen again they are still one.
    [Fact]
    public void AnNpcThatSeesTwelveCaribou_RemembersOneHerdOfTwelve()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var npc = w.Agent(Npc.Spawn(w, camp));
        var there = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        var herd = Agents.SpawnGroup(w, Species.Caribou, there, 12);
        Memories.Glance(w, npc, []);
        var seen = Assert.Single(Memories.Of(npc).Sightings.Values, s => s.Species == Species.Caribou);
        Assert.Equal((there, 12), (seen.Tile, seen.Count));

        // Their first walks off: the eleven left are still the one herd, and the one on its own is another.
        w.Agent(herd[0]).Tile = Map.Step(there, Direction.North);
        Memories.Glance(w, npc, []);
        var now = Memories.Of(npc).Sightings.Values.Where(s => s.Species == Species.Caribou).OrderBy(s => s.Count).ToList();
        Assert.Equal([1, 11], now.Select(s => s.Count));
        Assert.All(herd, id => Assert.NotNull(Memories.LastSeenMinute(npc, id)));
    }

    /// A pup born into a pack knows its mother and whom she knows, and they know it: it is of their pack.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void AWolfBornIntoAPack_IsOfThePack()
    {
        var w = World.New(17);
        while (Weather.DayOfYear(w.Minute) != 112) w.Minute += 1440; // the whelping season, before the clock starts
        int born = w.NextAgentId;
        var packs = w.Agents.Values.Where(a => a.Species == Species.Wolf).Select(a => Fauna.HerdOf(w, a).Select(k => k.Id).ToHashSet())
            .DistinctBy(p => p.Min()).ToList(); // each pack once
        Agent? pup = null;
        for (int day = 0; day < 40 && pup == null; day++)
        {
            Sim.Advance(w, 1440);
            var body = w.Agent(P).Body;
            (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (3000, 2000, 900);
            // A pup is born on its mother's tile; a pack that arrives is placed out of everyone's sight.
            pup = w.Agents.Values.FirstOrDefault(a => a.Species == Species.Wolf && a.Id.Value >= born
                && packs.SelectMany(p => p).Any(id => w.Agents.TryGetValue(id, out var o) && o.Tile == a.Tile));
        }
        Assert.NotNull(pup);
        var pack = packs.Single(p => p.Any(id => Fauna.Familiarity(pup, id) > 0));
        Assert.All(pack.Where(w.Agents.ContainsKey).Select(w.Agent), wolf =>
        {
            Assert.True(Fauna.Familiarity(pup, wolf.Id) > 0, $"the pup does not know {wolf.Id}");
            Assert.True(Fauna.Familiarity(wolf, pup.Id) > 0, $"{wolf.Id} does not know the pup");
        });
        Assert.DoesNotContain(packs.Where(p => p != pack).SelectMany(p => p), id => Fauna.Familiarity(pup, id) > 0);
    }

    /// Same seed, same world; a save and load mid-run changes nothing.
    [Fact]
    public void ThePopulatedValley_IsTheSameWorld_ThroughASaveMidRun()
    {
        Assert.Equal(Save.ToJson(World.New(23)), Save.ToJson(World.New(23)));
        var straight = World.New(23);
        Sim.Advance(straight, 2 * 1440);
        var saved = World.New(23);
        Sim.Advance(saved, 1440 + 17);
        saved = Save.FromJson(Save.ToJson(saved));
        Sim.Advance(saved, 1440 - 17);
        Assert.Equal(Save.ToJson(straight), Save.ToJson(saved));
    }
}

/// The first part of the cohesion gate (spec "Testing Decisions"): 60 days, several seeds, per open-group
/// species, fewer than a tenth of its animals alone on average. The no-blob and split/join checks wait
/// for crowding (#83). One class per seed, so they run side by side.
public static class CohesionGate
{
    const int Days = 60;
    const double MaxAlonePct = 0.10;
    static readonly Species[] OpenGroup = [Species.Caribou, Species.Bison, Species.Megaloceros, Species.Mammoth];

    internal static void Run(int seed, ITestOutputHelper output)
    {
        var w = World.New(seed);
        var alone = OpenGroup.ToDictionary(s => s, _ => (Alone: 0L, Animals: 0L));
        for (int hour = 0; hour < Days * 24; hour++)
        {
            Sim.Advance(w, 60);
            var body = w.Agent(AgentId.Player).Body;
            (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (3000, 2000, 900); // the player stays out of it, alive
            foreach (var a in w.Agents.Values.Where(a => a.Mind is HerdMind && alone.ContainsKey(a.Species) && Sim.IsAlive(a)))
            {
                var (lone, all) = alone[a.Species];
                alone[a.Species] = (lone + (Fauna.HerdOf(w, a).Count == 1 ? 1 : 0), all + 1);
            }
        }
        output.WriteLine($"seed {seed}: " + string.Join(", ", alone.Where(kv => kv.Value.Animals > 0)
            .Select(kv => $"{kv.Key} {(double)kv.Value.Alone / kv.Value.Animals:P1} alone of {kv.Value.Animals / (Days * 24.0):0.0}")));
        foreach (var (species, (lone, all)) in alone.Where(kv => kv.Value.Animals > 0))
            Assert.True((double)lone / all < MaxAlonePct, $"seed {seed}: {species} alone {(double)lone / all:P1} of the time on average");
    }
}

[Trait("Category", TestCategories.Slow)]
public class CohesionGateSeed17Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_FewOpenGroupAnimalsAlone() => CohesionGate.Run(17, output);
}

[Trait("Category", TestCategories.Slow)]
public class CohesionGateSeed23Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_FewOpenGroupAnimalsAlone() => CohesionGate.Run(23, output);
}

[Trait("Category", TestCategories.Slow)]
public class CohesionGateSeed42Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_FewOpenGroupAnimalsAlone() => CohesionGate.Run(42, output);
}

[Trait("Category", TestCategories.Slow)]
public class CohesionGateSeed123Tests(ITestOutputHelper output)
{
    /// A valley with a Bone Hollow, so the mammoths are measured too.
    [Fact] public void SixtyDays_FewOpenGroupAnimalsAlone() => CohesionGate.Run(123, output);
}
