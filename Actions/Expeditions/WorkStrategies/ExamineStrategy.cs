using text_survival.Actions;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for examining environmental details (fallen logs, animal tracks, etc.).
/// Quick one-time interactions that may yield loot, information, or create tensions.
/// </summary>
public class ExamineStrategy : IWorkStrategy
{
    private readonly string _detailId;

    public ExamineStrategy(string detailId) => _detailId = detailId;

    public Task<string?> ValidateLocation(GameContext ctx, Location location)
    {
        var detail = FindDetail(location);
        if (detail == null)
            return Task.FromResult<string?>("Nothing to examine here.");
        if (!detail.CanInteract)
            return Task.FromResult<string?>("Nothing to interact with.");
        return Task.FromResult<string?>(null);
    }

    public Task<Choice<int>?> GetTimeOptions(GameContext ctx, Location location)
    {
        // Fixed time based on detail - no player choice
        return Task.FromResult<Choice<int>?>(null);
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var detail = FindDetail(location);
        int workTime = detail?.InteractionMinutes ?? 5;

        // Examining is light work - no significant impairments
        return (workTime, []);
    }

    public ActivityType GetActivityType() => ActivityType.Resting; // Light activity

    public string GetActivityName() => "examining";

    public bool AllowedInDarkness => false;

    public async Task<WorkResult> Execute(GameContext ctx, Location location, int actualTime)
    {
        var detail = FindDetail(location);
        if (detail == null || !detail.CanInteract)
        {
            return WorkResult.Empty(actualTime);
        }

        var (loot, examinationText, tension) = detail.Interact();

        // Remove the detail from the location after use
        location.Features.Remove(detail);

        var collected = new List<string>();

        // Log examination text if present
        if (!string.IsNullOrEmpty(examinationText))
        {
            ctx.Log.Add(examinationText, LogLevel.Normal);
        }

        // Add loot to inventory
        if (loot != null && !loot.IsEmpty)
        {
            // Add tools
            foreach (var tool in loot.Tools)
            {
                ctx.Inventory.Tools.Add(tool);
                collected.Add(tool.Name);
            }

            // Add equipment (from dictionary - filter out null values)
            foreach (var kvp in loot.Equipment)
            {
                if (kvp.Value is Gear equip)
                {
                    var replaced = ctx.Inventory.Equip(equip);
                    collected.Add(replaced != null ? $"{equip.Name} (replaced {replaced.Name})" : equip.Name);
                }
            }

            // Add resources using Combine
            bool hasResources = false;
            foreach (Resource type in Enum.GetValues<Resource>())
            {
                if (loot.Count(type) > 0)
                {
                    hasResources = true;
                    break;
                }
            }
            if (hasResources)
            {
                collected.Add(loot.GetDescription());
                InventoryCapacityHelper.CombineAndReport(ctx, loot);
            }
        }

        // Add tension if generated
        if (tension != null)
        {
            ctx.Tensions.AddTension(tension);
        }

        // Show results
        if (collected.Count > 0)
        {
            await ctx.Ui.ShowWorkResult(new WorkResultView(detail.DisplayName, examinationText ?? "You examine it closely.", collected));
        }
        else if (!string.IsNullOrEmpty(examinationText))
        {
            // Info-only detail - render to show the text
        }

        return new WorkResult(collected, null, actualTime, false);
    }

    private EnvironmentalDetail? FindDetail(Location location)
    {
        return location.Features
            .OfType<EnvironmentalDetail>()
            .FirstOrDefault(d => d.Id == _detailId);
    }
}
