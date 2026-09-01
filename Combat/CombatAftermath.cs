using text_survival.Actions;
using text_survival.Actions.Tensions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Combat;

/// <summary>
/// Everything the world remembers after a fight, whoever fought it: carcasses and bodies,
/// herd losses, fear, feeding and flight, wounded prey that got away, small game depletion,
/// relationship memory, and hunting experience. Player hunts, predator encounters, NPC
/// defence, and pack hunts all end here so none of them can forget a step.
/// </summary>
public static class CombatAftermath
{
    public static void Apply(GameContext ctx, CombatScenario scenario, CombatResult result, Location where)
    {
        var teamA = scenario.Team1.Select(u => u.actor).ToList();
        var teamB = scenario.Team2.Select(u => u.actor).ToList();
        bool playerFought = scenario.Player != null;
        GridPosition? position = ctx.Map?.GetPosition(where);

        LeaveRemains(ctx, teamA.Concat(teamB), where);
        UpdateHerds(ctx, scenario, teamA, teamB, result, position);

        if (playerFought && result == CombatResult.AnimalFled && position != null)
            TrackWoundedPrey(ctx, teamB, where, position.Value);

        if (teamA.Count > 1 && result is CombatResult.Victory or CombatResult.AnimalFled)
            RelationshipEvents.FoughtTogether(teamA);

        if (playerFought && result == CombatResult.Victory && teamB.Any(a => a is Animal))
            ctx.player.Skills.GetSkill("Hunting")?.GainExperience(5);
    }

    /// <summary>Dead animals become carcasses; dead NPCs become bodies with their belongings.</summary>
    private static void LeaveRemains(GameContext ctx, IEnumerable<Actor> actors, Location where)
    {
        foreach (var actor in actors.Where(a => !a.IsAlive))
        {
            switch (actor)
            {
                case Animal animal:
                    where.AddFeature(new CarcassFeature(animal));
                    if (animal.AnimalType.IsSmallGame())
                        where.GetFeature<SmallGameFeature>()?.RecordSuccessfulHunt();
                    break;

                case NPC npc when ctx.NPCs.Remove(npc):
                    var cause = NPCBodyFeature.DetermineDeathCause(npc);
                    where.AddFeature(new NPCBodyFeature(npc.Name, cause, ctx.GameTime, npc.Inventory ?? new Inventory()));
                    break;
            }
        }
    }

    /// <summary>
    /// Every herd with a member in the fight loses its dead, learns fear from how it went,
    /// feeds if it killed, and runs if it lost or was scattered.
    /// </summary>
    private static void UpdateHerds(GameContext ctx, CombatScenario scenario, List<Actor> teamA, List<Actor> teamB, CombatResult result, GridPosition? position)
    {
        foreach (var (herd, onTeamA) in HerdsIn(ctx, scenario))
        {
            var own = onTeamA ? teamA : teamB;
            var opponents = onTeamA ? teamB : teamA;
            var herdResult = onTeamA ? result : Mirror(result);

            foreach (var dead in own.OfType<Animal>().Where(a => !a.IsAlive && herd.Members.Contains(a)).ToList())
                herd.RemoveMember(dead);

            herd.LastCombatMinutes = ctx.TotalMinutesElapsed;

            if (herd.IsPredator && opponents.Any(o => !o.IsAlive))
            {
                herd.Hunger = 0;
                herd.TransitionTo(HerdState.Feeding);
            }

            double fear = herdResult switch
            {
                CombatResult.Defeat => 0.9,      // lost members and the fight
                CombatResult.Fled => 0.7,        // ran from it
                CombatResult.AnimalFled => 0.2,  // the other side ran
                CombatResult.Victory => 0.0,
                _ => 0.4                          // broke off
            };
            herd.Fear = Math.Max(herd.Fear, fear);

            bool scattered = herdResult is CombatResult.Fled or CombatResult.AnimalDisengaged;
            bool preyThatDidNotWin = !herd.IsPredator && herdResult != CombatResult.Victory;
            if ((scattered || preyThatDidNotWin) && herd.Count > 0 && position != null)
                herd.Behavior?.TriggerFlee(herd, position.Value, ctx);
        }

        ctx.Herds.RemoveAll(h => h.IsEmpty);
    }

    /// <summary>The player saw a wounded animal escape: a trail to follow, and a herd of one to track.</summary>
    private static void TrackWoundedPrey(GameContext ctx, List<Actor> enemies, Location where, GridPosition playerPos)
    {
        var wounded = enemies.OfType<Animal>()
            .Where(a => a.IsAlive && a.WoundLevel > 0.05)
            .OrderByDescending(a => a.WoundLevel)
            .FirstOrDefault();
        if (wounded == null) return;

        double severity = 0.3 + wounded.WoundLevel * 0.5;

        Herd? woundedHerd = null;
        var sourceHerd = ctx.Herds.ContainingAnimal(wounded);
        if (sourceHerd != null)
            woundedHerd = ctx.Herds.SplitWounded(sourceHerd, wounded, FleeDirection(playerPos, sourceHerd.Position));

        ctx.Tensions.AddTension(ActiveTension.WoundedPrey(severity, wounded.AnimalType, where, woundedHerd));

        string woundDesc = severity > 0.6
            ? "Bright red arterial spray—you could follow the blood trail."
            : "Dark blood stains mark its escape route.";
        GameDisplay.AddNarrative(ctx, $"The wounded {wounded.Name.ToLower()} escapes. {woundDesc}");
    }

    private static GridPosition FleeDirection(GridPosition threat, GridPosition from)
    {
        int dx = from.X - threat.X;
        int dy = from.Y - threat.Y;
        if (dx == 0 && dy == 0)
            return new GridPosition(from.X + Utils.RandInt(-1, 2), from.Y + Utils.RandInt(-1, 2));
        return new GridPosition(from.X + Math.Sign(dx), from.Y + Math.Sign(dy));
    }

    private static IEnumerable<(Herd herd, bool onTeamA)> HerdsIn(GameContext ctx, CombatScenario scenario)
    {
        var seen = new HashSet<Herd>();
        foreach (var (units, onTeamA) in new[] { (scenario.Team1, true), (scenario.Team2, false) })
        {
            foreach (var animal in units.Select(u => u.actor).OfType<Animal>())
            {
                var herd = ctx.Herds.ContainingAnimal(animal);
                if (herd != null && seen.Add(herd))
                    yield return (herd, onTeamA);
            }
        }
    }

    /// <summary>The same outcome seen from the other side of the field.</summary>
    private static CombatResult Mirror(CombatResult result) => result switch
    {
        CombatResult.Victory => CombatResult.Defeat,
        CombatResult.Defeat => CombatResult.Victory,
        CombatResult.Fled => CombatResult.AnimalFled,
        CombatResult.AnimalFled => CombatResult.Fled,
        _ => CombatResult.AnimalDisengaged
    };
}
