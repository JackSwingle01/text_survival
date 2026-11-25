namespace Survival.Tests;

/// Every verb through the faculties (documentation/specs/2026-11-24-a-body-is-read-in-tiers.md, "Refusing and
/// slowing", the "Needs above zero" column): a verb is refused when a faculty it needs is at zero, with the
/// reason the faculty carries. Waiting, resting and sleeping never are.
public class CrippledVerbsTests
{
    static readonly AgentId P = AgentId.Player;

    internal static void SetLayers(BodyState b, string region, double condition)
    {
        foreach (var layer in b.Regions.Single(r => r.Name == region).Layers) layer.Condition = condition;
    }

    internal static void Armless(BodyState b)
    {
        SetLayers(b, "Left Arm", 0);
        SetLayers(b, "Right Arm", 0);
    }

    internal static void Legless(BodyState b)
    {
        SetLayers(b, "Left Leg", 0);
        SetLayers(b, "Right Leg", 0);
    }

    /// Alive, with deep cold, shock and a daze taking the whole of consciousness.
    internal static void KnockedOut(BodyState b)
    {
        foreach (var kind in new[] { EffectKind.Hypothermia, EffectKind.Shock, EffectKind.Dazed }) Effects.Set(b, kind, 1.0);
    }

    /// No eye and no ear left.
    internal static void BlindAndDeaf(BodyState b)
    {
        foreach (var organ in b.Regions.SelectMany(r => r.Organs).Where(o => o.Name.Contains("Eye") || o.Name.Contains("Ear")))
            organ.Condition = 0;
    }

    /// Camp, carrying a striker, tinder, sticks, a knife, an axe, meat and water (light enough for an armless body
    /// to walk under), thirsty, beside a caribou carcass, by day.
    /// With trees on the tile, unless the scene must survive a save and load (the pool cache is not saved).
    static World Scene(out Carcass carcass, bool trees = true)
    {
        var w = TestWorlds.Bare(17);
        var camp = w.Agent(P).Tile;
        if (trees) w.PoolCache = new() { [camp] = [.. Pools.At(w, camp), new TreePool("trees", Resource.BirchFirewood, 100)] };
        var pack = Items.Pack(w, P);
        pack.Gear.Add(Items.Make("fire_striker"));
        TestKit.Give(pack, Resource.BirchBark, 0.5);
        TestKit.Give(pack, Resource.Stick, 2);
        pack.Gear.Add(Items.Make("flint_knife"));
        pack.Gear.Add(Items.Make("flint_axe"));
        TestKit.Give(pack, Resource.RawMeat, 1);
        TestKit.Give(pack, Resource.Water, 1);
        w.Agent(P).Body.HydrationMl = Body.MaxHydrationMl / 2;
        carcass = KillChainTests.Caribou(w, w.Agent(P).Tile);
        Sim.Step(w, new Wait(P), 1);
        return w;
    }

    /// What each verb of the table is, as the activity that does it here.
    static Dictionary<string, Activity> TableVerbs(World w, Carcass carcass)
    {
        var tile = w.Agent(P).Tile;
        var pools = Pools.At(w, tile);
        int PoolOf(Func<PoolDef, bool> kind) => Enumerable.Range(0, pools.Count).First(i => kind(pools[i]));
        var carried = Recipes.All.First(r => r.Station == null && !r.Result.Placed && r.Result.Repair == null);
        var placed = Recipes.All.First(r => r.Station == null && r.Result.Placed);
        return new()
        {
            ["forage"] = new Gather(P, PoolOf(p => p is ForagePool)),
            ["fell"] = new Gather(P, PoolOf(p => p is TreePool)),
            ["butcher"] = new Butcher(P, carcass.Id),
            ["make"] = new Make(P, carried.Id),
            ["build"] = new Make(P, placed.Id),
            ["fire"] = new StartFire(P, ToolType.FireStriker, Resource.BirchBark, Verbs.TinderKg, Verbs.KindlingKg),
            ["eat"] = new Eat(P, Resource.RawMeat, 0.1),
            ["drink"] = new Drink(P),
            ["search"] = new WalkAbout(P),
            ["wait"] = new Wait(P),
            ["rest"] = new Rest(P),
            ["sleep"] = new Sleep(P),
        };
    }

    static World Wounded(Action<BodyState> wound, out Dictionary<string, Activity> verbs)
    {
        var w = Scene(out var carcass);
        verbs = TableVerbs(w, carcass);
        wound(w.Agent(P).Body);
        return w;
    }

    static void AssertRefused(World w, Activity a, Faculty faculty, string named)
    {
        var refused = Sim.Refusal(w, a);
        Assert.True(refused != null, $"{a} was offered");
        var missing = Assert.IsType<MissingFaculty>(refused.Missing);
        Assert.Equal(faculty, missing.Faculty);
        Assert.Contains(named, refused.Reason);
    }

