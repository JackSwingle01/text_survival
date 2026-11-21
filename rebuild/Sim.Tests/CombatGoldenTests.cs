using static Survival.Tests.CombatTests;

namespace Survival.Tests;

/// Outputs of the old combat code, captured by a harness that ran text_survival.csproj on the same
/// inputs (animals with SizeModifier normalised to 1, humans with no armor, tile visibility 1).
/// Exact where the old code is deterministic; statistical, with tolerances, where it rolls.
/// Old numbers that went through the "unit is its own ally" bug were taken with that bug patched
/// out in the harness (each unit removed from its own allies list), since the port fixes it.
public class CombatGoldenTests
{
    const int Digits = 9;

    // ---- formulas (old CombatFormulas, exact) ----

    [Theory]
    [InlineData(10, Awareness.Unaware, 0.928901697685371)]
    [InlineData(30, Awareness.Unaware, 0.779055912670449)]
    [InlineData(50, Awareness.Unaware, 0.6155722066724582)]
    [InlineData(50, Awareness.Alert, 0.9233583100086873)]
    [InlineData(80, Awareness.Unaware, 0.32413131933855244)]
    [InlineData(80, Awareness.Alert, 0.48619697900782866)]
    [InlineData(80, Awareness.Engaged, 0.6482626386771049)]
    [InlineData(5, Awareness.Unaware, 0.95)]
    public void DetectionChance_MatchesOld(double distance, Awareness awareness, double expected) =>
        Assert.Equal(expected, Combat.DetectionChance(distance, awareness), Digits);

    [Fact]
    public void StepDetectionAndThrowing_MatchOld()
    {
        Assert.Equal(0.0987495373891698, Combat.StepDetectionChance(0.5, 3), Digits);
        Assert.Equal(0.5783034965714178, Combat.StepDetectionChance(0.9, 7.5), Digits);
        Assert.Equal(0, Combat.StepDetectionChance(0.9, 0));
        Assert.Equal(0.595, Combat.ThrownAccuracy(3, 20, 0.70, false), Digits);
        Assert.Equal(0.3927, Combat.ThrownAccuracy(3, 20, 0.70, true), Digits);
        Assert.Equal(0.72, Combat.ThrownAccuracy(3, 15, 0.90, false), Digits);
        Assert.Equal(0.35, Combat.ThrownAccuracy(10, 20, 0.70, false), Digits);
        Assert.Equal(0.07, Combat.ThrownAccuracy(18, 20, 0.70, false), Digits);
        Assert.Equal(0.05, Combat.ThrownAccuracy(18, 20, 0.70, true), Digits);
        Assert.Equal(0, Combat.ThrownAccuracy(22, 20, 0.70, false));
        Assert.Equal(Zone.Close, Combat.ZoneOf(1));
        Assert.Equal(Zone.Near, Combat.ZoneOf(3));
        Assert.Equal(Zone.Mid, Combat.ZoneOf(15));
        Assert.Equal(Zone.Far, Combat.ZoneOf(15.01));
    }

    // ---- species bodies (exact) ----

