namespace Survival.Tests;

/// Ported from text_survival.Tests/Bodies: body composition, tissues, capacities, abilities, damage.
public class BodyTests
{
    static BodyState Baseline() => BodyState.BaselineHuman();
    static BodyState Custom(double weightKg, double fatPct, double musclePct) => BodyState.Create(BodyKind.Human, weightKg, fatPct, musclePct);
    static Region RegionOf(BodyState b, string name) => b.Regions.Single(r => r.Name == name);
    static Tissue OrganOf(BodyState b, string name) => b.Regions.SelectMany(r => r.Organs).Single(o => o.Name == name);
    static Rng TestRng() => Rng.ForSystem(1, "test");

    /// A faculty as its share of the same whole body's, which is the share of the capacity it is a product of when nothing else changed.
    static double SpeedShare(BodyState b) => Body.Speed(b) / Body.Speed(Baseline());

    static void Destroy(Region r) { r.Skin.Condition = 0; r.Muscle.Condition = 0; r.Bone.Condition = 0; }
    static void SetLayers(Region r, double condition) { r.Skin.Condition = condition; r.Muscle.Condition = condition; r.Bone.Condition = condition; }

    // ---- composition ----

    [Fact]
    public void Composition_SumsAndPercentages()
    {
        var b = Baseline();
        Assert.Equal(68.0, b.WeightKg, 6);
        Assert.Equal(34.0, b.StructuralKg, 6);
        Assert.Equal(0.12, b.FatPct, 6);
        Assert.Equal(0.38, b.MusclePct, 6);
    }

    [Fact]
    public void BodyPlans_HumanHasSevenRegions_QuadrupedFourLegs()
    {
        Assert.Equal(7, Baseline().Regions.Count);
        var wolf = BodyState.Create(BodyKind.Quadruped, 40, 0.1, 0.4);
        Assert.Equal(4, wolf.Regions.Count(r => r.Name.Contains("Leg")));
    }

    [Fact]
    public void AWolfWithWholeLegs_HasFullSpeed_AndNoneWithThemDestroyed()
    {
        var wolf = SpeciesTable.Average(Species.Wolf);
        Assert.Equal(1, Body.Share(wolf, Species.Wolf, Faculty.Speed), 9);
        foreach (var leg in wolf.Regions.Where(r => r.Name.Contains("Leg"))) Destroy(leg);
        Assert.Equal(0, Body.Speed(wolf));
    }

    // ---- tissues ----

    /// Damage is measured in tissue condition: what a tissue can stop grows with its toughness and the
    /// size of the body, and a point meets less soft tissue than an edge, a club least; bone stops a
    /// club best.
    [Fact]
    public void EffectiveToughness_GrowsWithToughnessAndSize_AndDependsOnTheBlow()
    {
        var muscle = new Tissue { Name = "Muscle", Kind = TissueKind.Muscle, Toughness = 2 };
        var bone = new Tissue { Name = "Bone", Kind = TissueKind.Bone, Toughness = 2 };
        Assert.True(Body.EffectiveToughness(muscle, DamageType.Sharp, 2) > Body.EffectiveToughness(muscle, DamageType.Sharp, 1));
        Assert.True(Body.EffectiveToughness(muscle, DamageType.Pierce, 1) < Body.EffectiveToughness(muscle, DamageType.Sharp, 1));
        Assert.True(Body.EffectiveToughness(muscle, DamageType.Blunt, 1) < Body.EffectiveToughness(muscle, DamageType.Sharp, 1));
        Assert.True(Body.EffectiveToughness(bone, DamageType.Blunt, 1) > Body.EffectiveToughness(bone, DamageType.Sharp, 1));
        Assert.True(Body.EffectiveToughness(bone, DamageType.Blunt, 1) > Body.EffectiveToughness(bone, DamageType.Pierce, 1));
    }

    [Theory]
    [InlineData("Heart", DamageType.Blunt, 0.08)]
    [InlineData("Brain", DamageType.Blunt, 0.06)]
    [InlineData("Left Lung", DamageType.Blunt, 0.10)]
    [InlineData("Heart", DamageType.Sharp, 0.032)]
    [InlineData("Heart", DamageType.Pierce, 0.016)]
    [InlineData("Muscle", DamageType.Blunt, 0.01)]
    public void NaturalAbsorption_OrgansShrugOffMore(string name, DamageType type, double expected)
    {
        var t = new Tissue { Name = name, Kind = name == "Muscle" ? TissueKind.Muscle : TissueKind.Organ };
        Assert.Equal(expected, Body.NaturalAbsorption(t, type), 6);
    }