    static void AssertNotRefusedByTheBody(World w, Activity a) =>
        Assert.True(Sim.Refusal(w, a)?.Missing is not MissingFaculty, $"{a}: {Sim.Refusal(w, a)}");

    [Fact]
    public void AWholeBody_IsOfferedEveryVerbOfTheTable()
    {
        var w = Wounded(_ => { }, out var verbs);
        foreach (var (name, a) in verbs.Where(v => v.Key is not ("make" or "build")))
            Assert.True(Sim.Refusal(w, a) == null, $"{name}: {Sim.Refusal(w, a)}");
        foreach (var name in new[] { "make", "build" }) AssertNotRefusedByTheBody(w, verbs[name]);
    }

    [Fact]
    public void BothArmsDestroyed_RefusesEveryHandVerb_NamingTheArms()
    {
        var w = Wounded(Armless, out var verbs);
        foreach (var name in new[] { "forage", "fell", "butcher", "make", "build", "fire", "eat", "drink" })
            AssertRefused(w, verbs[name], Faculty.Dexterity, "arm");
        foreach (var name in new[] { "wait", "rest", "sleep", "search" }) Assert.Null(Sim.Refusal(w, verbs[name]));
        Assert.Contains(Verbs.At(w, P).Walk, v => v.Allowed);
    }

    [Fact]
    public void BothLegsDestroyed_RefusesWhatNeedsLegs_NamingTheLegs_AndOffersTheHandVerbs()
    {
        var w = Wounded(Legless, out var verbs);
        foreach (var name in new[] { "forage", "fell", "build", "search" }) AssertRefused(w, verbs[name], Faculty.Speed, "leg");
        foreach (var name in new[] { "butcher", "fire", "eat", "drink", "wait", "rest", "sleep" })
            Assert.True(Sim.Refusal(w, verbs[name]) == null, $"{name}: {Sim.Refusal(w, verbs[name])}");
        AssertNotRefusedByTheBody(w, verbs["make"]);
    }

    [Fact]
    public void OneWorkingArm_HoweverWeak_IsOfferedEveryHandVerb()
    {
        var w = Wounded(b => { SetLayers(b, "Left Arm", 0); SetLayers(b, "Right Arm", 0.01); }, out var verbs);
        Assert.True(Body.Dexterity(w.Agent(P).Body) > 0);
        foreach (var name in new[] { "forage", "fell", "butcher", "fire", "eat", "drink" })
            Assert.True(Sim.Refusal(w, verbs[name]) == null, $"{name}: {Sim.Refusal(w, verbs[name])}");
        foreach (var name in new[] { "make", "build" }) AssertNotRefusedByTheBody(w, verbs[name]);
    }

    /// Eating is gated only: a weak hand never makes it take longer.
    [Fact]
    public void Eating_WithOneWeakArm_TakesTheTimeItTakesAWholeBody()
    {
        int Minutes(Action<BodyState> wound)
        {
            var w = Wounded(wound, out var verbs);
            return Sim.Step(w, verbs["eat"], Verbs.UntilDoneMinutes).MinutesElapsed;
        }
        Assert.Equal(Minutes(_ => { }), Minutes(b => { SetLayers(b, "Left Arm", 0); SetLayers(b, "Right Arm", 0.01); }));
    }

    [Fact]
    public void Unconscious_IsRefusedEveryVerbThatNeedsAFaculty_AndWaitingPassesTime()
    {
        var w = Wounded(KnockedOut, out var verbs);
        Assert.True(Body.IsAlive(w.Agent(P).Body));
        foreach (var (name, a) in verbs.Where(v => v.Key is not ("wait" or "rest" or "sleep")))
        {
            var refused = Sim.Refusal(w, a);
            Assert.True(refused?.Missing is MissingFaculty { Because: var why } && why.Contains(CrippledBy.Unconscious), $"{name}: {refused}");
            Assert.Contains("unconscious", refused!.Reason);
        }
        long before = w.Minute;
        var waited = Sim.Step(w, verbs["wait"], 30);
        Assert.True(waited.MinutesElapsed > 0, waited.InterruptedReason);
        Assert.Equal(before + waited.MinutesElapsed, w.Minute);
    }

    [Fact]
    public void ABodyThatPerceivesNothing_IsRefusedSearching_NamingIt()
    {
        var w = Wounded(BlindAndDeaf, out var verbs);
        Assert.Equal(0, Body.Perception(w.Agent(P).Body));
        foreach (var name in new[] { "search", "forage" }) AssertRefused(w, verbs[name], Faculty.Perception, "blind and deaf");
        foreach (var name in new[] { "fell", "butcher", "eat" }) Assert.Null(Sim.Refusal(w, verbs[name]));
    }

    public static TheoryData<string> Bodies => ["armless", "legless", "unconscious", "blind and deaf", "one weak arm"];

