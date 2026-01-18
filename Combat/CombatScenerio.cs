using System.Numerics;
using text_survival.Actions.Handlers;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Combat;

/// <summary>
/// Result of a combat action (attack, throw, etc.)
/// </summary>
public record CombatActionResult(
    bool Hit,
    bool Dodged,
    bool Blocked,
    DamageResult? Damage
);


public class CombatScenario
{
    public List<Unit> Units = new();
    public bool IsOver = false;
    public bool InvolvesPlayer => Player != null;
    public Unit? Player;
    public List<Gear> ThrownWeapons = new();
    private int _currentAIIndex;

    /// <summary>
    /// Location where combat is occurring. Used for visibility-based detection modifiers.
    /// </summary>
    public Location? Location { get; set; }

    public CombatScenario(List<Unit> team1, List<Unit> team2, Unit? player, Location? location = null)
    {
        Team1 = team1;
        Team2 = team2;
        Location = location;
        Units.AddRange(team1);
        Units.AddRange(team2);
        foreach (var unit in team1)
        {
            unit.allies = team1;
            unit.enemies = team2;
        }
        foreach (var unit in team2)
        {
            unit.allies = team2;
            unit.enemies = team1;
        }
        Player = player;
    }
    public readonly List<Unit> Team1;
    public readonly List<Unit> Team2;

    /// <summary>
    /// Process all AI turns in batch (for NPC-vs-NPC combat).
    /// </summary>
    public void ProcessAITurns()
    {
        foreach (Unit unit in Units.ToList())
        {
            ProcessSingleAITurn(unit);
            if (IsOver) return;
        }
    }

    /// <summary>
    /// Process a single AI unit's turn. Returns narrative of the action taken, or null if skipped.
    /// Used by CombatOrchestrator to show each turn individually.
    /// </summary>
    public string? ProcessSingleAITurn(Unit unit)
    {
        if (unit == Player) return null;
        if (!Units.Contains(unit)) return null; // Already removed (fled/died)
        if (!unit.actor.IsAlive) return null;

        unit.ResetStateBeforeTurn();
        var action = CombatAI.DetermineAction(unit, this);
        string narrative;

        if (action == CombatActions.Wait)
        {
            // Unaware/alert animals staying in place - no action needed
            narrative = $"The {unit.actor.Name.ToLower()} remains still.";
        }
        else if (action == CombatActions.Move)
        {
            var oldPos = unit.Position;
            var newPosition = CombatAI.DetermineMovePosition(unit);
            Move(unit, newPosition);
            var nearestEnemy = GetNearestEnemy(unit);
            if (nearestEnemy != null)
            {
                var oldDist = oldPos.DistanceTo(nearestEnemy.Position);
                var newDist = unit.Position.DistanceTo(nearestEnemy.Position);
                narrative = newDist < oldDist
                    ? $"The {unit.actor.Name.ToLower()} advances."
                    : $"The {unit.actor.Name.ToLower()} backs away.";
            }
            else
            {
                narrative = $"The {unit.actor.Name.ToLower()} moves.";
            }
        }
        else if (action == CombatActions.Shove)
        {
            var target = CombatAI.DetermineTarget(unit, this);
            if (target != null)
            {
                var (success, dodged) = Shove(unit, target);
                narrative = CombatNarrator.DescribeShove(unit.actor, target.actor, success, dodged);
            }
            else
            {
                narrative = $"The {unit.actor.Name.ToLower()} acts.";
            }
        }
        else if (ActionNeedsTarget(action))
        {
            var target = CombatAI.DetermineTarget(unit, this);
            var result = ExecuteAction(action, unit, target);
            narrative = GetActionNarrative(action, unit, target, result);
        }
        else
        {
            var result = ExecuteAction(action, unit, null);
            narrative = GetActionNarrative(action, unit, null, result);
        }

        unit.ApplyBoldnessChange(MoraleEvent.RoundAdvanced, null);
        unit.ResetStateAfterTurn();

        if (!unit.actor.IsAlive) HandleUnitDeath(unit);
        CheckIfOver();

        return narrative;
    }

