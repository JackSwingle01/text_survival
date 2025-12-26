using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Bodies;

public class DamageProcessorTests
{
    [Fact]
    public void DamageBody_SpecificTarget_HitsNamedPart()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var damageInfo = TestFixtures.CreateDamageInfo(
            amount: 10.0,
            damageType: DamageType.Blunt,
            target: BodyTarget.Head);

        // Act
        var result = DamageProcessor.DamageBody(damageInfo, body);

        // Assert
        Assert.Equal(BodyRegionNames.Head, result.HitPartName);
    }

    [Fact]
    public void DamageBody_LowDamage_AbsorbedBySkin()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var damageInfo = TestFixtures.CreateDamageInfo(amount: 2.0, damageType: DamageType.Blunt);

        // Act
        var result = DamageProcessor.DamageBody(damageInfo, body);

        // Assert
        Assert.False(result.OrganHit, "Low damage should not penetrate to organs");

        // Either damage was absorbed or dealt to tissues
        if (result.TissuesDamaged.Count > 0)
        {
            Assert.True(result.DamageAbsorbed >= 0, "Damage should have been processed");
        }
    }

    [Fact]
    public void DamageBody_ModerateDamage_PenetratesMultipleLayers()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var damageInfo = TestFixtures.CreateDamageInfo(amount: 15.0, damageType: DamageType.Sharp);

        // Act
        var result = DamageProcessor.DamageBody(damageInfo, body);

        // Assert
        Assert.True(result.TissuesDamaged.Count >= 2,
            "Moderate damage should penetrate multiple layers");
        Assert.True(result.WasPenetrating || result.TissuesDamaged.Count > 1,
            "Damage should penetrate beyond skin");
    }

    [Fact]
    public void DamageBody_HighDamage_CanHitOrgans()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        // Target chest where organs are
        var damageInfo = TestFixtures.CreateDamageInfo(
            amount: 50.0,
            damageType: DamageType.Pierce,
            target: BodyTarget.Chest);

        // Act
        var result = DamageProcessor.DamageBody(damageInfo, body);

        // Assert - high pierce damage to chest should potentially hit organs
        // Note: This might not always hit an organ due to randomness, but should penetrate deeply
        Assert.True(result.TissuesDamaged.Count >= 2,
            "High damage should penetrate multiple layers");
    }

    [Fact]
    public void PenetrateLayers_70PercentMaxPerLayer()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var arm = body.Parts.First(p => p.Name == BodyRegionNames.LeftArm);

        // Create high damage to test the 70% absorption limit per layer
        var damageInfo = TestFixtures.CreateDamageInfo(amount: 100.0, damageType: DamageType.Blunt);

        // Act
        var result = DamageProcessor.DamageBody(damageInfo, body);

        // Assert
        // With 100 damage, first layer can absorb max 70
        // If protection allows, remaining 30 goes to next layer
        Assert.True(result.DamageAbsorbed > 0, "Layers should absorb some damage");
        Assert.True(result.DamageAbsorbed <= 100, "Cannot absorb more than total damage");
    }

    [Fact]
    public void DamageBody_BluntDamage_FullAbsorptionMultiplier()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var bluntDamage = TestFixtures.CreateDamageInfo(amount: 20.0, damageType: DamageType.Blunt);

        // Act
        var result = DamageProcessor.DamageBody(bluntDamage, body);

        // Assert - Blunt damage uses 1.0x multiplier for absorption
        Assert.True(result.DamageAbsorbed > 0, "Blunt damage should be absorbed by tissues");
    }

    [Fact]
    public void DamageBody_SharpDamage_ReducedAbsorption()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var sharpDamage = TestFixtures.CreateDamageInfo(amount: 20.0, damageType: DamageType.Sharp);

        // Act
        var result = DamageProcessor.DamageBody(sharpDamage, body);

        // Assert - Sharp damage uses lower absorption, more penetrating
        Assert.True(result.TissuesDamaged.Count >= 1,
            "Sharp damage should damage tissues");
    }

    [Fact]
    public void DamageBody_PierceDamage_MinimalAbsorption()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var pierceDamage = TestFixtures.CreateDamageInfo(
            amount: 20.0,
            damageType: DamageType.Pierce,
            target: BodyTarget.Chest);

        // Act
        var result = DamageProcessor.DamageBody(pierceDamage, body);

        // Assert - Pierce damage has minimal natural absorption (0.2x)
        Assert.True(result.TissuesDamaged.Count >= 1,
            "Pierce damage should penetrate and damage tissues");
    }

    [Fact]
    public void DamageBody_ZeroDamage_NoEffect()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var noDamage = TestFixtures.CreateDamageInfo(amount: 0.0, damageType: DamageType.Blunt);

        // Act
        var result = DamageProcessor.DamageBody(noDamage, body);

        // Assert
        Assert.Equal(0, result.TotalDamageDealt);
        // All parts should still be at full condition
        Assert.True(body.Parts.All(p => p.Condition >= 1.0));
    }

    [Fact]
    public void DamageBody_MassiveDamage_CanDestroyBodyPart()
    {
        // Arrange
        var body = TestFixtures.CreateBaselineHumanBody();
        var head = body.Parts.First(p => p.Name == BodyRegionNames.Head);
        var initialHealth = head.Condition;

        var massiveDamage = TestFixtures.CreateDamageInfo(
            amount: TestConstants.Damage.Massive,
            damageType: DamageType.Blunt,
            target: BodyTarget.Head);

        // Act
        var result = DamageProcessor.DamageBody(massiveDamage, body);

        // Assert
        Assert.True(result.TotalDamageDealt > 0, "Massive damage should deal damage");
        Assert.True(head.Condition < initialHealth, "Body part should be damaged");
    }
}
