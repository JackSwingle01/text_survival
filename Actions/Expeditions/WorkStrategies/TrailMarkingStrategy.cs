using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for marking and cutting trails between locations.
/// Trail markers (blazed trees, cairns) provide small navigation bonus.
/// Cut trails (cleared paths) provide larger bonus but require more work.
/// </summary>
public class TrailMarkingStrategy : IWorkStrategy
{
    private readonly Direction _direction;
    private readonly bool _fullCut;  // false = marker, true = cut trail

    public TrailMarkingStrategy(Direction direction, bool fullCut = false)
    {
        _direction = direction;
        _fullCut = fullCut;
    }

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var pos = ctx.Map?.GetPosition(location);
        if (pos == null) return "Cannot mark trails here.";

        var targetPos = _direction.GetNeighbor(pos.Value);
        var targetLoc = ctx.Map!.GetLocationAt(targetPos);

        if (targetLoc == null || !targetLoc.IsPassable)
            return "No accessible terrain in that direction.";

        // Can't mark through cliffs or water
        var edges = ctx.Map.GetEdgesBetween(pos.Value, targetPos);
        if (edges.Any(e => e.Type is EdgeType.Cliff or EdgeType.River))
            return "Cannot mark a trail through this terrain.";

        // Check if already has this trail type
        var existingType = _fullCut ? EdgeType.CutTrail : EdgeType.TrailMarker;
        if (edges.Any(e => e.Type == existingType))
            return $"Trail already {(_fullCut ? "cut" : "marked")} here.";

        // Tool check
        if (_fullCut)
        {
            var axe = ctx.Inventory.GetTool(ToolType.Axe);
            if (axe == null)
                return "You need an axe to cut a trail.";
            if (axe.IsBroken)
                return "Your axe is broken.";
        }
        else
        {
            var knife = ctx.Inventory.GetTool(ToolType.Knife);
            var axe = ctx.Inventory.GetTool(ToolType.Axe);
            if ((knife == null || knife.IsBroken) && (axe == null || axe.IsBroken))
                return "You need a knife or axe to blaze marks.";
        }

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        int baseTime = _fullCut ? 60 : 15;  // 1 hour to cut, 15 min to mark
        string action = _fullCut ? "cut trail" : "mark trail";

        var choice = new Choice<int>($"Time to {action}:");
        choice.AddOption($"{baseTime} minutes", baseTime);
        choice.AddOption("Cancel", 0);
        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Trail work requires arm strength and some mobility
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Crafting;  // Physical work

    public string GetActivityName() => _fullCut ? "cutting trail" : "marking trail";

    public bool AllowedInDarkness => false;  // Need to see what you're doing

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var pos = ctx.Map!.GetPosition(location)!.Value;
        var targetPos = _direction.GetNeighbor(pos);

        var edgeType = _fullCut ? EdgeType.CutTrail : EdgeType.TrailMarker;
        ctx.Map.AddEdge(pos, targetPos, new TileEdge(edgeType));

        // Degrade tool
        if (_fullCut)
        {
            var axe = ctx.Inventory.GetTool(ToolType.Axe)!;
            axe.Use();  // Uses 1 durability
            axe.Use();  // Extra wear for heavy work
            axe.Use();
        }
        else
        {
            var knife = ctx.Inventory.GetTool(ToolType.Knife);
            var axe = ctx.Inventory.GetTool(ToolType.Axe);
            var tool = (knife != null && !knife.IsBroken) ? knife : axe!;
            tool.Use();
        }

        string targetName = ctx.Map.GetLocationAt(targetPos)?.Name ?? "that direction";
        string message = _fullCut
            ? $"You clear a proper trail toward {targetName}."
            : $"You blaze marks on the trees toward {targetName}.";

        var collected = new List<string>
        {
            _fullCut ? "Cut trail created" : "Trail marker created"
        };

        WebIO.ShowWorkResult(ctx, _fullCut ? "Cutting Trail" : "Marking Trail", message, collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
