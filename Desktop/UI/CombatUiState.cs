using text_survival.Actions;
using text_survival.Combat;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Desktop.UI;

/// <summary>Selection and previews never mutate the combat simulation.</summary>
public sealed class CombatUiState
{
    private CombatScenario? _scenario;
    private GridPosition? _playerPosition;
    public Unit? Target { get; private set; }
    public GridPosition? Cursor { get; private set; }
    public bool Inspecting { get; private set; }
    public bool Throwing { get; set; }
    public string? Feedback { get; set; }
    public void Update(CombatScenario scenario)
    {
        if (_scenario != scenario) { _scenario = scenario; Target = null; Cancel(); }
        if (_playerPosition != scenario.Player?.Position) { Cursor = null; _playerPosition = scenario.Player?.Position; }
        if (Target == null || !Enemies(scenario).Contains(Target)) Target = Enemies(scenario).FirstOrDefault();
    }
    public static List<Unit> Enemies(CombatScenario s) => s.Units.Where(u => u.actor.IsAlive && s.Player?.enemies.Contains(u) == true).ToList();
    public void Cycle(CombatScenario s, int delta)
    {
        var enemies = Enemies(s);
        if (enemies.Count > 0) Target = enemies[(Math.Max(0, enemies.IndexOf(Target!)) + delta + enemies.Count) % enemies.Count];
    }
    public void Select(CombatScenario s, Unit unit) { if (Enemies(s).Contains(unit)) Target = unit; }
    public void Inspect(CombatScenario s) { Inspecting = true; Throwing = false; Cursor = s.Player?.Position; }
    public void Step(CombatScenario s, int dx, int dy)
    {
        if (s.Player is not { } player) return;
        var start = Cursor ?? player.Position;
        Cursor = new(Math.Clamp(start.X + dx, 0, CombatScenario.MAP_SIZE - 1), Math.Clamp(start.Y + dy, 0, CombatScenario.MAP_SIZE - 1));
    }
    public string? MoveReason(CombatScenario s) => Cursor is not { } cell || s.Player is not { } p ? "Choose a destination with WASD." :
        Inspecting ? "Inspection does not move you." : cell == p.Position ? "You are here." :
        p.Position.DistanceTo(cell) > CombatMovement.Allowance(p) ? "Beyond movement allowance." :
        s.Units.Any(u => u.actor.IsAlive && u.Position == cell) ? "That cell is occupied." : null;
    public void Cancel() { Cursor = null; Inspecting = false; Throwing = false; Feedback = null; }
    public static string? Reason(GameContext ctx, Unit? target, CombatActions action) => CombatActionRules.Reason(ctx, target, action);
}
