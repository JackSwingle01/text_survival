using Survival;
using Xunit;
using Xunit.Abstractions;
using Survival.Tests;

/// Herd renewal: births in season toward what the valley carries of each species, thinning over it, and herds arriving
/// from outside the valley. Most of these run a season of days, so they sit apart from KillChainTests:
/// a class's tests run one after another.
public class RenewalTests
{
    static readonly AgentId P = AgentId.Player;

    static World Bare(double tempC = 5, int seed = 17) => KillChainTests.Bare(tempC, seed);

    static Carcass Caribou(World w, TileId tile, Cause? cause = null) => KillChainTests.Caribou(w, tile, cause);

    static long Days(double d) => (long)(d * 1440);

    /// Day-of-year to world minute, at the hour the clock starts (09:00).
    static long MinuteOfDayOfYear(int dayOfYear) => (dayOfYear - 182 + 365) % 365 * 1440L;

    /// Where seed 17 put a herd of the species: open land it lives on.
    static TileId Put(Species species, int seed = 17) => TestWorlds.New(seed).Agents.Values.First(a => a.Species == species).Tile;

    static Animals CaribouHerd(World w, int members, TileId? at = null) => Animals.Spawn(w, Species.Caribou, at ?? Put(Species.Caribou), members);

    /// Calves are animals of their own: the valley's caribou, counted.
    static int CaribouIn(World w) => w.Agents.Values.Count(a => a.Species == Species.Caribou && a.Mind is HerdMind);

    [Fact]
    [Trait("Category", TestCategories.Slow)] // a long-run simulation: about 43 s in Release
    public void Prey_CalveInTheirSeason_TowardWhatTheValleyCarries()
    {
        var w = Bare();
        w.Minute = MinuteOfDayOfYear(140);
        CaribouHerd(w, 3);
        double capacity = Fauna.Capacity(w, Species.Caribou);
        Assert.True(capacity > 3, $"capacity {capacity:0.0}");
        for (int d = 0; d < 40; d++)
        {
            Sim.Advance(w, 1440);
            Assert.InRange(CaribouIn(w), 3, Math.Max(3, (int)Math.Floor(capacity)));
        }
        Assert.True(CaribouIn(w) > 3, "nothing born in forty days of the calving season");

        // Out of season none is born, though herds still come in from outside to a valley short of caribou.
        var off = Bare();
        off.Minute = MinuteOfDayOfYear(250);
        CaribouHerd(off, 3);
        Assert.Equal(0, CalvesOver(off, 40));
    }

    /// Caribou born over these days: those new at a midnight on a tile where one already stood (its mother's), not an arrival's.
    static int CalvesOver(World w, int days)
    {
        int calves = 0;
        for (int d = 0; d < days; d++)
        {
            Sim.Advance(w, 1440 - Weather.MinuteOfDay(w.Minute) - 1);
            var before = w.Agents.Values.Where(a => a.Species == Species.Caribou).ToDictionary(a => a.Id, a => a.Tile);
            Sim.Advance(w, 2);
            calves += w.Agents.Values.Count(a => a.Species == Species.Caribou && !before.ContainsKey(a.Id) && before.ContainsValue(a.Tile));
        }
        return calves;
    }

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void A_HerdAtCapacity_HasNoYoung()
    {
        var w = Bare();
        w.Minute = MinuteOfDayOfYear(140);
        int full = (int)Math.Ceiling(Fauna.Capacity(w, Species.Caribou));
        CaribouHerd(w, full);
        Sim.Advance(w, 30 * 1440);
        // Nor does any herd arrive: the valley is full of caribou.
        Assert.True(CaribouIn(w) <= full, $"{CaribouIn(w)} caribou in a valley carrying {full}");
    }

    /// Hunger kills from the body, one animal at a time, the leanest first (spec "Bodies"): renewal no longer starves a
    /// pack for days unfed. Old: a pack fourteen days unfed lost a wolf a day to a renewal roll, fat or not.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void A_PackLongUnfed_StarvesFromItsBodies_TheLeanestFirst()
    {
        var w = Bare();
        var wolves = Animals.Spawn(w, Species.Wolf, Put(Species.Wolf), 4);
        var pack = wolves.In(w).OrderBy(a => a.Id).ToList();
        for (int i = 0; i < pack.Count; i++)
        {
            var b = pack[i].Body;
            Fauna.MindOf(pack[i]).LastFedMinute = w.Minute - Days(30);
            if (i == pack.Count - 1) continue; // fed, though long unfed by the clock
            b.CaloriesKcal = 0;
            b.MuscleKg = 0.15 * b.WeightKg + 0.05 + 0.5 * i; // the first the leanest
            b.FatKg = 0.03 * b.WeightKg;
        }
        var deaths = new List<Killed>();
        for (int day = 0; day < 10; day++) deaths.AddRange(Sim.Advance(w, 1440).OfType<Killed>());
        Assert.NotEmpty(deaths);
        Assert.Equal(pack[0].Id, deaths[0].Agent);
        Assert.Equal(deaths.Count, deaths.Select(k => k.Minute).Distinct().Count());
        Assert.DoesNotContain(deaths, k => k.Agent == pack[^1].Id);
        Assert.All(w.Tiles.Values.SelectMany(t => t.Carcasses ?? []).Where(c => c.Animal == "Wolf"), c => Assert.Equal("starvation", c.Cause?.What));
    }