    // ---- capacities, read through the faculties ----

    [Fact]
    public void HealthyBody_AllFacultiesNearAFreshBodys()
    {
        var b = Baseline();
        foreach (var f in Enum.GetValues<Faculty>())
            Assert.True(Body.Share(b, Species.Human, f) > 0.9, $"{f} {Body.Share(b, Species.Human, f)} should be near a fresh body's");
        Assert.Equal(1, Body.TissueVitality(b));
    }

    [Fact]
    public void DestroyedHeart_Kills()
    {
        var b = Baseline();
        OrganOf(b, "Heart").Condition = 0;
        Assert.Equal(0, Body.TissueVitality(b));
        Assert.False(Body.IsAlive(b));
    }

    /// The chest's muscle and bone carry breath and blood: crushed, they loosen the body's hold on life.
    [Fact]
    public void ChestTrauma_LoosensTheHoldOnLife()
    {
        var b = Baseline();
        SetLayers(RegionOf(b, "Chest"), 0.1);
        Assert.InRange(Body.TissueVitality(b), 0.01, 0.99);
        Assert.True(Body.IsAlive(b));
    }

    /// Body review A2. An organ supplies its capacity in proportion to its own condition, and a region
    /// does its work only as far as its weakest layer lets it. Old (pinned here as a "known weakness"):
    /// every tissue in a region was averaged, so both lungs destroyed left breathing at 0.6-0.7, a
    /// brain at 0.02 left 0.88 consciousness, and a leg with no bone still walked at 0.55.
    [Fact]
    public void LostOrgans_TakeTheirCapacityWithThem()
    {
        var lungs = Baseline();
        OrganOf(lungs, "Left Lung").Condition = 0;
        OrganOf(lungs, "Right Lung").Condition = 0;
        Assert.Equal(0, Body.TissueVitality(lungs));
        Assert.False(Body.IsAlive(lungs));

        var eyes = Baseline();
        OrganOf(eyes, "Left Eye").Condition = 0;
        OrganOf(eyes, "Right Eye").Condition = 0;
        Assert.Equal(0, Body.Vision(eyes));
        Assert.Equal([CrippledBy.NoWorkingEye], Body.CrippledBy(eyes, Faculty.Vision));

        var brain = Baseline();
        OrganOf(brain, "Brain").Condition = 0.5;
        Assert.Equal(0.5, Body.Vision(brain), 9); // whole eyes, half conscious

        var heart = Baseline();
        OrganOf(heart, "Heart").Condition = 0.02;
        Assert.Equal(0.02, Body.TissueVitality(heart), 9);

        var gut = Baseline();
        OrganOf(gut, "Liver").Condition = 0;
        OrganOf(gut, "Stomach").Condition = 0;
        gut.CaloriesKcal = 0;
        Body.Eat(gut, 1000);
        Assert.Equal(500, gut.CaloriesKcal, 9); // a destroyed gut absorbs half

        var legs = Baseline();
        RegionOf(legs, "Left Leg").Bone.Condition = 0.2;
        RegionOf(legs, "Right Leg").Bone.Condition = 0.2;
        Assert.InRange(SpeedShare(legs), 0.03, 0.05); // a shattered bone stops the leg: a fifth of its bone, a twenty-fifth of its speed
    }

    /// Body review A2b: hurting any one tissue never makes any capacity better.
    [Fact]
    public void LoweringAnyTissue_NeverRaisesAnyCapacity()
    {
        foreach (var kind in Enum.GetValues<BodyKind>())
        {
            var tissues = BodyState.Create(kind, 60, 0.15, 0.3).Regions
                .SelectMany((r, ri) => r.Layers.Concat(r.Organs).Select((_, ti) => (ri, ti))).ToList();
            var rng = Rng.ForSystem(7, "monotone");
            for (int trial = 0; trial < 300; trial++)
            {
                var b = BodyState.Create(kind, 60, 0.15, 0.3);
                foreach (var r in b.Regions)
                    foreach (var t in r.Layers.Concat(r.Organs))
                        t.Condition = rng.NextDouble() < 0.5 ? 1 : rng.NextDouble();
                var (ri, ti) = tissues[rng.NextInt(tissues.Count)];
                var tissue = b.Regions[ri].Layers.Concat(b.Regions[ri].Organs).ElementAt(ti);
                var before = Body.GetCapacities(b);
                double beforeLife = Body.TissueVitality(b);
                tissue.Condition *= rng.NextDouble();
                var after = Body.GetCapacities(b);
                double afterLife = Body.TissueVitality(b);
                string[] names = ["moving", "manipulation", "consciousness", "sight", "hearing", "hold on life"];
                double[] x = [before.Moving, before.Manipulation, before.Consciousness, before.Sight, before.Hearing, beforeLife];
                double[] y = [after.Moving, after.Manipulation, after.Consciousness, after.Sight, after.Hearing, afterLife];
                for (int i = 0; i < x.Length; i++)
                    Assert.True(y[i] <= x[i] + 1e-12, $"{kind}: lowering {b.Regions[ri].Name} {tissue.Name} raised {names[i]} {x[i]} -> {y[i]}");
            }
        }
    }

