using text_survival.Actions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actions.Handlers;
using text_survival.Desktop.Input;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Desktop.UI;

public enum HudActionGroup { Navigation, Personal, Fire, Shelter, Resources, Storage, People, Other, Wait }
public sealed record HudAction(string Id, string Label, HudActionGroup Group, PlayerAction Payload,
    HotkeyAction? Shortcut = null, string? DisabledReason = null)
{
    public bool Enabled => DisabledReason == null;
}

/// <summary>One action list supplies buttons and keyboard shortcuts. Handlers still validate on execution.</summary>
public static class HudActions
{
    public static IReadOnlyList<HudAction> Build(GameContext ctx)
    {
        var result = new List<HudAction>();
        void Add(CampAction action, string label, HudActionGroup group, HotkeyAction? key = null, string? reason = null)
            => result.Add(new(action.ToString(), label, group, new PlayerAction.Camp(action), key, reason));
        Add(CampAction.Inventory, "Inventory", HudActionGroup.Navigation, HotkeyAction.Inventory);
        Add(CampAction.Crafting, "Crafting", HudActionGroup.Navigation, HotkeyAction.Crafting);
        Add(CampAction.DiscoveryLog, "Discoveries", HudActionGroup.Navigation, HotkeyAction.DiscoveryLog);
        Add(CampAction.NPCs, "People", HudActionGroup.People, HotkeyAction.NPCs);
        Add(CampAction.Wait, "Wait · 5 min", HudActionGroup.Wait, HotkeyAction.Wait);
        var location = ctx.CurrentLocation;
        var fire = location.GetFeature<HeatSourceFeature>();
        bool lit = fire != null && (fire.IsActive || fire.HasEmbers);
        bool hasTool = ctx.Inventory.Tools.Any(t => t.ToolType is ToolType.FireStriker or ToolType.HandDrill or ToolType.BowDrill);
        Add(lit ? CampAction.TendFire : CampAction.StartFire, lit ? "Tend fire..." : "Start fire...", HudActionGroup.Fire,
            HotkeyAction.Fire, lit ? (!ctx.Inventory.HasFuel ? "You need fuel to tend the fire." : null)
            : !hasTool ? "You need a fire-starting tool." : !ctx.Inventory.CanStartFire ? "You need suitable tinder and kindling." : null);
        Add(CampAction.Food, "Food & water...", HudActionGroup.Personal, reason:
            ctx.Inventory.HasFood || ctx.Inventory.HasWater || fire?.IsActive == true ? null : "No food or water available.");
        bool wounds = ctx.player.EffectRegistry.GetAll().Any(e => e.EffectKind is "Bleeding" or "Burn" or "Infected");
        if (wounds)
            Add(CampAction.TreatWounds, "Treat wounds...", HudActionGroup.Personal, reason:
                ctx.Inventory.GetCount(ResourceCategory.Medicine) > 0 ? null : "You need treatment supplies.");
        Add(CampAction.Storage, "Camp storage...", HudActionGroup.Storage, HotkeyAction.Storage,
            location == ctx.Camp && ctx.Camp.GetFeature<CacheFeature>() != null ? null : "Camp storage is available at your camp.");
        var rack = location.GetFeature<CuringRackFeature>();
        if (rack != null)
            Add(CampAction.CuringRack, rack.HasReadyItems ? "Curing rack · ready..." : $"Curing rack · {rack.ItemCount} curing...", HudActionGroup.Storage);
        bool bedding = location.GetFeature<BeddingFeature>() != null;
        Add(bedding ? CampAction.Sleep : CampAction.MakeCamp, bedding ? "Sleep..." : "Make camp...", HudActionGroup.Shelter);
        var tent = CampHandler.GetDeployableTent(ctx);
        if (tent != null && CampHandler.CanDeployTent(ctx)) Add(CampAction.PitchTent, $"Pitch {tent.Name}...", HudActionGroup.Shelter);
        else if (CampHandler.CanPackTent(ctx)) Add(CampAction.PackTent, "Pack up tent...", HudActionGroup.Shelter);
        foreach (var option in location.GetWorkOptions(ctx))
        {
            var strategy = option.Strategy;
            result.Add(new($"work:{option.Id}", option.Label + "...", GroupFor(strategy), new PlayerAction.Work(strategy),
                strategy is ForageStrategy ? HotkeyAction.Forage : null));
        }
        return result;
    }

    public static HudActionGroup GroupFor(IWorkStrategy strategy) => strategy switch
    {
        ShelterImprovementStrategy => HudActionGroup.Shelter,
        CacheStrategy or GroundStashStrategy or ButcherStrategy or CraftingProjectStrategy => HudActionGroup.Storage,
        ForageStrategy or HarvestStrategy or HuntStrategy or MegafaunaStrategy or FishingStrategy or SetNetStrategy or CheckNetStrategy
            or TrapStrategy or ExamineStrategy or TrailMarkingStrategy or LootBodyStrategy or SalvageStrategy or IceCuttingStrategy => HudActionGroup.Resources,
        _ => HudActionGroup.Other
    };
}
