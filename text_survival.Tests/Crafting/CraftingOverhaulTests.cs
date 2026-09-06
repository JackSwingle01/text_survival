using System.Text.Json;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Persistence;
using text_survival.Tests.Support;

namespace text_survival.Tests.Crafting;

public class CraftingOverhaulTests
{
    private readonly NeedCraftingSystem _crafting = new();
    private CraftOption Recipe(string id) => _crafting.AllOptions.Single(o => o.Id == id);
    private Gear Gear(string id)
    {
        var option = Recipe(id);
        var gear = option.GearFactory!(option.Durability);
        gear.DesignId = option.Id;
        return gear;
    }
    private static void Supply(Inventory inv, Resource resource, int count)
    {
        for (int i = 0; i < count; i++) inv.Add(resource, 0.2);
    }

    [Fact]
    public void EveryRecipeHasUniqueIdentityAndReachableFamily()
    {
        Assert.Equal(_crafting.AllOptions.Count, _crafting.AllOptions.Select(o => o.Id).Distinct().Count());
        Assert.All(_crafting.AllOptions, o => Assert.Contains(CraftFamilies.Get(o.FamilyId).Section, CraftFamilies.Sections));
        Assert.Equal("Food gathering", CraftFamilies.Get(Recipe("fishing-net").FamilyId).Section);
        Assert.Single(_crafting.AllOptions.Where(o => o.Name.Contains("Knife") || o.Name is "Crude Edge" or "Sharp Rock").Select(o => o.FamilyId).Distinct());
    }

    [Fact]
    public void OverlappingRequirementsReserveSpecificMaterialsAndDoNotSpendTwice()
    {
        var inv = new Inventory();
        Supply(inv, Resource.Pine, 1);
        var option = new CraftOption
        {
            Name = "Overlapping", Description = "", Category = NeedCategory.Processing,
            CraftingTimeMinutes = 1, Durability = 0,
            Requirements = [new(ResourceCategory.Log, 1), new(Resource.Pine, 1)]
        };
        var inputs = CraftInputs.Resolve(option, inv);
        Assert.False(inputs.Ready);
        Assert.Throws<InvalidOperationException>(() => inputs.Consume(inv));
        Assert.Equal(1, inv.Count(Resource.Pine));
        Supply(inv, Resource.Birch, 1);
        inputs = CraftInputs.Resolve(option, inv);
        Assert.True(inputs.Ready);
        inputs.Consume(inv);
        Assert.Equal(0, inv.GetCount(ResourceCategory.Log));
        Assert.Throws<InvalidOperationException>(() => inputs.Consume(inv));
    }

    [Fact]
    public void BrokenEquippedToolDoesNotMaskUsableOrInfiniteTool()
    {
        var inv = new Inventory();
        var broken = Gear("stone-knife");
        broken.Durability = 0;
        inv.EquipWeapon(broken);
        var infinite = new Gear { Name = "Enduring knife", Category = GearCategory.Tool, ToolType = ToolType.Knife };
        inv.Tools.Add(infinite);
        Supply(inv, Resource.Stick, 2);
        var inputs = CraftInputs.Resolve(Recipe("hand-drill"), inv);
        Assert.True(inputs.Ready);
        Assert.Same(infinite, Assert.Single(inputs.Tools).Tool);
        inputs.Consume(inv);
        Assert.Equal(-1, infinite.Durability);
        Assert.Equal(0, broken.Durability);
    }

    [Fact]
    public void StaleInputSelectionCannotConsumeAnythingOrSwitchTools()
    {
        var inv = new Inventory();
        var knife = Gear("stone-knife");
        inv.Tools.Add(knife);
        Supply(inv, Resource.Stick, 2);
        var inputs = CraftInputs.Resolve(Recipe("hand-drill"), inv);
        inv.Tools.Remove(knife);
        inv.Tools.Add(Gear("stone-knife"));
        Assert.False(inputs.CanConsume(inv));
        Assert.Throws<InvalidOperationException>(() => inputs.Consume(inv));
        Assert.Equal(2, inv.Count(Resource.Stick));
    }

    [Fact]
    public async Task ExistingShelterBlocksBeforeWorkAndLeavesMaterialsUntouched()
    {
        var ctx = GameContext.CreateNewGame();
        ctx.Camp.AddFeature(ShelterFeature.CreateBranchFrame());
        Supply(ctx.Inventory, Resource.Stick, 4);
        ctx.Ui = new ScriptedUi();
        int before = ctx.Inventory.Count(Resource.Stick);
        var time = ctx.GameTime;
        Assert.False(CraftEvaluation.For(ctx, Recipe("branch-frame-shelter")).Ready);
        await CraftingHandler.Craft(ctx, Recipe("branch-frame-shelter"));
        Assert.Equal(time, ctx.GameTime);
        Assert.Equal(before, ctx.Inventory.Count(Resource.Stick));
    }

    [Fact]
    public void ProjectPreviewIncludesLaterWorkAndWorkingShovelBonus()
    {
        var ctx = GameContext.CreateNewGame();
        ctx.Inventory.Tools.Clear();
        ctx.Inventory.UnequipWeapon();
        var project = Recipe("stone-fire-pit");
        Assert.Equal(300, CraftEvaluation.For(ctx, project).LaterWorkMinutes);
        var shovel = Gear("bone-shovel");
        ctx.Inventory.Tools.Add(shovel);
        Assert.Equal(150, CraftEvaluation.For(ctx, project).LaterWorkMinutes);
        shovel.Durability = 0;
        Assert.Equal(300, CraftEvaluation.For(ctx, project).LaterWorkMinutes);
        foreach (var recipe in _crafting.AllOptions.Where(o => o.ProjectWorkMinutes > 0))
        {
            var feature = Assert.IsAssignableFrom<CraftingProjectFeature>(recipe.FeatureFactory!());
            Assert.Equal(feature.TimeRequiredMinutes, recipe.ProjectWorkMinutes);
            Assert.Equal(feature.BenefitsFromShovel, recipe.ProjectBenefitsFromShovel);
        }
    }