    /// A scalp wound does not dim the eyes: skin carries a limb's work, not the organs'.
    [Fact]
    public void ASkinWound_OnTheHead_LeavesVisionAndPerceptionWhole()
    {
        var b = Baseline();
        RegionOf(b, "Head").Skin.Condition = 0.3;
        Assert.Equal(1.0, Body.Vision(b), 9);
        Assert.Equal(1.0, Body.Share(b, Species.Human, Faculty.Perception), 9);
    }

    [Fact]
    public void DestroyedChest_NoBreath_SeesAndPerceivesNothing_AndIsDead()
    {
        var b = Baseline();
        Destroy(RegionOf(b, "Chest"));
        Assert.Equal(0, Body.TissueVitality(b));
        Assert.Equal(0, Body.Vision(b)); // breath below three tenths leaves no consciousness
        Assert.Equal(0, Body.Perception(b));
        Assert.False(Body.IsAlive(b));
    }

    [Fact]
    public void Unconscious_CannotMoveOrManipulate()
    {
        var b = Baseline();
        Destroy(RegionOf(b, "Head"));
        var c = Body.GetCapacities(b);
        Assert.Equal(0, c.Consciousness);
        Assert.Equal(0, c.Moving); // unconscious, the limbs give nothing
        Assert.Equal(0, c.Manipulation);
    }

    [Fact]
    public void HalfDamagedArms_RoughlyHalveDexterity()
    {
        var b = Baseline();
        double healthy = Body.Dexterity(b);
        SetLayers(RegionOf(b, "Left Arm"), 0.5);
        SetLayers(RegionOf(b, "Right Arm"), 0.5);
        double damaged = Body.Dexterity(b) / healthy;
        Assert.InRange(damaged, 0.2, 0.7);
    }

    [Fact]
    public void Hypothermia_AtFull_TakesThreeTenthsOfSpeed()
    {
        var b = Baseline();
        Effects.Add(b, EffectKind.Hypothermia, 1.0); // -0.3 Moving
        Assert.Equal(0.7, SpeedShare(b), 6);
    }

    [Fact]
    public void DestroyedLegs_LeaveNoSpeed()
    {
        var b = Baseline();
        Destroy(RegionOf(b, "Left Leg"));
        Destroy(RegionOf(b, "Right Leg"));
        Assert.Equal(0, Body.Speed(b));
    }

    [Fact]
    public void DestroyedArms_LeaveNoDexterity()
    {
        var b = Baseline();
        Destroy(RegionOf(b, "Left Arm"));
        Destroy(RegionOf(b, "Right Arm"));
        Assert.Equal(0, Body.Dexterity(b));
    }

    [Fact]
    public void BloodLoss_AtThreeQuarters_HalvesVision_AndAtHalfKills()
    {
        var b = Baseline();
        b.BloodPct = 0.75;
        Assert.Equal(0.5, Body.Vision(b), 6); // whole eyes, half conscious
        b.BloodPct = 0.5;
        Assert.False(Body.IsAlive(b));
    }

    // ---- abilities ----

    [Fact]
    public void Strength_BaselineNearOne_MuscleRaisesIt_ThinStaysPositive()
    {
        double baseline = Body.Strength(Baseline());
        Assert.InRange(baseline, 0.9, 1.1);
        Assert.True(Body.Strength(Custom(90, 0.10, 0.45)) > baseline);
        Assert.True(Body.Strength(Custom(70, 0.03, 0.35)) > 0);
    }

