using text_survival.Actions.Tensions;
using text_survival.Actors.Animals;

namespace text_survival.Tests.Actions;

public class TensionRegistryTests
{
    [Fact]
    public void AddTension_NewTension_AddsSuccessfully()
    {
        // Arrange
        var registry = new TensionRegistry();
        var tension = ActiveTension.WoundedPrey(0.5, AnimalType.Wolf);

        // Act
        registry.AddTension(tension);

        // Assert
        Assert.True(registry.HasTension("WoundedPrey"));
    }

    [Fact]
    public void AddTension_DuplicateType_UpdatesToHigherSeverity()
    {
        // Arrange
        var registry = new TensionRegistry();
        registry.AddTension(ActiveTension.WoundedPrey(0.3, AnimalType.Wolf));

        // Act - add same type with higher severity
        registry.AddTension(ActiveTension.WoundedPrey(0.8, AnimalType.Bear));

        // Assert - severity updates but original tension properties remain
        var tension = registry.GetTension("WoundedPrey");
        Assert.NotNull(tension);
        Assert.Equal(0.8, tension.Severity);
        // Note: AnimalType remains AnimalType.Wolf as only severity is updated
        Assert.Equal(AnimalType.Wolf, tension.AnimalType);
    }

    [Fact]
    public void GetTension_ExistingType_ReturnsTension()
    {
        // Arrange
        var registry = new TensionRegistry();
        registry.AddTension(ActiveTension.WoundedPrey(0.6, AnimalType.Wolf));

        // Act
        var tension = registry.GetTension("WoundedPrey");

        // Assert
        Assert.NotNull(tension);
        Assert.Equal("WoundedPrey", tension.Type);
        Assert.Equal(0.6, tension.Severity);
    }

    [Fact]
    public void GetTension_NonExistingType_ReturnsNull()
    {
        // Arrange
        var registry = new TensionRegistry();

        // Act
        var tension = registry.GetTension("WoundedPrey");

        // Assert
        Assert.Null(tension);
    }

    [Fact]
    public void HasTensionAbove_AboveThreshold_ReturnsTrue()
    {
        // Arrange
        var registry = new TensionRegistry();
        registry.AddTension(ActiveTension.WoundedPrey(0.7));

        // Act & Assert
        Assert.True(registry.HasTensionAbove("WoundedPrey", 0.5));
        Assert.True(registry.HasTensionAbove("WoundedPrey", 0.6));
        Assert.False(registry.HasTensionAbove("WoundedPrey", 0.7));
        Assert.False(registry.HasTensionAbove("WoundedPrey", 0.8));
    }

    [Fact]
    public void ResolveTension_ExistingTension_RemovesTension()
    {
        // Arrange
        var registry = new TensionRegistry();
        registry.AddTension(ActiveTension.WoundedPrey(0.5));

        // Act
        registry.ResolveTension("WoundedPrey");

        // Assert
        Assert.False(registry.HasTension("WoundedPrey"));
    }

    [Fact]
    public void EscalateTension_ExistingTension_IncreasesSeverity()
    {
        // Arrange
        var registry = new TensionRegistry();
        registry.AddTension(ActiveTension.WoundedPrey(0.3));

        // Act
        registry.EscalateTension("WoundedPrey", 0.2);

        // Assert
        var tension = registry.GetTension("WoundedPrey");
        Assert.NotNull(tension);
        Assert.Equal(0.5, tension.Severity, precision: 2);
    }

    [Fact]
    public void EscalateTension_CapsAtOne()
    {
        // Arrange
        var registry = new TensionRegistry();
        registry.AddTension(ActiveTension.WoundedPrey(0.9));

        // Act
        registry.EscalateTension("WoundedPrey", 0.5);

        // Assert
        var tension = registry.GetTension("WoundedPrey");
        Assert.NotNull(tension);
        Assert.Equal(1.0, tension.Severity);
    }

    [Fact]
    public void Update_DecaysTensions()
    {
        // Arrange
        var registry = new TensionRegistry();
        var tension = ActiveTension.WoundedPrey(0.5);
        registry.AddTension(tension);
        double initialSeverity = tension.Severity;

        // Act - simulate 60 minutes (1 hour)
        registry.Update(60, atCamp: false);

        // Assert - severity should have decreased
        Assert.True(tension.Severity < initialSeverity);
    }

    [Fact]
    public void Update_AtCamp_DecaysTensionsThatDecayAtCamp()
    {
        // Arrange
        var registry = new TensionRegistry();
        var woundedPrey = ActiveTension.WoundedPrey(0.5); // DecaysAtCamp = true
        registry.AddTension(woundedPrey);
        double initialSeverity = woundedPrey.Severity;

        // Act - simulate 60 minutes at camp
        registry.Update(60, atCamp: true);

        // Assert - WoundedPrey decays at camp
        Assert.True(woundedPrey.Severity < initialSeverity);
    }

    [Fact]
    public void Update_RemovesExpiredTensions()
    {
        // Arrange
        var registry = new TensionRegistry();
        var tension = ActiveTension.WoundedPrey(0.01); // Very low severity
        registry.AddTension(tension);

        // Act - simulate enough time to fully decay
        registry.Update(120, atCamp: false);

        // Assert - tension should be removed when severity hits 0
        Assert.False(registry.HasTension("WoundedPrey"));
    }

    [Fact]
    public void ActiveTension_WoundedPrey_HasCorrectDefaults()
    {
        // Act
        var tension = ActiveTension.WoundedPrey(0.5, AnimalType.Wolf);

        // Assert
        Assert.Equal("WoundedPrey", tension.Type);
        Assert.Equal(0.5, tension.Severity);
        Assert.Equal(AnimalType.Wolf, tension.AnimalType);
        Assert.True(tension.DecaysAtCamp);
        Assert.True(tension.DecayPerHour > 0);
    }

    [Fact]
    public void ActiveTension_SmokeSpotted_HasCorrectDefaults()
    {
        // Act
        var tension = ActiveTension.SmokeSpotted(0.5, "north");

        // Assert
        Assert.Equal("SmokeSpotted", tension.Type);
        Assert.Equal(0.5, tension.Severity);
        Assert.Equal("north", tension.Direction);
        Assert.False(tension.DecaysAtCamp); // Smoke doesn't decay at camp
    }

    [Fact]
    public void ActiveTension_Infested_HasCorrectDefaults()
    {
        // Act
        var tension = ActiveTension.Infested(0.5);

        // Assert
        Assert.Equal("Infested", tension.Type);
        Assert.False(tension.DecaysAtCamp); // Infestation doesn't decay at camp
    }
}