    /// <summary>
    /// Resets AI turn tracking. Call at start of AI phase each round.
    /// </summary>
    public void ResetAITurns(Unit playerUnit)
    {
        _currentAIIndex = 0;
    }

    /// <summary>
    /// Returns true if there are more AI turns remaining this round.
    /// </summary>
    public bool HasRemainingAITurns(Unit playerUnit)
    {
        if (IsOver) return false;
        var aiUnits = Units.Where(u => u != playerUnit && u.actor.IsAlive).ToList();
        return _currentAIIndex < aiUnits.Count;
    }

    /// <summary>
    /// Executes the next AI turn. Returns narrative text.
    /// </summary>
    public string? RunNextAITurn(Unit playerUnit)
    {
        var aiUnits = Units.Where(u => u != playerUnit && u.actor.IsAlive).ToList();
        if (_currentAIIndex >= aiUnits.Count) return null;

        var unit = aiUnits[_currentAIIndex];
        _currentAIIndex++;
        return ProcessSingleAITurn(unit);
    }

    private string GetActionNarrative(CombatActions action, Unit actor, Unit? target, CombatActionResult? result)
    {
        string actorName = $"The {actor.actor.Name.ToLower()}";
        string targetName = target != null ? $"the {target.actor.Name.ToLower()}" : "";

        // For attacks with results, use the narrator
        if ((action == CombatActions.Attack || action == CombatActions.Throw) && result != null && target != null)
        {
            return CombatNarrator.DescribeAttack(actor.actor, target.actor, result);
        }

        return action switch
        {
            CombatActions.Attack => $"{actorName} attacks {targetName}!",
            CombatActions.Throw => $"{actorName} throws at {targetName}!",
            CombatActions.Dodge => $"{actorName} readies to dodge.",
            CombatActions.Block => $"{actorName} raises its guard.",
            CombatActions.Shove => $"{actorName} shoves {targetName}!",
            CombatActions.Intimidate => CombatNarrator.DescribeIntimidate(actor.actor, isPlayer: false),
            _ => $"{actorName} acts."
        };
    }

    private void CheckIfOver()
    {
        // Player death ends combat immediately
        if (Player != null && !Player.actor.IsAlive)
        {
            IsOver = true;
            return;
        }
        var team1Alive = Units.Any(u => Team1.Contains(u));
        var team2Alive = Units.Any(Team2.Contains);
        IsOver = !team1Alive || !team2Alive;
    }
    private void HandleUnitDeath(Unit unit)
    {
        unit.allies.ToList().ForEach(u => u.ApplyBoldnessChange(MoraleEvent.AllyKilled, unit));
        unit.allies.ToList().ForEach(u => u.allies.Remove(unit));
        unit.enemies.ToList().ForEach(u => u.enemies.Remove(unit));
        Units.Remove(unit);
    }

