using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Tensions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Effects;
using text_survival.Actors.Animals;

namespace text_survival.Tests.Actions;

public class HandleOutcomeTests
{
    private static GameContext CreateTestContext()
    {
        // Create a minimal GameContext for testing
        var player = new Player();
        var weather = new Weather(-10);
        var campLocation = new Location("Test Location", "[test]", weather, 5);

        var ctx = new GameContext(player, campLocation, weather);
        return ctx;
    }

    [Fact]
    public void HandleOutcome_DamageTool_ReducesDurability()
    {
        // Arrange
        var ctx = CreateTestContext();
        var knife = Gear.Knife("Test Knife");
        knife.Durability = 10;
        ctx.Inventory.Tools.Add(knife);

        var outcome = new EventResult("Test")
        {
            DamageGear = new GearDamage(GearCategory.Tool, 3, ToolType: ToolType.Knife)
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
        var knife = Gear.Knife("Test Knife");
        knife.Durability = 2;
        ctx.Inventory.Tools.Add(knife);

        var outcome = new EventResult("Test")
        {
            DamageGear = new GearDamage(GearCategory.Tool, 5, ToolType: ToolType.Knife)
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
        var spear = Gear.Spear("Test Spear");
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
    public void HandleOutcome_DamageClothing_ReducesDurability()
    {
        // Arrange
        var ctx = CreateTestContext();
        var chest = Gear.FurChestWrap("Test Wrap");
        chest.Durability = 100;
        double originalInsulation = chest.Insulation;
        ctx.Inventory.Chest = chest;

        var outcome = new EventResult("Test")
        {
            DamageGear = new GearDamage(GearCategory.Equipment, 10, Slot: EquipSlot.Chest)
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.Equal(90, chest.Durability);
        Assert.True(chest.Insulation < originalInsulation); // Insulation degrades with durability
    }

    [Fact]
    public void HandleOutcome_DamageClothing_CapsAtZero()
    {
        // Arrange
        var ctx = CreateTestContext();
        var chest = Gear.FurChestWrap("Test Wrap");
        chest.Durability = 5;
        double originalInsulation = chest.Insulation;
        ctx.Inventory.Chest = chest;

        var outcome = new EventResult("Test")
        {
            DamageGear = new GearDamage(GearCategory.Equipment, 10, Slot: EquipSlot.Chest)
        };

        // Act
        GameEventRegistry.HandleOutcome(ctx, outcome);

        // Assert
        Assert.Equal(0, chest.Durability); // Durability capped at zero
        Assert.Equal(0.0, chest.Insulation); // Insulation = BaseInsulation * 0
    }

    [Fact]
    public void HandleOutcome_CreatesTension_AddsTensionToRegistry()
    {
        // Arrange
        var ctx = CreateTestContext();
        var outcome = new EventResult("Test")
        {
            CreatesTension = new TensionCreation("Stalked", 0.5, AnimalType: AnimalType.Wolf)
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
            DamageGear = new GearDamage(GearCategory.Tool, 3, ToolType: ToolType.Knife)
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
            DamageGear = new GearDamage(GearCategory.Equipment, 5, Slot: EquipSlot.Chest)
        };

        // Act & Assert - should not throw
        var exception = Record.Exception(() => GameEventRegistry.HandleOutcome(ctx, outcome));
        Assert.Null(exception);
    }
}
