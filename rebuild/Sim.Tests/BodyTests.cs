namespace Survival.Tests;

/// Ported from text_survival.Tests/Bodies: body composition, tissues, capacities, abilities, damage.
public class BodyTests
{
    static BodyState Baseline() => BodyState.BaselineHuman();
    static BodyState Custom(double weightKg, double fatPct, double musclePct) => BodyState.Create(BodyKind.Human, weightKg, fatPct, musclePct);
    static Region RegionOf(BodyState b, string name) => b.Regions.Single(r => r.Name == name);
    static Tissue OrganOf(BodyState b, string name) => b.Regions.SelectMany(r => r.Organs).Single(o => o.Name == name);
    static Rng TestRng() => Rng.ForSystem(1, "test");

    static void Destroy(Region r) { r.Skin.Condition = 0; r.Muscle.Condition = 0; r.Bone.Condition = 0; }
    static void SetLayers(Region r, double condition) { r.Skin.Condition = condition; r.Muscle.Condition = condition; r.Bone.Condition = condition; }

    // ---- composition ----

    [Fact]
    public void Composition_SumsAndPercentages()
    {
        var b = Baseline();
        Assert.Equal(75.0, b.WeightKg, 6);
        Assert.Equal(41.25, b.StructuralKg, 6);
        Assert.Equal(0.15, b.FatPct, 6);
        Assert.Equal(0.30, b.MusclePct, 6);
    }

    [Fact]
    public void BodyPlans_HumanHasSevenRegions_QuadrupedFourLegs()
    {
        Assert.Equal(7, Baseline().Regions.Count);
        var wolf = BodyState.Create(BodyKind.Quadruped, 40, 0.1, 0.4);
        Assert.Equal(4, wolf.Regions.Count(r => r.Name.Contains("Leg")));
        Assert.True(Body.GetCapacities(wolf).Moving > 0.9, "Quadruped legs must count toward Moving");
    }

    // ---- tissues ----