    // primary actions
    public void Move(Unit unit, GridPosition destination)
    {
        destination = ResolveDestination(destination, unit);
        var moveDirection = unit.Position.DirectionTo(destination);
        unit.Position = destination;
        foreach (var enemy in unit.enemies.ToList())
        {
            var enemyDirection = unit.Position.DirectionTo(enemy.Position);
            float dot = Vector2.Dot(moveDirection, enemyDirection); // if positive - same direction, if negative - opposite
            float dotThreshold = 0.5f; // 30 degree buffer of lateral movement doesn't count as either
            if (dot > dotThreshold)
            {
                enemy.ApplyBoldnessChange(MoraleEvent.EnemyAdvanced, unit);
            }
            else if (dot < -dotThreshold)
            {
                enemy.ApplyBoldnessChange(MoraleEvent.EnemyRetreated, unit);
            }
        }
        if (IsAtMapEdge(unit))
        {
            Flee(unit);
        }
    }
    private bool IsAtMapEdge(Unit unit)
    {
        var pos = unit.Position;
        return pos.X < 0 || pos.X >= MAP_SIZE || pos.Y < 0 || pos.Y >= MAP_SIZE;
    }
    public CombatActionResult Attack(Unit attacker, Unit defender)
    {
        double hitChance = 0.9;
        if (Utils.DetermineSuccess(hitChance))
        {
            return ApplyDamage(attacker, defender);
        }
        return new CombatActionResult(Hit: false, Dodged: false, Blocked: false, Damage: null);
    }
    public CombatActionResult RangedAttack(Unit attacker, Unit target)
    {
        var weapon = attacker.GetWeapon() ?? throw new InvalidOperationException("No weapon");
        double dist = attacker.Position.DistanceTo(target.Position);

        // Use weapon-specific range and accuracy
        double maxRange = weapon.Name.Contains("Stone") ? 25.0 : 20.0; // Stone-tipped spears have longer range
        double baseAccuracy = weapon.Name.Contains("Stone") ? 0.75 : 0.70;

        // Calculate hit chance with small target penalty
        bool isSmallTarget = target.actor is Animal animal && animal.Size == AnimalSize.Small;
        double hitChance = HuntingCalculator.CalculateThrownAccuracy(
            dist, maxRange, baseAccuracy,
            targetIsSmall: isSmallTarget);

        CombatActionResult result;
        if (Utils.DetermineSuccess(hitChance))
        {
            result = ApplyDamage(attacker, target);
        }
        else
        {
            result = new CombatActionResult(Hit: false, Dodged: false, Blocked: false, Damage: null);
        }
        attacker.UnequipWeapon(); // do this after for damage calculations
        ThrownWeapons.Add(weapon); // for retrieval after
        return result;
    }

    /// <summary>
    /// Stone throw attack - secondary ranged option. Lower damage but doesn't require equipped weapon.
    /// </summary>
    public CombatActionResult StoneAttack(Unit attacker, Unit target)
    {
        double dist = attacker.Position.DistanceTo(target.Position);

        // Calculate hit chance with small target penalty (0.66 multiplier)
        bool isSmallTarget = target.actor is Animal animal && animal.Size == AnimalSize.Small;
        double hitChance = HuntingCalculator.CalculateThrownAccuracy(
            dist, HuntHandler.GetStoneRange(), HuntHandler.GetStoneBaseAccuracy(),
            targetIsSmall: isSmallTarget);

        CombatActionResult result;
        if (Utils.DetermineSuccess(hitChance))
        {
            // Stone does 0.25x damage compared to normal attack
            var damage = attacker.actor.GetAttackDamage();
            damage.Amount *= 0.25;

            // Apply ambush multiplier
            double awarenessMultiplier = GetAwarenessDamageMultiplier(target.Awareness);
            damage.Amount *= awarenessMultiplier;

            // Being attacked makes you Engaged
            if (target.Awareness != AwarenessState.Engaged)
            {
                target.Awareness = AwarenessState.Engaged;
            }

            var damageResult = target.actor.Damage(damage);

            attacker.ApplyBoldnessChange(MoraleEvent.DealtDamage, target);
            target.ApplyBoldnessChange(MoraleEvent.TookDamage, attacker);

            target.allies.ForEach(x => x.ApplyBoldnessChange(MoraleEvent.AllyDamaged, target));
            attacker.allies.ForEach(x => x.ApplyBoldnessChange(MoraleEvent.AllyDealtDamage, attacker));

            if (!target.actor.IsAlive)
            {
                HandleUnitDeath(target);
                CheckIfOver();
            }

            result = new CombatActionResult(Hit: true, Dodged: false, Blocked: false, Damage: damageResult);
        }
        else
        {
            result = new CombatActionResult(Hit: false, Dodged: false, Blocked: false, Damage: null);
        }

        return result;
    }
    private CombatActionResult ApplyDamage(Unit attacker, Unit defender)
    {
        // Check dodge first (only if defender is aware)
        if (defender.Awareness == AwarenessState.Engaged && defender.DodgeSet && ResolveDodge(defender, attacker))
        {
            return new CombatActionResult(Hit: false, Dodged: true, Blocked: false, Damage: null);
        }

        var damage = attacker.actor.GetAttackDamage();

        // Apply ambush damage multiplier (2x if target is Unaware)
        double awarenessMultiplier = GetAwarenessDamageMultiplier(defender.Awareness);
        damage.Amount *= awarenessMultiplier;

        // Block only works if defender is aware
        bool blocked = defender.Awareness == AwarenessState.Engaged && defender.BlockSet;
        if (blocked) damage.Amount = ResolveBlock(defender, attacker, damage.Amount);

        var damageResult = defender.actor.Damage(damage);

        // Being attacked makes you Engaged
        if (defender.Awareness != AwarenessState.Engaged)
        {
            defender.Awareness = AwarenessState.Engaged;
        }

        attacker.ApplyBoldnessChange(MoraleEvent.DealtDamage, defender);
        defender.ApplyBoldnessChange(MoraleEvent.TookDamage, attacker);

        defender.allies.ForEach(x => x.ApplyBoldnessChange(MoraleEvent.AllyDamaged, defender));
        attacker.allies.ForEach(x => x.ApplyBoldnessChange(MoraleEvent.AllyDealtDamage, attacker));
        if (!defender.actor.IsAlive)
        {
            HandleUnitDeath(defender);
            CheckIfOver();
        }

        return new CombatActionResult(Hit: true, Dodged: false, Blocked: blocked, Damage: damageResult);
    }
    public void Dodge(Unit unit) { unit.DodgeSet = true; }
    public void Block(Unit unit) { unit.BlockSet = true; }
    // public void Brace(Unit unit) { unit.BraceSet = true; } // todo

