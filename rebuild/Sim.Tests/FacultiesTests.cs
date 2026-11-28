using CsCheck;
using Survival.Tests.Verification;

namespace Survival.Tests;

/// The body's faculties seam (documentation/specs/2026-11-24-a-body-is-read-in-tiers.md, "The faculties" and
/// "Vitality"): build a body, read its four faculties, vitality and life. A foggy head reaches only dexterity
/// and perception; vitality is read from the faculties, and no faculty reads it.
public class FacultiesTests
{
    static BodyState Whole() => BodyState.BaselineHuman();

    static BodyState BlowToTheHead(double brain = 0.5)
    {
        var b = Whole();
        b.Regions.SelectMany(r => r.Organs).Single(o => o.Name == "Brain").Condition = brain;
        return b;
    }

    static void Destroy(BodyState b, string region)
    {
        foreach (var layer in b.Regions.Single(r => r.Name == region).Layers) layer.Condition = 0;
    }

    [Fact]
    public void ABlowToTheHead_LeavesStrengthAndSpeedWhole()
    {
        var hit = BlowToTheHead();
        Assert.True(Body.Perception(hit) < Body.Perception(Whole())); // the blow landed
        Assert.Equal(Body.Strength(Whole()), Body.Strength(hit), 12);
        Assert.Equal(Body.Speed(Whole()), Body.Speed(hit), 12);
        var load = new AbilityContext(EncumbrancePct: 0.8);
        Assert.Equal(Body.Speed(Whole(), load), Body.Speed(hit, load), 12);
    }

    /// The brain at half is consciousness at half: dexterity is manipulation times (a half plus half of it),
    /// and perception counts it once.
    [Fact]
    public void ABlowToTheHead_LowersDexterityAndPerception()
    {
        var hit = BlowToTheHead();
        Assert.Equal(Body.Dexterity(Whole()) * 0.75, Body.Dexterity(hit), 12);
        Assert.Equal(Body.Perception(Whole()) * 0.5, Body.Perception(hit), 12);
        Assert.Contains("foggy head", Body.DexterityCosts(hit));
        Assert.DoesNotContain("foggy head", Body.DexterityCosts(Whole()));
    }

    /// Vitality is read with no surroundings, so the dark and a full pack, which cost speed, perception and
    /// dexterity, do not reach it.
    [Fact]
    public void AWholeBody_HasVitalityOne()
    {
        Assert.Equal(1, Body.Vitality(Whole(), Species.Human));
        Assert.Equal(1, Body.Vitality(BodyState.BaselineHuman(Sex.Female), Species.Human));
        var wet = Whole();
        Effects.Set(wet, EffectKind.Wet, 1.0);
        Assert.True(Body.Dexterity(wet) < Body.Dexterity(Whole()));
        Assert.Equal(1, Body.Vitality(wet, Species.Human));
    }

    [Fact]
    public void Vitality_IsTheMeanOfTheFacultiesShares()
    {
        var hit = BlowToTheHead();
        var whole = Whole();
        double expected = (1 + 1 + Body.Dexterity(hit) / Body.Dexterity(whole) + Body.Perception(hit) / Body.Perception(whole)) / 4;
        Assert.Equal(expected, Body.Vitality(hit, Species.Human), 12);
    }

    [Fact]
    public void AWolf_HasNoDexterity_SoItsVitalityLeavesItOut()
    {
        var wolf = SpeciesTable.Average(Species.Wolf);
        Assert.Equal(0, Body.Dexterity(wolf));
        Assert.Equal(1, Body.Vitality(wolf, Species.Wolf));
        Destroy(wolf, "Front Left Leg");
        double strength = Body.Strength(wolf) / Body.Strength(SpeciesTable.Average(Species.Wolf));
        double speed = Body.Speed(wolf) / Body.Speed(SpeciesTable.Average(Species.Wolf));
        Assert.Equal((strength + speed + 1) / 3, Body.Vitality(wolf, Species.Wolf), 12);
    }

    static readonly Gen<double> Condition = Gen.Double[0, 1];

