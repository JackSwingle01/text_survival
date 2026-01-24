using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for setting fishing nets in water.
/// Requires a fishing net tool and accessible water (open or ice hole).
/// </summary>
public class SetNetStrategy : IWorkStrategy
{
    private Gear? _selectedNet;

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        // Validate location has water
        var water = location.GetFeature<WaterFeature>();
        if (water == null)
            return "There's no water here.";

        // Need open water or ice hole
        if (water.IsFrozen && !water.HasIceHole)
            return "The water is frozen. You need to cut an ice hole first.";

        // Get available nets from inventory
        var nets = ctx.Inventory.Tools.Where(t => t.ToolType == ToolType.FishingNet && t.Works).ToList();
        if (nets.Count == 0)
            return "You don't have any fishing nets to set.";

        // Select which net to use
        if (nets.Count == 1)
        {
            _selectedNet = nets[0];
        }
        else
        {
            GameDisplay.Render(ctx, statusText: "Planning.");
            var netChoice = new Choice<Gear>("Which net do you want to set?");
            foreach (var net in nets)
            {
                string durability = net.Durability > 0 ? $"{net.Durability} uses" : "unlimited";
                netChoice.AddOption($"{net.Name} ({durability})", net);
            }
            _selectedNet = netChoice.GetPlayerChoice(ctx);
        }

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        // Fixed time operation
        return null;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        baseTime = 10; // Base time to set a net

        // Use Dexterity which combines manipulation, wetness, darkness, vitality
        double dexterity = AbilityCalculator.GetDexterity(ctx.player, ctx);

        if (dexterity < 0.7)
        {
            double timePenalty = 1.0 + ((0.7 - dexterity) / 0.7 * 0.5);
            baseTime = (int)(baseTime * timePenalty);

            var abilityContext = AbilityContext.FromFullContext(
                ctx.player, ctx.Inventory, ctx.player.CurrentLocation, ctx.GameTime.Hour);

            if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
                return (baseTime, ["The darkness makes setting the net difficult."]);
            else if (abilityContext.WetnessPct > 0.3)
                return (baseTime, ["Your wet hands make setting the net difficult."]);
            else
                return (baseTime, ["Your clumsy hands make setting the net difficult."]);
        }

        return (baseTime, []);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => "setting net";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var water = location.GetFeature<WaterFeature>()!;

        // Get or create NetFishingFeature at this location
        var netFeature = location.GetFeature<NetFishingFeature>();
        if (netFeature == null)
        {
            netFeature = new NetFishingFeature(water);
            location.AddFeature(netFeature);
        }

        // Place the net
        netFeature.PlaceNet(_selectedNet!.Durability);

        // Remove net from inventory
        ctx.Inventory.Tools.Remove(_selectedNet);

        string resultMessage = "Net set in the water. Return in 4+ hours to check for fish.";
        if (water.IsFrozen && water.HasIceHole)
        {
            resultMessage += " Watch the ice hole - if it refreezes, you'll lose the net!";
        }

        var collected = new List<string> { $"Set {_selectedNet.Name}" };

        DesktopIO.ShowWorkResult(ctx, "Setting Net", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