    // weight kg, strength, speed, vitality, combat allowance m, average blow (before luck)
    // Species round (2026-11-21): realistic builds (species-stats-research.md), the 68 kg man, Homotherium for the
    // sabre-tooth. Old (the old game's AnimalFactory bodies) → new, weight / strength / speed / blow:
    //   Wolf 40 / 0.950 / 1.605 / 0.439 → 45 / 0.895 / 1.210 / 0.426; Bear 250 / 1.245 / 1.395 / 0.673 → 250 / 1.082 / 1.010 / 0.625
    //   CaveBear 350 / 1.400 / 1.392 / 0.960 → 400 / 1.249 / 0.981 / 0.900; SaberTooth 300 / 1.492 / 1.678 / 1.122 → Homotherium 190 / 1.083 / 1.129 / 0.937
    //   Hyena 70 / 1.028 / 1.684 / 0.406 → 85 / 0.947 / 1.183 / 0.389; Caribou 120 / 1.080 / 1.524 / 0.260 → 110 / 0.972 / 1.121 / 0.247
    //   Megaloceros 600 / 1.864 / 1.481 / 0.716 → 550 / 1.498 / 1.092 / 0.625; Bison 800 / 2.069 / 1.336 / 0.844 → 600 / 1.559 / 1.092 / 0.704
    //   Mammoth 6000 / 9.222 / 0.624 / 6.133 → 5000 / 6.244 / 1.227 / 4.347; Rabbit 2 / 0.847 / 1.415 / 0.018 → 3 / 0.848 / 1.303 / 0.018
    //   Fox 6 / 0.873 / 1.586 / 0.112 → 6 / 0.831 / 1.232 / 0.110; Human 75 / 0.977 / 0.849 / 0.148 → 68 / 1.068 / 0.992 / 0.155
    // Speed's kilogram terms are a person's (body review B11): the mammoth's doubles. Combat reads speed only as a
    // share of a fresh body of its kind (SpeciesTable.Running), so the allowances do not move.
    public static readonly TheoryData<Species, double, double, double, double, int, double> SpeciesBodies = new()
    {
        { Species.Wolf, 45, 0.8947715999999999, 1.2096121549930419, 1, 6, 0.42632360999999996 },
        { Species.Bear, 250, 1.08205, 1.0099605245894718, 1, 5, 0.6246149999999999 },
        { Species.CaveBear, 400, 1.2488800000000002, 0.9813297123518419, 1, 4, 0.8995520000000001 },
        { Species.Homotherium, 190, 1.08302016, 1.1286958123788615, 1, 7, 0.9373590720000001 },
        { Species.Hyena, 85, 0.9468708, 1.182606086953345, 1, 6, 0.38937416 },
        { Species.Caribou, 110, 0.9721110399999999, 1.1214800761239843, 1, 3, 0.24651388 },
        { Species.Megaloceros, 550, 1.4980128000000001, 1.0920166381655916, 1, 3, 0.6245032 },
        { Species.Bison, 600, 1.5592776000000002, 1.0922935825680593, 1, 3, 0.70380134 },
        { Species.Mammoth, 5000, 6.2444, 1.2274532803110698, 1, 3, 4.34664 },
        { Species.Rabbit, 3, 0.8482441199999999, 1.3033011900540226, 1, 3, 0.0184824412 },
        { Species.Fox, 6, 0.83143248, 1.2321723978837684, 1, 3, 0.10988594880000001 },
        // Old Player: unarmed 0.15.
        { Species.Human, 68, 1.0677653333333335, 0.9917410466689817, 1, 3, 0.1550824 },
    };