    [Fact]
    public void AWolfsVitality_IsARealNumber_ForAnyWounds()
    {
        Gen.Select(Condition.Array[18], Condition.Array[5], Gen.Double[0.4, 1]).Check(t =>
        {
            var (layers, organs, blood) = t;
            var wolf = SpeciesTable.Average(Species.Wolf);
            int i = 0;
            foreach (var layer in wolf.Regions.SelectMany(r => r.Layers)) layer.Condition = layers[i++];
            var head = wolf.Regions.Single(r => r.Name == "Head").Organs;
            for (int o = 0; o < head.Count; o++) head[o].Condition = organs[o];
            wolf.BloodPct = blood;
            Assert.InRange(Body.Vitality(wolf, Species.Wolf), 0, 1);
        }, iter: 300);
    }

    /// Strength's build stays when the limbs and senses are gone; life is heart, lungs and brain.
    [Fact]
    public void ABodyWithEveryFacultyAtItsLowest_IsAliveWhileItsHeartLungsAndBrainLive()
    {
        var b = Whole();
        foreach (var limb in new[] { "Left Arm", "Right Arm", "Left Leg", "Right Leg" }) Destroy(b, limb);
        foreach (var organ in b.Regions.SelectMany(r => r.Organs).Where(o => o.Name.Contains("Eye") || o.Name.Contains("Ear"))) organ.Condition = 0;
        Deprive.Wholly(b);
        Effects.Add(b, EffectKind.Hypothermia, 1.0);
        foreach (var kind in new[] { EffectKind.Shock, EffectKind.Pain, EffectKind.Dazed }) Effects.Add(b, kind, 0.8);
        Assert.Equal(0, Body.Dexterity(b), 9);
        Assert.Equal(0, Body.Perception(b), 9);
        Assert.True(Body.IsAlive(b));
    }

    // ---- zero means the part gives nothing ----

    static Region RegionOf(BodyState b, string name) => b.Regions.Single(r => r.Name == name);
    static Tissue OrganOf(BodyState b, string name) => b.Regions.SelectMany(r => r.Organs).Single(o => o.Name == name);

    static void SetLayers(BodyState b, string region, double condition)
    {
        foreach (var layer in RegionOf(b, region).Layers) layer.Condition = condition;
    }

    [Fact]
    public void NoWorkingLeg_IsSpeedAtZero_AndAnyWorkingLegIsAboveIt()
    {
        var legless = Whole();
        Destroy(legless, "Left Leg");
        Destroy(legless, "Right Leg");
        Assert.Equal(0, Body.Speed(legless));

        var oneWeakLeg = Whole();
        Destroy(oneWeakLeg, "Left Leg");
        SetLayers(oneWeakLeg, "Right Leg", 0.01);
        Assert.True(Body.Speed(oneWeakLeg) > 0);
    }

    [Fact]
    public void NoWorkingArm_IsDexterityAtZero_AndAnyWorkingArmIsAboveIt()
    {
        var armless = Whole();
        Destroy(armless, "Left Arm");
        Destroy(armless, "Right Arm");
        Assert.Equal(0, Body.Dexterity(armless));

        var oneWeakArm = Whole();
        Destroy(oneWeakArm, "Left Arm");
        SetLayers(oneWeakArm, "Right Arm", 0.01);
        Assert.True(Body.Dexterity(oneWeakArm) > 0);
    }

    [Fact]
    public void AWolfWithNoWorkingLeg_HasSpeedAtZero_AndAnyOneWorkingLegIsAboveIt()
    {
        var legs = SpeciesTable.Average(Species.Wolf).Regions.Where(r => r.Name.Contains("Leg")).Select(r => r.Name).ToList();
        Assert.Equal(4, legs.Count);
        var legless = SpeciesTable.Average(Species.Wolf);
        foreach (var leg in legs) Destroy(legless, leg);
        Assert.Equal(0, Body.Speed(legless));

        foreach (var working in legs)
        {
            var wolf = SpeciesTable.Average(Species.Wolf);
            foreach (var leg in legs.Where(l => l != working)) Destroy(wolf, leg);
            Assert.True(Body.Speed(wolf) > 0, working);
        }
    }

