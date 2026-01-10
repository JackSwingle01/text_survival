using text_survival.Actions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actors.Animals;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using Xunit;

namespace text_survival.Tests.Environments;

/// <summary>
/// Tests for the butchering inventory overflow fix - ensures items left behind
/// remain on the carcass for later butchering instead of disappearing.
/// </summary>
public class CarcassFeatureButcheringOverflowTests
{
    private static GameContext CreateTestContext()
    {
        var player = new Player();
        var weather = new Weather(-10);
        var camp = new Location("Test Camp", "[test]", weather, 5);

        player.CurrentLocation = camp;

        var ctx = new GameContext(player, camp, weather);
        return ctx;
    }
    [Fact]
    public void Butchering_WhenInventoryFull_LeavesMaterialsOnCarcass()
    {
        // Arrange: Create a caribou carcass (significant meat)
        var caribou = AnimalFactory.FromType(AnimalType.Caribou, null!, null!)!;
        var carcass = new CarcassFeature(caribou);

        // Player inventory with very limited capacity (5kg)
        var ctx = CreateTestContext();
        ctx.Inventory.MaxWeightKg = 5.0;

        // Ensure cutting tool available
        ctx.Inventory.Tools.Add(Gear.Knife());

        // Record initial yields
        double initialMeat = carcass.MeatRemainingKg;
        double initialBone = carcass.BoneRemainingKg;
        double initialHide = carcass.HideRemainingKg;
        double initialSinew = carcass.SinewRemainingKg;
        double initialFat = carcass.FatRemainingKg;
        double totalInitialYield = carcass.GetTotalRemainingKg();

        Assert.True(totalInitialYield > 10, "Caribou should have >10kg total yield");
        Assert.True(initialMeat > 5, "Caribou should have >5kg meat (more than capacity)");

        // Act: Butcher for a short time (enough to generate some yield, but not everything)
        bool hasKnife = ctx.Inventory.HasCuttingTool;
        var yield = carcass.Harvest(
            minutes: 20,  // Partial butchering
            hasCuttingTool: hasKnife,
            manipulationImpaired: false,
            mode: ButcheringMode.Careful
        );

        // Simulate what ButcherStrategy.Execute does: add to inventory, get leftovers
        var leftovers = ctx.Inventory.CombineWithCapacity(yield);

        // Restore leftovers to carcass
        if (!leftovers.IsEmpty)
        {
            carcass.RestoreYields(leftovers);
        }

        // Assert: Verify nothing disappeared
        double finalMeat = carcass.MeatRemainingKg;
        double finalBone = carcass.BoneRemainingKg;
        double finalHide = carcass.HideRemainingKg;
        double finalSinew = carcass.SinewRemainingKg;
        double finalFat = carcass.FatRemainingKg;
        double totalFinalYield = carcass.GetTotalRemainingKg();

        double inventoryMeat = ctx.Inventory.Weight(Resource.RawMeat);
        double inventoryBone = ctx.Inventory.Weight(Resource.Bone);
        double inventoryHide = ctx.Inventory.Weight(Resource.Hide);
        double inventorySinew = ctx.Inventory.Weight(Resource.Sinew);
        double inventoryFat = ctx.Inventory.Weight(Resource.RawFat);

        // Total materials should be conserved: initial = (carcass remaining) + (player inventory)
        double totalMeat = finalMeat + inventoryMeat;
        double totalBone = finalBone + inventoryBone;
        double totalHide = finalHide + inventoryHide;
        double totalSinew = finalSinew + inventorySinew;
        double totalFat = finalFat + inventoryFat;

        // Allow small rounding errors (0.1kg tolerance)
        Assert.True(Math.Abs(initialMeat - totalMeat) < 0.1,
            $"Meat conservation failed: initial={initialMeat:F2}, final carcass={finalMeat:F2}, inventory={inventoryMeat:F2}");
        Assert.True(Math.Abs(initialBone - totalBone) < 0.1,
            $"Bone conservation failed: initial={initialBone:F2}, final carcass={finalBone:F2}, inventory={inventoryBone:F2}");
        Assert.True(Math.Abs(initialHide - totalHide) < 0.1,
            $"Hide conservation failed: initial={initialHide:F2}, final carcass={finalHide:F2}, inventory={inventoryHide:F2}");

        // Inventory should be near capacity (5kg max)
        Assert.True(ctx.Inventory.CurrentWeightKg <= ctx.Inventory.MaxWeightKg + 0.1,
            $"Inventory exceeds capacity: {ctx.Inventory.CurrentWeightKg:F2}kg > {ctx.Inventory.MaxWeightKg:F2}kg");

        // Carcass should still have material remaining (we only took 5kg from 10+kg)
        Assert.True(totalFinalYield > 0, "Carcass should still have material remaining");

        // Inventory should have SOME material (not zero)
        Assert.True(ctx.Inventory.CurrentWeightKg > 0, "Inventory should contain some butchered materials");
    }

