using text_survival.Actors.Animals;
using text_survival.Actions;
using text_survival.Combat;
using text_survival.Desktop.UI;
using text_survival.Environments;
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
    private static int Pct(double value) => (int)Math.Round(value * 100);
    public static object Summary(GameContext ctx)
    {
        var warnings = SurvivorWarnings.Build(ctx);
        return new {
            time = $"Day {ctx.DayNumber} {ctx.GameTime:HH:mm}", at = Position(ctx.Map!.CurrentPosition), location = ctx.CurrentLocation.Name,
            dead = ctx.player.IsAlive ? (bool?)null : true, vitality = Pct(ctx.player.Vitality),
            energy = Pct(ctx.player.Body.EnergyPct), food = Pct(ctx.player.Body.FullPct), water = Pct(ctx.player.Body.HydratedPct),
            tempF = Math.Round(ctx.player.Body.BodyTemperature, 1), load = $"{ctx.Inventory.CurrentWeightKg:F1}/{ctx.Inventory.MaxWeightKg:F0}kg",
            warnings = warnings.Count == 0 ? null : warnings
        };
    }
    public static int[] Position(GridPosition position) => [position.X, position.Y];
    public static object Status(GameContext ctx) => Capture(() => SurvivorPanel.Render(ctx, CliUi.Rect, HudActions.Build(ctx), false));
    public static object Weather(GameContext ctx) => new {
        season = ctx.Weather.GetSeasonLabel(), condition = ctx.CurrentLocation.Weather.GetConditionLabel(),
        windMph = ctx.CurrentLocation.Weather.WindSpeedMPH, windDirection = ctx.CurrentLocation.Weather.CurrentWindDirection,
        precipitation = ctx.CurrentLocation.Weather.PrecipitationPct, front = ctx.CurrentLocation.Weather.GetFrontLabel(),
        temperature = ctx.CurrentLocation.GetTemperatureBreakdown(ctx.CurrentActivity)
    };
    private static char Glyph(TerrainType t) => t switch {
        TerrainType.Forest => 'T', TerrainType.Clearing => ',', TerrainType.Plain => '.', TerrainType.Hills => 'n',
        TerrainType.Water => '~', TerrainType.Marsh => '%', TerrainType.Rock => 'o', TerrainType.Mountain => '^', _ => '='
    };
    public const string Legend = "@ you, C camp, T forest, , clearing, . plain, n hills, ~ ice, % marsh, o rock, ^ mountain, = deep water, blank unexplored. Rows are y, columns start at x0; north is y-1.";
    /// <summary>ASCII grid plus the tiles worth naming: camp, unique names, icons, people, animals.</summary>
    public static object Map(GameContext ctx, int radius = 3, bool all = false)
    {
        var map = ctx.Map!;
        var center = map.CurrentPosition;
        int x0 = all ? 0 : Math.Max(0, center.X - radius), x1 = all ? map.Width - 1 : Math.Min(map.Width - 1, center.X + radius);
        int y0 = all ? 0 : Math.Max(0, center.Y - radius), y1 = all ? map.Height - 1 : Math.Min(map.Height - 1, center.Y + radius);
        var grid = new List<string>();
        var tiles = new List<(int x, int y, Location tile, bool visible)>();
        for (int y = y0; y <= y1; y++)
        {
            var row = new System.Text.StringBuilder();
            for (int x = x0; x <= x1; x++)
            {
                var visibility = map.GetVisibility(x, y);
                if (visibility == TileVisibility.Unexplored) { row.Append(' '); continue; }
                var tile = map.GetLocationAt(x, y)!;
                tiles.Add((x, y, tile, visibility == TileVisibility.Visible && !map.IsCaveConcealed(tile)));
                row.Append(x == center.X && y == center.Y ? '@' : tile == ctx.Camp ? 'C' : Glyph(map.DisplayTerrain(tile)));
            }
            string text = row.ToString().TrimEnd();
            if (all && text.Length == 0) continue;
            grid.Add($"{y,3} {text}");
        }
        var nameCounts = tiles.Where(t => !map.IsCaveConcealed(t.tile)).GroupBy(t => t.tile.Name).ToDictionary(g => g.Key, g => g.Count());
        var places = tiles.Select(t => {
            var icons = t.visible ? t.tile.Features.Where(f => f.MapIcon != null).Take(4).Select(f => f.MapIcon!) : [];
            var people = t.visible ? ctx.NPCs.Where(n => n.CurrentLocation == t.tile).Select(n => n.Name) : [];
            var animals = t.visible ? ctx.Herds.At(new GridPosition(t.x, t.y)).Take(3).Select(Describe) : [];
            var extras = icons.Concat(people).Concat(animals).ToArray();
            bool named = !map.IsCaveConcealed(t.tile) && nameCounts[t.tile.Name] == 1;
            if (!named && extras.Length == 0 && t.tile != ctx.Camp) return null;
            return $"{t.x},{t.y} {t.tile.Name}" + (t.visible ? "" : " (remembered)") + (extras.Length > 0 ? ": " + string.Join(", ", extras) : "");
        }).OfType<string>().ToArray();
        return new { x0, grid, places, legend = Legend };
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
        return Capture(() => new CombatPanel().Render(ctx, CliUi.Rect, combat.Units[index], new CombatUiState(), false));
    }
}
