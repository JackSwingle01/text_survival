using Survival;
using Xunit;
using Xunit.Abstractions;

/// Walking back and forth with nothing done (review 4.1): a held stat whose fallback walked it home from one tile
/// out, a wood trip re-picked at every step, and a camp moved off its shelter to a woodpile next door and back.
public class NpcShuttleTests(ITestOutputHelper output)
{
    /// Immediate reversals: a one-tile move straight back onto the tile it came from within a quarter-hour, having
    /// done nothing on the tile it turns back from (a load picked up or put down, wood felled or gathered, a fire fed,
    /// a meal or a drink: hauling a woodpile home a load at a time is work, not a shuttle); and the longest unbroken
    /// run of them, which is a shuttle, not an errand that came back.
    internal sealed class Reversals
    {
        Moved? previous;
        bool worked;
        int run;
        public int Count { get; private set; }
        public int Longest { get; private set; }
        public long LongestAtMinute { get; private set; }

        static bool Work(WorldEvent e, AgentId who) => e switch
        {
            Gathered g => g.Who == who,
            Stashed s => s.Who == who,
            Unstashed u => u.Who == who,
            TreeFelled t => t.Who == who,
            Ate a => a.Who == who,
            Drank d => d.Who == who,
            Butchered b => b.Who == who,
            FuelAdded f => f.By == who,
            FireLit l => l.By == who,
            _ => false,
        };

        public void See(IEnumerable<WorldEvent> events, AgentId who)
        {
            foreach (var e in events)
            {
                if (Work(e, who)) worked = true;
                if (e is not Moved m || m.Who != who) continue;
                if (previous != null && m.To == previous.From && m.Minute - previous.Minute <= 15 && !worked)
                {
                    Count++;
                    if (++run > Longest) (Longest, LongestAtMinute) = (run, m.Minute);
                }
                else run = 0;
                (previous, worked) = (m, false);
            }
        }
    }

    /// A bench NPC on this seed lived for this many days: its reversals, and whether it lived.
    internal static (Reversals Reversals, bool Alive, double Days, BodyState Body) Live(int seed, int days)
    {
        var w = World.New(seed);
        var npc = NpcBench.Spawn(w, Map.Of(w).Camp);
        var seen = new Reversals();
        while (w.Minute < days * 1440L && Body.IsAlive(w.Agent(npc).Body)) seen.See(Sim.Advance(w, 1), npc);
        return (seen, Body.IsAlive(w.Agent(npc).Body), w.Minute / 1440.0, w.Agent(npc).Body);
    }

    /// Seed 17, day 1.96: 98 one-tile moves in a row, camp and one tile out. Water was held at Need until 0.9 with
    /// nothing to drink at camp, no fire to melt snow on and no river known, so its plan did nothing there; warmth's
    /// Want walked it out for wood, and one tile out water's fallback (home) outranked it and walked it back. A held
    /// stat with no plan lets go, and water's fallback walks home only to water to be had there.
    [Fact]
    public void Seed17_FirstThreeDays_NoShuttle()
    {
        var (seen, alive, days, _) = Live(17, 3);
        output.WriteLine($"alive {alive} to day {days:0.00}: {seen.Count} reversals, longest run {seen.Longest} at day {seen.LongestAtMinute / 1440.0:0.00}");
        Assert.True(seen.Longest < 10, $"a run of {seen.Longest} reversals at day {seen.LongestAtMinute / 1440.0:0.00}");
    }

    static readonly int[] BenchSeeds = [5, 17, 42, 123, 2024, 1, 3, 777, 99, 2025, 7, 11, 31, 64, 256, 1000];

    /// The 16-seed bench, 60 days each: wood trips at warmth's Need shuttled, re-picked at every step (5 to 520
    /// reversals a seed; runs of 98, 59, 45, 39 and 32), one seed setting out with a load in hand and turning home at
    /// the first step out with it. A trip once started goes on to its goal while the budget covers it (WoodGoal),
    /// the pack, not what it set out holding, the measure of its load. No seed walks a run of ten.
    [Trait("Category", TestCategories.Slow)]
    [Fact]
    public void BenchSeeds_SixtyDays_NoShuttle()
    {
        var results = new (int Seed, Reversals Reversals, bool Alive, double Days, double FatKg)[BenchSeeds.Length];
        Parallel.For(0, BenchSeeds.Length, i =>
        {
            var (seen, alive, days, body) = Live(BenchSeeds[i], 60);
            results[i] = (BenchSeeds[i], seen, alive, days, body.FatKg);
        });
        foreach (var r in results)
            output.WriteLine($"seed {r.Seed}: {(r.Alive ? "alive" : "dead")} day {r.Days:0.0}, fat {r.FatKg:0.0} kg; {r.Reversals.Count} reversals, longest {r.Reversals.Longest} at day {r.Reversals.LongestAtMinute / 1440.0:0.00}");
        var shuttles = results.Where(r => r.Reversals.Longest >= 10).Select(r => $"seed {r.Seed}: {r.Reversals.Longest} at day {r.Reversals.LongestAtMinute / 1440.0:0.00}").ToList();
        Assert.True(shuttles.Count == 0, string.Join("; ", shuttles));
    }

