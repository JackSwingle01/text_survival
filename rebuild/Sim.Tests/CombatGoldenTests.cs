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
    // The blow column moved to the damage unit (decision 2026-11-21-damage-is-measured-in-tissue-condition):
    //   Wolf 0.45 → 1.86, Bear 0.6 → 3.22, CaveBear 0.8 → 2.1, SaberTooth 0.9 → 2.4, Hyena 0.4 → 0.9, Caribou 0.25 → 0.6,
    //   Megaloceros 0.5 → 1.2, Bison 0.55 → 1.5, Mammoth 1.2 → 3.5, Hare 0.02 → 0.05, Fox 0.12 → 0.25, Human 0.15 → 0.05.
    // Species round (2026-11-21): realistic builds (species-stats-research.md), the 68 kg man, SaberTooth for the
    // sabre-tooth. Old (the old game's AnimalFactory bodies) → new, weight / strength / speed / blow:
    //   Wolf 40 / 0.950 / 1.605 / 0.439 → 45 / 0.895 / 1.210 / 0.426; Bear 250 / 1.245 / 1.395 / 0.673 → 250 / 1.082 / 1.010 / 0.625
    //   CaveBear 350 / 1.400 / 1.392 / 0.960 → 400 / 1.249 / 0.981 / 0.900; SaberTooth 300 / 1.492 / 1.678 / 1.122 → SaberTooth 190 / 1.083 / 1.129 / 0.937
    //   Hyena 70 / 1.028 / 1.684 / 0.406 → 85 / 0.947 / 1.183 / 0.389; Caribou 120 / 1.080 / 1.524 / 0.260 → 110 / 0.972 / 1.121 / 0.247
    //   Megaloceros 600 / 1.864 / 1.481 / 0.716 → 550 / 1.498 / 1.092 / 0.625; Bison 800 / 2.069 / 1.336 / 0.844 → 600 / 1.559 / 1.092 / 0.704
    //   Mammoth 6000 / 9.222 / 0.624 / 6.133 → 5000 / 6.244 / 1.227 / 4.347; Hare 2 / 0.847 / 1.415 / 0.018 → 3 / 0.848 / 1.303 / 0.018
    //   Fox 6 / 0.873 / 1.586 / 0.112 → 6 / 0.831 / 1.232 / 0.110; Human 75 / 0.977 / 0.849 / 0.148 → 68 / 1.068 / 0.992 / 0.155
    // Speed's kilogram terms are a person's (body review B11): the mammoth's doubles. Combat reads speed only as a
    // share of a fresh body of its kind (SpeciesTable.Running), so the allowances do not move.
    public static readonly TheoryData<Species, double, double, double, double, int, double> SpeciesBodies = new()
    {
        { Species.Wolf, 45, 0.8947715999999999, 1.2096121549930419, 1, 6, 1.86 },
        { Species.Bear, 250, 1.08205, 1.0099605245894718, 1, 5, 3.22 },
        { Species.CaveBear, 400, 1.2488800000000002, 0.9813297123518419, 1, 4, 2.1 },
        { Species.SaberTooth, 190, 1.08302016, 1.1286958123788615, 1, 7, 2.4 },
        { Species.Hyena, 85, 0.9468708, 1.182606086953345, 1, 6, 0.9 },
        { Species.Caribou, 110, 0.9721110399999999, 1.1214800761239843, 1, 3, 0.6 },
        { Species.Megaloceros, 550, 1.4980128000000001, 1.0920166381655916, 1, 3, 1.2 },
        { Species.Bison, 600, 1.5592776000000002, 1.0922935825680593, 1, 3, 1.5 },
        { Species.Mammoth, 5000, 6.2444, 1.2274532803110698, 1, 3, 3.5 },
        { Species.Hare, 3, 0.8482441199999999, 1.3033011900540226, 1, 3, 0.05 },
        { Species.Fox, 6, 0.83143248, 1.2321723978837684, 1, 3, 0.25 },
        // Each blow is its table row: strength counts against an average member of the kind.
        { Species.Human, 68, 1.0677653333333335, 0.9917410466689817, 1, 3, 0.05 },
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
    public void ASpearedBlow_IsTheTables()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        var e = Combat.Create(w, [new(AgentId.Player) { Weapon = CombatTests.Spear(w) }], [new(wolf)], t, 5, Awareness.Engaged, Awareness.Engaged);
        var (amount, type) = Combat.AverageBlow(w, e.Units[0]);
        Assert.Equal(4.84, amount, Digits); // the wooden spear's row, in the reference man's hands (8.271 before the damage unit)
        Assert.Equal(DamageType.Pierce, type);
    }

    // The old Unit's steering goldens (morale vectors, WantsToAttack, PursuitTarget, MovePosition) went with the
    // steering itself: the fighter state machine replaced it (combat round 1). Its bands are pinned in
    // CombatStateTests.

    // ---- shove, blows, intimidate ----

    /// Shoving reads weight and how well each body moves now (Running.SpeedPct), not Body.Strength (B11).
    [Fact]
    public void ShoveOdds_AreWeightAndFitness()
    {
        var (w, t) = Field();
        var wolf = Herd(w, Species.Wolf, 1, t);
        var bear = Herd(w, Species.Bear, 1, t);
        Normalize(w, wolf);
        Normalize(w, bear);
        var (h, wb, bb) = (w.Agent(AgentId.Player).Body, w.Agent(wolf).Body, w.Agent(bear).Body);
        Running R(Species s, BodyState b) => SpeciesTable.Running(s, b);
        // 68 kg against 45 kg, all fresh: a ratio of 1.511.
        AssertShove(Combat.ShoveOdds(h, R(Species.Human, h), wb, R(Species.Wolf, wb)), 0.5 * 68 / 45.0, 1 + 3 * 68 / 45.0);
        AssertShove(Combat.ShoveOdds(wb, R(Species.Wolf, wb), h, R(Species.Human, h)), 0.5 * 45 / 68.0, 1 + 3 * 45 / 68.0);
        AssertShove(Combat.ShoveOdds(bb, R(Species.Bear, bb), h, R(Species.Human, h)), 0.95, 10); // capped
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
        // No weapon term: animals do not fear weapons (round 2). Old: 0.7 with the spear.
        Assert.Equal(0.5, Combat.IntimidationChance(w, e.Units[1], e.Units[0]), Digits);
        // A mean drop of 0.5 x 0.3 + 0.5 x 0.05 = 0.175 (old harness, at 0.7: 0.2251).
        double drop = 0;
        for (int i = 0; i < 20000; i++)
        {
            double before = e.Units[1].BoldnessModifier;
            Combat.Apply(w, e, e.Units[0], CombatAction.Intimidate, []);
            drop += before - e.Units[1].BoldnessModifier;
        }
        // Within 0.003 (about 3.5 standard errors), not rounded to two places: the mean is 0.225, on
        // the rounding boundary, so a two-place check passed or failed on which way the stream fell.
        Assert.Equal(0.175, drop / 20000, 0.003);
    }

    [Fact]
    public void Blows_MatchOldOdds()
    {
        // A melee attack lands 90% of the time against a target that does not defend itself (an unaware one).
        var (w, t) = Field();
        int hits = 0, tries = 2000;
        for (int i = 0; i < tries; i++)
        {
            var bison = Herd(w, Species.Bison, 1, t);
            var e = Combat.Create(w, [new(AgentId.Player)], [new(bison)], t, 1, Awareness.Engaged, Awareness.Unaware);
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
}