    public (bool success, bool dodged) Shove(Unit attacker, Unit target)
    {
        double strengthRatio = attacker.actor.Strength / target.actor.Strength;
        double weightRatio = attacker.actor.Body.WeightKG / target.actor.Body.WeightKG;
        double baseChance = .5;
        double successChance = baseChance * strengthRatio * weightRatio;
        if (target.BlockSet || target.BraceSet) successChance /= 2;
        if (!Utils.DetermineSuccess(successChance))
        {
            return (success: false, dodged: false);
        }

        if (target.DodgeSet && ResolveDodge(target, attacker))
        {
            return (success: false, dodged: true);
        }

        float pushDistance = (float)(1.0 + (2 * strengthRatio) + weightRatio);
        var direction = attacker.Position.DirectionTo(target.Position);
        var pushDest = target.Position.Move(direction, pushDistance);
        Move(target, pushDest);
        return (success: true, dodged: false);
    }
    public void Intimidate(Unit unit)
    {
        unit.enemies.ForEach(e => e.ApplyBoldnessChange(MoraleEvent.Intimidated, unit));
    }

    public CombatActionResult? ExecuteAction(CombatActions action, Unit unit, Unit? target = null)
    {
        if (ActionNeedsTarget(action) && target == null)
            throw new InvalidOperationException($"Target required for {action}");

        switch (action)
        {
            case CombatActions.Attack: return Attack(unit, target!);
            case CombatActions.Throw: return RangedAttack(unit, target!);
            case CombatActions.ThrowStone: return StoneAttack(unit, target!);
            case CombatActions.Dodge: Dodge(unit); return null;
            case CombatActions.Block: Block(unit); return null;
            case CombatActions.Shove: Shove(unit, target!); return null;
            case CombatActions.Intimidate: Intimidate(unit); return null;
            case CombatActions.Move: throw new InvalidOperationException("Call move directly");
            default: throw new NotImplementedException();
        }
    }