    static readonly WeatherSample FrostyNight = new(0, WeatherCondition.Clear, -12, 0.2, 0, 0, WindDirection.North);

    /// Seed 256, day 8.69 (01:33): asleep under its overhang with the fire out and the stash short of a morning
    /// fire, it moved home to a woodpile a tile off, lit the fire there, and moved back to the overhang by dawn: 26
    /// such shelter, bare, shelter loops on the 16 seeds. With makings a step off a sheltered home it fetches them and
    /// makes the fire under the shelter; home never leaves it.
    [Fact]
    public void Seed256_OverhangShortOfAMorningFire_FetchesTheWoodpileHome()
    {
        var w = World.New(256);
        w.FixedWeather = FrostyNight;
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
        var m = Map.Of(w);
        var camp = m.Camp;
        var pile = new TileId(camp.X, camp.Y - 1);
        Assert.True(m.TerrainAt(pile).IsPassable() && Map.Blocked(m, camp, pile) == null);
        w.Minute = 8 * 1440L + (25 - 9) * 60 + 33; // day 8, 01:33
        var npc = NpcBench.Spawn(w, camp);
        var agent = w.Agent(npc);
        var mind = (NpcMind)agent.Mind;
        mind.Pack.Kg.Clear();
        mind.Pack.Kg[Resource.Water] = 2;
        agent.Body.EnergyMinutes = 0.4 * Body.MaxEnergyMinutes;
        agent.Body.CoreTempF = 97.6;
        mind.SeeingTo = Stat.Rest;
        // Tinder at home but no morning fire's fuel; kindling and birch on the woodpile next door.
        Items.Add(Items.StashAt(w, camp), Resource.Tinder, 2);
        Items.Add(Items.StashAt(w, pile), Resource.Kindling, 1.5);
        Items.Add(Items.StashAt(w, pile), Resource.Birch, 8);
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute };
        Memories.Observe(w, agent, []);
        mind.Memory.Places[pile] = new TileMemory { SeenMinute = w.Minute, VisitedMinute = w.Minute, Stash = new() { [Resource.Kindling] = 1.5, [Resource.Birch] = 8 } };
        Assert.NotNull(mind.Memory.Places[camp].Shelter);
        var camps = new List<TileId>();
        var lit = new List<TileId>();
        while (w.Minute < 8 * 1440L + (29 - 9) * 60 && Body.IsAlive(agent.Body)) // until 05:00
        {
            lit.AddRange(Sim.Advance(w, 1).OfType<FireLit>().Where(f => f.By == npc).Select(f => f.Tile));
            if (camps.Count == 0 || camps[^1] != mind.Camp) camps.Add(mind.Camp);
        }
        output.WriteLine($"homes {string.Join(" -> ", camps)}; fires lit at {string.Join(", ", lit)}; stash at home {string.Join(",", Items.StashAt(w, camp).Kg.Where(x => x.Value > 0.05).Select(x => $"{x.Key}:{x.Value:0.0}"))}");
        Assert.Equal([camp], camps);
        Assert.Contains(camp, lit);
    }

    /// Seed 31, day 2.09: hungry, its stomach near empty, it walked to roots it had left on a stash a tile out, and
    /// there food stopped being Need - food to hand - but nothing at Want eats off a stash, so it walked back to the
    /// ground beside it, and on arriving food was Need again: thirteen times. Hungry beside food it can eat, on a
    /// stash or in its pack, it eats it.
    [Fact]
    public void Hungry_BesideRootsOnAStash_EatsThem()
    {
        var w = World.New(31);
        w.FixedWeather = new(0, WeatherCondition.Clear, 40, 0.2, 0, 1, WindDirection.North);
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
        w.Minute = 2 * 1440L + 60; // day 2, 10:00
        var m = Map.Of(w);
        var camp = m.Camp;
        var npc = NpcBench.Spawn(w, camp);
        var agent = w.Agent(npc);
        var mind = (NpcMind)agent.Mind;
        var off = MapGen.Neighbors(camp).Where(t => m.TerrainAt(t).IsPassable() && Map.Blocked(m, camp, t) == null).OrderBy(t => t).First();
        agent.Tile = off;
        foreach (var r in mind.Pack.Kg.Keys.Where(r => Items.Resources[r].Category == ResourceCategory.Food).ToList()) mind.Pack.Kg.Remove(r);
        Items.Add(Items.StashAt(w, off), Resource.Roots, 0.23);
        agent.Body.CaloriesKcal = 0.1 * Body.MaxCaloriesKcal; // hungry
        var log = new List<WorldEvent>();
        for (int i = 0; i < 20 && !log.Any(e => e is Ate a && a.Who == npc); i++) log.AddRange(Sim.Advance(w, 1));
        output.WriteLine(string.Join(", ", log.Where(e => e is Moved mv && mv.Who == npc || e is Unstashed u && u.Who == npc || e is Ate a && a.Who == npc)));
        Assert.Contains(log, e => e is Ate { Food: Resource.Roots } a && a.Who == npc);
    }

    /// Seed 5, day 15.9: food's walk east stepped to two tiles off a wolf it had just fled, the flee took it back to
    /// three (FleeTiles), and the walk stepped in again, nineteen times. A walk keeps out of the ground it would run
    /// from: walking home past a predator two tiles off its way, it goes round.
    [Fact]
    public void WalkingHome_PastAPredatorItJustSaw_KeepsOutOfFleeingRange()
    {
        var w = World.New(17);
        w.FixedWeather = new(0, WeatherCondition.Clear, 40, 0.2, 0, 1, WindDirection.North);
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
        w.Minute = 10 * 60 + 30; // 19:30: evening, home is where it is going
        var m = Map.Of(w);
        var camp = m.Camp;
        var start = new TileId(camp.X + 4, camp.Y);
        var wolf = new TileId(camp.X + 2, camp.Y + 2);
        for (int dx = 0; dx <= 4; dx++)
            for (int dy = -1; dy <= 0; dy++)
                Assert.True(m.TerrainAt(new TileId(camp.X + dx, camp.Y + dy)).IsPassable(), $"precondition: {camp.X + dx},{camp.Y + dy} passable");
        var npc = NpcBench.Spawn(w, start, camp);
        var agent = w.Agent(npc);
        var mind = (NpcMind)agent.Mind;
        (mind.ThreatTile, mind.ThreatMinute) = (wolf, w.Minute);
        var walked = new List<TileId>();
        for (int i = 0; i < 60 && agent.Tile != camp; i++)
            walked.AddRange(Sim.Advance(w, 1).OfType<Moved>().Where(mv => mv.Who == npc).Select(mv => mv.To));
        output.WriteLine(string.Join(" ", walked.Select(t => $"({t.X - camp.X},{t.Y - camp.Y})")));
        Assert.Equal(camp, agent.Tile);
        Assert.DoesNotContain(walked, t => MapGen.Manhattan(t, wolf) < 3);
    }

    /// Seed 2024, day 21: 0.094 kg of kindling on the camp stash, remembered as 0.1 (memory keeps a tenth of a kilo).
    /// At camp that is too little to follow the tinder, so home had no fire to be had and tonight's wood was short; a
    /// tile out the remembered 0.1 was enough, so home had sixteen kilos of oak to burn and the wood trip was done. It
    /// walked camp, trees, camp, trees: sixty-six reversals. Home is judged the same from camp and from a tile out.
    [Fact]
    public void KindlingJustUnderTheLine_HomeJudgedAlikeFromCampAndATileOut()
    {
        var w = World.New(2024);
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
        var camp = new TileId(6, 45);
        var npc = NpcBench.Spawn(w, camp);
        var agent = w.Agent(npc);
        var mind = (NpcMind)agent.Mind;
        mind.Pack.Kg.Clear();
        mind.Pack.Kg[Resource.Tinder] = 0.3;
        var stash = Items.StashAt(w, camp);
        Items.Add(stash, Resource.Tinder, 2.86);
        Items.Add(stash, Resource.Kindling, 0.094);
        Items.Add(stash, Resource.Oak, 16.2);
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute };
        Memories.Observe(w, agent, []);
        Assert.Equal(0.1, mind.Memory.Places[camp].Stash![Resource.Kindling]);
        double atCamp = Npc.ReckonedCampFuelHours(w, agent);
        agent.Tile = new TileId(5, 45);
        double aTileOut = Npc.ReckonedCampFuelHours(w, agent);
        output.WriteLine($"camp fuel reckoned at camp {atCamp:0.0} h, a tile out {aTileOut:0.0} h");
        Assert.Equal(atCamp > 0, aTileOut > 0);
    }
}