    [Fact]
    public void Butchering_MultiplePartialSessions_EventuallyCompleteCarcass()
    {
        // Arrange: Create a rabbit carcass (smaller, can fully butcher in parts)
        var rabbit = AnimalFactory.FromType(AnimalType.Rabbit, null!, null!)!;
        var carcass = new CarcassFeature(rabbit);

        var ctx = CreateTestContext();
        ctx.Inventory.MaxWeightKg = 2.0;  // Very small capacity
        ctx.Inventory.Tools.Add(Gear.Knife());

        double initialYield = carcass.GetTotalRemainingKg();
        Assert.True(initialYield > 0, "Rabbit should have some yield");

        int sessionsNeeded = 0;
        double totalCollected = 0;

        // Act: Butcher in multiple sessions until carcass is empty
        while (!carcass.IsCompletelyButchered && sessionsNeeded < 10)
        {
            sessionsNeeded++;

            // Butcher some
            var yield = carcass.Harvest(10, true, false, ButcheringMode.Careful);
            var leftovers = ctx.Inventory.CombineWithCapacity(yield);

            if (!leftovers.IsEmpty)
            {
                carcass.RestoreYields(leftovers);
            }

            // Track what was collected this session
            totalCollected += ctx.Inventory.CurrentWeightKg;

            // Empty inventory for next session (simulate returning to camp)
            ctx.player.Inventory = Inventory.CreatePlayerInventory(2.0);
            ctx.Inventory.Tools.Add(Gear.Knife());
        }

        // Assert: Should eventually complete
        Assert.True(carcass.IsCompletelyButchered,
            $"Carcass should be completely butchered after {sessionsNeeded} sessions");

        // Total collected should be close to initial yield (accounting for waste)
        // Careful mode has no waste, but small rounding errors OK
        Assert.True(Math.Abs(initialYield - totalCollected) < 0.5,
            $"Total collected {totalCollected:F2}kg should be close to initial yield {initialYield:F2}kg");
    }

    [Fact]
    public void RestoreYields_OnlyRestoresActualLeftovers()
    {
        // Arrange: Create a caribou carcass
        var caribou = AnimalFactory.FromType(AnimalType.Caribou, null!, null!)!;
        var carcass = new CarcassFeature(caribou);

        double initialMeat = carcass.MeatRemainingKg;

        // Create fake leftovers (2kg meat, 1kg bone)
        var leftovers = new Inventory();
        leftovers.Add(Resource.RawMeat, 2.0);
        leftovers.Add(Resource.Bone, 1.0);

        // Act: Restore these leftovers
        carcass.RestoreYields(leftovers);

        // Assert: Only meat and bone should increase
        Assert.Equal(initialMeat + 2.0, carcass.MeatRemainingKg, precision: 2);
        // Note: Bone might have initial value, just check it increased
        Assert.True(carcass.BoneRemainingKg >= 1.0, "Bone should be at least 1kg");
    }

    [Fact]
    public void CombineAndReport_ReturnsLeftovers()
    {
        // Arrange: Create context with limited capacity
        var ctx = CreateTestContext();
        ctx.Inventory.MaxWeightKg = 3.0;

        // Create source inventory with 5kg of meat (exceeds capacity)
        var source = new Inventory();
        source.Add(Resource.RawMeat, 2.5);
        source.Add(Resource.RawMeat, 2.5);  // Total 5kg

        // Act: Combine
        var leftovers = InventoryCapacityHelper.CombineAndReport(ctx, source);

        // Assert: Some should be left behind
        Assert.False(leftovers.IsEmpty, "Should have leftovers when exceeding capacity");
        Assert.True(leftovers.Weight(Resource.RawMeat) > 0, "Meat should be in leftovers");

        // Player inventory should be at or near capacity
        Assert.True(ctx.Inventory.CurrentWeightKg <= ctx.Inventory.MaxWeightKg + 0.1);

        // Total should be conserved
        double totalMeat = ctx.Inventory.Weight(Resource.RawMeat) + leftovers.Weight(Resource.RawMeat);
        Assert.Equal(5.0, totalMeat, precision: 2);
    }
}
