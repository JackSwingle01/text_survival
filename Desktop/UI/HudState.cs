using text_survival.Actions;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.UI;

public sealed class HudState
{
    public (int x, int y)? SelectedTile { get; private set; }
    public bool HistoryExpanded { get; set; }
    public string? Feedback { get; private set; }
    private float _feedbackSeconds;
    private GameMap? _map;
    private GridPosition? _position;

    public void Select(GameContext ctx, int x, int y)
    {
        var map = ctx.Map;
        if (map == null || !map.IsValidPosition(x, y)) return;
        var location = map.GetLocationAt(x, y);
        if (location == null || location.Visibility == TileVisibility.Unexplored) return;
        SelectedTile = map.CurrentPosition.X == x && map.CurrentPosition.Y == y ? null : (x, y);
    }

    public void ClearSelection() => SelectedTile = null;
    public void ShowFeedback(string message) { Feedback = message; _feedbackSeconds = 4; }

    public void Update(GameContext ctx, float dt)
    {
        if (_map != ctx.Map || _position != ctx.Map?.CurrentPosition)
            ClearSelection();
        _map = ctx.Map;
        _position = ctx.Map?.CurrentPosition;
        if (SelectedTile is { } tile && ctx.Map?.GetLocationAt(tile.x, tile.y)?.Visibility is null or TileVisibility.Unexplored)
            ClearSelection();
        _feedbackSeconds -= dt;
        if (_feedbackSeconds <= 0) Feedback = null;
    }
}