    [Fact]
    public void Speed_BaselineFatAndSize()
    {
        double baseline = Body.Speed(Baseline());
        Assert.InRange(baseline, 0.7, 1.0);
        Assert.True(Body.Speed(Custom(100, 0.35, 0.20)) < 0.8, "Obesity slows");
        Assert.True(Body.Speed(Custom(680, 0.12, 0.38)) < baseline, "Large is slower");
        Assert.True(Body.Speed(Custom(6.8, 0.12, 0.38)) > baseline, "Small is faster");
    }

    [Fact]
    public void Speed_HeavyLoadSlows()
    {
        var b = Baseline();
        Assert.True(Body.Speed(b, new AbilityContext(EncumbrancePct: 1.0)) < Body.Speed(b));
    }

    /// Body review B5 (decided 2026-11-21): effects disable but never kill. Old: Tired, Thirsty and
    /// Hungry at full brought consciousness to 0 and killed a body with water left and no organ harmed.
    [Fact]
    public void Effects_DisableButNeverKill()
    {
        var b = Baseline();
        foreach (var kind in new[] { EffectKind.Tired, EffectKind.Thirsty, EffectKind.Hungry, EffectKind.Hypothermia })
            Effects.Add(b, kind, 1.0);
        foreach (var kind in new[] { EffectKind.Shock, EffectKind.Pain, EffectKind.Dazed })
            Effects.Add(b, kind, 0.8);
        Assert.Equal(0, Body.Perception(b));
        Assert.Equal([CrippledBy.Unconscious], Body.CrippledBy(b, Faculty.Perception));
        Assert.True(Body.IsAlive(b));
    }

    /// Discomforts stack with diminishing returns. Hunger, thirst and tiredness at full together leave
    /// every faculty at about a fifth of a fresh body's (dexterity, the product of two capacities, at 0.18);
    /// each alone is nearly what its row says.
    [Fact]
    public void Discomforts_StackWithDiminishingReturns_NeverBelowAboutAFifth()
    {
        var b = Baseline();
        foreach (var kind in new[] { EffectKind.Hungry, EffectKind.Thirsty, EffectKind.Tired }) Effects.Add(b, kind, 1.0);
        foreach (var f in Enum.GetValues<Faculty>())
            Assert.True(Body.Share(b, Species.Human, f) >= 0.18, $"{f} {Body.Share(b, Species.Human, f):0.000}");
        Assert.InRange(SpeedShare(b), 0.2, 0.25);

        var tired = Baseline();
        Effects.Add(tired, EffectKind.Tired, 1.0);
        Assert.InRange(SpeedShare(tired), 0.55, 0.57); // -0.45 alone, a hair less
    }

    /// Only discomfort saturates: wounds, blood loss and the cold's effects still take what they take,
    /// on top of it, to nothing.
    [Fact]
    public void Discomforts_Saturate_ButDamageBloodLossAndColdStillTakeSpeedToNothing()
    {
        BodyState Miserable()
        {
            var b = Baseline();
            foreach (var kind in new[] { EffectKind.Hungry, EffectKind.Thirsty, EffectKind.Tired }) Effects.Add(b, kind, 1.0);
            return b;
        }
        var bled = Miserable();
        bled.BloodPct = 0.5;
        Assert.Equal(0, SpeedShare(bled), 9);
        var lame = Miserable();
        Destroy(RegionOf(lame, "Left Leg"));
        Destroy(RegionOf(lame, "Right Leg"));
        Assert.True(SpeedShare(lame) < 0.02);
        var frozen = Miserable();
        Effects.Add(frozen, EffectKind.Frostbite, 0.3); // -0.15 moving, added in full below the fifth
        Assert.InRange(SpeedShare(frozen), 0.06, 0.10);
    }

