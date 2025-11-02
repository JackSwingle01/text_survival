namespace text_survival.Tests.Bodies;

public class TissueTests
{
    [Fact]
    public void GetProtection_BluntDamage_UsesFullMultiplier()
    {
        // Arrange
        var tissue = new Tissue("Test Muscle", toughness: 10.0)
        {
            Condition = 1.0,
            BluntMultiplier = 1.0
        };

        // Act
        double protection = tissue.GetProtection(DamageType.Blunt);

        // Assert
        // protection = Toughness * Condition * BluntMultiplier = 10 * 1.0 * 1.0 = 10
        Assert.Equal(10.0, protection, precision: 2);
    }

    [Fact]
    public void GetProtection_SharpDamage_UsesDifferentMultiplier()
    {
        // Arrange
        var tissue = new Tissue("Test Skin", toughness: 10.0)
        {
            Condition = 1.0,
            BluntMultiplier = 1.0,
            SharpMultiplier = 0.7 // Sharp cuts through easier
        };

        // Act
        double protection = tissue.GetProtection(DamageType.Sharp);

        // Assert
        // protection = 10 * 1.0 * 0.7 = 7
        Assert.Equal(7.0, protection, precision: 2);
    }

    [Fact]
    public void GetProtection_DamagedTissue_ReducedProtection()
    {
        // Arrange
        var tissue = new Tissue("Damaged Muscle", toughness: 10.0)
        {
            Condition = 0.5, // 50% damaged
            BluntMultiplier = 1.0
        };

        // Act
        double protection = tissue.GetProtection(DamageType.Blunt);

        // Assert
        // protection = 10 * 0.5 * 1.0 = 5
        Assert.Equal(5.0, protection, precision: 2);
    }

    [Fact]
    public void GetNaturalAbsorption_Heart_HighThreshold()
    {
        // Arrange
        var heart = new Organ(OrganNames.Heart, 5.0, new CapacityContainer { BloodPumping = 1.0 });

        // Act
        double absorption = heart.GetNaturalAbsorption(DamageType.Blunt);

        // Assert
        Assert.Equal(8.0, absorption, precision: 2); // Heart has threshold of 8
    }

    [Fact]
    public void GetNaturalAbsorption_Brain_HighThreshold()
    {
        // Arrange
        var brain = new Organ(OrganNames.Brain, 0.25, new CapacityContainer { Consciousness = 1.0 });

        // Act
        double absorption = brain.GetNaturalAbsorption(DamageType.Blunt);

        // Assert
        Assert.Equal(6.0, absorption, precision: 2); // Brain has threshold of 6
    }

    [Fact]
    public void GetNaturalAbsorption_Lung_HighThreshold()
    {
        // Arrange
        var lung = new Organ(OrganNames.LeftLung, 3.0, new CapacityContainer { Breathing = 0.5 });

        // Act
        double absorption = lung.GetNaturalAbsorption(DamageType.Blunt);

        // Assert
        Assert.Equal(10.0, absorption, precision: 2); // Lungs have threshold of 10
    }

    [Fact]
    public void GetNaturalAbsorption_SharpDamage_ReducedThreshold()
    {
        // Arrange
        var heart = new Organ(OrganNames.Heart, 5.0, new CapacityContainer { BloodPumping = 1.0 });

        // Act
        double absorption = heart.GetNaturalAbsorption(DamageType.Sharp);

        // Assert
        // Sharp damage: threshold * 0.4 = 8 * 0.4 = 3.2
        Assert.Equal(3.2, absorption, precision: 2);
    }

    [Fact]
    public void GetNaturalAbsorption_PierceDamage_MinimalThreshold()
    {
        // Arrange
        var heart = new Organ(OrganNames.Heart, 5.0, new CapacityContainer { BloodPumping = 1.0 });

        // Act
        double absorption = heart.GetNaturalAbsorption(DamageType.Pierce);

        // Assert
        // Pierce damage: threshold * 0.2 = 8 * 0.2 = 1.6
        Assert.Equal(1.6, absorption, precision: 2);
    }

    [Fact]
    public void GetNaturalAbsorption_GenericTissue_LowThreshold()
    {
        // Arrange
        var muscle = new Tissue("Generic Muscle");

        // Act
        double absorption = muscle.GetNaturalAbsorption(DamageType.Blunt);

        // Assert
        // Generic tissues have threshold of 1
        Assert.Equal(1.0, absorption, precision: 2);
    }
}