    /// An arrival comes in out of everyone's sight: the player's and every other person's. A valley emptied
    /// of animals, the player in its middle and someone else a quarter of the way across, so most of the
    /// open land is in somebody's sight. Arrivals come in at midnight and nothing else in the empty
    /// valley's day bears on where: the clock goes to each midnight and lives through it.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void An_Arrival_ComesInOutOfEveryonesSight()
    {
        var w = Bare(seed: 123);
        var m = Map.Of(w);
        w.Agent(P).Tile = new TileId(m.Width / 2, m.Height / 2);
        var npc = Npc.Spawn(w, new TileId(m.Width / 4, m.Height / 2));
        var people = new[] { P, npc };
        int arrivals = 0;
        for (int day = 0; day < 100; day++)
        {
            foreach (var body in people.Select(id => w.Agent(id).Body))
                (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (3000, 2000, 900);
            int before = w.NextAgentId;
            w.Minute += 1440 - Weather.MinuteOfDay(w.Minute) - 1;
            var watching = people.Select(id => w.Agent(id).Tile).ToList();
            Sim.Advance(w, 1);
            foreach (var herd in w.Agents.Values.Where(a => a.Mind is HerdMind && a.Id.Value >= before).GroupBy(a => a.Tile))
            {
                arrivals++;
                Assert.All(watching, seer => Assert.True(MapGen.Manhattan(herd.Key, seer) > Fauna.ArrivalClearanceTiles,
                    $"{herd.First().Species} came in at {herd.Key}, in sight of someone at {seer}"));
            }
            Sim.Advance(w, 1);
        }
        Assert.True(arrivals >= 10, $"only {arrivals} arrivals in a hundred days of an empty valley");
    }

    /// Arrivals come where the valley is empty, never megafauna, outside camp's safe zone and out of
    /// everyone's sight, no bigger than the valley has room for, and no pack by another's den.
    /// Review: an arrival was sized from its row alone (caribou twelve strong on land carrying four, a
    /// wolf pack of ten on land carrying two), and a pack could come in on top of another's land.
    /// #96: the lone player on open land was killed by a pack that came for them, so the days watched
    /// were however long they lived (95 on seed 123, and the check was cut to 80); a fire kept burning
    /// on their tile now holds packs off, and they watch all 200 days. Each seed its own class.
    internal static void ArrivalsRun(int seed, ITestOutputHelper output)
    {
        var w = Bare(seed: seed);
        var camp = Map.Of(w).Camp;
        Assert.True(Fauna.ValleyCapacity(w, Species.Caribou) > 0);
        Assert.Equal(0, Fauna.ValleyCapacity(w, Species.Mammoth));
        // The player out on the open land, where arrivals could otherwise land in plain view.
        var open = TestWorlds.New(seed).Agents.Values.First(a => a.Species is Species.Bison or Species.Caribou).Tile;
        w.Agent(P).Tile = open;
        var fire = new FireId(w.NextFireId++);
        var seen = new HashSet<AgentId>(w.Agents.Keys);
        int arrivals = 0;
        var arrived = new HashSet<Species>();
        // Each day's look is the minute after midnight, when the day's arrivals came in (Fauna.Renew).
        Sim.Advance(w, 1440 - Weather.MinuteOfDay(w.Minute));
        int day = 0;
        for (; day < 200 && Body.IsAlive(w.Agent(P).Body); day++)
        {
            var room = Enum.GetValues<Species>().ToDictionary(s => s, s => Fauna.ValleyCapacity(w, s));
            for (int hour = 0; hour < 24; hour++)
            {
                var body = w.Agent(P).Body;
                (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (3000, 2000, 900); // alive, and so someone to be seen by
                w.Fires[fire] = FireDeterrenceTests.Pit(open, w.Minute, pineKg: 5); // kept burning
                Sim.Advance(w, 60);
            }
            var newcomers = w.Agents.Values.Where(a => a.Mind is HerdMind && !seen.Contains(a.Id)).ToList();
            var packDens = w.Agents.Values.Where(a => a.Species == Species.Wolf && seen.Contains(a.Id) && Fauna.MindOf(a).Den != null)
                .Select(a => Fauna.MindOf(a).Den!.Value).ToHashSet();
            var oldTiles = w.Agents.Values.Where(a => seen.Contains(a.Id)).Select(a => (a.Species, a.Tile)).ToHashSet();
            // Those that came together came onto one tile; a newborn is born on its mother's tile, not arrived.
            foreach (var group in newcomers.GroupBy(a => (a.Species, a.Tile)))
            {
                var (species, tile) = group.Key;
                foreach (var a in group) seen.Add(a.Id);
                if (oldTiles.Contains((species, tile))) continue;
                arrivals++;
                arrived.Add(species);
                Assert.False(Fauna.IsMegafauna(species));
                Assert.True(MapGen.Manhattan(tile, camp) > Fauna.SafeZoneTiles, $"{species} came in at {tile}, inside camp's safe zone");
                Assert.True(MapGen.Manhattan(tile, open) > Fauna.ArrivalClearanceTiles, $"{species} came in at {tile}, in sight of the player at {open}");
                Assert.True(group.Count() <= room[species] + 1e-9, $"{species} came in {group.Count()} strong to a valley carrying {room[species]:0.0}");
                if (species == Species.Wolf) Assert.All(packDens, d => Assert.True(MapGen.Manhattan(d, tile) > Fauna.SpacingTiles, $"a pack came in at {tile}, by the den at {d}"));
            }
        }
        output.WriteLine($"seed {seed}: the player watched {day} days; {arrivals} herds arrived");
        Assert.True(day >= 100, $"seed {seed}: the player was there to be seen by for only {day} days"); // enough days of arrivals to judge
        Assert.True(arrivals > 0, $"seed {seed}: nothing came into an empty valley in two hundred days");
        // Caribou are among what comes, whoever is left at the end: on seed 17 the packs that arrive after
        // them have eaten the last by day 155, and bison hold the grazing from then on (#177).
        Assert.Contains(Species.Caribou, arrived);
        // Capacity is derived, never saved: a loaded world reads the same capacities off the same valley.
        var loaded = TestWorlds.Load(Save.ToJson(w));
        foreach (var s in Enum.GetValues<Species>()) Assert.Equal(Fauna.Capacity(w, s), Fauna.Capacity(loaded, s));
    }

    /// Every herd of a kind shares what the valley carries of it (spec "What animals know"): two caribou herds
    /// far apart, a valley's worth between them, have no young, not one each.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void Herds_FarApart_ShareWhatTheValleyCarries()
    {
        var w = Bare();
        w.Minute = MinuteOfDayOfYear(140);
        int full = (int)Math.Floor(Fauna.Capacity(w, Species.Caribou));
        Assert.True(full >= 4, $"capacity {full}");
        var here = Put(Species.Caribou);
        var far = TestWorlds.New(17).Agents.Values.Where(a => a.Mind is HerdMind && !Fauna.IsPredator(a.Species)).MaxBy(a => MapGen.Manhattan(a.Tile, here))!.Tile;
        CaribouHerd(w, full / 2, here);
        CaribouHerd(w, full - full / 2, far);
        Sim.Advance(w, 50 * 1440);
        Assert.True(CaribouIn(w) <= full, $"{CaribouIn(w)} caribou in a valley carrying {full}");
    }

    /// Review: no pack would hunt a mammoth to keep it (IsQuarry), but every pack hunted one anyway
    /// (PreyHere, PreySensed asked IsPrey), and seed 123's packs ate its mammoths out by day 540. One
    /// rule now: a pack neither goes for nor follows a mammoth.
    [Fact]
    public void A_Pack_LeavesMammothsAlone()
    {
        var w = Bare(seed: 123);
        var hollow = Put(Species.Mammoth, 123);
        var mammoths = Animals.Spawn(w, Species.Mammoth, hollow, 6);
        var wolves = Animals.Spawn(w, Species.Wolf, hollow, 6);
        foreach (var wolf in wolves.In(w)) TestKit.SetHunger(wolf, 0.95);
        Assert.False(Fauna.IsQuarry(Species.Mammoth));
        Assert.True(Fauna.IsQuarry(Species.Caribou) && Fauna.IsQuarry(Species.Bison));
        var events = new List<WorldEvent>();
        for (int i = 0; i < 3 * 1440 && wolves.Living(w) > 0; i++)
        {
            foreach (var wolf in wolves.In(w)) TestKit.SetHunger(wolf, 0.95); // starving the whole time, among the mammoths
            events.AddRange(Sim.Advance(w, 1));
        }
        Assert.DoesNotContain(events.OfType<EncounterEnded>(), f => mammoths.Ids.Contains(f.Agent));
        // Hunger may thin the starving pack (Fauna.Thin); no wolf is lost to a mammoth.
        Assert.DoesNotContain(events.OfType<Killed>(), k => wolves.Ids.Contains(k.Agent) && k.By is AgentId by && mammoths.Ids.Contains(by));
        Assert.Equal(6, mammoths.Living(w));
    }

    /// Review: hyenas and the carrion flock (now ravens) never died but in a fight, and a pack only after fourteen
    /// days unfed, so they followed nothing down. What lives on kills thins toward what its land
    /// carries: a fed pack and hyenas in a valley with no prey, and more ravens than it carries, all lose
    /// members, and the herd that is left is no bigger than before.
    /// One seed is enough (#96): the world is hand-built, with nothing else alive, and every loss is an
    /// animal's own daily roll (Fauna.Thin, 0.02 a day for a wolf on land carrying none, 0.015 for a
    /// hyena), so another seed is another draw at the same odds, not another valley. No loss in 60 days:
    /// the pack of eight about e^-9.6, the six hyenas about e^-5.4.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void What_LivesOnKills_ThinsWhenTheLandCannotFeedIt()
    {
        var w = Bare();
        var den = Put(Species.Wolf);
        var wolves = Animals.Spawn(w, Species.Wolf, den, 8);
        var hyenas = Animals.Spawn(w, Species.Hyena, den, 6);
        const int flock = 32; // well over what the valley carries: about 23 since #152 raised the forage
        var ravens = Animals.Spawn(w, Species.Raven, den, flock);
        Assert.Equal(0, Fauna.Capacity(w, Species.Wolf)); // no prey left in the valley
        double ravensRoom = Fauna.Capacity(w, Species.Raven);
        Assert.True(ravensRoom < flock - 4, $"the valley carries {ravensRoom:0.0} ravens");
        Sim.Advance(w, 60 * 1440);
        Assert.True(wolves.Living(w) < 8, "a pack in a valley with no prey lost no one");
        Assert.True(hyenas.Living(w) < 6, "hyenas in a valley with no prey lost no one");
        Assert.True(ravens.Living(w) < flock, "ravens over what the valley carries lost no one");
        Assert.Contains(w.Tiles.Values.SelectMany(t => t.Carcasses ?? []), c => c.Animal == "Hyena" && c.Cause?.What == "starvation");
    }

    /// The wolves took my kill: a pack on the player's own kill holds it against the player (the gate
    /// is unwired; the rule is what it will be). What the player made is still the player's.
    [Fact]
    public void A_Pack_OnYourKill_OccupiesIt()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var kill = Caribou(w, camp, new Cause(P, "the player's wooden spear"));
        TestKit.Give(Items.PileAt(w, camp, new Cause(P, "put down by the player")), Resource.PineBranch, 2);
        Animals.Spawn(w, Species.Wolf, camp, 3);
        w.AccessGates = Access.Gate.All;
        Assert.Equal("a wolf pack guards it", Work.Refusal(w, new Butcher(P, kill.Id))?.Reason);
        Assert.Null(Work.Refusal(w, new PickUp(P, Resource.PineBranch, 1)));
    }

    /// Review: an NPC that died holding a lit torch left it burning in the world's fires for good.
    /// A dropped torch goes out within the hour.
    [Fact]
    public void A_TorchInADeadHand_GoesOut()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var torch = new FireId(w.NextFireId++);
        w.Fires[torch] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, CarriedBy = npc, TorchMinutesLeft = 600 };
        w.Agent(npc).Body.BloodPct = 0;
        Body.MarkDead(w.Agent(npc).Body, w.Minute, new Cause(null, "the cold"));
        Sim.Advance(w, 61);
        Assert.False(w.Fires.ContainsKey(torch), "the dead carry no torch");

