using Survival;
using Xunit;
using Xunit.Abstractions;

/// Stat tiers (decision record 2026-11-21-stat-tiers): every stat is Critical, Need or Want; tier ranks
/// first, then warmth, water, food, rest. And the camp and work rules the review of the old scores asked
/// for: a shelter with fuel is not left for a bare tile, home moves back to a known shelter, and no long
/// work with warmth Critical.
public class NpcTierTests(ITestOutputHelper output)
{
    static readonly WeatherSample Mild = new(0, WeatherCondition.Clear, 20, 0.0, 0, 1, WindDirection.North);
    static readonly WeatherSample Frost = new(0, WeatherCondition.Clear, -12, 0.2, 0, 1, WindDirection.North);

    static void NoHerds(World w)
    {
        foreach (var h in w.Agents.Values.Where(a => a.Mind is HerdMind).Select(a => a.Id).ToList()) w.Agents.Remove(h);
    }

    static Activity DecideFor(World w, AgentId npc) => Npc.Decide(w, w.Agent(npc), []).Activity;

    // ---- the tier functions ----

    [Theory]
    [InlineData(98.6, false, 30.0, Tier.None)]
    [InlineData(98.6, false, 20.0, Tier.Want)]   // tonight's 16 h in, not a spare half-day
    [InlineData(98.6, false, 4.0, Tier.Need)]    // tonight's fuel short is warmth, Need
    [InlineData(97.4, false, 30.0, Tier.Need)]
    [InlineData(97.6, true, 30.0, Tier.Need)]    // seeing to it: held to the satisfied line
    [InlineData(97.6, false, 30.0, Tier.None)]
    [InlineData(95.0, false, 30.0, Tier.Critical)]
    public void Warmth(double coreF, bool seeing, double fuelHours, Tier tier) =>
        Assert.Equal(tier, Tiers.WarmthTier(coreF, seeing, fuelHours, tonightHours: 16));

    [Theory]
    [InlineData(0.15, false, 3.0, 2.0, Tier.Critical)]
    [InlineData(0.40, false, 3.0, 2.0, Tier.Need)]
    [InlineData(0.80, true, 3.0, 2.0, Tier.Need)]
    [InlineData(0.80, false, 3.0, 2.0, Tier.None)]
    [InlineData(0.80, false, 1.0, 2.0, Tier.Want)]
    [InlineData(0.55, false, 0.0, 18.0, Tier.Need)] // nothing to hand and the evening's loss takes it to the line
    public void Water(double hydrationPct, bool seeing, double toHandL, double hoursLeft, Tier tier) =>
        Assert.Equal(tier, Tiers.WaterTier(hydrationPct, seeing, toHandL, hoursLeft));

    [Theory]
    [InlineData(0.10, false, Tier.Critical)]
    [InlineData(0.20, false, Tier.Need)]
    [InlineData(0.40, true, Tier.Need)]
    [InlineData(0.40, false, Tier.Want)]
    [InlineData(0.90, false, Tier.None)]
    public void Rest(double energyPct, bool seeing, Tier tier) => Assert.Equal(tier, Tiers.RestTier(energyPct, seeing));

    [Fact]
    public void Food_UnderTheFloorIsNeed_StarvingIsCritical()
    {
        var b = BodyState.BaselineHuman();
        double target = b.FatKg;
        Assert.Equal(Tier.Want, Tiers.FoodTier(b, target, false, foodToHand: true));
        b.CaloriesKcal = 0;
        Assert.Equal(Tier.Need, Tiers.FoodTier(b, target, false, foodToHand: false)); // stomach empty, nothing for today
        b.CaloriesKcal = 1500;
        b.FatKg = 0.09 * b.WeightKg; // under the 10% floor: weeks of reserve, Need
        Assert.Equal(Tier.Need, Tiers.FoodTier(b, b.FatKg, false, foodToHand: true));
        b.FatKg = 0.025 * b.WeightKg; // the body's own floor, 3% of what it weighs now
        Assert.Equal(Tier.Need, Tiers.FoodTier(b, b.FatKg, false, foodToHand: true)); // still something in the stomach
        b.CaloriesKcal = 0;
        Assert.True(Tiers.Starving(b));
        Assert.Equal(Tier.Critical, Tiers.FoodTier(b, b.FatKg, false, foodToHand: true));
    }

