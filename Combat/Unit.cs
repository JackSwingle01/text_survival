using System.Numerics;
using text_survival.Actors;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Combat;

/// <summary>
/// Wrapper class for actors to separate the combat-specific calculations
/// </summary>
/// <param name="actor"></param>
public class Unit(Actor actor, GridPosition position)
{
    public Actor actor = actor;
    public List<Unit> allies = [];
    public List<Unit> enemies = [];

    public GridPosition Position = position;

    // primary stats    
    public double Threat => actor.BaseThreat * actor.Vitality;
    public double Boldness => actor.StartingBoldness + GetBonusBoldnessFromAllies() + BoldnessModifier;
    public double Aggression => actor.BaseAggression + (JustDamaged ? RETALIATION_BONUS : 0);
    public double CohesionTowards(Unit other)
    {
        if (actor.GetType() != typeof(NPC)) return actor.BaseCohesion;
        double relationship = ((NPC)actor).GetRelationship(other.actor);
        return Math.Max(0, relationship * actor.BaseCohesion) * 2; // .5 relationship -> base; 1 -> 2 x base
    }
    public double PerceivedThreat(Unit other)
    {
        var distance = Position.DistanceTo(other.Position);
        return Math.Max(0, (1 - (distance * THREAT_DISTANCE_DROP_PCT)) * other.Threat);
    }

    public double BoldnessModifier = 0;
    public void ApplyBoldnessChange(MoraleEvent evt, Unit? other)
    {
        double cohesion = other != null ? CohesionTowards(other) : 0;
        double enemyThreat = other?.Threat ?? 0;
        BoldnessModifier += evt switch
        {
            MoraleEvent.TookDamage => -0.5,
            MoraleEvent.DealtDamage => 0.3,
            MoraleEvent.AllyDamaged => 0.3 * cohesion,
            MoraleEvent.AllyKilled => -2.0 * cohesion,
            MoraleEvent.AllyFled => -1.0 * cohesion,
            MoraleEvent.EnemyAdvanced => -0.1 * enemyThreat,
            MoraleEvent.EnemyRetreated => +0.05,
            MoraleEvent.RoundAdvanced => 0.01,
            MoraleEvent.Intimidated => -CalculateIntimidation(other!),
            _ => throw new NotImplementedException(),
        };
        if (evt == MoraleEvent.TookDamage)
        {
            JustDamaged = true;
        }
    }
    private double CalculateIntimidation(Unit other)
    {
        double effectiveness = .3;
        effectiveness += other.actor.Vitality * .2;
        if (other.GetWeapon() != null) effectiveness += .2;
        if (this.actor.Vitality < .5) effectiveness += .15;
        if (Utils.DetermineSuccess(effectiveness))
        {
            return .3;
        }
        else
        {
            return .05; // small effect on failure
        }
    }
    public double GetBonusBoldnessFromAllies()
    {
        // how powerful are my allies, and how much can I trust them
        double threatBonus = allies.Sum(a => Math.Max(0, a.Threat * CohesionTowards(a)));
        return 0.15 * threatBonus; // up to 15% bonus per ally
    }

    // state
    public bool JustDamaged = false;
    public bool DodgeSet = false;
    public bool BlockSet = false;
    public bool BraceSet = false;
    public void ResetStateBeforeTurn()
    {
        // clear before turn since these only affect enemy actions
        DodgeSet = false;
        BlockSet = false;
        BraceSet = false;
    }
    public void ResetStateAfterTurn()
    {
        // clear after turn since this effects AI decision making
        JustDamaged = false;
    }
    public double EngagementRange => Aggression * 15f;

    // movement calculation
    public Vector2 GetMovementVector()
    {
        var basePull = new Vector2();
        var enemyReaction = new Vector2();
        foreach (var enemy in enemies)
        {
            var direction = Position.DirectionTo(enemy.Position);
            var dist = Position.DistanceTo(enemy.Position);

            basePull += direction * BASE_APPROACH;

            var advantage = Boldness - PerceivedThreat(enemy); // determine if unit feels stronger
            var distWeight = Math.Max(.5, 3.0 / dist); // weight adv/retreat factor higher when closer
            if (advantage > MIN_ADVANTAGE_THRESHOLD)
            {
                // advance if has advantage
                var aggressionFactor = dist <= EngagementRange
                    ? Math.Max(0, 1 - ((dist - EngagementRange) / 10))
                    : 1.0;
                var advance = Math.Max(0, advantage + MIN_ADVANTAGE_THRESHOLD) * distWeight * aggressionFactor * ADVANCE_FACTOR;
                enemyReaction += direction * (float)advance;
            }
            else
            {
                // retreat if at a disadvantage
                var retreat = (Math.Abs(dist) - MIN_ADVANTAGE_THRESHOLD) * distWeight * RETREAT_FACTOR;
                enemyReaction += direction * (float)retreat;
            }
        }
        var allyPull = new Vector2();
        foreach (var ally in allies)
        {
            var dist = (float)Position.DistanceTo(ally.Position);
            if (dist >= 2.0f)
            {
                float allyPullFactor = 0.5f;
                allyPull += dist * allyPullFactor * Position.DirectionTo(ally.Position);
            }
        }
        return basePull + enemyReaction + allyPull;
    }

    public bool DetermineAttack(Unit other)
    {
        var threatPenalty = 1 - (PerceivedThreat(other) * 0.5);
        var attackThreshold = threatPenalty * Aggression;
        return Boldness > attackThreshold;
    }

    public Gear? GetWeapon() => actor.Inventory?.Weapon;
    public void UnequipWeapon() => actor.Inventory?.UnequipWeapon();

    // constants 
    private const double RETALIATION_BONUS = 0.3;
    private const double THREAT_DISTANCE_DROP_PCT = .07; // per meter
    private const float BASE_APPROACH = 0.3f;
    private const double MIN_ADVANTAGE_THRESHOLD = -0.3;
    private const double ADVANCE_FACTOR = 1.0;
    private const double RETREAT_FACTOR = 2.0;


}

public enum MoraleEvent
{
    TookDamage,
    DealtDamage,
    AllyDamaged,
    AllyKilled,
    AllyFled,
    EnemyAdvanced,
    EnemyRetreated,
    RoundAdvanced,
    Intimidated,
}