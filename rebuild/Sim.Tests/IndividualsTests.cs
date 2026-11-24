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
        for (int seed = 1; seed <= 6; seed++)
        {
            var w = TestWorlds.New(seed);
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
    /// one caribou of ten, the nine that never fought run too. A valley emptied of animals, so nothing
    /// but the pack and the herd is near.
    [Fact]
    public void APackHunt_SendsTheRestOfThePreyHerdRunning()
    {
        int hunts = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var w = TestWorlds.New(seed);
            foreach (var other in Animals(w).Select(a => a.Id).ToList()) Agents.Remove(w, other);
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
        var w = TestWorlds.New(seed);
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

    /// A flock follows a raven that flies off, though its flight starts and ends inside its own minute and
    /// no other raven ever sees it under way; kin deciding before it in id order follow the minute after.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void AFlock_FollowsAFlightTooShortToSeeUnderWay(int leader)
    {
        var w = TestWorlds.New(1);
        var from = Quiet(w);
        var to = Map.Step(from, Direction.East);
        var flock = Agents.SpawnGroup(w, Species.Raven, from, 4);
        Assert.Equal(1, Traversal.FlightMinutesPerTile(w.Agent(flock[leader])));
        Sim.Start(w, new Travel(flock[leader], Direction.East), int.MaxValue, [], out var doing);
        w.Agent(flock[leader]).Doing = doing;
        var moved = new HashSet<AgentId>();
        for (int i = 0; i < 10 && moved.Count < flock.Count; i++)
            foreach (var m in Sim.Advance(w, 1).OfType<Moved>().Where(m => m.From == from && m.To == to)) moved.Add(m.Who);
        Assert.Equal(flock.ToHashSet(), moved);
    }

    /// Boldness from numbers counts the derived herd: the wolves on the tile that know each other.
    [Fact]
    public void ABoldnessFromNumbers_CountsTheWolvesItKnows_OnItsTile()
    {
        var w = TestWorlds.New(1);
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
        int apartTiles = Fauna.DetectionRangeTiles(Species.Wolf) + 8;
        bool Open(TileId t) => m.InBounds(t) && m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && m.SiteAt(t) == null;
        var den = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(t => MapGen.Manhattan(t, m.Camp) > 30 && Open(t) && Open(Map.Step(t, Direction.North)) && Open(new TileId(t.X + apartTiles, t.Y)));
        var strangersDen = Map.Step(den, Direction.North);
        var grazing = new TileId(den.X + apartTiles, den.Y);
        Agents.SpawnGroup(w, Species.Bison, grazing, 60, [grazing]);
        var pack = Agents.SpawnGroup(w, Species.Wolf, den, 8, [den]);
        var strangers = Agents.SpawnGroup(w, Species.Wolf, strangersDen, 3, [strangersDen]);
        var wolves = pack.Concat(strangers).ToList();
        double capacity = Fauna.CapacityOf(w, w.Agent(pack[0]));
        Assert.True(capacity >= 3 * wolves.Count, $"the land carries {capacity:0.0} wolves");

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
            // A pup has a copy of its mother's land; a pack that arrives comes to land of its own.
            pup = w.Agents.Values.FirstOrDefault(a => a.Species == Species.Wolf && !wolves.Contains(a.Id)
                && Fauna.MindOf(a).Territory.SequenceEqual([den]));
        }
        output.WriteLine($"a pup on day {day} of the season, on land carrying {capacity:0.0} wolves");
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
/// 2026-11-23-83). Pack stability and kill rates are #84's. One class per seed, so they run side by side. A look every
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
    /// Their hunger is the stand-ins' until #84 (the hunt) and #85 (the carrion flock), so the deaths band skips them.
    static readonly Species[] StandIns = [Species.Raven, Species.Hyena, Species.Wolf];
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

    sealed class Tally
    {
        public long Alone, AnimalLooks, Regroups, Moves, Deaths, BlobLooks, CountedLooks;
        /// The biggest herd's look: its day, its tiles, and whether any of them has water.
        public (int Day, int Tiles, bool AtWater) Biggest;
        public double MaxHerdPct, WaterTilePctSum;
        public int Looks;
        public List<int> Herds = [];
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
        long allocated = 0;
        for (int minute = 0; minute < Days * 1440; minute += LookEveryMinutes)
        {
            if (minute == WarmUpDays * 1440) allocated = GC.GetAllocatedBytesForCurrentThread();
            var events = Sim.Advance(w, LookEveryMinutes);
            var body = w.Agent(AgentId.Player).Body;
            (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (3000, 2000, 900); // the player stays out of it, alive
            if (minute < WarmUpDays * 1440) continue;
            foreach (var e in events)
            {
                if (e is Moved m && w.Agents.TryGetValue(m.Who, out var mover) && mover.Mind is HerdMind) Of(mover.Species).Moves++;
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
        double mbPerDay = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 1e6 / (Days - WarmUpDays);
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
            Band(t.Alone / (double)t.AnimalLooks < MaxAlonePct, $"{s} alone {t.Alone / (double)t.AnimalLooks:P1} of the time");
            double blobLooks = t.BlobLooks / (double)Math.Max(1, t.CountedLooks);
            Band(blobLooks <= MaxBlobLooksPct, $"one {s} herd held over {MaxHerdPct:P0} of its species at {blobLooks:P1} of looks");
            Band(t.Regroups / animalDays >= MinRegroupsPerAnimalDay, $"{s} regrouped {t.Regroups / animalDays:F2} times an animal-day");
        }
        foreach (var o in outside) output.WriteLine($"seed {seed} outside its band: {o}");
        // Broken: these fail.
        Assert.True(mbPerDay < BrokenAllocationFactor * MaxAllocatedMbPerDay, $"seed {seed}: {mbPerDay:F0} MB a day allocated, over {BrokenAllocationFactor * MaxAllocatedMbPerDay:F0}");
        foreach (var (s, t) in tallies)
            if (OpenGroup.Contains(s))
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
