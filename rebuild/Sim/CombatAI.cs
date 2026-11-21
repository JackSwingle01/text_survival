using System.Numerics;

namespace Survival;

/// What moves a fighter's morale (old Unit.ApplyBoldnessChange).
public enum MoraleEvent { TookDamage, DealtDamage, AllyDealtDamage, AllyDamaged, AllyKilled, AllyFled, EnemyAdvanced, EnemyRetreated, RoundAdvanced, Intimidated }

/// Morale, steering and the AI's turn: port of old Unit, CombatAI and CombatMovement. Any unit the
/// player is not driving takes its turn here, animal or person.
public static partial class Combat
{
    const double RetaliationBonus = 0.3, ThreatDropPerM = 0.07, MinAdvantage = -0.3, AdvanceFactor = 1.0, RetreatFactor = 2.0;
    const float BaseApproach = 0.3f;
    const int WanderM = 2;

    // ---- morale ----

    /// How dangerous this fighter is right now: its species' threat, scaled by how alive it is.
    public static double Threat(World world, CombatUnit u) => StatsOf(world, u).Threat * Body.Vitality(BodyOf(world, u));

    /// Courage now: the species' own, its allies' strength, what has happened so far, an opening
    /// optimism proportional to its nature, less what its wounds cost it.
    public static double UnitBoldness(World world, Encounter enc, CombatUnit u)
    {
        var stats = StatsOf(world, u);
        return stats.Boldness + AllyBonus(world, enc, u) + u.BoldnessModifier
            + (u.InitialOptimism ? Math.Min(1.0, stats.Boldness) : 0) - HealthPenalty(BodyOf(world, u));
    }

    /// 0 healthy; up to 1.5 for a destroyed part, 1 for lost blood, 0.3 while bleeding.
    static double HealthPenalty(BodyState body) =>
        (1 - body.Regions.Min(r => r.Condition)) * 1.5 + (1 - body.BloodPct) * 1.0 + (Effects.Has(body, EffectKind.Bleeding) ? 0.3 : 0);

    static double Aggression(World world, CombatUnit u) => StatsOf(world, u).Aggression + (u.JustDamaged ? RetaliationBonus : 0);

    /// How much this fighter trusts its side.
    // Old NPCs scaled cohesion by their opinion of each ally; nothing writes opinions yet (Npc Opinions).
    static double Cohesion(World world, CombatUnit u) => StatsOf(world, u).Cohesion;

    /// Threat seen from here: falls off 7% a metre, so nothing past 14 m frightens.
    public static double PerceivedThreat(World world, CombatUnit u, CombatUnit other) =>
        Math.Max(0, (1 - u.Position.DistanceTo(other.Position) * ThreatDropPerM) * Threat(world, other));

    /// Up to 15% of each ally's threat, as far as this fighter trusts it.
    static double AllyBonus(World world, Encounter enc, CombatUnit u) =>
        0.15 * Allies(enc, u).Sum(a => Math.Max(0, Threat(world, a) * Cohesion(world, u)));

    static void Morale(World world, Encounter enc, CombatUnit u, MoraleEvent evt, CombatUnit? other)
    {
        double cohesion = other != null ? Cohesion(world, u) : 0;
        double enemyThreat = other != null ? Threat(world, other) : 0;
        u.BoldnessModifier += evt switch
        {
            MoraleEvent.TookDamage => -0.1,
            MoraleEvent.DealtDamage => 0.4,
            MoraleEvent.AllyDealtDamage => 0.2,
            MoraleEvent.AllyDamaged => 0.3 * cohesion,
            MoraleEvent.AllyKilled => -2.0 * cohesion,
            MoraleEvent.AllyFled => -1.0 * cohesion,
            MoraleEvent.EnemyAdvanced => -0.05 * enemyThreat,
            MoraleEvent.EnemyRetreated => 0.05,
            MoraleEvent.RoundAdvanced => -0.01,
            MoraleEvent.Intimidated => -Intimidation(world, u, other ?? throw new ArgumentNullException(nameof(other), "Intimidated by nobody")),
            _ => throw new ArgumentOutOfRangeException(nameof(evt), evt, "Unknown morale event"),
        };
        if (evt == MoraleEvent.TookDamage) u.JustDamaged = true;
        if (evt is MoraleEvent.AllyKilled or MoraleEvent.TookDamage or MoraleEvent.AllyFled) u.InitialOptimism = false;
    }