    // secondary actions
    private bool ResolveDodge(Unit defender, Unit attacker)
    {
        double baseChance = .6;
        double successChance = baseChance * defender.actor.Speed;

        if (Utils.DetermineSuccess(successChance))
        {
            defender.actor.Body.Energy -= 10;
            var direction = attacker.Position.DirectionTo(defender.Position);
            var pushDest = defender.Position.Move(direction, 1.0f);
            Move(defender, pushDest);
            return true;
        }
        else
        {
            return false;
        }
    }
    private double ResolveBlock(Unit defender, Unit attacker, double incomingDamage)
    {
        Gear? weapon = defender.GetWeapon();
        if (weapon == null) return incomingDamage * Utils.RandDouble(.5, .95);
        weapon.Use();
        return incomingDamage * Utils.RandDouble(0, .7);
    }
    private void Flee(Unit unit)
    {
        Units.Remove(unit);
        foreach (Unit ally in unit.allies.ToList())
        {
            ally.allies.Remove(unit);
            ally.ApplyBoldnessChange(MoraleEvent.AllyFled, unit);
        }
        foreach (Unit enemy in unit.enemies.ToList())
        {
            enemy.enemies.Remove(unit);
        }
        CheckIfOver();
    }

    // Helpers
    public Unit? GetNearestEnemy(Unit from) =>
        from.enemies
            .Where(e => Units.Contains(e))
            .OrderBy(e => from.Position.DistanceTo(e.Position))
            .FirstOrDefault();

    public bool ActionNeedsTarget(CombatActions action) => action switch
    {
        CombatActions.Attack => true,
        CombatActions.Throw => true,
        CombatActions.ThrowStone => true,
        CombatActions.Shove => true,
        _ => false,
    };
    public static Zone GetZone(double dist) => dist switch
    {
        <= 1 => Zone.close,
        <= 3 => Zone.near,
        <= 15 => Zone.mid,
        _ => Zone.far
    };
    public const int MAP_SIZE = 50;
    private const int FLEE_THRESHOLD = 3;
    private const int FLEE_ENERGY_COST = 20;
    private static readonly Random _rng = new();

    /// <summary>
    /// Resolves a destination to an unoccupied tile. If dest is occupied,
    /// returns a random unoccupied neighbor. If none available, returns mover's current position.
    /// </summary>
    private GridPosition ResolveDestination(GridPosition dest, Unit mover)
    {
        bool occupied = Units.Any(u => u != mover && u.actor.IsAlive &&
            u.Position.X == dest.X && u.Position.Y == dest.Y);
        if (!occupied) return dest;

        var neighbors = GetAllNeighbors(dest)
            .Where(p => IsInBounds(p))
            .Where(p => !Units.Any(u => u != mover && u.actor.IsAlive &&
                u.Position.X == p.X && u.Position.Y == p.Y))
            .ToList();

        if (neighbors.Count == 0) return mover.Position;
        return neighbors[_rng.Next(neighbors.Count)];
    }

