using text_survival.Actions;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.UI;

namespace text_survival.Desktop.UI;

public sealed record TravelInspection(string? Reason, IReadOnlyList<HudAction> Actions)
{
    public static TravelInspection Build(GameContext ctx, (int x, int y) tile)
    {
        var map = ctx.Map;
        var target = new GridPosition(tile.x, tile.y);
        var location = map?.GetLocationAt(tile.x, tile.y);
        string? reason = map == null || location == null || map.GetVisibility(tile.x, tile.y) == TileVisibility.Unexplored ? "Unexplored location." :
            map.IsCaveConcealed(location) ? "Mountain wall. Enter through a cave mouth." :
            target == map.CurrentPosition ? "You are here." :
            !map.CurrentPosition.IsAdjacentTo(target) ? "Select an adjacent tile to travel." :
            !map.CanMoveTo(tile.x, tile.y) ? "Impassable terrain." :
            map.IsEdgeBlocked(map.CurrentPosition, target, ctx.Weather.CurrentSeason) ? "The way is blocked." : null;
        if (reason != null) return new(reason, []);
        var preview = TravelProcessor.PreviewCrossing(ctx.CurrentLocation, location!, ctx.player, ctx.Weather, ctx.Inventory, map!);
        HudAction Travel(string id, string label, string? pace) => new(id, label, HudActionGroup.Other, new PlayerAction.Travel(tile.x, tile.y, pace));
        return preview.IsHazardous
            ? new("Hazardous crossing", [Travel("travel-careful", $"Travel carefully · {preview.CarefulMinutes} min", "careful"),
                Travel("travel-quick", $"Travel quickly · {preview.QuickMinutes} min · {preview.RiskLevel:P0} risk", "quick")])
            : new(null, [Travel("travel", $"Travel here · {preview.QuickMinutes} min", null)]);
    }
}
