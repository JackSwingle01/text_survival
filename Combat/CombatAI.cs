using System.Numerics;
using text_survival;
using text_survival.Combat;
using text_survival.Environments.Grid;

public static class CombatAI
{
    private const int MOVE_DIST = 3;
    private const int WANDER_DIST = 2;
    private static readonly Random _rng = new();

    public static CombatActions DetermineAction(Unit unit, CombatScenario scenario)
    {
        // Handle non-engaged awareness states first
        switch (unit.Awareness)
        {
            case AwarenessState.Unaware:
                return DetermineUnawareAction(unit);
            case AwarenessState.Alert:
                return DetermineAlertAction(unit, scenario);
        }

        // Engaged behavior (original combat AI)
        return DetermineEngagedAction(unit, scenario);
    }

    /// <summary>
    /// Unaware units wander randomly or stay put (grazing/resting behavior).
    /// No aggression - they don't know enemies are there.
    /// </summary>
    private static CombatActions DetermineUnawareAction(Unit unit)
    {
        // 60% stay put, 40% wander
        if (_rng.NextDouble() < 0.6)
        {
            return CombatActions.Wait; // Stay in place
        }
        return CombatActions.Move; // Wander to a new position
    }

    /// <summary>
    /// Alert units are suspicious but not yet aggressive.
    /// They may wander away from last perceived threat direction, scan, or stay put.
    /// </summary>
    private static CombatActions DetermineAlertAction(Unit unit, CombatScenario scenario)
    {
        // Alert units don't attack, they stay cautious
        // 50% stay put (scanning), 50% wander
        return CombatActions.Move;
    }

    /// <summary>
    /// Engaged behavior - original combat AI logic.
    /// </summary>
    private static CombatActions DetermineEngagedAction(Unit unit, CombatScenario scenario)
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
                // 50% block, 25% shove or move
                else return Utils.FlipCoin() ? CombatActions.Block : Utils.FlipCoin() ? CombatActions.Shove : CombatActions.Move;
            case Zone.near:
                if (wantsToAttack) return CombatActions.Attack;
                // 50% move, 25% block or dodge
                else return Utils.FlipCoin() ? CombatActions.Move : Utils.FlipCoin() ? CombatActions.Block : CombatActions.Dodge;
            case Zone.mid:
                return CombatActions.Move;
            case Zone.far:
                return CombatActions.Move;
        }
        return CombatActions.Move;
    }

    public static Unit? DetermineTarget(Unit unit, CombatScenario scenario)
    {
        // Non-engaged units don't target enemies
        if (unit.Awareness != AwarenessState.Engaged)
        {
            return null;
        }
        return scenario.GetNearestEnemy(unit);
    }

    public static GridPosition DetermineMovePosition(Unit unit, CombatScenario? scenario = null)
    {
        // Unaware/Alert units wander randomly
        if (unit.Awareness != AwarenessState.Engaged)
        {
            return DetermineWanderPosition(unit, scenario);
        }

        // Engaged units use tactical movement
        var movement = unit.GetMovementVector();
        if (movement == Vector2.Zero) return unit.Position;

        // Cap movement to MOVE_DIST like player
        var direction = Vector2.Normalize(movement);
        var cappedMagnitude = Math.Min(movement.Length(), MOVE_DIST);
        return unit.Position.Move(direction, (float)cappedMagnitude);
    }

    /// <summary>
    /// Determine a random wander position for unaware/alert units.
    /// </summary>
    private static GridPosition DetermineWanderPosition(Unit unit, CombatScenario? scenario)
    {
        // 40% chance to stay put
        if (_rng.NextDouble() < 0.4)
        {
            return unit.Position;
        }

        // Random direction wander
        double angle = _rng.NextDouble() * 2 * Math.PI;
        var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        float distance = (float)(_rng.NextDouble() * WANDER_DIST + 1); // 1-3m wander

        var newPos = unit.Position.Move(direction, distance);

        // Clamp to map bounds
        int clampedX = Math.Clamp(newPos.X, 1, CombatScenario.MAP_SIZE - 2);
        int clampedY = Math.Clamp(newPos.Y, 1, CombatScenario.MAP_SIZE - 2);

        return new GridPosition(clampedX, clampedY);
    }
}