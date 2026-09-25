using text_survival.Combat;
using text_survival.Environments.Grid;

namespace text_survival.UI;

/// <summary>
/// What the player committed to on their combat turn: exactly one of a typed action
/// from the action panel, or a confirmed movement cell. Target is an explicit
/// enemy selection; null preserves the nearest-enemy default for existing callers.
/// </summary>
public record CombatInput(CombatActions? Action, GridPosition? MoveTarget, Unit? Target = null);
