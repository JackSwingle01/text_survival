using text_survival.Items;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles equipment maintenance actions like waterproofing.
/// </summary>
public static class MaintenanceHandler
{
    /// <summary>
    /// Check if player can apply resin waterproofing treatment.
    /// </summary>
    public static bool CanApplyWaterproofing(GameContext ctx)
    {
        if (ctx.Inventory.Count(Resource.PineResin) <= 0)
            return false;

        // Check for any untreated or worn equipment
        var equipment = ctx.Inventory.Equipment.Values.Where(e => e != null).Cast<Gear>();
        return equipment.Any(e => !e.IsResinTreated || e.ResinTreatmentDurability < 25);
    }

    /// <summary>
    /// Apply resin waterproofing treatment to equipment.
    /// </summary>
    public static void ApplyWaterproofing(GameContext ctx)
    {
        var inv = ctx.Inventory;
        int resinCount = inv.Count(Resource.PineResin);

        if (resinCount <= 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have any pine resin for waterproofing.");
            return;
        }

        // Build list of treatable equipment
        var equipment = inv.Equipment.Values.Where(e => e != null).Cast<Gear>().ToList();
        var treatableItems = new List<Gear>();
        var treatableLabels = new List<string>();

        foreach (var item in equipment)
        {
            string label;
            if (!item.IsResinTreated)
            {
                label = $"{item.Name} ({item.Slot}) - untreated";
            }
            else if (item.ResinTreatmentDurability < 25)
            {
                label = $"{item.Name} ({item.Slot}) - treatment worn ({item.ResinTreatmentDurability} uses left)";
            }
            else
            {
                // Skip fully treated items
                continue;
            }

            treatableItems.Add(item);
            treatableLabels.Add(label);
        }

        if (treatableItems.Count == 0)
        {
            GameDisplay.AddNarrative(ctx, "All your equipment is already well waterproofed.");
            return;
        }

        GameDisplay.AddNarrative(ctx, $"You have {resinCount} pine resin. Waterproofing reduces wetness accumulation.");

        var choice = new Choice<Gear?>("Apply waterproofing to:");
        foreach (var (item, label) in treatableItems.Zip(treatableLabels))
        {
            choice.AddOption(label, item);
        }
        choice.AddOption("Cancel", null);

        GameDisplay.Render(ctx, statusText: "Deciding.");
        var selected = choice.GetPlayerChoice(ctx);

        if (selected == null)
            return;

        // Consume resin and apply treatment
        inv.Pop(Resource.PineResin);
        selected.ApplyResinTreatment(50);  // 50 uses of protection

        GameDisplay.AddSuccess(ctx, $"You coat your {selected.Name} with sticky pine resin. It should repel moisture now.");

        // Time cost
        ctx.Update(10, ActivityType.Crafting);
    }
}