    static Action<BodyState> WoundOf(string body) => body switch
    {
        "armless" => Armless,
        "legless" => Legless,
        "unconscious" => KnockedOut,
        "blind and deaf" => BlindAndDeaf,
        _ => b => { SetLayers(b, "Left Arm", 0); SetLayers(b, "Right Arm", 0.01); }
        ,
    };

    [Theory]
    [MemberData(nameof(Bodies))]
    public void EveryVerb_IsListedRefused_ExactlyWhenSteppingRefusesIt(string body)
    {
        var w = Scene(out _, trees: false);
        WoundOf(body)(w.Agent(P).Body);
        VerbsTests.AssertEveryVerbIsTrue(w, P);
    }

    /// A caribou on the nearest tile with water and grazing: whole, it is offered both; unconscious, neither, and
    /// its mind, choosing by hunger and thirst, has it lie where it is instead of asking for them.
    [Fact]
    public void AWholeAnimal_GrazesAndDrinks_AndAnUnconsciousOneDoesNeither()
    {
        var w = TestWorlds.Bare(17);
        var camp = w.Agent(P).Tile;
        AgentId caribou = default;
        var near = from dx in Enumerable.Range(-20, 41)
                   from dy in Enumerable.Range(-20, 41)
                   let t = new TileId(camp.X + dx, camp.Y + dy)
                   where Map.Of(w).InBounds(t)
                   orderby Math.Abs(dx) + Math.Abs(dy)
                   select t;
        foreach (var tile in near)
        {
            caribou = Agents.Spawn(w, Species.Caribou, tile);
            if (Fauna.Refusal(w, new Graze(caribou)) == null && Fauna.Refusal(w, new Watering(caribou)) == null) break;
            Agents.Remove(w, caribou);
        }
        var animal = w.Agent(caribou);
        KnockedOut(animal.Body);
        foreach (var a in new Activity[] { new Graze(caribou), new Watering(caribou), new Feed(caribou) })
        {
            var refused = Fauna.Refusal(w, a);
            Assert.True(refused?.Missing is MissingFaculty { Faculty: Faculty.Speed }, $"{a}: {refused}");
            Assert.Contains("unconscious", refused!.Reason);
        }
        TestKit.SetHunger(animal, 0.9);
        animal.Body.HydrationMl = 0;
        double kcal = animal.Body.CaloriesKcal;
        for (int i = 0; i < 10 && w.Agents.ContainsKey(caribou); i++)
        {
            Sim.Step(w, new Wait(P), 1);
            Assert.True(w.Agents.TryGetValue(caribou, out var now) && Fauna.MindOf(now).Choice is Rest, $"{Fauna.MindOf(animal).Choice}");
        }
        Assert.True(animal.Body.CaloriesKcal <= kcal);
        Assert.Equal(0, animal.Body.HydrationMl);
    }

    public static TheoryData<string> Wounds => ["armless", "legless", "unconscious", "blind and deaf"];

    /// The refusals are the body's, not the player's: an NPC beside them with the same wounds is refused the same verbs, in the same words.
    [Theory]
    [MemberData(nameof(Wounds))]
    public void AnNpcWithTheSameWounds_IsRefusedTheSameVerbs(string wound)
    {
        var w = Wounded(WoundOf(wound), out var verbs);
        var npc = Npc.Spawn(w, w.Agent(P).Tile);
        WoundOf(wound)(w.Agent(npc).Body);
        foreach (var (name, a) in verbs)
        {
            var mine = Sim.Refusal(w, a);
            var theirs = Sim.Refusal(w, a with { Who = npc });
            Assert.True(mine?.Missing is MissingFaculty == theirs?.Missing is MissingFaculty, $"{name}: mine '{mine}', theirs '{theirs}'");
            if (mine?.Missing is MissingFaculty) Assert.Equal(mine.Reason, theirs!.Reason);
        }
    }

    /// The legs gone two tiles from camp: it does what it can where it lies, and plans nothing that needs a walk.
    [Fact]
    public void AnNpcWithBothLegsDestroyed_AwayFromCamp_PlansOnlyOnItsOwnTile()
    {
        var w = TestWorlds.Bare(17);
        var camp = w.Agent(P).Tile;
        var away = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        var npc = Npc.Spawn(w, away, camp);
        Legless(w.Agent(npc).Body);
        var events = new List<WorldEvent>();
        for (int minute = 0; minute < 6 * 60; minute++)
        {
            events.AddRange(Sim.Step(w, new Wait(P), 1).Events);
            var me = w.Agent(npc);
            Assert.Equal(away, me.Tile);
            Assert.True(me.Mind is NpcMind { Plan: null } || ((NpcMind)me.Mind).Plan!.At is null || ((NpcMind)me.Mind).Plan!.At == away,
                $"minute {minute}: plans at {((NpcMind)me.Mind).Plan?.At}");
            Assert.True(me.Doing?.Activity is not Travel, $"minute {minute}: walking");
        }
        Assert.DoesNotContain(events.OfType<PlanRefused>(), e => e.Who == npc && e.Activity == nameof(Travel));
    }
}