    [Theory, MemberData(nameof(SpeciesBodies))]
    public void SpeciesBodies_AreAsMeasured(Species species, double weightKg, double strength, double speed, double vitality, int allowance, double blow)
    {
        var (w, t) = Field();
        var id = Herd(w, species, 1, t);
        Normalize(w, id);
        var e = Combat.Create(w, [new(AgentId.Player)], [new(id)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var unit = e.Units[1];
        var body = Combat.BodyOf(w, unit);
        Assert.Equal(weightKg, body.WeightKg, Digits);
        Assert.Equal(strength, Body.Strength(body), Digits);
        Assert.Equal(speed, Body.Speed(body), Digits);
        Assert.Equal(vitality, Body.Vitality(body), Digits);
        Assert.Equal(allowance, Combat.Allowance(w, unit));
        Assert.Equal(blow, Combat.AverageBlow(w, unit).Amount, Digits);
    }

    [Fact]
    public void ASpearedBlow_MatchesOld()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        var e = Combat.Create(w, [new(AgentId.Player) { Weapon = CombatTests.Spear(w) }], [new(wolf)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var (amount, type) = Combat.AverageBlow(w, e.Units[0]);
        Assert.Equal(8.271061333333334, amount, Digits); // the 68 kg man; the old 75 kg player's was 7.909
        Assert.Equal(DamageType.Pierce, type);
    }

    // ---- morale and steering (old Unit, exact; self-as-ally patched out) ----

    [Fact]
    public void LoneWolfAndHuman_MatchOld()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        Normalize(w, wolf);
        var e = Combat.Create(w, [new(AgentId.Player)], [new(wolf)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var (me, it) = (e.Units[0], e.Units[1]);
        Assert.Equal(new Cell(23, 8), me.Position);
        Assert.Equal(new Cell(23, 13), it.Position);
        Assert.Equal(2.3, Combat.UnitBoldness(w, e, it), Digits);
        Assert.Equal(2.0, Combat.UnitBoldness(w, e, me), Digits);
        Assert.Equal(0.7, Combat.Threat(w, it), Digits);
        Assert.Equal(0.6499999999999999, Combat.PerceivedThreat(w, it, me), Digits);
        Assert.Equal(0.4549999999999999, Combat.PerceivedThreat(w, me, it), Digits);
        Assert.True(Combat.WantsToAttack(w, e, it, me));
        Assert.True(Combat.WantsToAttack(w, e, me, it));
        AssertVector(0, -5.55, Combat.MovementVector(w, e, it));
        AssertVector(0, 5.2349997, Combat.MovementVector(w, e, me));
    }

    [Fact]
    public void PackOfThree_MatchesOld()
    {
        var (w, t) = Field();
        var pack = Herd(w, Species.Wolf, 3, t);
        Normalize(w, pack);
        var e = Combat.Create(w, [new(AgentId.Player)], [new(pack, 0), new(pack, 1), new(pack, 2)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var wolves = e.Units.Skip(1).ToList();
        Assert.Equal([new Cell(23, 13), new Cell(25, 13), new Cell(27, 13)], wolves.Select(u => u.Position));
        Assert.All(wolves, u => Assert.Equal(2.7199999999999998, Combat.UnitBoldness(w, e, u), Digits));
        AssertVector(6, -6.8100004, Combat.MovementVector(w, e, wolves[0]));
        AssertVector(-2.6021242, -6.505311, Combat.MovementVector(w, e, wolves[1]));
        AssertVector(-10.936438, -6.1705465, Combat.MovementVector(w, e, wolves[2]));
    }

    // boldness, movement vector, wants to attack, where it moves (as Combat.MovePosition)
    public static readonly TheoryData<Species, double, double, double, bool, int, int> LoneAnimals = new()
    {
        { Species.Bear, 2.6, 0, -6.45, true, 23, 9 },
        { Species.Caribou, 0.6, 0, -1.5, false, 23, 16 }, // skittish: bolts its full 3 m
        { Species.Bison, 1.0, 0, -1.5975, true, 23, 10 },
        { Species.Mammoth, 2.0, 0, -3.5475001, true, 23, 10 },
    };

    [Theory, MemberData(nameof(LoneAnimals))]
    public void LoneAnimals_MatchOld(Species species, double boldness, double vx, double vy, bool attacks, int toX, int toY)
    {
        var (w, t) = Field();
        var id = Herd(w, species, 1, t);
        Normalize(w, id);
        var e = Combat.Create(w, [new(AgentId.Player)], [new(id)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var it = e.Units[1];
        Assert.Equal(boldness, Combat.UnitBoldness(w, e, it), Digits);
        AssertVector(vx, vy, Combat.MovementVector(w, e, it));
        Assert.Equal(attacks, Combat.WantsToAttack(w, e, it, e.Units[0]));
        Assert.Equal(new Cell(toX, toY), Combat.MovePosition(w, e, it));
    }

    [Fact]
    public void HurtWolf_MatchesOld()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        Normalize(w, wolf);
        var body = w.Agent(wolf).Body;
        body.Regions.First(r => r.Name.Contains("Leg")).Muscle.Condition = 0.5;
        body.BloodPct = 0.8;
        Effects.Add(body, EffectKind.Bleeding, 0.4);
        var e = Combat.Create(w, [new(AgentId.Player)], [new(wolf)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var it = e.Units[1];
        Assert.Equal(0.6000000000000001, Body.Vitality(body), Digits);
        Assert.Equal(1.0499999999999998, Combat.UnitBoldness(w, e, it), Digits);
        Assert.Equal(3, Combat.Allowance(w, it));
        AssertVector(0, -1.8, Combat.MovementVector(w, e, it));
        Assert.True(Combat.WantsToAttack(w, e, it, e.Units[0]));
    }

    static void AssertVector(double x, double y, System.Numerics.Vector2 v)
    {
        Assert.Equal(x, v.X, 4);
        Assert.Equal(y, v.Y, 4);
    }

    // ---- shove, dodge, block, intimidate ----

    [Fact]
    public void ShoveAndDodge_MatchOld()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        var bear = Herd(w, Species.Bear, 1, t);
        Normalize(w, wolf);
        Normalize(w, bear);
        var (h, wb, bb) = (w.Agent(AgentId.Player).Body, w.Agent(wolf).Body, w.Agent(bear).Body);
        // Species round bodies. Old → new: shoves 0.964 / 4.932, 0.259 / 3.478, 2.123 / 6.881 → 0.902 / 4.898,
        // 0.277 / 3.338, 1.863 / 6.703; dodges 0.510, 0.963, 0.837 → 0.595, 0.726, 0.606.
        AssertShove(Combat.ShoveOdds(h, wb), 0.901633477894951, 4.897787964362452);
        AssertShove(Combat.ShoveOdds(wb, h), 0.2772745313136292, 3.3377352062669563);
        AssertShove(Combat.ShoveOdds(bb, h), 1.8628273815470062, 6.703226779358436);
        Assert.Equal(0.595044628001389, Combat.DodgeChance(h), Digits);
        Assert.Equal(0.7257672929958251, Combat.DodgeChance(wb), Digits);
        Assert.Equal(0.6059763147536831, Combat.DodgeChance(bb), Digits);
    }

    static void AssertShove((double Chance, double PushM) odds, double chance, double push)
    {
        Assert.Equal(chance, odds.Chance, Digits);
        Assert.Equal(push, odds.PushM, Digits);
    }

    [Fact]
    public void Intimidation_MatchesOld()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        var e = Combat.Create(w, [new(AgentId.Player) { Weapon = CombatTests.Spear(w) }], [new(wolf)], t, 10, Awareness.Engaged, Awareness.Engaged);
        Assert.Equal(0.7, Combat.IntimidationChance(w, e.Units[1], e.Units[0]), Digits);
        // Old harness: mean boldness drop 0.2251 over 20000 shouts (0.3 on success, 0.05 on failure).
        double drop = 0;
        for (int i = 0; i < 20000; i++)
        {
            double before = e.Units[1].BoldnessModifier;
            Combat.Apply(w, e, e.Units[0], CombatAction.Intimidate, []);
            drop += before - e.Units[1].BoldnessModifier;
        }
        // Within 0.003 (about 3.5 standard errors), not rounded to two places: the mean is 0.225, on
        // the rounding boundary, so a two-place check passed or failed on which way the stream fell.
        Assert.Equal(0.2251, drop / 20000, 0.003);
    }

    [Fact]
    public void BlocksAndBlows_MatchOldOdds()
    {
        var rng = Rng.ForSystem(1, "test");
        var bare = new CombatUnit { Agent = AgentId.Player, Member = 0, Team = 0 };
        var armed = new CombatUnit { Agent = AgentId.Player, Member = 0, Team = 0, Weapon = Items.Make("wooden_spear") };
        double bareMean = Enumerable.Range(0, 20000).Average(_ => Combat.ResolveBlock(bare, 1.0, rng));
        double armedMean = Enumerable.Range(0, 20000).Average(_ => Combat.ResolveBlock(armed, 1.0, rng));
        Assert.Equal(0.725, bareMean, 2); // uniform 0.5-0.95
        Assert.Equal(0.35, armedMean, 2); // uniform 0-0.7

        // A melee attack lands 90% of the time against a target that is not dodging.
        var (w, t) = Field();
        int hits = 0, tries = 2000;
        for (int i = 0; i < tries; i++)
        {
            var bison = Herd(w, Species.Bison, 1, t);
            var e = Combat.Create(w, [new(AgentId.Player)], [new(bison)], t, 1, Awareness.Engaged, Awareness.Engaged);
            if (Combat.Apply(w, e, e.Units[0], CombatAction.Attack, []).Hit) hits++;
            w.Encounters.Clear();
            w.Agents.Remove(bison);
        }
        Assert.InRange(hits / (double)tries, 0.88, 0.92);
    }

    // ---- herd boldness (old Herd.BoldnessToward at 09:00, exact) ----

    // members, toward a healthy person, defending, toward a bleeding person
    public static readonly TheoryData<Species, int, double, double, double> HerdBoldness = new()
    {
        { Species.Wolf, 1, 0.25, 0.85, 0.4 },
        { Species.Wolf, 3, 0.35000000000000003, 0.9, 0.5 },
        { Species.Wolf, 6, 0.5, 0.9, 0.65 },
        { Species.Bear, 1, 0.15, 0.7000000000000001, 0.3 },
        { Species.Hyena, 4, 0.42000000000000004, 0.5700000000000001, 0.5700000000000001 },
        { Species.Homotherium, 1, 0.25, 0.75, 0.4 },
        { Species.Caribou, 5, 0, 0, 0 },
    };

    [Theory, MemberData(nameof(HerdBoldness))]
    public void HerdBoldness_MatchesOld(Species species, int count, double healthy, double defending, double bleeding)
    {
        var (w, t) = Field();
        var herd = Herd(w, species, count, t);
        var person = TestKit.OldBaselineHuman();
        var hurt = TestKit.OldBaselineHuman();
        Effects.Add(hurt, EffectKind.Bleeding, 0.5);
        Assert.Equal(healthy, Combat.Boldness(w, herd, person), Digits);
        Assert.Equal(defending, Combat.Boldness(w, herd, person, defending: true), Digits);
        Assert.Equal(bleeding, Combat.Boldness(w, herd, hurt), Digits);
    }

    [Fact]
    public void HerdBoldness_NightHungerAndFear()
    {
        var (w, t) = Field();
        var herd = Herd(w, Species.Wolf, 1, t);
        var person = TestKit.OldBaselineHuman();
        Assert.Equal(0.45, Combat.Boldness(w, herd, person, hunger: 0.8), Digits);
        Assert.Equal(0.65, Combat.Boldness(w, herd, person, hunger: 0.95), Digits);
        Assert.Equal(0.125, Combat.Boldness(w, herd, person, fear: 0.5), Digits);
        w.Minute = 13 * 60; // 22:00
        Assert.Equal(0.35, Combat.Boldness(w, herd, person), Digits);
    }

    // ---- headless outcomes (old CombatScenario.ResolveHeadless, statistical) ----

    // Old harness, 2000 fights each, unarmored human (old Player: unarmed 0.15), self-as-ally patched out:
    // Victory, Defeat, Fled, AnimalFled, AnimalDisengaged rates and mean rounds.
    // rebuild: body fixes A2/A4/B5 (2026-11-21); combat is being redesigned, so old-game parity is not a target.
    // The three rows marked are this rebuild's own numbers, 1000 fights each. Toggling the fixes one at a time,
    // A2 moves them (a region works only as far as its weakest layer, so a bitten leg no longer runs as well
    // and fewer fighters get away); A4 adds a few victories (a spear can reach a head's brain); B5 changes
    // nothing measurable. Old (the old game's) → new:
    //   wolf vs spear   [0.082, 0.388, 0.213, 0.269, 0.049, 11.24] → [0.140, 0.411, 0.145, 0.242, 0.062, 11.53]
    //   wolf vs unarmed [0.007, 0.797, 0.171, 0.000, 0.025, 11.05] → [0.005, 0.861, 0.105, 0.000, 0.029, 11.49]
    //   bear vs spear   [0.032, 0.720, 0.176, 0.063, 0.009, 9.48]  → [0.073, 0.762, 0.112, 0.031, 0.022, 9.72]
    // Species round (2026-11-21): realistic builds, the sprint reserve, dodges paid from it. Every row is now this
    // rebuild's, 1000 fights each. The wolf, at a realistic 48% muscle, is slower by Body.Speed (1.60 → 1.21), so it
    // dodges a spear less (0.96 → 0.73) and stays to die instead of breaking off. Old → new:
    //   wolf vs spear       [0.140, 0.411, 0.145, 0.242, 0.062, 11.53] → [0.333, 0.421, 0.116, 0.057, 0.073, 11.54]
    //   wolf vs unarmed     [0.005, 0.861, 0.105, 0.000, 0.029, 11.49] → [0.007, 0.876, 0.083, 0.000, 0.034, 11.80]
    //   3 wolves vs spear   [0.000, 1.000, 0.000, 0.001, 0.000, 4.95]  → [0.000, 0.999, 0.000, 0.000, 0.001, 5.18]
    //   4 wolves ambush 2   [1.000, 0.000, 0.000, 0.000, 0.000, 4.24]  → [1.000, 0.000, 0.000, 0.000, 0.000, 4.30]
    //   4 wolves noticed    [0.949, 0.000, 0.000, 0.051, 0.000, 8.14]  → [0.955, 0.000, 0.000, 0.045, 0.000, 8.37]
    //   bear vs spear       [0.073, 0.762, 0.112, 0.031, 0.022, 9.72]  → [0.101, 0.778, 0.090, 0.011, 0.020, 9.94]
    public static readonly TheoryData<string, double[]> Headless = new()
    {
        { "wolf vs spear", [0.333, 0.421, 0.116, 0.057, 0.073, 11.54] },
        { "wolf vs unarmed", [0.007, 0.876, 0.083, 0.000, 0.034, 11.80] },
        { "3 wolves vs spear", [0.000, 0.999, 0.000, 0.000, 0.001, 5.18] },
        { "4 wolves ambush 2 caribou", [1.000, 0.000, 0.000, 0.000, 0.000, 4.30] },
        { "4 wolves noticed by 2 caribou", [0.955, 0.000, 0.000, 0.045, 0.000, 8.37] },
        { "bear vs spear", [0.101, 0.778, 0.090, 0.011, 0.020, 9.94] },
    };

    [Trait("Category", TestCategories.Slow)]
    [Theory, MemberData(nameof(Headless))]
    public void HeadlessOutcomes_MatchOldStatistically(string scenario, double[] old)
    {
        const int fights = 1000;
        var (w, t) = Field(11);
        var counts = new int[5];
        double rounds = 0;
        for (int i = 0; i < fights; i++)
        {
            List<Fighter> Team(Species s, int n, bool spear = false)
            {
                var id = Herd(w, s, n, t);
                if (s == Species.Human) foreach (var m in w.Agent(id).Members) m.Worn.Clear();
                Inventory? pack = null;
                if (s == Species.Human)
                {
                    // People, not a herd: a herd mends as its rounds bleed it, and old people did not.
                    // A spear is wielded out of a pack, so these people carry one.
                    var herd = w.Agent(id);
                    pack = new Inventory { Gear = spear ? [.. Enumerable.Range(0, n).Select(_ => Items.Make("wooden_spear"))] : [] };
                    w.Agents[id] = new Agent { Id = id, Tile = herd.Tile, Species = herd.Species, Members = herd.Members, Mind = new PlayerMind { Pack = pack } };
                }
                return [.. Enumerable.Range(0, n).Select(k => new Fighter(id, k) { Weapon = spear ? pack!.Gear[k] : null })];
            }
            var (a, b, d, aa, ba) = scenario switch
            {
                "wolf vs spear" => (Team(Species.Human, 1, true), Team(Species.Wolf, 1), 5, Awareness.Engaged, Awareness.Engaged),
                "wolf vs unarmed" => (Team(Species.Human, 1), Team(Species.Wolf, 1), 5, Awareness.Engaged, Awareness.Engaged),
                "3 wolves vs spear" => (Team(Species.Human, 1, true), Team(Species.Wolf, 3), 5, Awareness.Engaged, Awareness.Engaged),
                "4 wolves ambush 2 caribou" => (Team(Species.Wolf, 4), Team(Species.Caribou, 2), 5, Awareness.Engaged, Awareness.Unaware),
                "4 wolves noticed by 2 caribou" => (Team(Species.Wolf, 4), Team(Species.Caribou, 2), 15, Awareness.Engaged, Awareness.Alert),
                "bear vs spear" => (Team(Species.Human, 1, true), Team(Species.Bear, 1), 5, Awareness.Engaged, Awareness.Engaged),
                _ => throw new ArgumentException(scenario),
            };
            var e = Combat.Create(w, a, b, t, d, aa, ba);
            counts[(int)Combat.ResolveHeadless(w, e, [])]++;
            rounds += e.Rounds;
            foreach (var id in a.Concat(b).Select(f => f.Agent).Distinct()) w.Agents.Remove(id);
            w.Tiles.Remove(t);
        }
        for (int r = 0; r < 5; r++)
            Assert.True(Math.Abs(counts[r] / (double)fights - old[r]) <= 0.06,
                $"{scenario}: {(CombatResult)r} {counts[r] / (double)fights:F3}, old {old[r]:F3}");
        Assert.True(Math.Abs(rounds / fights - old[5]) <= 1.0, $"{scenario}: {rounds / fights:F2} rounds, old {old[5]:F2}");
    }
}
