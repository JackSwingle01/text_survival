using text_survival.Actors;

namespace text_survival.Combat;

/// <summary>
/// Combat actions available to NPCs.
/// </summary>
public enum NPCCombatAction
{
    Hold,      // Stay in position
    Approach,  // Close distance to target
    Attack,    // Strike at target
    Threaten,  // Intimidate / feint
    Flee       // Leave combat
}

/// <summary>
/// Combat behavior AI for NPCs. Uses personality + relationship for targeting decisions.
/// Unlike animals (which use boldness-driven state machines), NPCs make discrete decisions
/// based on personality, relationships, and tactical situation.
/// </summary>
public class NPCCombatBehavior
{
    private readonly NPC _npc;
    private readonly Actor _primaryAlly;  // Usually the player

    public CombatActor? CurrentTarget { get; private set; }

    public NPCCombatBehavior(NPC npc, Actor primaryAlly)
    {
        _npc = npc;
        _primaryAlly = primaryAlly;
    }

    /// <summary>
    /// Decide next action based on personality and situation.
    /// </summary>
    public NPCCombatAction DecideAction(CombatState state, CombatActor self)
    {
        // 1. Flee check - injured + timid = likely to run
        if (ShouldFlee())
            return NPCCombatAction.Flee;

        // 2. Target selection - nearest enemy unless high-relationship ally in danger
        CurrentTarget = ChooseTarget(state, self);
        if (CurrentTarget == null)
            return NPCCombatAction.Hold;

        // 3. Action based on distance zone and boldness
        var zone = state.GetZoneTo(CurrentTarget);
        return zone switch
        {
            DistanceZone.Far => NPCCombatAction.Approach,
            DistanceZone.Mid => _npc.Personality.Boldness > 0.5
                ? NPCCombatAction.Approach
                : NPCCombatAction.Threaten,
            DistanceZone.Close => NPCCombatAction.Attack,
            DistanceZone.Melee => NPCCombatAction.Attack,
            _ => NPCCombatAction.Hold
        };
    }

    /// <summary>
    /// Determine if NPC should flee combat based on health and personality.
    /// </summary>
    private bool ShouldFlee()
    {
        // Critical health - always flee
        if (_npc.Vitality < 0.3)
            return true;

        // Low health + low boldness = flee
        if (_npc.Vitality < 0.5 && _npc.Personality.Boldness < 0.3)
            return true;

        return false;
    }

    /// <summary>
    /// Choose which enemy to target based on tactical situation and relationships.
    /// </summary>
    private CombatActor? ChooseTarget(CombatState state, CombatActor self)
    {
        var enemies = state.ActiveEnemies.ToList();
        if (enemies.Count == 0) return null;

        // High relationship with player + player in danger = prioritize defending player
        double playerRelationship = _npc.GetRelationship(_primaryAlly);
        if (playerRelationship > 0.5 && state.PlayerActor != null && state.Map != null)
        {
            // Find enemy closest to player (using player's position)
            var enemyNearPlayer = enemies
                .Select(e => new { Enemy = e, Distance = state.Map.GetDistanceMeters(state.PlayerActor, e) })
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (enemyNearPlayer != null)
            {
                // If enemy is in striking distance of player, prioritize it
                var zoneToPlayer = DistanceZoneHelper.GetZone(enemyNearPlayer.Distance);
                if (zoneToPlayer <= DistanceZone.Close)
                    return enemyNearPlayer.Enemy;
            }
        }

        // Otherwise target nearest enemy to self (using NPC's position, not player's)
        if (state.Map == null)
            return enemies.FirstOrDefault();

        return enemies
            .OrderBy(e => state.Map.GetDistanceMeters(self, e))
            .FirstOrDefault();
    }

}