    [Fact]
    public void Protection_IsToughnessTimesCondition()
    {
        var t = new Tissue { Name = "Test", Kind = TissueKind.Muscle, Toughness = 10 };
        Assert.Equal(10.0, Body.Protection(t, DamageType.Blunt), 6);
        t.Condition = 0.5;
        Assert.Equal(5.0, Body.Protection(t, DamageType.Blunt), 6);
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

    // ---- capacities ----

    [Fact]
    public void HealthyBody_AllCapacitiesNearPerfect()
    {
        var c = Body.GetCapacities(Baseline());
        foreach (var v in new[] { c.Moving, c.Manipulation, c.Breathing, c.BloodPumping, c.Consciousness, c.Sight, c.Hearing, c.Digestion })
            Assert.True(v > 0.9, $"Capacity {v} should be near 1 for a healthy body: {c}");
    }

    [Fact]
    public void DestroyedHeart_StopsBloodPumping_AndKills()
    {
        var b = Baseline();
        OrganOf(b, "Heart").Condition = 0;
        Assert.True(Body.GetCapacities(b).BloodPumping < 0.9);
        Assert.False(Body.IsAlive(b));
    }

    [Fact]
    public void DestroyedLung_HalvesBreathingRoughly()
    {
        var b = Baseline();
        OrganOf(b, "Left Lung").Condition = 0;
        double breathing = Body.GetCapacities(b).Breathing;
        Assert.InRange(breathing, 0.3, 0.9);
    }

    [Fact]
    public void ChestTrauma_ReducesBreathing_AndPumping()
    {
        var b = Baseline();
        SetLayers(RegionOf(b, "Chest"), 0.1);
        var c = Body.GetCapacities(b);
        Assert.True(c.Breathing < 1.0 && c.BloodPumping < 1.0, c.ToString());
    }

    [Fact]
    public void LostLungs_AreDilutedByAveraging()
    {
        // Known weakness, kept from the old model: an organ's condition is averaged with every
        // other tissue in its region, so losing both lungs leaves two-thirds of your breathing.
        var b = Baseline();
        OrganOf(b, "Left Lung").Condition = 0;
        OrganOf(b, "Right Lung").Condition = 0;
        Assert.InRange(Body.GetCapacities(b).Breathing, 0.6, 0.7);
    }

    [Fact]
    public void DestroyedChest_NoBreath_NoConsciousness()
    {
        var b = Baseline();
        Destroy(RegionOf(b, "Chest"));
        var c = Body.GetCapacities(b);
        Assert.Equal(0, c.Breathing);
        Assert.Equal(0, c.Consciousness); // the breathing cascade
        Assert.False(Body.IsAlive(b));
    }

    [Fact]
    public void Unconscious_CannotMoveOrManipulate()
    {
        var b = Baseline();
        Destroy(RegionOf(b, "Head"));
        var c = Body.GetCapacities(b);
        Assert.Equal(0, c.Consciousness);
        Assert.True(c.Moving < 0.1 && c.Manipulation < 0.1, c.ToString());
    }

    [Fact]
    public void HalfDamagedArm_RoughlyHalvesItsManipulation()
    {
        var arm = RegionOf(Baseline(), "Left Arm");
        double healthy = Body.RegionCapacities(arm)[(int)Capacity.Manipulation];
        SetLayers(arm, 0.5);
        double damaged = Body.RegionCapacities(arm)[(int)Capacity.Manipulation];
        Assert.True(damaged < healthy);
        Assert.InRange(damaged, 0.2, 0.7);
    }

    [Fact]
    public void Effects_ModifyCapacities()
    {
        var b = Baseline();
        double before = Body.GetCapacities(b).Moving;
        Effects.Add(b, EffectKind.Hypothermia, 1.0); // -0.3 Moving
        Assert.Equal(before - 0.3, Body.GetCapacities(b).Moving, 6);
    }

    [Fact]
    public void DestroyedLegs_LeaveOnlyACrawl()
    {
        var b = Baseline();
        Destroy(RegionOf(b, "Left Leg"));
        Destroy(RegionOf(b, "Right Leg"));
        Assert.True(Body.GetCapacities(b).Moving < 0.15);
    }

    [Fact]
    public void DestroyedArms_LeaveAlmostNoManipulation()
    {
        var b = Baseline();
        Destroy(RegionOf(b, "Left Arm"));
        Destroy(RegionOf(b, "Right Arm"));
        Assert.True(Body.GetCapacities(b).Manipulation < 0.15);
    }

    [Fact]
    public void BloodLoss_CollapsesCirculationAtHalf()
    {
        var b = Baseline();
        b.BloodPct = 0.75;
        Assert.Equal(0.5, Body.GetCapacities(b).Consciousness, 6);
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
        Assert.True(Body.Speed(Custom(750, 0.15, 0.30)) < 1.0, "Large is slower");
        Assert.True(Body.Speed(Custom(7.5, 0.15, 0.30)) > baseline, "Small is faster");
    }

    [Fact]
    public void Speed_HeavyLoadSlows()
    {
        var b = Baseline();
        Assert.True(Body.Speed(b, new AbilityContext(EncumbrancePct: 1.0)) < Body.Speed(b));
    }

    [Fact]
    public void Vitality_HealthyIsOne_AndConsciousnessEffectsDragItDown()
    {
        Assert.Equal(1.0, Body.Vitality(Baseline()), 2);
        Assert.True(Body.Vitality(Custom(75, 0.20, 0.30)) > 0.9);

        // The scenario from a playtest: Hypothermia 98%, Tired 65%, plus 0.088 more = -0.87 consciousness.
        var b = Baseline();
        Effects.Add(b, EffectKind.Hypothermia, 0.98);
        Effects.Add(b, EffectKind.Tired, 0.65);
        Effects.Add(b, EffectKind.Shock, 0.22); // stands in for the old Fever 22%: -0.4 consciousness each
        Assert.True(Body.Vitality(b) < 0.20, $"Vitality {Body.Vitality(b):P0}");
    }

    [Fact]
    public void Perception_FullInDaylight_HearingCarriesItInTheDark()
    {
        var b = Baseline();
        Assert.Equal(1.0, Body.Perception(b), 2);
        double dark = Body.Perception(b, new AbilityContext(DarknessLevel: 1.0));
        Assert.Equal(1.0, dark, 2); // full darkness: hearing is all of it
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
        var result = Body.Damage(Baseline(), new DamageInfo(2, DamageType.Blunt, BodyTarget.Chest), TestRng());
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

    [Fact]
    public void Damage_EachLayerAbsorbsAtMostSeventyPercent()
    {
        var result = Body.Damage(Baseline(), new DamageInfo(100, DamageType.Blunt, BodyTarget.LeftArm), TestRng());
        Assert.True(result.DamageAbsorbed > 0 && result.DamageAbsorbed <= 100 * (1 - 0.3 * 0.3 * 0.3) + 1e-9);
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
    public void Armor_ReducesBluntByCushioning_ButNotInternal()
    {
        var bare = Baseline();
        var padded = Baseline();
        Body.Damage(bare, new DamageInfo(0.3, DamageType.Blunt, BodyTarget.LeftArm), TestRng());
        Body.Damage(padded, new DamageInfo(0.3, DamageType.Blunt, BodyTarget.LeftArm, ArmorCushioningLevel: 0.5), TestRng());
        Assert.True(RegionOf(padded, "Left Arm").Condition > RegionOf(bare, "Left Arm").Condition);

        var b = Baseline();
        Body.Damage(b, new DamageInfo(0.5, DamageType.Internal, BodyTarget.Heart, ArmorCushioningLevel: 0.8), TestRng());
        Assert.Equal(0.9, OrganOf(b, "Heart").Condition, 6); // 0.5 / toughness 5, unarmored
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
        Body.Heal(b, 0.4); // liver toughness 4: 0.4 heals 0.1 condition
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