    /// Unconscious is consciousness at zero: the limbs give nothing, but a body keeps what its size gives it.
    [Fact]
    public void AnUnconsciousBody_HasSpeedDexterityAndPerceptionAtZero_AndStrengthAboveIt()
    {
        var knockedOut = BlowToTheHead(brain: 0);
        Assert.Equal(0, Body.Speed(knockedOut));
        Assert.Equal(0, Body.Dexterity(knockedOut));
        Assert.Equal(0, Body.Perception(knockedOut));
        Assert.True(Body.Strength(knockedOut) > 0);
    }

    /// No cut at a tenth of consciousness: a body barely awake keeps its limbs, so it walks as fast as a whole one.
    [Fact]
    public void ABodyBarelyConscious_HasNoFacultyAtZero_AndItsLimbsWhole()
    {
        var dim = BlowToTheHead(brain: 0.05);
        foreach (var (name, faculty) in new[] { ("strength", Body.Strength(dim)), ("speed", Body.Speed(dim)), ("dexterity", Body.Dexterity(dim)), ("perception", Body.Perception(dim)) })
            Assert.True(faculty > 0, name);
        Assert.Equal(Body.Speed(Whole()), Body.Speed(dim), 12);
        Assert.Equal(Body.Strength(Whole()), Body.Strength(dim), 12);
    }

    /// Lost blood scales consciousness, moving and manipulation; at half, circulation fails.
    [Fact]
    public void LostBlood_LowersStrengthSpeedAndDexterity()
    {
        foreach (double blood in new[] { 0.75, 0.5 })
        {
            var bled = Whole();
            bled.BloodPct = blood;
            Assert.True(Body.Strength(bled) < Body.Strength(Whole()), $"{blood}");
            Assert.True(Body.Speed(bled) < Body.Speed(Whole()), $"{blood}");
            Assert.True(Body.Dexterity(bled) < Body.Dexterity(Whole()), $"{blood}");
        }
        var half = Whole();
        half.BloodPct = 0.5;
        Assert.Equal(0, Body.Speed(half));
        Assert.True(Body.Strength(half) > 0);
    }

    /// One lung is breath at a half, above the three tenths where breath starts to fog the head. With the other
    /// failing too, perception and dexterity fall through consciousness, and the limbs are untouched.
    [Fact]
    public void ADestroyedLung_FogsTheHead_OnceBreathIsBelowThreeTenths()
    {
        var oneLung = Whole();
        OrganOf(oneLung, "Left Lung").Condition = 0;
        Assert.Equal(Body.Perception(Whole()), Body.Perception(oneLung), 12);
        Assert.Equal(Body.Dexterity(Whole()), Body.Dexterity(oneLung), 12);

        var breathless = Whole();
        OrganOf(breathless, "Left Lung").Condition = 0;
        OrganOf(breathless, "Right Lung").Condition = 0.4; // breath 0.2
        Assert.Equal(Body.Perception(Whole()) * 2 / 3, Body.Perception(breathless), 12);
        Assert.True(Body.Dexterity(breathless) < Body.Dexterity(Whole()));
        Assert.Equal(Body.Speed(Whole()), Body.Speed(breathless), 12);
    }

    /// Wasted to nothing, parched and a tenth bled, a full pack is more than the body can carry: speed stops at zero.
    [Fact]
    public void AFullPack_OnABodyWithAlmostNoStrength_IsSpeedAtZero_NeverBelow()
    {
        var weak = BodyState.Create(BodyKind.Human, 60, fatPct: 0, musclePct: 0.01);
        Deprive.Thirsty(weak, 1.0); // tiredness no longer weakens (#333): thirst does, through the blood
        weak.BloodPct = 0.9;
        Assert.True(Body.Strength(weak) < 0.15);
        Assert.Equal(0, Body.Speed(weak, new AbilityContext(EncumbrancePct: 1.0)));
        Assert.Equal(0, Body.Speed(weak, new AbilityContext(EncumbrancePct: 3.0)));
        Assert.True(Body.Speed(weak) > 0);
    }

