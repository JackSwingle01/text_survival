using text_survival.Actions;
using text_survival.Actors;
using text_survival.Environments;
using text_survival.Environments.Grid;

namespace text_survival.Combat;

public enum EncounterPurpose { Defense, Hunt }

/// <summary>Participation at world-tick boundaries. Combat AI retains tactical control.</summary>
public static class CompanionCombat
{
    public static bool WillAssist(NPC npc, Actor ally, Actor enemy, EncounterPurpose purpose)
    {
        if (!npc.IsAlive || npc.CurrentLocation != ally.CurrentLocation || npc.Vitality < 0.35 || npc.IsHostileTo(ally)) return false;
        if (purpose == EncounterPurpose.Defense && npc.WouldDefend(ally, enemy)) return true;
        return npc.Following?.Target == ally && npc.GetRelationship(ally) >= 0.15 && npc.Personality.Boldness >= 0.3;
    }

    public static IEnumerable<CombatScenario> Encounters(GameContext ctx) =>
        ctx.ActiveCombat == null ? ctx.BackgroundCombats : ctx.BackgroundCombats.Prepend(ctx.ActiveCombat);

    public static bool Owns(GameContext ctx, Actor actor) => Encounters(ctx).Any(s => s.Units.Any(u => u.actor == actor));

    public static void JoinArrivals(GameContext ctx)
    {
        foreach (var scenario in Encounters(ctx).ToList())
        {
            if (scenario.IsOver) continue;
            foreach (var unit in scenario.Units)
                if (unit.CompanionSignalModifier != 0 && ctx.TotalMinutesElapsed >= unit.CompanionSignalExpiresAt)
                {
                    unit.BoldnessModifier -= unit.CompanionSignalModifier;
                    unit.CompanionSignalModifier = 0;
                }
            foreach (var npc in ctx.NPCs.Where(n => n.CurrentLocation == scenario.Location).ToList())
            {
                if (scenario.Team1.Concat(scenario.Team2).Any(u => u.actor == npc) || Owns(ctx, npc) || !scenario.ConsideredHelpers.Add(npc)) continue;
                foreach (var (own, opposing) in new[] { (scenario.Team1, scenario.Team2), (scenario.Team2, scenario.Team1) })
                {
                    var enemy = opposing.FirstOrDefault(u => scenario.Units.Contains(u));
                    var ally = own.FirstOrDefault(u => scenario.Units.Contains(u) && WillAssist(npc, u.actor, enemy?.actor ?? u.actor, scenario.Purpose));
                    if (ally == null || enemy == null) continue;
                    SettleWork(npc);
                    scenario.AddParticipant(npc, own == scenario.Team1);
                    break;
                }
            }
        }
    }

    public static void StartDefense(GameContext ctx, NPC defender, IReadOnlyList<Actor> enemies)
    {
        if (Owns(ctx, defender) || enemies.Count == 0) return;
        // One encounter owns a battlefield. A late defender joins the existing appropriate side.
        var existing = Encounters(ctx).FirstOrDefault(s => s.Location == defender.CurrentLocation && !s.IsOver);
        if (existing != null)
        {
            bool enemyOnB = existing.Team2.Any(u => enemies.Contains(u.actor));
            bool enemyOnA = existing.Team1.Any(u => enemies.Contains(u.actor));
            if (enemyOnA || enemyOnB) existing.AddParticipant(defender, enemyOnB);
            return;
        }
        var available = enemies.Where(e => e.IsAlive && !Owns(ctx, e)).ToList();
        if (available.Count == 0) return;
        var scenario = CombatScenario.Create([defender], available, defender.CurrentLocation, 5,
            AwarenessState.Engaged, AwarenessState.Engaged);
        ctx.BackgroundCombats.Add(scenario);
    }

    public static void TickBackground(GameContext ctx)
    {
        foreach (var scenario in ctx.BackgroundCombats.ToList())
        {
            scenario.AdvanceAutonomousRound();
            if (!scenario.IsOver && ++scenario.ElapsedRounds < 120) continue;
            scenario.IsOver = true;
            CombatAftermath.Apply(ctx, scenario, scenario.DetermineResult(), scenario.Location!);
            ctx.BackgroundCombats.Remove(scenario);
        }
    }

    public static void SettleWork(NPC npc)
    {
        npc.CurrentAction?.Interrupt(npc);
        npc.CurrentAction = null;
        npc.Social.PendingNeed = null;
    }

    public static bool Signal(CombatScenario scenario, Actor caller, bool retreat, double minute)
    {
        if (!scenario.Units.Any(u => u.actor == caller)) return false;
        if (scenario.LastSignalMinute.TryGetValue(caller, out double last) && minute - last < 5) return false;
        scenario.LastSignalMinute[caller] = minute;
        var own = scenario.Team1.Any(u => u.actor == caller) ? scenario.Team1 : scenario.Team2;
        foreach (var unit in own.Where(u => u.actor != caller && scenario.Units.Contains(u)))
        {
            if (unit.actor is not NPC npc || npc.GetRelationship(caller) < 0) continue;
            // Replace the previous nudge instead of stacking it. Existing morale and AI decide the response.
            unit.BoldnessModifier -= unit.CompanionSignalModifier;
            unit.CompanionSignalModifier = retreat ? -0.75 : 0.25;
            unit.BoldnessModifier += unit.CompanionSignalModifier;
            unit.CompanionSignalExpiresAt = minute + 5;
        }
        return true;
    }
}