    private static IEnumerable<GridPosition> GetAllNeighbors(GridPosition pos)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    yield return new GridPosition(pos.X + dx, pos.Y + dy);
    }

    private static bool IsInBounds(GridPosition pos) =>
        pos.X >= 0 && pos.X < MAP_SIZE && pos.Y >= 0 && pos.Y < MAP_SIZE;

    public static int GetDistanceFromEdge(GridPosition pos)
    {
        int distLeft = pos.X;
        int distRight = MAP_SIZE - 1 - pos.X;
        int distTop = pos.Y;
        int distBottom = MAP_SIZE - 1 - pos.Y;
        return Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));
    }

    public static bool CanFlee(GridPosition pos) => GetDistanceFromEdge(pos) <= FLEE_THRESHOLD;

    public bool ExecuteFlee(Unit unit)
    {
        if (!CanFlee(unit.Position)) return false;
        unit.actor.Body.Energy -= FLEE_ENERGY_COST;
        Flee(unit);
        return true;
    }

    #region Awareness & Detection

    /// <summary>
    /// Run detection checks for all enemies of the acting unit.
    /// Call after any action when enemies are Unaware or Alert.
    /// Returns list of units whose awareness changed.
    /// </summary>
    public List<(Unit unit, AwarenessState oldState, AwarenessState newState)> RunDetectionChecks(Unit actingUnit, int huntingSkill = 0)
    {
        var changes = new List<(Unit, AwarenessState, AwarenessState)>();

        foreach (var enemy in actingUnit.enemies.ToList())
        {
            if (enemy.Awareness == AwarenessState.Engaged) continue;
            if (!Units.Contains(enemy)) continue;

            var oldState = enemy.Awareness;
            var newState = CheckDetection(actingUnit, enemy, huntingSkill);

            if (newState != oldState)
            {
                enemy.Awareness = newState;
                changes.Add((enemy, oldState, newState));
            }
        }

        return changes;
    }

    /// <summary>
    /// Check if a detector (enemy) detects the mover.
    /// Returns the new awareness state.
    /// </summary>
    private AwarenessState CheckDetection(Unit mover, Unit detector, int huntingSkill)
    {
        double distance = mover.Position.DistanceTo(detector.Position);

        double detectionChance = HuntingCalculator.CalculateDetectionChance(
            distance,
            detector.Awareness,
            huntingSkill,
            0 // failedAttempts - could track this per unit if desired
        );

        // Location visibility reduces detection (good cover = lower detection)
        if (Location != null)
        {
            // VisibilityFactor: 0 = deep cave/thick forest (good cover), 2 = open plain/overlook (no cover)
            // Lower visibility = more cover = lower detection
            double visibilityNormalized = Location.VisibilityFactor / 2.0; // 0-1 scale
            double coverBonus = (1.0 - visibilityNormalized) * 0.3; // Up to 30% reduction in detection
            detectionChance *= (1.0 - coverBonus);
        }

        double roll = _rng.NextDouble();

        if (roll < detectionChance)
        {
            // Detected - escalate awareness
            return detector.Awareness == AwarenessState.Unaware
                ? AwarenessState.Alert
                : AwarenessState.Engaged;
        }
        else if (HuntingCalculator.ShouldBecomeAlert(roll, detectionChance) && detector.Awareness == AwarenessState.Unaware)
        {
            // Near-miss becomes alert
            return AwarenessState.Alert;
        }

        return detector.Awareness;
    }

    /// <summary>
    /// Calculate detection chance for Assess action (shows risk without rolling).
    /// </summary>
    public double CalculateDetectionRisk(Unit mover, Unit detector, int huntingSkill)
    {
        double distance = mover.Position.DistanceTo(detector.Position);

        double detectionChance = HuntingCalculator.CalculateDetectionChance(
            distance,
            detector.Awareness,
            huntingSkill,
            0
        );

        // Apply location visibility modifier
        if (Location != null)
        {
            double visibilityNormalized = Location.VisibilityFactor / 2.0; // 0-1 scale
            double coverBonus = (1.0 - visibilityNormalized) * 0.3; // Up to 30% reduction
            detectionChance *= (1.0 - coverBonus);
        }

        return Math.Clamp(detectionChance, 0.05, 0.95);
    }

    /// <summary>
    /// Calculate damage multiplier based on defender's awareness state.
    /// Unaware targets take 2x damage (ambush bonus).
    /// </summary>
    public static double GetAwarenessDamageMultiplier(AwarenessState awareness)
    {
        return awareness switch
        {
            AwarenessState.Unaware => 2.0,
            AwarenessState.Alert => 1.0,
            AwarenessState.Engaged => 1.0,
            _ => 1.0
        };
    }

    #endregion
}

public enum CombatActions { Move, Attack, Throw, ThrowStone, Dodge, Block, Shove, Intimidate, Advance, Retreat, Flee, Assess, Wait }

public enum Zone { close, near, mid, far }