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
        var w = TestWorlds.New(seed);
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
        for (int seed = 1; seed <= 12; seed++)
        {
            var w = TestWorlds.New(seed);
            var tile = Quiet(w);
            var ours = Agents.SpawnGroup(w, Species.Wolf, tile, 3);
            var theirs = Agents.SpawnGroup(w, Species.Wolf, tile, 3);
            Agents.Spawn(w, Species.Caribou, tile);
            foreach (var wolf in ours.Concat(theirs).Select(w.Agent)) TestKit.SetHunger(wolf, 0.9);
            var events = new List<WorldEvent>();
            for (int i = 0; i < 120 && !events.OfType<EncounterEnded>().Any(e => ours.Contains(e.Agent) || theirs.Contains(e.Agent)); i++)
                events.AddRange(Sim.Advance(w, 1));
            var fought = events.OfType<EncounterEnded>().Select(e => e.Agent).Where(id => ours.Contains(id) || theirs.Contains(id)).ToHashSet();
            if (fought.Count == 0) continue;
            hunts++;
            Assert.True(fought.IsSubsetOf(ours) || fought.IsSubsetOf(theirs), "wolves of two packs fought on one side");
            Assert.True(fought.Count > 1, "a wolf hunted alone beside its own pack");
        }
        Assert.True(hunts >= 2, $"only {hunts} of 12 packs hunted the caribou beside them");
    }

    /// A fight's outcome reaches the whole herd, not only the animals drawn into it: when a pack takes
    /// one caribou of ten, the nine that never fought run too. A valley emptied of animals, so nothing
    /// but the pack and the herd is near.
    [Fact]
    public void APackHunt_SendsTheRestOfThePreyHerdRunning()
    {
        int hunts = 0;
        for (int seed = 1; seed <= 12; seed++)
        {
            var w = TestWorlds.New(seed);
            foreach (var other in Animals(w).Select(a => a.Id).ToList()) Agents.Remove(w, other);
            var tile = Quiet(w);
            var pack = Agents.SpawnGroup(w, Species.Wolf, tile, 3);
            var herd = Agents.SpawnGroup(w, Species.Caribou, tile, 10);
            foreach (var wolf in pack.Select(w.Agent)) TestKit.SetHunger(wolf, 0.9);
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
                    Assert.True(Fauna.Fleeing(Fauna.MindOf(c)), $"caribou {c.Id} saw its herd attacked and does not run");
                    Assert.True(Fauna.MindOf(c).Fear > 0, $"caribou {c.Id} saw its herd attacked and is not afraid");
                });
                break;
            }
        }
        Assert.True(hunts >= 2, $"only {hunts} of 12 packs hunted the caribou beside them");
    }

    /// A hungry pack that senses a caribou down a run of open plain goes to it and kills it (the quarry as a food source,
    /// valued by the kill-chance forecast); a valley emptied of animals, so the pack has only the one caribou to go after.
    /// (Was: a pack keeps on to where it last smelled prey, the old stalk's scent; a quarry sighting now stays until the
    /// pack sees its tile empty.)
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AHungryPack_GoesToTheQuarryItSenses_AndKillsIt(int seed)
    {
        var w = TestWorlds.New(seed);
        foreach (var other in Animals(w).Select(a => a.Id).ToList()) Agents.Remove(w, other);
        var m = Map.Of(w);
        const int range = 6;
        bool Open(TileId t) => m.InBounds(t) && m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && m.SiteAt(t) == null;
        var tile = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(t => MapGen.Manhattan(t, m.Camp) > 30 && Enumerable.Range(0, range + 1).All(i => Open(new TileId(t.X + i, t.Y))
                && (i == 0 || Map.Blocked(m, new TileId(t.X + i - 1, t.Y), new TileId(t.X + i, t.Y)) == null)));
        var at = new TileId(tile.X + range, tile.Y);
        var pack = Agents.SpawnGroup(w, Species.Wolf, tile, 3);
        foreach (var wolf in pack.Select(w.Agent)) TestKit.SetHunger(wolf, 0.95);
        var caribou = Agents.Spawn(w, Species.Caribou, at);
        var killed = new List<Killed>();
        for (int i = 0; i < 6 * 60 && killed.Count == 0; i++) killed.AddRange(Sim.Advance(w, 1).OfType<Killed>().Where(k => k.Agent == caribou));
        var kill = Assert.Single(killed);
        Assert.Contains(pack, id => kill.By == id);
    }

    /// Ravens follow a raven that flies off, though its flight starts and ends inside its own minute and
    /// no other raven ever sees it under way; kin deciding before it in id order follow the minute after.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void AFlock_FollowsAFlightTooShortToSeeUnderWay(int leader)
    {
        var w = TestWorlds.New(1);
        var from = Quiet(w);
        var to = Map.Step(from, Direction.East);
        var ravens = Agents.SpawnGroup(w, Species.Raven, from, 4);
        Assert.Equal(1, Traversal.FlightMinutesPerTile(w.Agent(ravens[leader])));
        Sim.Start(w, new Travel(ravens[leader], Direction.East), int.MaxValue, [], out var doing);
        w.Agent(ravens[leader]).Doing = doing;
        var moved = new HashSet<AgentId>();
        for (int i = 0; i < 10 && moved.Count < ravens.Count; i++)
            foreach (var m in Sim.Advance(w, 1).OfType<Moved>().Where(m => m.From == from && m.To == to)) moved.Add(m.Who);
        Assert.Equal(ravens.ToHashSet(), moved);
    }

    /// Boldness from numbers counts the derived herd: the wolves on the tile that know each other.
    [Fact]
    public void ABoldnessFromNumbers_CountsTheWolvesItKnows_OnItsTile()
    {
        var w = TestWorlds.New(1);
        var tile = Quiet(w);
        var person = BodyState.BaselineHuman();
        var lone = Agents.Spawn(w, Species.Wolf, tile);
        double alone = Combat.Boldness(w, lone, person, Species.Human);
        Agents.SpawnGroup(w, Species.Wolf, tile, 4); // strangers
        Assert.Equal(alone, Combat.Boldness(w, lone, person, Species.Human), 9);
        var pack = Agents.SpawnGroup(w, Species.Wolf, Map.Step(tile, Direction.North), 5);
        double inPack = Combat.Boldness(w, pack[0], person, Species.Human);
        Assert.True(inPack > alone, $"a pack of five ({inPack:0.00}) no bolder than a lone wolf ({alone:0.00})");
    }

    /// A herd crossing an edge animal by animal leaves the prints and wear the same herd left as one agent.
    [Fact]
    public void AHerdCrossing_LeavesThePrintsAndWear_OfTheSameHerdCrossingAsOne()
    {
        var w = TestWorlds.New(1);
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
        double trodden = Tracks.TroddenAt(w, from, to);

        // The same herd crossing at once: twelve stamps, at the minute the last one crossed.
        var once = TestWorlds.New(1);
        once.Minute = w.Minute;
        foreach (var _ in herd) Tracks.RecordMove(once, from, to, TrackMaker.Hoof, Tracks.IndividualDepth(Species.Caribou));
        var alone = Tracks.Read(once, to).Single(t => t.Maker == TrackMaker.Hoof);
        output.WriteLine($"crossed over {w.Minute - start} min: traffic {together.Traffic} vs {alone.Traffic}, trodden {trodden:0.000} vs {Tracks.TroddenAt(once, from, to):0.000}");
        Assert.Equal(alone.Traffic, together.Traffic);
        Assert.Equal(alone.Depth, together.Depth, 1);
        Assert.Equal(alone.Heading, together.Heading);
        Assert.Equal(Tracks.TroddenAt(once, from, to), trodden, 2);
    }

    /// A hungry grazer with kin within one tile is with them (as KinAt): it grazes where it stands rather than walk
    /// onto their tile, so a herd spread over a few tiles stays spread. Kin farther off, it still goes to them.
    /// A valley emptied of animals; the kin are fed, so only the one is hungry.
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    public void AHungryGrazer_WithKinWithinOneTile_StaysPut_AndGoesToKinFartherOff(int apartTiles, bool goes)
    {
        var w = TestWorlds.New(1);
        foreach (var other in Animals(w).Select(a => a.Id).ToList()) Agents.Remove(w, other);
        var m = Map.Of(w);
        bool Open(TileId t) => m.InBounds(t) && m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && m.SiteAt(t) == null;
        var tile = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(t => MapGen.Manhattan(t, m.Camp) > 30 && Enumerable.Range(0, apartTiles + 1).All(i => Open(new TileId(t.X + i, t.Y))
                && (i == 0 || Map.Blocked(m, new TileId(t.X + i - 1, t.Y), new TileId(t.X + i, t.Y)) == null)));
        var kin = Agents.SpawnGroup(w, Species.Mammoth, new TileId(tile.X + apartTiles, tile.Y), 4);
        var lone = Agents.Spawn(w, Species.Mammoth, tile);
        // An open group's kin are any of its species, so the four are the lone one's kin without having met it.
        foreach (var k in kin.Select(w.Agent)) TestKit.SetHunger(k, 0);
        TestKit.SetHunger(w.Agent(lone), 0.8);
        var body = w.Agent(lone).Body;
        Assert.Equal(Priority.Need, Priorities.AnimalFoodPriority(body, SpeciesTable.Of(Species.Mammoth).FatPct, seeingTo: false));
        var moved = new List<Moved>();
        for (int i = 0; i < 60; i++) moved.AddRange(Sim.Advance(w, 1).OfType<Moved>().Where(e => e.Who == lone));
        if (goes) Assert.Contains(moved, e => e.To.X > e.From.X);
        else Assert.Empty(moved);
    }

    /// Twelve caribou on a tile are one herd of twelve to whoever sees them, and seen again they are still one.
    [Fact]
    public void AnNpcThatSeesTwelveCaribou_RemembersOneHerdOfTwelve()
    {
        var w = TestWorlds.New(17);
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

    /// A pup born into a pack knows its mother and whom she knows, and they know it: it is of their pack,
    /// and not of the strangers denning beside it. A valley emptied of animals: two packs on neighbouring
    /// dens in the whelping season, fed so they never hunt, and bison pinned to one tile in range, beyond
    /// scent, so the land carries far more wolves than there are and a pup comes within the season
    /// (eight wolves at 0.02 a day each, times the land's room, (51.6 - 11) / 51.6: no pup in its 41 days about 1 in 250).
    [Fact]
    public void AWolfBornIntoAPack_IsOfThePack()
    {
        var w = TestWorlds.New(17);
        foreach (var other in Animals(w).Select(a => a.Id).ToList()) Agents.Remove(w, other);
        while (Weather.DayOfYear(w.Minute) != 110) w.Minute += 1440; // the whelping season opens, before anyone is put down
        var m = Map.Of(w);
        int apartTiles = 12 + 8; // past a wolf's old 12-tile reach for people, as it was
        bool Open(TileId t) => m.InBounds(t) && m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && m.SiteAt(t) == null;
        var den = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(t => MapGen.Manhattan(t, m.Camp) > 30 && Open(t) && Open(Map.Step(t, Direction.North)) && Open(new TileId(t.X + apartTiles, t.Y)));
        var strangersDen = Map.Step(den, Direction.North);
        var grazing = new TileId(den.X + apartTiles, den.Y);
        Agents.SpawnGroup(w, Species.Bison, grazing, 150);
        var pack = Agents.SpawnGroup(w, Species.Wolf, den, 8);
        var strangers = Agents.SpawnGroup(w, Species.Wolf, strangersDen, 3);
        var wolves = pack.Concat(strangers).ToList();
        double capacity = Fauna.Capacity(w, Species.Wolf);
        Assert.True(capacity >= 3 * wolves.Count, $"the valley carries {capacity:0.0} wolves");

        Agent? pup = null;
        int day = 0;
        for (; day <= 40 && pup == null; day++)
        {
            foreach (var wolf in wolves.Where(w.Agents.ContainsKey).Select(w.Agent))
            {
                TestKit.SetHunger(wolf, 0);
                Fauna.MindOf(wolf).LastFedMinute = w.Minute;
            }
            // Births are rolled at midnight and nothing in the fed packs' day bears on them: the clock goes
            // straight to the minute before, and lives through midnight.
            w.Minute += 1440 - Weather.MinuteOfDay(w.Minute) - 1;
            Sim.Advance(w, 2);
            // A pup dens at its mother's den; a pack that arrives dens where it came in, off the packs' dens.
            pup = w.Agents.Values.FirstOrDefault(a => a.Species == Species.Wolf && !wolves.Contains(a.Id) && Fauna.MindOf(a).Den == den);
        }
        output.WriteLine($"a pup on day {day} of the season, in a valley carrying {capacity:0.0} wolves");
        Assert.NotNull(pup);
        Assert.All(pack.Where(w.Agents.ContainsKey).Select(w.Agent), wolf =>
        {
            Assert.True(Fauna.Familiarity(pup, wolf.Id) > 0, $"the pup does not know {wolf.Id}");
            Assert.True(Fauna.Familiarity(wolf, pup.Id) > 0, $"{wolf.Id} does not know the pup");
        });
        Assert.DoesNotContain(strangers, id => Fauna.Familiarity(pup, id) > 0);
    }

    /// Same seed, same world; a save and load mid-run changes nothing.
    [Trait("Category", TestCategories.Slow)]
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

/// The cohesion gate (spec "Testing Decisions"): 60 days, several seeds, in Release, every metric per species. A herd
/// here is the animals of a species on a connected group of occupied tiles (8 neighbours), so a herd spread over
/// several tiles while it grazes counts as one (#83, Jack 2026-11-24). Bands were set from a first run (work log,
/// 2026-11-23-83). Packs are the wolves (or hyenas) familiar with each other (#84): those at the first look after the
/// warm-up must neither merge nor dissolve unless killed off, and rival packs should keep apart. One class per seed, so they run side by side. A look every
/// seven minutes, which no cycle fitting the hour can keep landing on the same phase of, as a look every hour could (#96).
/// It measures after a warm-up, so the populated start isn't counted. It fails only on what means something is broken
/// (allocation past twice the ceiling, an open-group species mostly alone); every other limit is reported, and one
/// outside its band is a follow-up to tune, not a failure (#83, Jack 2026-11-24).
public static class CohesionGate
{
    const int Days = 60, WarmUpDays = 3, LookEveryMinutes = 7;
    const double MaxAlonePct = 0.10, MaxHerdPct = 0.60;
    /// A herd over MaxHerdPct in more than this share of the looks is a blob that never splits.
    const double MaxBlobLooksPct = 0.5;
    /// What fails the gate: allocation past twice its ceiling, or an open-group species alone this much.
    const double BrokenAllocationFactor = 2, BrokenAlonePct = 0.25;
    /// No species' hunger is a stand-in now (the hunt is #84, the ravens' food #85), so the deaths band skips none.
    static readonly Species[] StandIns = [];
    static readonly Species[] ClosedGroup = [Species.Wolf, Species.Hyena];
    /// Kills a week by a pack species, a band set from the runs with the hunt on the planner (work log 2026-11-24-84,
    /// final run: wolves 0.1-2.9, hyenas 1.2-3.3 on the four seeds).
    const double MinKillsPerWeek = 0.5, MaxKillsPerWeek = 10;
    /// The longest a pack animal whose food is due goes with no quarry within its sight's reach, in days (final run: 4.9-27.6).
    const double MaxDaysWithoutPrey = 25;
    /// The share of looks two rival packs are within a tile of each other.
    const double MaxRivalsNearPct = 0.05;
    /// The most the count of packs may move from the first look's.
    const int MaxPackCountDrift = 2;
    const int BlobFloor = 20;
    /// Splits and joins an animal-day (a look at which its herd's members had changed) at least: a frozen lockstep has none.
    const double MinRegroupsPerAnimalDay = 0.5;
    /// Tile changes an animal-day: a herd that never moves, or one that oscillates.
    const double MinMovesPerAnimalDay = 0.5, MaxMovesPerAnimalDay = 40;
    /// The share of a species on its single busiest water tile, on average over the looks.
    const double MaxWaterTilePct = 0.5;
    /// Deaths of hunger and thirst (bodies, and renewal's thinning over capacity) per head living on average, over the 60 days.
    const double MaxHungerAndThirstDeathsPerHead = 0.5;
    /// Allocation a day: 3x the herd baseline (#81: 4,064 MB for 30 populated days on seed 17), kept after #133 (Jack).
    const double MaxAllocatedMbPerDay = 3 * 4064.0 / 30;
    static readonly Species[] OpenGroup = [Species.Caribou, Species.Bison, Species.Megaloceros, Species.Mammoth];
    /// Open-group species the alone limits skip: a lone giant deer is ordinary, not a broken herd (#292, Jack
    /// 2026-11-26). Their alone share is still printed, as for bears and foxes.
    static readonly Species[] MayBeAlone = [Species.Megaloceros];

    sealed class Tally
    {
        public long Alone, AnimalLooks, Regroups, Moves, Deaths, BlobLooks, CountedLooks;
        /// The biggest herd's look: its day, its tiles, and whether any of them has water.
        public (int Day, int Tiles, bool AtWater) Biggest;
        public double MaxHerdPct, WaterTilePctSum;
        public int Looks;
        public List<int> Herds = [];
        public long Kills, HungryLooks, RivalsNearLooks;
        public int MinPacks = int.MaxValue, MaxPacks, FirstPacks = -1;
        public double LongestWithoutPreyDays;
    }

    /// The packs of a closed-group species: its living animals linked by mutual kinship (Fauna.IsKin), two or more.
    internal static List<HashSet<AgentId>> Packs(List<Agent> ofOneSpecies)
    {
        var packs = new List<HashSet<AgentId>>();
        var placed = new HashSet<AgentId>();
        foreach (var start in ofOneSpecies)
        {
            if (!placed.Add(start.Id)) continue;
            var pack = new HashSet<AgentId> { start.Id };
            var queue = new Queue<Agent>([start]);
            while (queue.TryDequeue(out var a))
                foreach (var b in ofOneSpecies)
                    if (!placed.Contains(b.Id) && Fauna.IsKin(a, b) && Fauna.IsKin(b, a)) { placed.Add(b.Id); pack.Add(b.Id); queue.Enqueue(b); }
            if (pack.Count > 1) packs.Add(pack);
        }
        return packs;
    }

    /// A predator's look while its food is due: the stretch it has gone with no quarry within its roam's reach (its
    /// species' clear sight), the longest kept.
    static void Hungry(World w, List<Agent> predators, Tally t, Dictionary<AgentId, int> withoutPrey, double looksPerDay)
    {
        foreach (var p in predators)
        {
            bool due = Priorities.AnimalFoodPriority(p.Body, SpeciesTable.Of(p.Species).FatPct, seeingTo: false) >= Priority.Need;
            int reach = (int)(Sight.OpenGroundTiles * SpeciesTable.Of(p.Species).PerceptionFactor);
            bool prey = Nearby.Within(w, p.Tile, reach).Any(a => a.Mind is HerdMind && Fauna.IsQuarry(a.Species) && Sim.IsAlive(a));
            if (!due || prey) { withoutPrey.Remove(p.Id); continue; }
            t.HungryLooks++;
            int looks = withoutPrey[p.Id] = withoutPrey.GetValueOrDefault(p.Id) + 1;
            t.LongestWithoutPreyDays = Math.Max(t.LongestWithoutPreyDays, looks / looksPerDay);
        }
    }

    /// The herds of one species: its animals on each connected group of occupied tiles, 8 neighbours.
    internal static List<List<Agent>> Herds(IEnumerable<Agent> ofOneSpecies)
    {
        var byTile = ofOneSpecies.GroupBy(a => a.Tile).ToDictionary(g => g.Key, g => g.ToList());
        var seen = new HashSet<TileId>();
        var herds = new List<List<Agent>>();
        foreach (var start in byTile.Keys.OrderBy(t => t.X).ThenBy(t => t.Y))
        {
            if (!seen.Add(start)) continue;
            var herd = new List<Agent>();
            var queue = new Queue<TileId>([start]);
            while (queue.TryDequeue(out var t))
            {
                herd.AddRange(byTile[t]);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var n = new TileId(t.X + dx, t.Y + dy);
                        if (byTile.ContainsKey(n) && seen.Add(n)) queue.Enqueue(n);
                    }
            }
            herds.Add(herd);
        }
        return herds;
    }

    internal static void Run(int seed, ITestOutputHelper output)
    {
        var w = TestWorlds.New(seed);
        var tallies = new SortedDictionary<Species, Tally>();
        Tally Of(Species s) => tallies.TryGetValue(s, out var t) ? t : tallies[s] = new Tally();
        var company = new Dictionary<AgentId, int>();
        var firstPacks = new Dictionary<Species, List<HashSet<AgentId>>>();
        var withoutPrey = new Dictionary<AgentId, int>();
        var merges = new List<string>();
        // The thread's count at the first look after the warm-up. Null until then: a look falls on a multiple of
        // LookEveryMinutes, which the warm-up's last minute need not be, and a count never taken reads as the
        // thread's whole life, every test it ran before this one included.
        long? allocatedAtWarmUp = null;
        for (int minute = 0; minute < Days * 1440; minute += LookEveryMinutes)
        {
            if (minute >= WarmUpDays * 1440) allocatedAtWarmUp ??= GC.GetAllocatedBytesForCurrentThread();
            var events = Sim.Advance(w, LookEveryMinutes);
            var body = w.Agent(AgentId.Player).Body;
            (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (3000, 2000, 900); // the player stays out of it, alive
            if (minute < WarmUpDays * 1440) continue;
            foreach (var e in events)
            {
                if (e is Moved m && w.Agents.TryGetValue(m.Who, out var mover) && mover.Mind is HerdMind) Of(mover.Species).Moves++;
                if (e is Killed { By: AgentId by } kill && w.Agents.TryGetValue(by, out var killer) && killer.Mind is HerdMind && Fauna.IsPredator(killer.Species)
                    && Fauna.IsQuarry(kill.Species)) Of(killer.Species).Kills++;
                if (e is Killed { By: null } k && w.Tiles.GetValueOrDefault(k.Tile)?.Carcasses?.Find(c => c.DiedMinute == k.Minute)?.Cause?.What is "starvation" or "thirst")
                    Of(k.Species).Deaths++;
            }
            var animals = w.Agents.Values.Where(a => a.Mind is HerdMind && Sim.IsAlive(a)).ToList();
            foreach (var group in animals.GroupBy(a => a.Species))
            {
                var t = Of(group.Key);
                int living = group.Count();
                t.Looks++;
                t.AnimalLooks += living;
                var herds = Herds(group);
                if (living > BlobFloor)
                {
                    var top = herds.MaxBy(h => h.Count)!;
                    double pct = top.Count / (double)living;
                    t.CountedLooks++;
                    if (pct > MaxHerdPct) t.BlobLooks++;
                    if (pct > t.MaxHerdPct)
                    {
                        var tiles = top.Select(a => a.Tile).Distinct().ToList();
                        (t.MaxHerdPct, t.Biggest) = (pct, (minute / 1440, tiles.Count, tiles.Any(x => Work.HasUnfrozenWater(w, x) || Work.HasOpenWater(w, x))));
                    }
                }
                t.Herds = herds.Select(h => h.Count).OrderDescending().ToList();
                if (Fauna.IsPredator(group.Key) && Combat.Hunts(group.Key)) Hungry(w, group.ToList(), t, withoutPrey, looksPerDay: 1440.0 / LookEveryMinutes);
                if (ClosedGroup.Contains(group.Key))
                {
                    var packs = Packs(group.ToList());
                    if (!firstPacks.ContainsKey(group.Key)) (firstPacks[group.Key], t.FirstPacks) = (packs, packs.Count);
                    (t.MinPacks, t.MaxPacks) = (Math.Min(t.MinPacks, packs.Count), Math.Max(t.MaxPacks, packs.Count));
                    var first = firstPacks[group.Key];
                    foreach (var pack in packs)
                        if (first.Count(f => f.Overlaps(pack)) > 1) merges.Add($"day {minute / 1440}: {group.Key} packs merged into [{string.Join(" ", pack)}]");
                    var tileOf = group.ToDictionary(a => a.Id, a => a.Tile);
                    bool near = false;
                    for (int i = 0; i < first.Count && !near; i++)
                        for (int j = i + 1; j < first.Count && !near; j++)
                            near = first[i].Where(tileOf.ContainsKey).Any(a => first[j].Where(tileOf.ContainsKey).Any(b => MapGen.Manhattan(tileOf[a], tileOf[b]) <= 1));
                    if (near) t.RivalsNearLooks++;
                }
                var atWater = group.GroupBy(a => a.Tile).Where(h => Work.HasUnfrozenWater(w, h.Key) || Work.HasOpenWater(w, h.Key))
                    .Select(h => h.Count()).DefaultIfEmpty(0).Max();
                t.WaterTilePctSum += atWater / (double)living;
                foreach (var herd in herds)
                {
                    int key = herd.Select(k => k.Id.GetHashCode()).OrderBy(h => h).Aggregate(17, (acc, h) => acc * 31 + h);
                    foreach (var a in herd)
                    {
                        if (herd.Count == 1) t.Alone++;
                        if (company.TryGetValue(a.Id, out var was) && was != key) t.Regroups++;
                        company[a.Id] = key;
                    }
                }
            }
        }
        double mbPerDay = (GC.GetAllocatedBytesForCurrentThread() - allocatedAtWarmUp!.Value) / 1e6 / (Days - WarmUpDays);
        output.WriteLine($"seed {seed}: {mbPerDay:F0} MB a day allocated");
        double looksPerDay = 1440.0 / LookEveryMinutes;
        foreach (var (s, t) in tallies)
        {
            double animalDays = t.AnimalLooks / looksPerDay, heads = t.AnimalLooks / (double)Math.Max(1, t.Looks);
            output.WriteLine($"seed {seed} {s}: {heads:F1} living on average; alone {t.Alone / (double)t.AnimalLooks:P1}; biggest herd {t.MaxHerdPct:P0} (day {t.Biggest.Day}, {t.Biggest.Tiles} tiles, water {t.Biggest.AtWater}; over {MaxHerdPct:P0} at {t.BlobLooks / (double)Math.Max(1, t.CountedLooks):P1} of looks); "
                + $"regroups {t.Regroups / animalDays:F2}/animal-day; moves {t.Moves / animalDays:F2}/animal-day; busiest water tile {t.WaterTilePctSum / t.Looks:P1}; "
                + $"hunger and thirst deaths {t.Deaths} ({t.Deaths / Math.Max(1, heads):F2} a head); herds at the end [{string.Join(" ", t.Herds)}]");
        }
        // Outside a band: reported, for a follow-up to tune.
        var outside = new List<string>();
        void Band(bool within, string what) { if (!within) outside.Add(what); }
        Band(mbPerDay < MaxAllocatedMbPerDay, $"{mbPerDay:F0} MB a day allocated, over {MaxAllocatedMbPerDay:F0}");
        foreach (var (s, t) in tallies)
        {
            double animalDays = t.AnimalLooks / looksPerDay, heads = t.AnimalLooks / (double)Math.Max(1, t.Looks);
            if (!StandIns.Contains(s))
                Band(t.Deaths / Math.Max(1, heads) <= MaxHungerAndThirstDeathsPerHead, $"{s} lost {t.Deaths} to hunger and thirst, {heads:F1} living on average");
            Band(t.WaterTilePctSum / t.Looks < MaxWaterTilePct, $"{s} {t.WaterTilePctSum / t.Looks:P1} on its busiest water tile");
            double moves = t.Moves / animalDays;
            Band(moves >= MinMovesPerAnimalDay && moves <= MaxMovesPerAnimalDay, $"{s} moved {moves:F1} tiles an animal-day");
            if (!OpenGroup.Contains(s)) continue;
            if (!MayBeAlone.Contains(s)) Band(t.Alone / (double)t.AnimalLooks < MaxAlonePct, $"{s} alone {t.Alone / (double)t.AnimalLooks:P1} of the time");
            double blobLooks = t.BlobLooks / (double)Math.Max(1, t.CountedLooks);
            Band(blobLooks <= MaxBlobLooksPct, $"one {s} herd held over {MaxHerdPct:P0} of its species at {blobLooks:P1} of looks");
            Band(t.Regroups / animalDays >= MinRegroupsPerAnimalDay, $"{s} regrouped {t.Regroups / animalDays:F2} times an animal-day");
        }
        var dissolved = new List<string>();
        foreach (var (s, first) in firstPacks)
        {
            var living = w.Agents.Values.Where(a => a.Species == s && a.Mind is HerdMind && Sim.IsAlive(a)).ToList();
            var now = Packs(living);
            foreach (var pack in first)
            {
                int left = pack.Count(w.Agents.ContainsKey);
                bool held = now.Any(p => p.Count(pack.Contains) >= 2);
                output.WriteLine($"seed {seed} {s} pack of {pack.Count} on day {WarmUpDays}: {left} living on day {Days}, {(held ? "still a pack" : left < 2 ? "killed off" : "dissolved")}");
                if (!held && left >= 2) dissolved.Add($"{s} pack [{string.Join(" ", pack)}] dissolved with {left} living");
            }
        }
        foreach (var (s, t) in tallies)
        {
            double weeks = (Days - WarmUpDays) / 7.0;
            // The saber-tooth lives on its small game (the stand-in grazing) and hunts only when that pays less: no band.
            if (ClosedGroup.Contains(s))
            {
                output.WriteLine($"seed {seed} {s}: {t.Kills / weeks:F1} kills a week; longest without prey in reach while hungry {t.LongestWithoutPreyDays:F1} days");
                Band(t.Kills / weeks >= MinKillsPerWeek && t.Kills / weeks <= MaxKillsPerWeek, $"{s} made {t.Kills / weeks:F1} kills a week");
                Band(t.LongestWithoutPreyDays <= MaxDaysWithoutPrey, $"{s} went {t.LongestWithoutPreyDays:F1} days hungry with no prey in reach");
            }
            if (t.FirstPacks < 0) continue;
            output.WriteLine($"seed {seed} {s}: packs {t.FirstPacks} at first, {t.MinPacks}-{t.MaxPacks} over the run; rival packs within a tile at {t.RivalsNearLooks / (double)t.Looks:P2} of looks");
            Band(t.MaxPacks - t.FirstPacks <= MaxPackCountDrift && t.FirstPacks - t.MinPacks <= MaxPackCountDrift, $"{s} packs ranged {t.MinPacks}-{t.MaxPacks} from {t.FirstPacks}");
            Band(t.RivalsNearLooks / (double)t.Looks <= MaxRivalsNearPct, $"{s} rival packs within a tile at {t.RivalsNearLooks / (double)t.Looks:P1} of looks");
        }
        foreach (var o in outside) output.WriteLine($"seed {seed} outside its band: {o}");
        foreach (var m in merges.Take(5).Concat(dissolved)) output.WriteLine($"seed {seed} BROKEN: {m}");
        // The ticket's own (#84): rival packs never merge, and a pack lasts the run unless killed off.
        Assert.True(merges.Count == 0, $"seed {seed}: {merges.FirstOrDefault()}");
        Assert.True(dissolved.Count == 0, $"seed {seed}: {dissolved.FirstOrDefault()}");
        // Broken: these fail.
        Assert.True(mbPerDay < BrokenAllocationFactor * MaxAllocatedMbPerDay, $"seed {seed}: {mbPerDay:F0} MB a day allocated, over {BrokenAllocationFactor * MaxAllocatedMbPerDay:F0}");
        foreach (var (s, t) in tallies)
            if (OpenGroup.Contains(s) && !MayBeAlone.Contains(s))
                Assert.True(t.Alone / (double)t.AnimalLooks < BrokenAlonePct, $"seed {seed}: {s} alone {t.Alone / (double)t.AnimalLooks:P1} of the time");
    }
}

[Trait("Category", TestCategories.Slow)]
public class CohesionGateSeed17Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_PassesTheCohesionGate() => CohesionGate.Run(17, output);
}

[Trait("Category", TestCategories.Slow)]
public class CohesionGateSeed23Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_PassesTheCohesionGate() => CohesionGate.Run(23, output);
}

[Trait("Category", TestCategories.Slow)]
public class CohesionGateSeed42Tests(ITestOutputHelper output)
{
    [Fact] public void SixtyDays_PassesTheCohesionGate() => CohesionGate.Run(42, output);
}

[Trait("Category", TestCategories.Slow)]
public class CohesionGateSeed123Tests(ITestOutputHelper output)
{
    /// A valley with a Bone Hollow, so the mammoths are measured too.
    [Fact] public void SixtyDays_PassesTheCohesionGate() => CohesionGate.Run(123, output);
}
