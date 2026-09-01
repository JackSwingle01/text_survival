using text_survival.Combat;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Dto;

/// <summary>
/// What the player committed to on their combat turn: exactly one of a typed action
/// from the action panel, or a cell they clicked to move to.
/// </summary>
public record CombatInput(CombatActions? Action, GridPosition? MoveTarget);