    /// Body review B5: a body that dies is named after the damage that took it over the line, as it
    /// landed; a cause no damage named is the body's injuries, and a living body has no cause.
    [Fact]
    public void ADeath_IsNamedForTheDamageThatCrossedTheLine()
    {
        var b = Baseline();
        Assert.Throws<InvalidOperationException>(() => Body.CauseOfDeath(b));
        var spear = new Cause(new AgentId(7), "a person's stone spear");
        Body.Damage(b, new DamageInfo(0.4, DamageType.Internal, BodyTarget.Heart, By: new Cause(null, "the cold")), TestRng());
        Assert.True(Body.IsAlive(b));
        Assert.Null(b.DeathCause);
        Body.Damage(b, new DamageInfo(0.6, DamageType.Internal, BodyTarget.Heart, By: spear), TestRng());
        Assert.False(Body.IsAlive(b));
        Assert.Equal(spear, Body.CauseOfDeath(b));
        Body.Damage(b, new DamageInfo(0.5, DamageType.Internal, BodyTarget.Brain, By: new Cause(null, "thirst")), TestRng());
        Assert.Equal(spear, Body.CauseOfDeath(b)); // the dead die once

        var unnamed = Baseline();
        unnamed.BloodPct = 0.51;
        Body.Damage(unnamed, new DamageInfo(100, DamageType.Bleed, BodyTarget.Blood), TestRng());
        Assert.Equal(Body.UnnamedInjuries, Body.CauseOfDeath(unnamed));
    }

    [Fact]
    public void Vitality_HealthyIsOne_AndConsciousnessEffectsDragItDown()
    {
        Assert.Equal(1.0, Body.Vitality(Baseline(), Species.Human), 2);
        Assert.True(Body.Vitality(Custom(75, 0.20, 0.30), Species.Human) > 0.9);

        // The scenario from a playtest: Hypothermia 98%, Tired 65%, plus 0.088 more = -0.87 consciousness.
        var b = Baseline();
        Effects.Add(b, EffectKind.Hypothermia, 0.98);
        Effects.Add(b, EffectKind.Tired, 0.65);
        Effects.Add(b, EffectKind.Shock, 0.22); // stands in for the old Fever 22%: -0.4 consciousness each
        // The mean of the faculties, not the least of them (#200): strength and speed keep their build.
        Assert.True(Body.Vitality(b, Species.Human) < 0.40, $"Vitality {Body.Vitality(b, Species.Human):P0}");
    }

    /// Body review A3. Sight and hearing are half each, and darkness takes sight's half away. Old (pinned
    /// here): hearing's weight grew as sight's fell, so the healthy perceived as well in pitch dark (1.0)
    /// as at noon, and worse at dusk (0.875) than in either.
    [Fact]
    public void Perception_FallsWithDarkness_HearingKeepsItsHalf()
    {
        var b = Baseline();
        Assert.Equal(1.0, Body.Perception(b), 2);
        double previous = double.MaxValue;
        for (double d = 0; d <= 1.0001; d += 0.05)
        {
            double p = Body.Perception(b, new AbilityContext(DarknessLevel: d));
            Assert.True(p <= previous + 1e-12, $"perception rose from {previous} to {p} at darkness {d:0.00}");
            previous = p;
        }
        double dark = Body.Perception(b, new AbilityContext(DarknessLevel: 1.0));
        Assert.Equal(0.5, dark, 2); // full darkness: hearing's half
        Assert.Equal(Body.Perception(b), Body.Perception(b, new AbilityContext(DarknessLevel: 1.0, HasLightSource: true)), 9);
        OrganOf(b, "Left Ear").Condition = 0;
        OrganOf(b, "Right Ear").Condition = 0;
        double deafInDark = Body.Perception(b, new AbilityContext(DarknessLevel: 1.0));
        Assert.True(deafInDark < dark);
        Assert.True(Body.Perception(b, new AbilityContext(DarknessLevel: 1.0, HasLightSource: true)) > deafInDark);
    }

    [Fact]
    public void Dexterity_DarknessAndWetHandsHurt()
    {
        var b = Baseline();
        double dry = Body.Dexterity(b);
        Assert.True(Body.Dexterity(b, new AbilityContext(DarknessLevel: 1.0)) < dry);
        Effects.Set(b, EffectKind.Wet, 1.0);
        Assert.True(Body.Dexterity(b) < dry);
    }

    [Theory]
    [InlineData(70, 0.03, 0.35, 0.0, 0.1)]
    [InlineData(75, 0.10, 0.30, 0.05, 0.20)]
    [InlineData(90, 0.30, 0.25, 0.15, 0.25)]
    public void ColdResistance_FatInsulatesWithDiminishingReturns(double kg, double fat, double muscle, double min, double max)
    {
        Assert.InRange(Body.ColdResistance(Custom(kg, fat, muscle)), min, max);
    }

    // ---- damage ----

    [Fact]
    public void Damage_SpecificTarget_HitsNamedRegion()
    {
        var result = Body.Damage(Baseline(), new DamageInfo(10, DamageType.Blunt, BodyTarget.Head), TestRng());
        Assert.Equal("Head", result.HitRegion);
    }