    // ---- a faculty at zero says why ----

    /// Each way to speed at zero, alone, names only itself; a body that can walk names nothing.
    [Fact]
    public void SpeedAtZero_NamesWhatTookIt()
    {
        var legless = Whole();
        Destroy(legless, "Left Leg");
        Destroy(legless, "Right Leg");
        Assert.Equal([CrippledBy.NoWorkingLimb], Body.SpeedCrippledBy(legless));

        var bled = Whole();
        bled.BloodPct = 0.5;
        Assert.Equal([CrippledBy.LostBlood], Body.SpeedCrippledBy(bled));

        Assert.Equal([CrippledBy.Unconscious], Body.SpeedCrippledBy(BlowToTheHead(brain: 0)));

        var weak = BodyState.Create(BodyKind.Human, 60, fatPct: 0, musclePct: 0.01);
        Deprive.Thirsty(weak, 1.0); // tiredness no longer weakens (#333): thirst does, through the blood
        weak.BloodPct = 0.9;
        Assert.Equal([CrippledBy.Load], Body.SpeedCrippledBy(weak, new AbilityContext(EncumbrancePct: 1.0)));
        Assert.Empty(Body.SpeedCrippledBy(weak));

        Assert.Empty(Body.SpeedCrippledBy(Whole()));
        var oneWeakLeg = Whole();
        Destroy(oneWeakLeg, "Left Leg");
        SetLayers(oneWeakLeg, "Right Leg", 0.01);
        Assert.Empty(Body.SpeedCrippledBy(oneWeakLeg));
    }

    // ---- vision: how far a body sees ----

    static BodyState WithOrgans(BodyState b, string nameContains, double condition)
    {
        foreach (var organ in b.Regions.SelectMany(r => r.Organs).Where(o => o.Name.Contains(nameContains))) organ.Condition = condition;
        return b;
    }

    [Fact]
    public void AWholeBody_HasVisionOne_AWolfToo()
    {
        Assert.Equal(1, Body.Vision(Whole()));
        Assert.Equal(1, Body.Vision(SpeciesTable.Average(Species.Wolf)));
        Assert.Empty(Body.CrippledBy(Whole(), Faculty.Vision));
    }

    [Fact]
    public void Vision_IsTheEyesTimesConsciousness()
    {
        Assert.Equal(0.5, Body.Vision(BlowToTheHead(brain: 0.5)), 12);
        Assert.Equal(0, Body.Vision(BlowToTheHead(brain: 0)));
        Assert.Equal([CrippledBy.Unconscious], Body.CrippledBy(BlowToTheHead(brain: 0), Faculty.Vision));
    }

    /// Vision is the eyes alone, not half the ears as perception is: deaf, a body sees as far; blind, it sees nothing.
    [Fact]
    public void Vision_ReadsTheEyes_NotTheEars()
    {
        var deaf = WithOrgans(Whole(), "Ear", 0);
        Assert.Equal(1, Body.Vision(deaf));
        Assert.True(Body.Perception(deaf) < Body.Perception(Whole())); // perception still hears

        foreach (double ears in new[] { 1.0, 0.0 })
        {
            var blind = WithOrgans(WithOrgans(Whole(), "Ear", ears), "Eye", 0);
            Assert.Equal(0, Body.Vision(blind));
            Assert.Equal([CrippledBy.NoWorkingEye], Body.CrippledBy(blind, Faculty.Vision));
        }
    }

    // ---- properties over any body ----

    static readonly Gen<double> MostlyWhole = Gen.Frequency((1, Gen.Const(1.0)), (1, Condition), (1, Gen.Const(0.0)));

