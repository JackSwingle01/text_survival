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

    // ---- species (old AnimalFactory bodies, exact) ----

    // weight kg, strength, speed, vitality, combat allowance m, average blow (before luck)
    public static readonly TheoryData<Species, double, double, double, double, int, double> SpeciesBodies = new()
    {
        { Species.Wolf, 40, 0.9500064, 1.6046454955963703, 1, 6, 0.43875144 },
        { Species.Bear, 250, 1.24486, 1.3952516265968218, 1, 5, 0.673458 },
        { Species.CaveBear, 350, 1.3998979999999999, 1.3920369107804034, 1, 4, 0.9599592 },
        { Species.SaberTooth, 300.00000000000006, 1.4922240000000002, 1.6777226530718914, 1, 7, 1.1215008000000002 },
        { Species.Hyena, 70, 1.0281284, 1.6837407993867033, 1, 6, 0.40562568 },
        { Species.Caribou, 120, 1.0802544, 1.523673389593941, 1, 3, 0.26003180000000004 },
        { Species.Megaloceros, 600, 1.8636719999999998, 1.4808537256011423, 1, 3, 0.7159179999999999 },
        { Species.Bison, 800, 2.069228, 1.336430935458896, 1, 3, 0.8440377 },
        { Species.Mammoth, 6000, 9.22188, 0.6238583406370034, 1, 3, 6.133128 },
        { Species.Rabbit, 2, 0.84691216, 1.4145840534090237, 1, 3, 0.0184691216 },
        { Species.Fox, 6, 0.8733369599999999, 1.5856874045352152, 1, 3, 0.1124002176 },
        // Old Player: unarmed 0.15.
        { Species.Human, 75, 0.9772499999999998, 0.8491860359604892, 1, 3, 0.15 * (0.9772499999999998 / 2 + 0.5) },
    };

    [Theory, MemberData(nameof(SpeciesBodies))]
    public void SpeciesBodies_MatchOld(Species species, double weightKg, double strength, double speed, double vitality, int allowance, double blow)
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
        Assert.Equal(7.908999999999999, amount, Digits);
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
        AssertShove(Combat.ShoveOdds(h, wb), 0.9643849504592806, 4.932354560979799);
        AssertShove(Combat.ShoveOdds(wb, h), 0.2592325812228192, 3.4775776925044775);
        AssertShove(Combat.ShoveOdds(bb, h), 2.1230664279014246, 6.881013046815043);
        Assert.Equal(0.5095116215762935, Combat.DodgeChance(h), Digits);
        Assert.Equal(0.9627872973578226, Combat.DodgeChance(wb), Digits);
        Assert.Equal(0.8371509759580931, Combat.DodgeChance(bb), Digits);
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
        { Species.SaberTooth, 1, 0.25, 0.75, 0.4 },
        { Species.Caribou, 5, 0, 0, 0 },
    };

    [Theory, MemberData(nameof(HerdBoldness))]
    public void HerdBoldness_MatchesOld(Species species, int count, double healthy, double defending, double bleeding)
    {
        var (w, t) = Field();
        var herd = Herd(w, species, count, t);
        var person = BodyState.BaselineHuman();
        var hurt = BodyState.BaselineHuman();
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
        var person = BodyState.BaselineHuman();
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
    public static readonly TheoryData<string, double[]> Headless = new()
    {
        { "wolf vs spear", [0.140, 0.411, 0.145, 0.242, 0.062, 11.53] }, // rebuild, body fixes
        { "wolf vs unarmed", [0.005, 0.861, 0.105, 0.000, 0.029, 11.49] }, // rebuild, body fixes
        { "3 wolves vs spear", [0.000, 1.000, 0.000, 0.001, 0.000, 4.95] },
        { "4 wolves ambush 2 caribou", [1.000, 0.000, 0.000, 0.000, 0.000, 4.24] },
        { "4 wolves noticed by 2 caribou", [0.949, 0.000, 0.000, 0.051, 0.000, 8.14] },
        { "bear vs spear", [0.073, 0.762, 0.112, 0.031, 0.022, 9.72] }, // rebuild, body fixes
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