    // ---- the tier rule in the decision (acceptance 4) ----

    /// An NPC at camp in the morning, warm, with no wood on the stash, and this much fat.
    static (World World, AgentId Npc) Lean(double fatPct, double caloriesKcal)
    {
        var w = World.New(17);
        var npc = NpcBench.Spawn(w, Map.Of(w).Camp);
        var b = w.Agent(npc).Body;
        b.FatKg = fatPct * b.WeightKg;
        b.CaloriesKcal = caloriesKcal;
        Npc.AdaptTargetFat((NpcMind)w.Agent(npc).Mind, b, w.Minute);
        return (w, npc);
    }

    /// Fat under the 10% floor but far from starving, tonight's fuel short: wood first. Old: fat under the
    /// floor was critical food and outranked tonight's wood, and NPCs foraged all day and froze.
    [Fact]
    public void UnderTheFatFloor_NotStarving_TonightsWoodFirst()
    {
        var (w, npc) = Lean(0.08, 800);
        var a = DecideFor(w, npc);
        var mind = (NpcMind)w.Agent(npc).Mind;
        output.WriteLine($"{a}, seeing to {mind.SeeingTo}");
        Assert.NotEqual(Stat.Food, mind.SeeingTo);
        Assert.False(a is Gather { Focus: ForageFocus.Food });
    }

    /// Starving - fat at the floor the body burns to, the stomach empty, muscle going - is Critical food: it
    /// outranks tonight's wood, but not Critical warmth or water.
    [Fact]
    public void Starving_FoodFirst_UnlessWarmthOrWaterIsCritical()
    {
        var (w, npc) = Lean(0.025, 0);
        DecideFor(w, npc);
        Assert.Equal(Stat.Food, ((NpcMind)w.Agent(npc).Mind).SeeingTo);

        var (cold, coldNpc) = Lean(0.025, 0);
        cold.Agent(coldNpc).Body.CoreTempF = 94.8;
        DecideFor(cold, coldNpc);
        Assert.Equal(Stat.Warmth, ((NpcMind)cold.Agent(coldNpc).Mind).SeeingTo);

        var (dry, dryNpc) = Lean(0.025, 0);
        dry.Agent(dryNpc).Body.HydrationMl = 0.1 * Body.MaxHydrationMl;
        Items.Add(((NpcMind)dry.Agent(dryNpc).Mind).Pack, Resource.Water, 1);
        DecideFor(dry, dryNpc);
        Assert.Equal(Stat.Water, ((NpcMind)dry.Agent(dryNpc).Mind).SeeingTo);
    }

    // ---- camps weigh shelter (acceptance 2) ----

    /// The start camp's overhang, with a tree tile next to it: (the overhang, the trees).
    static (World World, AgentId Npc, TileId Overhang, TileId Trees) OverhangByTrees(int seed, WeatherSample sky)
    {
        var w = World.New(seed);
        w.FixedWeather = sky;
        NoHerds(w);
        var camp = Map.Of(w).Camp;
        Assert.NotNull(Shelters.Peek(w, camp));
        var trees = MapGen.Neighbors(camp).Where(t => Map.Of(w).InBounds(t) && HasTrees(w, t)).OrderBy(t => t).First();
        var npc = NpcBench.Spawn(w, camp);
        return (w, npc, camp, trees);
    }

    static bool HasTrees(World w, TileId t) =>
        Pools.At(w, t).Select((p, i) => (p, i)).Any(x => x.p is TreePool && Pools.HasTrees(w, new PoolId(t, x.i)));