    [Fact]
    public void AddingHandlePreservesEdgeConditionAndSelectedEquippedIdentity()
    {
        var inv = new Inventory();
        var edge = Gear("crude-edge");
        edge.Durability = 1;
        var other = Gear("crude-edge");
        inv.EquipWeapon(edge);
        inv.Tools.Add(other);
        Supply(inv, Resource.Stick, 1);
        Supply(inv, Resource.PlantFiber, 1);
        var action = GearCrafting.Options(inv, _crafting).Single(o => o.TargetGear == edge && o.Id.StartsWith("handle:"));
        action.Craft(inv);
        Assert.Equal("stone-knife", inv.Weapon!.DesignId);
        Assert.Equal(edge.InstanceId, inv.Weapon.InstanceId);
        Assert.Equal(0.5, inv.Weapon.ConditionPct);
        Assert.Contains(other, inv.Tools);
        Assert.Equal(other.MaxDurability, other.Durability);
        Assert.Throws<InvalidOperationException>(() => action.Craft(inv));
    }

    [Fact]
    public void SharpeningCapsConditionAndCannotRepairBrokenEdges()
    {
        var inv = new Inventory();
        var knife = Gear("stone-knife");
        knife.Durability = 1;
        inv.Tools.Add(knife);
        inv.Tools.Add(Gear("knapping-stone"));
        var action = GearCrafting.Options(inv, _crafting).Single(o => o.Id.StartsWith("sharpen:"));
        action.Craft(inv);
        var repaired = inv.Tools.Single(g => g.DesignId == "stone-knife");
        Assert.Equal((int)(repaired.MaxDurability * 0.8), repaired.Durability);
        Assert.NotNull(GearCrafting.Options(inv, _crafting).Single(o => o.Id.StartsWith("sharpen:")).TargetBlocker!());
        repaired.Durability = 0;
        var brokenAction = GearCrafting.Options(inv, _crafting).Single(o => o.Id.StartsWith("sharpen:"));
        Assert.Throws<InvalidOperationException>(() => brokenAction.Craft(inv));
    }

    [Fact]
    public void RefitKeepsHandleAndRestoresABrokenKnifeWithoutDuplicatingIt()
    {
        var inv = new Inventory();
        var knife = Gear("stone-knife");
        knife.Durability = 0;
        inv.Tools.Add(knife);
        inv.Tools.Add(Gear("knapping-stone"));
        Supply(inv, Resource.Flint, 1);
        Supply(inv, Resource.PlantFiber, 1);
        var action = GearCrafting.Options(inv, _crafting).Single(o => o.Id.StartsWith("refit:") && o.Id.EndsWith(":flint-knife"));
        action.Craft(inv);
        var result = Assert.Single(inv.Tools, g => g.ToolType == ToolType.Knife);
        Assert.Equal("flint-knife", result.DesignId);
        Assert.Equal(knife.InstanceId, result.InstanceId);
        Assert.Equal(1, result.ConditionPct);
        Assert.Equal(0, inv.Count(Resource.Flint));
    }

    [Fact]
    public void MendingTargetsUnequippedGarmentAndLeavesEquippedOneAlone()
    {
        var inv = new Inventory();
        var equipped = Gear("hide-boots");
        inv.Equip(equipped);
        var spare = Gear("hide-boots");
        spare.Durability = 1;
        inv.Tools.Add(spare);
        inv.Tools.Add(Gear("bone-needle"));
        Supply(inv, Resource.Hide, 1);
        Supply(inv, Resource.Sinew, 1);
        var action = GearCrafting.Options(inv, _crafting).Single(o => o.TargetGear == spare);
        action.Craft(inv);
        Assert.Same(equipped, inv.GetEquipment(EquipSlot.Feet));
        Assert.Equal(equipped.MaxDurability, equipped.Durability);
        Assert.Equal(1 + spare.MaxDurability / 2, inv.Tools.Single(g => g.Slot == EquipSlot.Feet).Durability);
    }

    [Fact]
    public void GearIdentityAndBrokenRepairTargetsSurviveSaveRoundTrip()
    {
        var inv = new Inventory();
        var knife = Gear("stone-knife");
        knife.Durability = 0;
        inv.Tools.Add(knife);
        var json = JsonSerializer.Serialize(inv, SaveManager.Options);
        var loaded = JsonSerializer.Deserialize<Inventory>(json, SaveManager.Options)!;
        var result = Assert.Single(loaded.Tools);
        Assert.Equal(knife.InstanceId, result.InstanceId);
        Assert.Equal("stone-knife", result.DesignId);
        Assert.Equal(0, result.Durability);
        Assert.Contains(GearCrafting.Options(loaded, _crafting), o => o.Id.StartsWith("refit:"));
        var legacy = Gear("stone-knife");
        legacy.DesignId = null;
        Assert.Equal("stone-knife", GearCrafting.FindDesign(legacy, _crafting)!.Id);
        legacy.Name = "Unusual knife";
        Assert.Null(GearCrafting.FindDesign(legacy, _crafting));
    }
}