    [Fact]
    public void Damage_LightBlowToChest_ReachesNoOrgan()
    {
        var result = Body.Damage(Baseline(), new DamageInfo(0.4, DamageType.Blunt, BodyTarget.Chest), TestRng());
        Assert.Null(result.OrganHit);
        Assert.True(result.DamageAbsorbed > 0);
    }

    [Fact]
    public void Damage_ModerateCut_PenetratesSeveralLayers()
    {
        var result = Body.Damage(Baseline(), new DamageInfo(15, DamageType.Sharp), TestRng());
        Assert.True(result.TissuesDamaged.Count >= 2);
    }

    [Fact]
    public void Damage_HeavyStabToChest_PenetratesDeep()
    {
        var result = Body.Damage(Baseline(), new DamageInfo(50, DamageType.Pierce, BodyTarget.Chest), TestRng());
        Assert.True(result.TissuesDamaged.Count >= 2);
        Assert.NotNull(result.OrganHit);
    }

    /// A layer stops no more than it can of the blow and passes the rest on: a huge blow to an arm,
    /// which has no organ behind it, goes out the other side.
    [Fact]
    public void Damage_EachLayerStopsAtMostItsEffectiveToughness()
    {
        var b = BodyState.Create(BodyKind.Human, Body.BaselineWeightKg, Body.BaselineFatPct, Body.BaselineMusclePct);
        var arm = RegionOf(b, "Left Arm");
        double most = arm.Layers.Sum(t => Body.EffectiveToughness(t, DamageType.Blunt, 1) + Body.NaturalAbsorption(t, DamageType.Blunt));
        var result = Body.Damage(b, new DamageInfo(100, DamageType.Blunt, BodyTarget.LeftArm), TestRng());
        Assert.InRange(result.DamageAbsorbed, 1e-9, most + 1e-9);
    }

    [Fact]
    public void Damage_Zero_DoesNothing()
    {
        var b = Baseline();
        var result = Body.Damage(b, new DamageInfo(0), TestRng());
        Assert.Equal(0, result.TotalDamageDealt);
        Assert.All(b.Regions, r => Assert.Equal(1.0, r.Condition));
        Assert.Empty(b.Effects);
    }

    [Fact]
    public void Damage_Massive_DestroysRegion_AndBleedsOut()
    {
        var b = Baseline();
        var result = Body.Damage(b, new DamageInfo(100, DamageType.Blunt, BodyTarget.Head), TestRng());
        Assert.True(result.TotalDamageDealt > 0);
        Assert.True(RegionOf(b, "Head").Condition < 1);
        Assert.Contains(result.TriggeredEffects, e => e.Kind == EffectKind.Dazed);
        Assert.True(Effects.Has(b, EffectKind.Pain));
    }

    [Fact]
    public void Damage_Cut_TriggersBleedingAndPain()
    {
        var b = Baseline();
        Body.Damage(b, new DamageInfo(0.3, DamageType.Sharp, BodyTarget.LeftArm), TestRng());
        Assert.True(Effects.Has(b, EffectKind.Bleeding));
        Assert.True(Effects.Has(b, EffectKind.Pain));
    }

    [Fact]
    public void Clothing_ReducesBluntByCushioning_ButNotInternal()
    {
        var bare = Baseline();
        bare.Worn.Clear();
        var padded = Baseline();
        Body.Damage(bare, new DamageInfo(0.3, DamageType.Blunt, BodyTarget.Chest), TestRng());
        Body.Damage(padded, new DamageInfo(0.3, DamageType.Blunt, BodyTarget.Chest), TestRng());
        Assert.True(RegionOf(padded, "Chest").Condition > RegionOf(bare, "Chest").Condition);

        var b = Baseline();
        Body.Damage(b, new DamageInfo(0.5, DamageType.Internal, BodyTarget.Heart), TestRng());
        // Taken as condition directly: internal harm ignores clothing and toughness (body review B4).
        Assert.Equal(0.5, OrganOf(b, "Heart").Condition, 6);
    }