    /// A shout and a show: works better from a healthy, armed shouter on a weakened target.
    static double Intimidation(World world, CombatUnit target, CombatUnit shouter)
    {
        double effectiveness = IntimidationChance(world, target, shouter);
        return world.Rng("combat").NextDouble() < effectiveness ? 0.3 : 0.05;
    }

    public static double IntimidationChance(World world, CombatUnit target, CombatUnit shouter) =>
        0.3 + Body.Vitality(BodyOf(world, shouter)) * 0.2 + (shouter.Weapon != null ? 0.2 : 0)
        + (Body.Vitality(BodyOf(world, target)) < 0.5 ? 0.15 : 0);

    /// Attacks when its courage beats the threat, discounted by how aggressive it is.
    public static bool WantsToAttack(World world, Encounter enc, CombatUnit u, CombatUnit other) =>
        UnitBoldness(world, enc, u) > PerceivedThreat(world, u, other) * (1 - Aggression(world, u) * 0.5);

    // ---- steering ----

    /// Tactical metres per action, cut by wounded legs: a crippled animal can be run down and a
    /// mauled person cannot sprint.
    public static int Allowance(World world, CombatUnit u)
    {
        double moving = Math.Clamp(Body.GetCapacities(BodyOf(world, u)).Moving, 0, 1);
        return Math.Max(1, (int)Math.Round(StatsOf(world, u).MoveAllowanceM * moving));
    }

    /// Pull toward every enemy, pushed further by advantage or back by disadvantage (more so close
    /// up), plus a pull toward allies more than 2 m away.
    public static Vector2 MovementVector(World world, Encounter enc, CombatUnit u)
    {
        var basePull = Vector2.Zero;
        var reaction = Vector2.Zero;
        double boldness = UnitBoldness(world, enc, u);
        double engagementRangeM = Aggression(world, u) * 15;
        foreach (var enemy in Enemies(enc, u))
        {
            var direction = u.Position.DirectionTo(enemy.Position);
            double dist = u.Position.DistanceTo(enemy.Position);
            basePull += direction * BaseApproach;
            double advantage = boldness - PerceivedThreat(world, u, enemy);
            double distWeight = dist > 0 ? Math.Max(0.5, 3.0 / dist) : 0.5;
            if (advantage > MinAdvantage)
            {
                double aggressionFactor = dist <= engagementRangeM ? 1.0 : Math.Max(0, 1 - (dist - engagementRangeM) / 10);
                reaction += direction * (float)(Math.Max(0, advantage + MinAdvantage) * distWeight * aggressionFactor * AdvanceFactor);
            }
            else
                reaction -= direction * (float)((Math.Abs(advantage) - MinAdvantage) * distWeight * RetreatFactor);
        }
        var allyPull = Vector2.Zero;
        foreach (var ally in Allies(enc, u))
            if ((float)u.Position.DistanceTo(ally.Position) >= 2.0f)
                allyPull += (float)(Cohesion(world, u) * 0.5) * u.Position.DirectionTo(ally.Position);
        return basePull + reaction + allyPull;
    }

    /// The enemy an engaged predator commits to running down, if its courage holds.
    public static CombatUnit? PursuitTarget(World world, Encounter enc, CombatUnit u)
    {
        var stats = StatsOf(world, u);
        if (u.Awareness != Awareness.Engaged || !stats.IsAnimal || stats.IsSkittish) return null;
        var target = NearestEnemy(enc, u);
        return target != null && WantsToAttack(world, enc, u, target) && UnitBoldness(world, enc, u) > PerceivedThreat(world, u, target)
            ? target : null;
    }

