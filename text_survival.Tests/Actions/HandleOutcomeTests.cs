using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Tensions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Effects;

namespace text_survival.Tests.Actions;

public class HandleOutcomeTests
{
    private static GameContext CreateTestContext()
    {
        // Create a minimal GameContext for testing
        var player = new Player();
        var zone = new Zone("Test Zone", "A test zone");
        var location = new Location("Test Location", "[test]", zone, 5);
        zone.Graph.Add(location);
        var camp = new Camp(location);

        return new GameContext(player, camp);
    }

    [Fact]
    public void HandleOutcome_DamageTool_ReducesDurability()
    {
        // Arrange
        var ctx = CreateTestContext();
        var knife = Tool.Knife("Test Knife");
        knife.Durability = 10;
        ctx.Inventory.Tools.Add(knife);

        var outcome = new EventResult("Test")
        {
            DamageTool = new ToolDamage(ToolType.Knife, 3)
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.Equal(7, knife.Durability);
        Assert.False(knife.IsBroken);
    }

    [Fact]
    public void HandleOutcome_DamageTool_CanBreakTool()
    {
        // Arrange
        var ctx = CreateTestContext();
        var knife = Tool.Knife("Test Knife");
        knife.Durability = 2;
        ctx.Inventory.Tools.Add(knife);

        var outcome = new EventResult("Test")
        {
            DamageTool = new ToolDamage(ToolType.Knife, 5)
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.Equal(0, knife.Durability);
        Assert.True(knife.IsBroken);
    }

    [Fact]
    public void HandleOutcome_BreakTool_SetsDurabilityToZero()
    {
        // Arrange
        var ctx = CreateTestContext();
        var spear = Tool.Spear("Test Spear");
        spear.Durability = 10;
        ctx.Inventory.Tools.Add(spear);

        var outcome = new EventResult("Test")
        {
            BreakTool = ToolType.Spear
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.Equal(0, spear.Durability);
        Assert.True(spear.IsBroken);
    }

    [Fact]
    public void HandleOutcome_DamageClothing_ReducesInsulation()
    {
        // Arrange
        var ctx = CreateTestContext();
        var chest = Equipment.FurChestWrap("Test Wrap");
        double originalInsulation = chest.Insulation;
        ctx.Inventory.Chest = chest;

        var outcome = new EventResult("Test")
        {
            DamageClothing = new ClothingDamage(EquipSlot.Chest, 0.1)
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.Equal(originalInsulation - 0.1, chest.Insulation, precision: 2);
    }

    [Fact]
    public void HandleOutcome_DamageClothing_CapsAtZero()
    {
        // Arrange
        var ctx = CreateTestContext();
        var chest = Equipment.FurChestWrap("Test Wrap");
        chest.Insulation = 0.05;
        ctx.Inventory.Chest = chest;

        var outcome = new EventResult("Test")
        {
            DamageClothing = new ClothingDamage(EquipSlot.Chest, 0.5)
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.Equal(0.0, chest.Insulation);
    }

    [Fact]
    public void HandleOutcome_CreatesTension_AddsTensionToRegistry()
    {
        // Arrange
        var ctx = CreateTestContext();
        var outcome = new EventResult("Test")
        {
            CreatesTension = new TensionCreation("Stalked", 0.5, AnimalType: "wolf")
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.True(ctx.Tensions.HasTension("Stalked"));
        var tension = ctx.Tensions.GetTension("Stalked");
        Assert.NotNull(tension);
        Assert.Equal(0.5, tension.Severity);
    }

    [Fact]
    public void HandleOutcome_ResolvesTension_RemovesTension()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.Stalked(0.5));

        var outcome = new EventResult("Test")
        {
            ResolvesTension = "Stalked"
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.False(ctx.Tensions.HasTension("Stalked"));
    }

    [Fact]
    public void HandleOutcome_EscalateTension_IncreasesSeverity()
    {
        // Arrange
        var ctx = CreateTestContext();
        ctx.Tensions.AddTension(ActiveTension.Stalked(0.3));

        var outcome = new EventResult("Test")
        {
            EscalateTension = ("Stalked", 0.2)
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        var tension = ctx.Tensions.GetTension("Stalked");
        Assert.NotNull(tension);
        Assert.Equal(0.5, tension.Severity, precision: 2);
    }

    [Fact]
    public void HandleOutcome_Effects_AddsBuff()
    {
        // Arrange
        var ctx = CreateTestContext();
        var buff = EffectFactory.Focused(0.5);

        var outcome = new EventResult("Test")
        {
            Effects = [buff]
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        var effects = ctx.player.EffectRegistry.GetAll();
        Assert.Contains(effects, e => e.EffectKind == "Focused");
    }

    [Fact]
    public void HandleOutcome_Effects_AddsNegativeEffect()
    {
        // Arrange
        var ctx = CreateTestContext();
        var effect = EffectFactory.Fear(0.5);

        var outcome = new EventResult("Test")
        {
            Effects = [effect]
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        var effects = ctx.player.EffectRegistry.GetAll();
        Assert.Contains(effects, e => e.EffectKind == "Fear");
    }

    // Note: TimeAddedMinutes is display-only; time tracking happens at the game flow level,
    // not within HandleOutcome. The "+X minutes" display informs the player but doesn't
    // modify expedition time since events occur during normal update cycles.

    [Fact]
    public void HandleOutcome_MissingTool_DoesNotThrow()
    {
        // Arrange
        var ctx = CreateTestContext();
        // No knife in inventory

        var outcome = new EventResult("Test")
        {
            DamageTool = new ToolDamage(ToolType.Knife, 3)
        };

        // Act & Assert - should not throw
        var exception = Record.Exception(() => GameEventRegistry.HandleOutcome(ctx, outcome));
        Assert.Null(exception);
    }

    [Fact]
    public void HandleOutcome_MissingEquipment_DoesNotThrow()
    {
        // Arrange
        var ctx = CreateTestContext();
        // No chest equipment

        var outcome = new EventResult("Test")
        {
            DamageClothing = new ClothingDamage(EquipSlot.Chest, 0.1)
        };

        // Act & Assert - should not throw
        var exception = Record.Exception(() => GameEventRegistry.HandleOutcome(ctx, outcome));
        Assert.Null(exception);
    }
}