        var gone = Npc.Spawn(w, camp); // carried off the map: an agent that has left the world
        var second = new FireId(w.NextFireId++);
        w.Fires[second] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, CarriedBy = gone, TorchMinutesLeft = 600 };
        Agents.Remove(w, gone);
        Sim.Advance(w, 61);
        Assert.False(w.Fires.ContainsKey(second));
    }

    /// A tile's forage scores for a species by its own grazing list: lichen for caribou and hare, grass for bison, berries
    /// for a bear, nothing for a wolf (#122).
    [Theory]
    [InlineData(Species.Caribou, 1)]
    [InlineData(Species.Hare, 1)]
    [InlineData(Species.Bison, 2)]
    [InlineData(Species.Bear, 3)]
    [InlineData(Species.Wolf, 0)]
    public void ATilesForage_ScoresForASpecies_ByItsGrazingList(Species species, double score)
    {
        var w = PlannerTests.Strip();
        var tile = new TileId(20, 1);
        w.PoolCache![tile] = [new ForagePool("pasture", 1.0, [new(Resource.Usnea, 1, 0.1, 0.2), new(Resource.Tinder, 2, 0.1, 0.2), new(Resource.Berries, 3, 0.1, 0.2)])];
        Assert.Equal(score, Fauna.ForageScore(w, tile, species));
    }
}