    /// Any species and sex, any wounds, blood, effects, build and load, in the dark or the light.
    internal static readonly Gen<(BodyState Body, AbilityContext Context)> AnyBody =
        Gen.Select(
            Gen.Select(Gen.Enum<Species>(), Gen.Enum<Sex>(), MostlyWhole.Array[48], Gen.Double[0, 1]),
            Gen.Select(Gens.Kind, Gens.Severity).Array[0, 6],
            Gen.Select(Gen.Double[0, 3], Gen.Double[0, 2]),
            Gen.Select(Gen.Double[0, 5], Gen.Double[0, 1], Gen.Bool))
        .Select((who, effects, build, around) =>
        {
            var (species, sex, conditions, blood) = who;
            var b = SpeciesTable.Average(species, sex);
            int i = 0;
            foreach (var t in b.Regions.SelectMany(r => r.Layers.Concat(r.Organs))) t.Condition = conditions[i++ % conditions.Length];
            b.BloodPct = blood;
            foreach (var (kind, severity) in effects) Effects.Set(b, kind, severity);
            b.FatKg *= build.Item1;
            b.MuscleKg *= build.Item2;
            return (b, new AbilityContext(around.Item1, around.Item2, around.Item3));
        });

    [Fact]
    public void EveryFaculty_IsARealNumberAtOrAboveZero_ForAnyBody()
    {
        AnyBody.Check(t =>
        {
            var (b, context) = t;
            foreach (var (name, faculty) in new[] { ("strength", Body.Strength(b)), ("speed", Body.Speed(b, context)),
                ("dexterity", Body.Dexterity(b, context)), ("perception", Body.Perception(b, context)), ("vision", Body.Vision(b)) })
                Assert.True(double.IsFinite(faculty) && faculty >= 0, $"{name} {faculty}");
        }, iter: 300);
    }

    /// A refusal always has its reasons: speed is at zero exactly when it names one.
    [Fact]
    public void SpeedIsAtZero_ExactlyWhenItNamesAReason_ForAnyBody()
    {
        AnyBody.Check(t =>
        {
            var (b, context) = t;
            Assert.Equal(Body.Speed(b, context) == 0, Body.SpeedCrippledBy(b, context).Count > 0);
        }, iter: 300);
    }

    /// The same for dexterity and perception, the other faculties a verb needs above zero (Body.CrippledBy), and vision.
    [Fact]
    public void DexterityPerceptionAndVision_AreAtZero_ExactlyWhenTheyNameAReason_ForAnyBody()
    {
        AnyBody.Check(t =>
        {
            var (b, context) = t;
            Assert.Equal(Body.Dexterity(b, context) == 0, Body.CrippledBy(b, Faculty.Dexterity, context).Count > 0);
            Assert.Equal(Body.Perception(b, context) == 0, Body.CrippledBy(b, Faculty.Perception, context).Count > 0);
            Assert.Equal(Body.Vision(b) == 0, Body.CrippledBy(b, Faculty.Vision).Count > 0);
        }, iter: 300);
    }

    /// A chase's numbers, over any body that can run, as any species runs: real numbers, the floor held. A body at
    /// speed zero has none (Combat.RunningOf is null for it, and nothing about running is computed).
    [Fact]
    public void RunningSpeedAndBurstCost_AreRealNumbers_ForAnyBodyThatCanRun_AndNoneAtSpeedZero()
    {
        AnyBody.Check(t =>
        {
            var (b, context) = t;
            foreach (var species in Enum.GetValues<Species>())
            {
                var running = SpeciesTable.RunningIfAble(species, b, context);
                Assert.Equal(Body.Speed(b, context) <= 0, running == null);
                if (running is not { } r) continue;
                Assert.True(r.SpeedPct >= SpeciesTable.MinSpeedPct && double.IsFinite(r.SpeedPct), $"{species}: {r.SpeedPct}");
                foreach (var (name, v) in new[] { ("burst cost", Body.BurstCostPct(r)), ("top speed", Body.TopSpeedMps(b, r)), ("endurance", Body.EnduranceSpeedMps(r)) })
                    Assert.True(double.IsFinite(v) && v > 0, $"{species} {name}: {v}");
            }
        }, iter: 100);
    }