    /// The reachable cell closest to the target within the allowance, never through an occupied
    /// cell or between two diagonally.
    public static Cell Pursue(World world, Encounter enc, CombatUnit u, CombatUnit target)
    {
        var occupied = Field(enc).Where(o => o != u && Alive(world, o)).Select(o => o.Position).ToHashSet();
        var costs = new Dictionary<Cell, double> { [u.Position] = 0 };
        var queue = new PriorityQueue<Cell, double>();
        queue.Enqueue(u.Position, 0);
        double budget = Allowance(world, u);
        while (queue.TryDequeue(out var current, out double cost))
        {
            if (cost > costs[current]) continue;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var next = new Cell(current.X + dx, current.Y + dy);
                    if (!InBounds(next) || occupied.Contains(next)) continue;
                    if (dx != 0 && dy != 0 && (occupied.Contains(new(current.X + dx, current.Y)) || occupied.Contains(new(current.X, current.Y + dy)))) continue;
                    double nextCost = cost + (dx != 0 && dy != 0 ? Math.Sqrt(2) : 1);
                    if (nextCost > budget || (costs.TryGetValue(next, out double existing) && existing <= nextCost)) continue;
                    costs[next] = nextCost;
                    queue.Enqueue(next, nextCost);
                }
        }
        return costs.Keys.OrderBy(p => p.DistanceTo(target.Position)).ThenBy(p => costs[p]).First();
    }

    /// Unaware and alert animals wander; skittish ones bolt; committed predators pursue; the rest
    /// steer by morale, as far as their legs allow.
    public static Cell MovePosition(World world, Encounter enc, CombatUnit u)
    {
        var rng = world.Rng("combat");
        if (u.Awareness != Awareness.Engaged)
        {
            if (rng.NextDouble() < 0.4) return u.Position;
            double angle = rng.NextDouble() * 2 * Math.PI;
            var wander = u.Position.Move(new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)), (float)(rng.NextDouble() * WanderM + 1));
            return new Cell(Math.Clamp(wander.X, 1, MapSize - 2), Math.Clamp(wander.Y, 1, MapSize - 2));
        }
        if (StatsOf(world, u).IsSkittish && NearestEnemy(enc, u) is CombatUnit threat)
            return u.Position.Away(threat.Position, Allowance(world, u));
        if (PursuitTarget(world, enc, u) is CombatUnit target) return Pursue(world, enc, u, target);
        var movement = MovementVector(world, enc, u);
        if (movement == Vector2.Zero) return u.Position;
        return u.Position.Move(movement, (float)Math.Min(movement.Length(), Allowance(world, u)));
    }

    // ---- the AI's turn ----

    /// What this unit does on its turn (old CombatAI.DetermineAction).
    public static CombatAction ChooseAction(World world, Encounter enc, CombatUnit u)
    {
        var rng = world.Rng("combat");
        if (u.Awareness == Awareness.Unaware) return rng.NextDouble() < 0.6 ? CombatAction.Wait : CombatAction.Move;
        if (u.Awareness == Awareness.Alert) return CombatAction.Move;
        var stats = StatsOf(world, u);
        if (NearestEnemy(enc, u) is not CombatUnit nearest || stats.IsSkittish) return CombatAction.Move; // a skittish one's Move bolts
        bool attack = WantsToAttack(world, enc, u, nearest);
        // Blocking needs something to block with; an animal sidesteps instead.
        var guard = stats.IsAnimal ? CombatAction.Dodge : CombatAction.Block;
        bool Coin() => rng.NextInt(2) == 0;
        return ZoneOf(u.Position.DistanceTo(nearest.Position)) switch
        {
            Zone.Close => attack ? CombatAction.Attack : Coin() ? guard : Coin() ? CombatAction.Shove : CombatAction.Move,
            Zone.Near => attack ? CombatAction.Attack : Coin() ? CombatAction.Move : Coin() ? guard : CombatAction.Dodge,
            _ => CombatAction.Move,
        };
    }

    /// One AI turn. A pursuing animal that closes to within 1.5 m strikes in the same turn.
    internal static void AiTurn(World world, Encounter enc, CombatUnit u, List<WorldEvent> events)
    {
        if (IsPlayer(u) || !u.InField || !Alive(world, u)) return;
        u.DodgeSet = u.BlockSet = false;
        var action = ChooseAction(world, enc, u);
        CombatUnit Target() => (u.Awareness == Awareness.Engaged ? NearestEnemy(enc, u) : null)
            ?? throw new InvalidOperationException($"{action} needs a target");
        switch (action)
        {
            case CombatAction.Wait: break;
            case CombatAction.Move:
                var from = u.Position;
                var pursued = PursuitTarget(world, enc, u);
                Move(world, enc, u, MovePosition(world, enc, u));
                if (pursued != null && pursued.InField && u.InField && from != u.Position && u.Position.DistanceTo(pursued.Position) <= 1.5)
                    Attack(world, enc, u, pursued, events);
                break;
            case CombatAction.Shove:
                if (u.Awareness == Awareness.Engaged && NearestEnemy(enc, u) is CombatUnit shoved) Shove(world, enc, u, shoved);
                break;
            case CombatAction.Attack: Attack(world, enc, u, Target(), events); break;
            case CombatAction.Dodge: u.DodgeSet = true; break;
            case CombatAction.Block: u.BlockSet = true; break;
            default: throw new NotSupportedException($"The AI never picks {action}");
        }
        Morale(world, enc, u, MoraleEvent.RoundAdvanced, null);
        u.JustDamaged = false;
        Sweep(world, enc, events, u.Agent);
    }
}
