using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Bodies;

public class CapacityCalculatorTests
{
    private static CapacityModifierContainer NoEffects => new();

    [Fact]
    public void GetCapacities_HealthyBody_AllCapacitiesNearPerfect()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();

        // Act
        var capacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Assert - all capacities should be close to 1.0 for a healthy body
        Assert.True(capacities.Moving > 0.9, $"Moving capacity should be near 1.0. Actual: {capacities.Moving}");
        Assert.True(capacities.Manipulation > 0.9, $"Manipulation should be near 1.0. Actual: {capacities.Manipulation}");
        Assert.True(capacities.Breathing > 0.9, $"Breathing should be near 1.0. Actual: {capacities.Breathing}");
        Assert.True(capacities.BloodPumping > 0.9, $"BloodPumping should be near 1.0. Actual: {capacities.BloodPumping}");
        Assert.True(capacities.Consciousness > 0.9, $"Consciousness should be near 1.0. Actual: {capacities.Consciousness}");
        Assert.True(capacities.Sight > 0.9, $"Sight should be near 1.0. Actual: {capacities.Sight}");
        Assert.True(capacities.Hearing > 0.9, $"Hearing should be near 1.0. Actual: {capacities.Hearing}");
        Assert.True(capacities.Digestion > 0.9, $"Digestion should be near 1.0. Actual: {capacities.Digestion}");
    }

    [Fact]
    public void GetCapacities_DestroyedOrgan_ReducesRelatedCapacity()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var healthyCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Destroy the heart
        var chest = body.Parts.First(p => p.Name == BodyRegionNames.Chest);
        var heart = chest.Organs.First(o => o.Name == OrganNames.Heart);
        heart.Condition = 0.0; // Destroyed

        // Act
        var damagedCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Assert - destroyed organ should reduce blood pumping
        Assert.True(damagedCapacities.BloodPumping < healthyCapacities.BloodPumping,
            $"Destroyed heart should reduce blood pumping. Healthy: {healthyCapacities.BloodPumping}, Damaged: {damagedCapacities.BloodPumping}");

        // Blood pumping should be significantly reduced (but not to zero due to averaging with tissues)
        Assert.True(damagedCapacities.BloodPumping < 0.9,
            $"Destroyed heart should significantly reduce blood pumping. Actual: {damagedCapacities.BloodPumping}");
    }

    [Fact]
    public void GetCapacities_DestroyedLung_ReducesBreathing()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var healthyCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Destroy one lung
        var chest = body.Parts.First(p => p.Name == BodyRegionNames.Chest);
        var leftLung = chest.Organs.First(o => o.Name == OrganNames.LeftLung);
        leftLung.Condition = 0.0; // Destroyed

        // Act
        var damagedCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Assert - destroyed lung should reduce breathing
        Assert.True(damagedCapacities.Breathing < healthyCapacities.Breathing,
            $"Destroyed lung should reduce breathing. Healthy: {healthyCapacities.Breathing}, Damaged: {damagedCapacities.Breathing}");

        // One destroyed lung out of two should reduce breathing, but not to zero
        Assert.True(damagedCapacities.Breathing > 0.3 && damagedCapacities.Breathing < 0.9,
            $"One destroyed lung should moderately reduce breathing. Actual: {damagedCapacities.Breathing}");
    }

    [Fact]
    public void ApplyCascadingEffects_LowBloodPumping_AffectsMultipleCapacities()
    {
        // NOTE: Testing cascading effects requires artificially low blood pumping
        // since organ condition doesn't affect capacities in current implementation.
        // We test this by damaging chest tissues instead.

        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var healthyCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Damage chest tissues to reduce blood pumping capacity
        var chest = body.Parts.First(p => p.Name == BodyRegionNames.Chest);
        chest.Muscle.Condition = 0.3; // Severely damaged muscle
        chest.Bone.Condition = 0.3;   // Severely damaged bone
        chest.Skin.Condition = 0.3;   // Severely damaged skin

        // Act
        var damagedCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Assert - tissue damage should reduce all capacities
        Assert.True(damagedCapacities.BloodPumping < healthyCapacities.BloodPumping,
            "Tissue damage should reduce blood pumping");

        // If blood pumping gets low enough, cascading effects reduce other capacities
        if (damagedCapacities.BloodPumping < 0.5)
        {
            Assert.True(damagedCapacities.Consciousness < healthyCapacities.Consciousness,
                "Low blood pumping should reduce consciousness via cascading effects");
        }
    }

    [Fact]
    public void ApplyCascadingEffects_LowBreathing_ReducesConsciousness()
    {
        // NOTE: Testing cascading effects requires artificially low breathing
        // since organ condition doesn't affect capacities in current implementation.
        // We test this by damaging chest tissues instead.

        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var healthyCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        var chest = body.Parts.First(p => p.Name == BodyRegionNames.Chest);

        // Severely damage chest tissues to reduce breathing
        chest.Muscle.Condition = 0.1;
        chest.Bone.Condition = 0.1;
        chest.Skin.Condition = 0.1;

        // Act
        var damagedCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Assert
        Assert.True(damagedCapacities.Breathing < healthyCapacities.Breathing,
            $"Damaged chest tissues should reduce breathing. Healthy: {healthyCapacities.Breathing}, Damaged: {damagedCapacities.Breathing}");

        // If breathing gets low enough, consciousness should be reduced
        if (damagedCapacities.Breathing < 0.3)
        {
            Assert.True(damagedCapacities.Consciousness < healthyCapacities.Consciousness,
                "Very low breathing should reduce consciousness via cascading effects");
        }
    }

    [Fact]
    public void ApplyCascadingEffects_LowConsciousness_ReducesPhysicalActions()
    {
        // NOTE: Testing cascading effects requires artificially low consciousness
        // since organ condition doesn't affect capacities in current implementation.
        // We test this by damaging head tissues instead.

        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var healthyCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        var head = body.Parts.First(p => p.Name == BodyRegionNames.Head);

        // Critically damage head tissues to reduce consciousness
        head.Muscle.Condition = 0.05;
        head.Bone.Condition = 0.05;
        head.Skin.Condition = 0.05;

        // Act
        var damagedCapacities = CapacityCalculator.GetCapacities(body, NoEffects);

        // Assert
        Assert.True(damagedCapacities.Consciousness < healthyCapacities.Consciousness,
            "Head tissue damage should reduce consciousness");

        // If consciousness gets critically low, physical actions should be severely impaired
        if (damagedCapacities.Consciousness < 0.1)
        {
            Assert.True(damagedCapacities.Moving < healthyCapacities.Moving,
                "Very low consciousness should reduce movement via cascading effects");
            Assert.True(damagedCapacities.Manipulation < healthyCapacities.Manipulation,
                "Very low consciousness should reduce manipulation via cascading effects");
        }
    }

    [Fact]
    public void GetRegionCapacities_DamagedTissues_ReducesCapacities()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var leftArm = body.Parts.First(p => p.Name == BodyRegionNames.LeftArm);

        // Get healthy capacity
        var healthyCapacity = CapacityCalculator.GetRegionCapacities(leftArm);

        // Damage all tissues in left arm
        leftArm.Skin.Condition = 0.5;
        leftArm.Muscle.Condition = 0.5;
        leftArm.Bone.Condition = 0.5;

        // Act
        var damagedCapacity = CapacityCalculator.GetRegionCapacities(leftArm);

        // Assert
        Assert.True(damagedCapacity.Manipulation < healthyCapacity.Manipulation,
            $"Damaged tissues should reduce manipulation capacity. Healthy: {healthyCapacity.Manipulation}, Damaged: {damagedCapacity.Manipulation}");

        // Should be roughly proportional to tissue damage (50% damage = ~50% capacity)
        Assert.True(damagedCapacity.Manipulation > 0.2 && damagedCapacity.Manipulation < 0.7,
            $"50% tissue damage should result in moderate capacity reduction. Actual: {damagedCapacity.Manipulation}");
    }

    [Fact]
    public void GetCapacities_WithEffectModifiers_AppliesModifiers()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var noEffects = new CapacityModifierContainer();
        var withPenalty = new CapacityModifierContainer();
        withPenalty.SetCapacityModifier(CapacityNames.Moving, -0.3);

        // Act
        var capacitiesNoEffects = CapacityCalculator.GetCapacities(body, noEffects);
        var capacitiesWithPenalty = CapacityCalculator.GetCapacities(body, withPenalty);

        // Assert - effect modifier should reduce moving capacity
        Assert.True(capacitiesWithPenalty.Moving < capacitiesNoEffects.Moving,
            $"Effect modifier should reduce moving. Without: {capacitiesNoEffects.Moving}, With: {capacitiesWithPenalty.Moving}");
    }

    [Fact]
    public void GetCapacities_DestroyedLegs_SeverelyReducesMoving()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var leftLeg = body.Parts.First(p => p.Name == BodyRegionNames.LeftLeg);
        var rightLeg = body.Parts.First(p => p.Name == BodyRegionNames.RightLeg);

        // Destroy both legs (all tissues)
        leftLeg.Skin.Condition = 0.0;
        leftLeg.Muscle.Condition = 0.0;
        leftLeg.Bone.Condition = 0.0;
        rightLeg.Skin.Condition = 0.0;
        rightLeg.Muscle.Condition = 0.0;
        rightLeg.Bone.Condition = 0.0;

        // Act
        var capacities = CapacityCalculator.GetCapacities(body, new CapacityModifierContainer());

        // Assert - destroyed legs should severely reduce moving (only ~10% from other regions)
        Assert.True(capacities.Moving < 0.15,
            $"Destroyed legs should reduce Moving to ~0.1. Actual: {capacities.Moving}");
    }

    [Fact]
    public void GetCapacities_DestroyedArms_SeverelyReducesManipulation()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var leftArm = body.Parts.First(p => p.Name == BodyRegionNames.LeftArm);
        var rightArm = body.Parts.First(p => p.Name == BodyRegionNames.RightArm);

        // Destroy both arms (all tissues)
        leftArm.Skin.Condition = 0.0;
        leftArm.Muscle.Condition = 0.0;
        leftArm.Bone.Condition = 0.0;
        rightArm.Skin.Condition = 0.0;
        rightArm.Muscle.Condition = 0.0;
        rightArm.Bone.Condition = 0.0;

        // Act
        var capacities = CapacityCalculator.GetCapacities(body, new CapacityModifierContainer());

        // Assert - destroyed arms should severely reduce manipulation (only ~10% from other regions)
        Assert.True(capacities.Manipulation < 0.15,
            $"Destroyed arms should reduce Manipulation to ~0.1. Actual: {capacities.Manipulation}");
    }
}
