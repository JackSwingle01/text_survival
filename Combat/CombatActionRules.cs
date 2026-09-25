using text_survival.Actions;
using text_survival.Items;

namespace text_survival.Combat;

/// <summary>Shared availability for player combat controls and execution.</summary>
public static class CombatActionRules
{
    public static string? Reason(GameContext ctx, Unit? target, CombatActions action)
    {
        var s = ctx.ActiveCombat;
        if (s?.Player is not { } p) return "Combat is not active.";
        if (action == CombatActions.Flee) return CombatScenario.CanFlee(p.Position) ? null : "Move nearer an edge to flee.";
        if (target == null || !s.Units.Contains(target) || !target.actor.IsAlive || !p.enemies.Contains(target)) return "No available target.";
        var zone = CombatScenario.GetZone(p.Position.DistanceTo(target.Position));
        var nearestZone = CombatScenario.GetZone(p.Position.DistanceTo((s.GetNearestEnemy(p) ?? target).Position));
        return action switch
        {
            CombatActions.Attack when zone is not (Zone.close or Zone.near) => "Requires close or near range.",
            CombatActions.Block when nearestZone is not (Zone.close or Zone.near) => "Requires a threat at close or near range.",
            CombatActions.Dodge when nearestZone != Zone.near => "Requires near range.",
            CombatActions.Shove when zone != Zone.close => "Requires close range.",
            CombatActions.Intimidate when nearestZone is not (Zone.mid or Zone.far) => "Requires mid or far range.",
            CombatActions.Assess when target.Awareness == AwarenessState.Engaged => "Assess is available while stalking.",
            CombatActions.Throw when ctx.Inventory.Weapon == null => "No weapon to throw.",
            CombatActions.Throw or CombatActions.ThrowStone when zone is not (Zone.mid or Zone.far) => "Requires mid or far range.",
            CombatActions.ThrowStone when zone != Zone.mid => "Stones require mid range.",
            CombatActions.ThrowStone when ctx.Inventory.Count(Resource.Stone) == 0 => "No stones remaining.",
            CombatActions.Throw when p.Position.DistanceTo(target.Position) > (ctx.Inventory.Weapon!.Name.Contains("Stone") ? 25 : 20) => "Beyond weapon range.",
            _ => null
        };
    }
}
