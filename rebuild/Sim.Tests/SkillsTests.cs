namespace Survival.Tests;

/// Skills you can see (short-loop plan, feature 3): every listed action teaches, a level-up is an
/// event, a level is worth something, and it all saves and steps the same however it is chunked.
public class SkillsTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    static World Mild17()
    {
        var w = World.New(17);
        w.FixedWeather = Mild;
        return w;
    }

    static SkillProgress? Progress(World w, AgentId who, Skill s) => ((PersonMind)w.Agent(who).Mind).Skills.GetValueOrDefault(s);
    static double Xp(World w, Skill s) => Progress(w, P, s)?.Xp ?? 0;

    static void SetLevel(World w, AgentId who, Skill s, int level) =>
        ((PersonMind)w.Agent(who).Mind).Skills[s] = new SkillProgress { Level = level };

    static FireId LightPit(World w)
    {
        var id = new FireId(w.NextFireId++);
        w.Fires[id] = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.Birch] = 5 } };
        return id;
    }

    // ---- the curve ----

    /// Old Skill.LevelUpThreshold was Level * 10: 0 at level 0, so the first attempt at anything
    /// granted a free level (bug, fixed).
    [Fact]
    public void TheFirstAttempt_DoesNotGrantALevel()
    {
        Assert.True(Skills.XpToNext(0) > 0);
        var w = World.New(1);
        var events = new List<WorldEvent>();
        Skills.Award(w, w.Agent(P), Skill.Firecraft, Skills.FireFailureXp, events);
        Assert.Equal(0, Skills.Level(w.Agent(P), Skill.Firecraft));
        Assert.Empty(events);
    }

    [Fact]
    public void TheCurve_IsLong_WithSmallSteps()
    {
        for (int l = 1; l < Skills.MaxLevel; l++) Assert.True(Skills.XpToNext(l) > Skills.XpToNext(l - 1));
        double toTop = Enumerable.Range(0, Skills.MaxLevel).Sum(Skills.XpToNext);
        Assert.True(toTop / Skills.ForageXpPerHour > 500, "the top of foraging should take a whole run of foraging");
        Assert.True(Skills.FirecraftPct(1) <= 0.03, "one level is a small step on the odds");
    }

    [Fact]
    public void LevelUp_EmitsAWorldEvent_WhereThePersonStands_AndCarriesTheRestOver()
    {
        var w = World.New(1);
        var me = w.Agent(P);
        var events = new List<WorldEvent>();
        Skills.Award(w, me, Skill.Foraging, Skills.XpToNext(0) + Skills.XpToNext(1) + 2, events);
        var ups = events.OfType<SkillLeveledUp>().ToList();
        Assert.Equal([1, 2], ups.Select(u => u.Level));
        Assert.All(ups, u => Assert.Equal((P, me.Tile, Skill.Foraging), (u.Who, u.Tile, u.Skill)));
        Assert.Equal(2, Progress(w, P, Skill.Foraging)!.Xp, 9);
    }

    [Fact]
    public void AHerd_LearnsNothing()
    {
        var w = World.New(17);
        var wolves = Agents.Spawn(w, Species.Wolf, Map.Of(w).Camp, 2);
        var events = new List<WorldEvent>();
        Skills.Award(w, w.Agent(wolves), Skill.Foraging, 100, events);
        Assert.Empty(events);
        Assert.Equal(0, Skills.Level(w.Agent(wolves), Skill.Foraging));
    }

    // ---- each action teaches ----

    [Fact]
    public void FireStarting_Teaches_ThreeForAFireAndOneForAMiss()
    {
        bool lit = false, missed = false;
        for (int seed = 0; seed < 60 && !(lit && missed); seed++)
        {
            var w = World.New(3);
            Items.Add(Items.Pack(w, P), Resource.Tinder, 1);
            Items.Add(Items.Pack(w, P), Resource.Kindling, 1);
            w.Rngs[StreamName.Of("fire", P)] = Rng.ForSystem(seed, "ignition trial");
            var r = Sim.Step(w, new StartFire(P, ToolType.HandDrill, Resource.Tinder, 0.05, 0.3), 1);
            if (r.Events.Any(e => e is FireLit)) { lit = true; Assert.Equal(Skills.FireSuccessXp, Xp(w, Skill.Firecraft)); }
            else { missed = true; Assert.Equal(Skills.FireFailureXp, Xp(w, Skill.Firecraft)); }
        }
        Assert.True(lit && missed);
    }

    [Fact]
    public void Foraging_TeachesByTheHour()
    {
        var w = Mild17();
        Sim.Step(w, new Gather(P, 0), 60);
        Assert.Equal(Skills.ForageXpPerHour, Xp(w, Skill.Foraging), 6);
    }

    [Fact]
    public void Cooking_TeachesByTheKilo()
    {
        var w = Mild17();
        LightPit(w);
        Items.Add(Items.Pack(w, P), Resource.RawMeat, 1);
        Assert.Equal("cooked", Sim.Step(w, new Cook(P, Resource.RawMeat, 1), 60).InterruptedReason);
        Assert.Equal(Skills.CookXpPerKg, Xp(w, Skill.Cooking), 6);
    }

    [Fact]
    public void Butchering_TeachesByTheHour()
    {
        var w = Mild17();
        var body = BodyState.Create(BodyKind.Quadruped, 100, 0.1, 0.4);
        var c = Carcasses.Create(w, Map.Of(w).Camp, "Caribou", body, 0);
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var r = Sim.Step(w, new Butcher(P, c.Id), 60);
        Assert.Equal(Skills.ButcherXpPerHour * r.MinutesElapsed / 60, Xp(w, Skill.Butchering), 6);
    }

    /// Crafting is camp building for now (the only recipes there are): it teaches by the hour of the work.
    [Fact]
    public void Crafting_TeachesByTheHourOfBuilding()
    {
        var w = Mild17();
        w.Beddings.Remove(Map.Of(w).Camp); // the camp's own bed is better than a makeshift one
        var r = Sim.Step(w, new Build(P, Structure.MakeshiftBedding), 120);
        Assert.Equal("built", r.InterruptedReason);
        Assert.Equal(Skills.CraftXpPerHour * r.MinutesElapsed / 60, Xp(w, Skill.Crafting), 6);
    }

    [Fact]
    public void AnNpc_LearnsByTheSameRules()
    {
        var w = World.New(17);
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        NpcDriver.Live(w, npc, 2 * 1440);
        var skills = ((NpcMind)w.Agent(npc).Mind).Skills;
        Assert.True(skills.Values.Sum(s => s.Level + s.Xp) > 0, "two days and the NPC learned nothing");
    }

    // ---- what a level is worth ----

    [Fact]
    public void Firecraft_IsATermOnTheOddsSheet()
    {
        var w = World.New(1);
        SetLevel(w, P, Skill.Firecraft, 5);
        var odds = Fire.StartOdds(w, w.Agent(P), ToolType.HandDrill, Resource.Tinder);
        var term = Assert.Single(odds.Terms, t => t.Factor == "Firecraft");
        Assert.Equal(5 * Skills.FirecraftPctPerLevel, term.Value, 12);
    }

    [Fact]
    public void Foraging_ScalesTheYield()
    {
        var w = Mild17();
        SetLevel(w, P, Skill.Foraging, 10);
        var odds = Work.ForageYield(w, w.Agent(P));
        Assert.Contains(odds.Terms, t => t.Factor == "Foraging" && t.Op == OddsOp.Scale && Math.Abs(t.Value - 1.15) < 1e-12);
    }

    [Fact]
    public void Cooking_TakesLessTimeAtTheFire()
    {
        int Minutes(int level)
        {
            var w = Mild17();
            SetLevel(w, P, Skill.Cooking, level);
            LightPit(w);
            Items.Add(Items.Pack(w, P), Resource.RawMeat, 1);
            return Sim.Step(w, new Cook(P, Resource.RawMeat, 1), 60).MinutesElapsed;
        }
        Assert.Equal(Work.CookMeatMinutesPerKg, Minutes(0));
        Assert.True(Minutes(10) < Minutes(0));
    }

    [Fact]
    public void Butchering_YieldsMore()
    {
        double Meat(int level)
        {
            var w = Mild17();
            SetLevel(w, P, Skill.Butchering, level);
            var c = Carcasses.Create(w, Map.Of(w).Camp, "Rabbit", BodyState.Create(BodyKind.Quadruped, 2, 0.1, 0.4), 0);
            Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
            Sim.Step(w, new Butcher(P, c.Id), 600);
            return Items.Have(Items.Pack(w, P), Resource.RawMeat);
        }
        Assert.Equal(Meat(0) * Skills.ButcherYieldFactor(10), Meat(10), 9);
    }

    // ---- saved, and the same however stepped ----

    [Fact]
    public void Skills_RoundTripThroughASave()
    {
        var w = World.New(1);
        Skills.Award(w, w.Agent(P), Skill.Cooking, 7.25, []);
        string json = Save.ToJson(w);
        var loaded = Save.FromJson(json);
        Assert.Equal(json, Save.ToJson(loaded));
        Assert.Equal((1, 2.25), (Skills.Level(loaded.Agent(P), Skill.Cooking), Progress(loaded, P, Skill.Cooking)!.Xp));
    }

    [Fact]
    public void Learning_IsTheSame_InOneStepOrMinuteByMinute()
    {
        var one = Mild17();
        SetLevel(one, P, Skill.Foraging, 0);
        Progress(one, P, Skill.Foraging)!.Xp = Skills.XpToNext(0) - 0.5; // levels up half an hour in
        var many = Save.FromJson(Save.ToJson(one));
        var levelled = Sim.Step(one, new Gather(P, 0), 120).Events.OfType<SkillLeveledUp>().Single();
        var events = new List<WorldEvent>();
        for (int i = 0; i < 120; i++) events.AddRange(Sim.Step(many, new Gather(P, 0), 1).Events);
        Assert.Equal(levelled.Minute, events.OfType<SkillLeveledUp>().Single().Minute);
        Assert.Equal(Save.ToJson(one), Save.ToJson(many));
    }
}
