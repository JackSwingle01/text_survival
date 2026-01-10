using System.Numerics;
using text_survival;
using text_survival.Combat;
using text_survival.Environments.Grid;

public static class CombatAI
{
    private const int MOVE_DIST = 3;

    public static CombatActions DetermineAction(Unit unit, CombatScenario scenario)
    {
        Unit? nearestEnemy = scenario.GetNearestEnemy(unit);
        if (nearestEnemy == null) return CombatActions.Move;
        
        var distance = unit.Position.DistanceTo(nearestEnemy.Position);
        bool wantsToAttack = unit.DetermineAttack(nearestEnemy);
        var zone = CombatScenario.GetZone(distance);
        switch (zone)
        {
            case Zone.close:
                if (wantsToAttack) return CombatActions.Attack;
                else return Utils.FlipCoin() ? CombatActions.Block : CombatActions.Shove;
            case Zone.near:
                if (wantsToAttack) return CombatActions.Attack;
                else return Utils.FlipCoin() ? CombatActions.Block : CombatActions.Dodge;
            case Zone.mid:
                return CombatActions.Move;
                // todo - handle throwing and intimidation
            case Zone.far:
                return CombatActions.Move;
        }
        return CombatActions.Move;
    }
    public static Unit? DetermineTarget(Unit unit, CombatScenario scenario)
    {
        return scenario.GetNearestEnemy(unit);
    }
    public static GridPosition DetermineMovePosition(Unit unit)
    {
        var movement = unit.GetMovementVector();
        if (movement == Vector2.Zero) return unit.Position;

        // Cap movement to MOVE_DIST like player
        var direction = Vector2.Normalize(movement);
        var cappedMagnitude = Math.Min(movement.Length(), MOVE_DIST);
        return unit.Position.Move(direction, (float)cappedMagnitude);
    }
}