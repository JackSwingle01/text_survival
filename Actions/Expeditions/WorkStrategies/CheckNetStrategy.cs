using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for checking and retrieving fishing nets.
/// Collects any fish caught and handles net loss/theft.
/// </summary>
public class CheckNetStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var netFeature = location.GetFeature<NetFishingFeature>();
        if (netFeature?.CanBeChecked != true)
            return "No nets set here.";

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        // Fixed time operation
        return null;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var netFeature = location.GetFeature<NetFishingFeature>();
        baseTime = 5 + (netFeature!.NetCount * 5); // Base time + per-net check time

        double dexterity = AbilityCalculator.GetDexterity(ctx.player, ctx);

        if (dexterity < 0.7)
        {
            double timePenalty = 1.0 + ((0.7 - dexterity) / 0.7 * 0.4);
            baseTime = (int)(baseTime * timePenalty);

            var abilityContext = AbilityContext.FromFullContext(
                ctx.player, ctx.Inventory, ctx.player.CurrentLocation, ctx.GameTime.Hour);

            if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
                return (baseTime, ["The darkness makes checking the nets difficult."]);
            else if (abilityContext.WetnessPct > 0.3)
                return (baseTime, ["Your wet hands make pulling the nets difficult."]);
            else
                return (baseTime, ["Your numb hands make checking the nets difficult."]);
        }

        return (baseTime, []);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => "checking nets";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var netFeature = location.GetFeature<NetFishingFeature>()!;
        var results = netFeature.CheckAllNets();

        var collected = new List<string>();
        var loot = new Inventory();
        int lostCount = 0;
        int stolenCount = 0;

        foreach (var result in results)
        {
            if (result.WasLost)
            {
                lostCount++;
            }
            else if (result.WasStolen)
            {
                stolenCount++;
            }
            else if (result.FishCount > 0)
            {
                foreach (var fishWeight in result.FishWeights)
                {
                    loot.Add(Resource.RawMeat, fishWeight);
                    loot.Add(Resource.Bone, fishWeight * 0.1);
                    collected.Add($"Fish ({fishWeight:F1}kg)");
                }
            }
        }

        if (collected.Count > 0)
            InventoryCapacityHelper.CombineAndReport(ctx, loot);

        // Build result message
        string resultMessage;
        int remaining = netFeature.NetCount;

        if (collected.Count == 0 && lostCount == 0 && stolenCount == 0)
        {
            resultMessage = "No fish yet. The nets need more time to soak.";
        }
        else if (collected.Count > 0)
        {
            double totalWeight = results.Sum(r => r.TotalWeight);
            resultMessage = $"Good catch! {collected.Count} fish ({totalWeight:F1}kg total).";
        }
        else
        {
            resultMessage = "No fish caught.";
        }

        if (lostCount > 0)
            resultMessage += $" {lostCount} net(s) lost to the current!";
        if (stolenCount > 0)
            resultMessage += $" {stolenCount} net(s) plundered by predators!";
        if (remaining > 0)
            resultMessage += $" {remaining} net(s) still set.";
        else if (collected.Count > 0 || lostCount > 0 || stolenCount > 0)
            resultMessage += " No nets remain.";

        DesktopIO.ShowWorkResult(ctx, "Checking Nets", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
