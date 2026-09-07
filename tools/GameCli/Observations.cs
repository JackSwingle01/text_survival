using text_survival.Actors.Animals;
using text_survival.Actions;
using text_survival.Combat;
using text_survival.Desktop.UI;
using text_survival.Environments.Grid;

namespace text_survival.GameCli;

public static class Observations
{
    private static readonly (int x, int y)[] Directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
    public static TextFrame Capture(Action render)
    {
        var frame = new TextFrame();
        if (GameGui.Capture != null) throw new InvalidOperationException("Nested capture.");
        GameGui.Capture = frame;
        try { render(); } finally { GameGui.Capture = null; }
        return frame;
    }
    public static object Summary(GameContext ctx) => new {
        time = ctx.GameTime, day = ctx.DaysSurvived + 1,
        position = Position(ctx.Map!.CurrentPosition), location = ctx.CurrentLocation.Name,
        alive = ctx.player.IsAlive, vitality = ctx.player.Vitality,
        energy = ctx.player.Body.EnergyPct, food = ctx.player.Body.FullPct, water = ctx.player.Body.HydratedPct,
        bodyTemperatureF = ctx.player.Body.BodyTemperature, weightKg = ctx.Inventory.CurrentWeightKg,
        capacityKg = ctx.Inventory.MaxWeightKg, warnings = SurvivorWarnings.Build(ctx)
    };
    public static int[] Position(GridPosition position) => [position.X, position.Y];
    public static object Status(GameContext ctx) => Capture(() => SurvivorPanel.Render(ctx, CliUi.Rect, HudActions.Build(ctx), false));
    public static object Weather(GameContext ctx) => new {
        season = ctx.Weather.GetSeasonLabel(), condition = ctx.CurrentLocation.Weather.GetConditionLabel(),
        windMph = ctx.CurrentLocation.Weather.WindSpeedMPH, windDirection = ctx.CurrentLocation.Weather.CurrentWindDirection,
        precipitation = ctx.CurrentLocation.Weather.PrecipitationPct, front = ctx.CurrentLocation.Weather.GetFrontLabel(),
        temperature = ctx.CurrentLocation.GetTemperatureBreakdown(ctx.CurrentActivity)
    };
    public static object Map(GameContext ctx, int radius = 3, bool all = false)
    {
        var map = ctx.Map!;
        var center = map.CurrentPosition;
        var rows = new List<object?[]>();
        for (int y = all ? 0 : Math.Max(0, center.Y - radius); y < (all ? map.Height : Math.Min(map.Height, center.Y + radius + 1)); y++)
        for (int x = all ? 0 : Math.Max(0, center.X - radius); x < (all ? map.Width : Math.Min(map.Width, center.X + radius + 1)); x++)
        {
            var visibility = map.GetVisibility(x, y);
            if (visibility == TileVisibility.Unexplored) continue;
            var tile = map.GetLocationAt(x, y)!;
            bool concealed = map.IsCaveConcealed(tile);
            rows.Add([x, y, visibility.ToString(), map.DisplayTerrain(tile).ToString(), concealed ? "Mountain" : tile.Name,
                tile == ctx.Camp, visibility == TileVisibility.Visible && !concealed ? tile.Features.Where(f => f.MapIcon != null).Take(4).Select(f => f.MapIcon).ToArray() : [],
                visibility == TileVisibility.Visible ? ctx.NPCs.Where(n => n.CurrentLocation == tile).Select(n => n.Name).ToArray() : [],
                visibility == TileVisibility.Visible ? ctx.Herds.At(new GridPosition(x, y)).Take(3).Select(Describe).ToArray() : []]);
        }
        return new { width = map.Width, height = map.Height, player = Position(center),
            columns = new[] { "x", "y", "visibility", "terrain", "name", "camp", "icons", "people", "animals" }, rows,
            omitted = "Unexplored tiles are omitted. Coordinates are zero-based; north is y-1." };
    }
    public static object Inspect(GameContext ctx, GridPosition position)
    {
        var map = ctx.Map!;
        if (!map.IsValidPosition(position.X, position.Y)) return new { position = Position(position), error = "Out of bounds" };
        var visibility = map.GetVisibility(position.X, position.Y);
        if (visibility == TileVisibility.Unexplored) return new { position = Position(position), visibility };
        var state = new HudState(); state.Select(ctx, position.X, position.Y);
        var detail = Capture(() => new LocationInspector().Render(ctx, CliUi.Rect, state, HudActions.Build(ctx), false));
        var travel = TravelInspection.Build(ctx, (position.X, position.Y));
        bool visible = visibility == TileVisibility.Visible;
        var tile = map.GetLocationAt(position.X, position.Y)!;
        var edges = Directions.Select(d => new GridPosition(position.X + d.x, position.Y + d.y))
            .Where(p => map.IsValidPosition(p.X, p.Y) && map.GetVisibility(p.X, p.Y) != TileVisibility.Unexplored)
            .Select(p => new { to = Position(p), types = map.GetEdgesBetween(position, p).Select(e => e.Type).ToArray(),
                trail = map.GetTrailTier(position, p), blocked = map.IsEdgeBlocked(position, p, ctx.Weather.CurrentSeason) }).ToArray();
        return new { position = Position(position), visibility, detail.Lines,
            travel = new { travel.Reason, actions = travel.Actions.Select(a => new { a.Id, a.Label }) }, edges,
            structure = map.IsCaveConcealed(tile) ? null : tile.Structure.ToString(),
            people = visible ? ctx.NPCs.Where(n => n.CurrentLocation == tile).Select(n => new { n.Name,
                action = n.CurrentAction?.Name, minutesSpent = n.CurrentAction?.MinutesSpent, durationMinutes = n.CurrentAction?.DurationMinutes }).ToArray() : [],
            animals = visible ? ctx.Herds.At(position).Select(Describe).ToArray() : [] };
    }
    private static string Describe(Herd herd) => herd.Count > 1 ? $"{herd.AnimalType} x{herd.Count}" : herd.AnimalType.ToString();
    public static object Combat(GameContext ctx)
    {
        var combat = ctx.ActiveCombat ?? throw new ArgumentException("No active combat.");
        return new { size = CombatScenario.MAP_SIZE, columns = new[] { "id", "name", "x", "y", "team", "awareness", "vitality" },
            rows = combat.Units.Select((u, i) => new object[] { i, u.actor.Name, u.Position.X, u.Position.Y,
                u == combat.Player ? "player" : combat.Player?.allies.Contains(u) == true ? "ally" : "enemy", u.Awareness.ToString(), u.actor.Vitality }),
            movementMetersPerAction = combat.Player == null ? 0 : CombatMovement.Allowance(combat.Player),
            thrownWeapons = combat.ThrownWeapons.Select(g => g.Name).ToArray() };
    }
    public static object Unit(GameContext ctx, int index)
    {
        var combat = ctx.ActiveCombat ?? throw new ArgumentException("No active combat.");
        if (index < 0 || index >= combat.Units.Count) throw new ArgumentException("Unit index is out of range.");
        return Capture(() => new CombatPanel().Render(ctx, CliUi.Rect, combat.Units[index]));
    }
}