    /// Seed 31, day 23: an overhang holding 16.3 kg of oak and 5.8 kg of tinder, no kindling. Old: Makings asked for
    /// kindling or pine, so the camp counted as having no fire to be had; the warming walk to the trees moved home to
    /// them, and it froze there. A shelter with fuel and tinder is a camp worth keeping: it fetches kindling.
    [Fact]
    public void AShelterWithFuelAndTinder_IsNotLeftForTheTrees()
    {
        var (w, npc, overhang, _) = OverhangByTrees(31, Frost);
        var mind = (NpcMind)w.Agent(npc).Mind;
        mind.Pack.Kg.Remove(Resource.Kindling);
        var stash = Items.StashAt(w, overhang);
        Items.Add(stash, Resource.Oak, 16.3);
        Items.Add(stash, Resource.Tinder, 5.8);
        // The overhang's floor gives no sticks (seed 99's was bare rock): kindling has to come from the trees.
        foreach (var r in new[] { Resource.Kindling, Resource.Pine }) Pools.SetGrazed(w, new PoolId(overhang, 0), r, 1);
        w.Agent(npc).Body.CoreTempF = 96.8; // warmth Need
        var moves = new List<string>();
        while (w.Minute < 12 * 60) // to nine at night
        {
            Sim.Advance(w, 1);
            if (mind.Camp != overhang) moves.Add($"{w.Minute} camp {mind.Camp}");
        }
        output.WriteLine(moves.Count == 0 ? "camp kept" : string.Join("; ", moves.Take(5)));
        Assert.Empty(moves);
        Assert.True(Fire.CurrentPitAt(w, overhang) is { } pit && (Fire.IsActive(pit) || pit.HasEmbers), "no fire made at the overhang");
    }

    /// Home on a bare tree tile next to an overhang it knows, with tonight's wood short: the wood plan takes home back
    /// to the overhang (old: home stayed wherever the last stand was, 115 of 117 moves to bare tiles).
    [Fact]
    public void ABareCampByAKnownShelter_MovesHomeBackToIt()
    {
        var (w, npc, overhang, trees) = OverhangByTrees(31, Mild);
        var agent = w.Agent(npc);
        var mind = (NpcMind)agent.Mind;
        Knowledge.Observe(w, agent, []); // it has seen the overhang
        agent.Tile = trees;
        mind.Camp = trees;
        for (int i = 0; i < 30 && mind.Camp == trees; i++) Sim.Advance(w, 10);
        output.WriteLine($"camp {mind.Camp}, overhang {overhang}");
        Assert.Equal(overhang, mind.Camp);
    }

    // ---- no felling while freezing (acceptance 3) ----

    /// Seed 31, day 34: at a cold pit on a tree tile with nothing to light, core 93.2, it committed an hour of
    /// felling and came out of it at 88.5. With warmth Critical it fells nothing and works no stint over 15 minutes.
    [Fact]
    public void WarmthCritical_NoFelling_NoLongWork()
    {
        var (w, npc, _, trees) = OverhangByTrees(31, Frost);
        var agent = w.Agent(npc);
        var mind = (NpcMind)agent.Mind;
        agent.Tile = trees;
        mind.Camp = trees;
        mind.Pack.Kg.Clear(); // nothing to light a fire with
        agent.Body.CoreTempF = 94.5;
        var work = new List<(Activity Activity, int Minutes)>();
        var felled = new List<TreeFelled>();
        Doing? prev = null;
        long end = w.Minute + 90;
        while (w.Minute < end && Body.IsAlive(agent.Body))
        {
            felled.AddRange(Sim.Advance(w, 1).OfType<TreeFelled>().Where(f => f.Who == npc));
            if (agent.Doing is { Activity: Gather or Butcher } d && !ReferenceEquals(d, prev)) work.Add((d.Activity, d.MinutesLeft + 1));
            prev = agent.Doing;
        }
        output.WriteLine(string.Join(", ", work.Select(s => $"{s.Activity} {s.Minutes}")));
        Assert.Empty(felled);
        int treePool = Pools.At(w, trees).ToList().FindIndex(p => p is TreePool);
        Assert.All(work, s => Assert.False(s.Activity is Gather g && g.Pool == treePool, $"felled with warmth Critical: {s.Activity}"));
        Assert.All(work, s => Assert.True(s.Minutes <= 15, $"a {s.Minutes}-minute stint with warmth Critical"));
    }
}