    /// So a faculty's share of a fresh body never divides by zero.
    [Fact]
    public void AFreshBody_HasEveryFacultyItsSpeciesHas_AboveZero()
    {
        foreach (var species in Enum.GetValues<Species>())
            foreach (var sex in Enum.GetValues<Sex>())
            {
                var fresh = SpeciesTable.Average(species, sex);
                Assert.True(Body.Strength(fresh) > 0, $"{species} {sex}");
                Assert.True(Body.Speed(fresh) > 0, $"{species} {sex}");
                Assert.True(Body.Perception(fresh) > 0, $"{species} {sex}");
                Assert.Equal(fresh.Kind == BodyKind.Human, Body.Dexterity(fresh) > 0); // only a human body has hands
            }
    }

    // ---- the world's reads ----

    static Agent Me(World w) => w.Agent(AgentId.Player);

    [Fact]
    public void ABlowToTheHead_SlowsMakingAndMakesItRiskier_ThroughDexterityAlone()
    {
        var w = TestWorlds.Bare(1);
        var r = Recipes.Get("flint_tipped_spear");
        var pack = Items.Pack(w, AgentId.Player);
        Me(w).Members[0] = Whole();
        var whole = Making.Odds(w, Me(w), r);
        double wholeRate = Making.WorkPerMinute(w, Me(w), pack, r);

        Me(w).Members[0] = BlowToTheHead(0.2);
        var hit = Making.Odds(w, Me(w), r);
        Assert.True(hit.Outcomes.Where(o => o.Outcome.FailureLike).Sum(o => o.Odds.TotalPct)
            > whole.Outcomes.Where(o => o.Outcome.FailureLike).Sum(o => o.Odds.TotalPct));
        Assert.True(Making.WorkPerMinute(w, Me(w), pack, r) < wholeRate);
        Assert.DoesNotContain(hit.Outcomes.SelectMany(o => o.Odds.Terms), t => t.Factor == "Dazed");
        var context = Fire.AbilityContextFor(w, Me(w));
        var byDexterity = Making.Odds(r, 0, Body.Dexterity(Me(w).Body, context), string.Join(", ", Body.DexterityCosts(Me(w).Body, context)));
        Assert.Equal(byDexterity.Outcomes.Select(o => o.Odds.TotalPct), hit.Outcomes.Select(o => o.Odds.TotalPct));
    }

    [Fact]
    public void ABlowToTheHead_MakesFireStartingLessLikely_ThroughDexterityAlone()
    {
        var w = TestWorlds.Bare(1);
        Me(w).Members[0] = Whole();
        double whole = Fire.StartOdds(w, Me(w), ToolType.HandDrill, Resource.Tinder).TotalPct;
        Me(w).Members[0] = BlowToTheHead();
        var hit = Fire.StartOdds(w, Me(w), ToolType.HandDrill, Resource.Tinder);
        Assert.True(hit.TotalPct < whole);
        Assert.DoesNotContain(hit.Terms, t => t.Factor == "Dazed");
        var context = Fire.AbilityContextFor(w, Me(w));
        Assert.Equal(Fire.StartOdds(ToolType.HandDrill, Resource.Tinder, 0, Body.Dexterity(Me(w).Body, context)).TotalPct, hit.TotalPct, 12);
    }

    /// Sight costs no speed and no old vitality: only the new vitality sees the blinded one as the weakest.
    [Fact]
    public void AHunter_SinglesOutTheHerdsLeastVital()
    {
        var (w, t) = CombatTests.Field();
        var herd = CombatTests.Herd(w, Species.Caribou, 3, t);
        CombatTests.Normalize(w, herd);
        var blinded = w.Agent(herd.Ids[1]);
        foreach (var eye in blinded.Body.Regions.SelectMany(r => r.Organs).Where(o => o.Name.Contains("Eye"))) eye.Condition = 0;
        Assert.Equal(Body.Speed(w.Agent(herd.Ids[0]).Body), Body.Speed(blinded.Body), 12);
        Assert.Equal(herd.Ids[1], Fauna.Weakest(w, w.Agent(herd.Ids[0])).Id);
    }
}