    /// Clothing protects only the region it lies over: leg wraps, however thick, do nothing for a blow
    /// to the head. Old: combat summed everything worn for a blow anywhere.
    [Fact]
    public void ClothingOnTheLegs_DoesNothingForAHeadBlow()
    {
        var bare = BodyState.BaselineHuman();
        bare.Worn.Clear();
        var legs = BodyState.BaselineHuman();
        legs.Worn.Clear();
        legs.Worn.Add(Items.Make("mammoth_hide_legs"));
        Body.Damage(bare, new DamageInfo(0.6, DamageType.Blunt, BodyTarget.Head), TestRng());
        Body.Damage(legs, new DamageInfo(0.6, DamageType.Blunt, BodyTarget.Head), TestRng());
        Assert.Equal(Whole(RegionOf(bare, "Head")), Whole(RegionOf(legs, "Head")), 9);

        var shin = BodyState.BaselineHuman();
        shin.Worn.Clear();
        shin.Worn.Add(Items.Make("mammoth_hide_legs"));
        var bareShin = BodyState.BaselineHuman();
        bareShin.Worn.Clear();
        Body.Damage(shin, new DamageInfo(0.6, DamageType.Blunt, BodyTarget.LeftLeg), TestRng());
        Body.Damage(bareShin, new DamageInfo(0.6, DamageType.Blunt, BodyTarget.LeftLeg), TestRng());
        Assert.True(Whole(RegionOf(shin, "Left Leg")) > Whole(RegionOf(bareShin, "Left Leg")));
    }

    static double Whole(Region r) => r.Layers.Sum(t => t.Condition);

    /// Body review B4: internal harm takes condition at its rate, whatever the organ's toughness or what
    /// it has already lost, so a failing organ does not fail faster. Old: divided by toughness times
    /// condition, so the brain (0.25) took four times a hit, and more as it failed.
    [Fact]
    public void InternalHarm_TakesConditionDirectly()
    {
        var b = Baseline();
        foreach (var name in new[] { "Brain", "Heart", "Liver" })
        {
            Body.Damage(b, new DamageInfo(0.1, DamageType.Internal, Enum.Parse<BodyTarget>(name)), TestRng());
            Assert.Equal(0.9, OrganOf(b, name).Condition, 9);
        }
        OrganOf(b, "Brain").Condition = 0.2;
        Body.Damage(b, new DamageInfo(0.1, DamageType.Internal, BodyTarget.Brain), TestRng());
        Assert.Equal(0.1, OrganOf(b, "Brain").Condition, 9);
    }

    /// Body review A4: a deep wound to the head can reach the brain. Old: any eye or ear came first, so
    /// 2,000 head stabs never touched it. A wound the skull stops reaches no organ.
    [Fact]
    public void ADeepHeadWound_CanReachTheBrain_AShallowOneCannot()
    {
        int brain = 0, external = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var hit = Body.Damage(Baseline(), new DamageInfo(3.0, DamageType.Pierce, BodyTarget.Head), Rng.ForSystem(seed, "head"));
            if (hit.OrganHit == "Brain") brain++;
            else if (hit.OrganHit != null) external++;
            var graze = Body.Damage(Baseline(), new DamageInfo(1.0, DamageType.Pierce, BodyTarget.Head), Rng.ForSystem(seed, "head"));
            Assert.Null(graze.OrganHit);
        }
        Assert.InRange(brain, 100, 180);  // about 70%
        Assert.InRange(external, 20, 100); // about 30%
    }

    [Fact]
    public void InternalDamage_ToLungs_HitsALung()
    {
        var b = Baseline();
        var result = Body.Damage(b, new DamageInfo(0.3, DamageType.Internal, BodyTarget.Lungs), TestRng());
        Assert.Contains(result.OrganHit, new[] { "Left Lung", "Right Lung" });
    }

    [Fact]
    public void Heal_MendsTheWorstRegion_IncludingOrgans()
    {
        var b = Baseline();
        OrganOf(b, "Liver").Condition = 0.5;
        Body.Heal(b, 0.06); // liver toughness 0.6: 0.06 heals 0.1 condition
        Assert.Equal(0.6, OrganOf(b, "Liver").Condition, 6);
    }

    [Fact]
    public void Eat_DamagedGutAbsorbsLess()
    {
        var healthy = Baseline();
        var sick = Baseline();
        healthy.CaloriesKcal = sick.CaloriesKcal = 0;
        OrganOf(sick, "Liver").Condition = 0;
        OrganOf(sick, "Stomach").Condition = 0;
        Body.Eat(healthy, 1000);
        Body.Eat(sick, 1000);
        Assert.Equal(1000, healthy.CaloriesKcal, 6);
        Assert.True(sick.CaloriesKcal < 1000);
    }
}
