using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Combat;


public class CombatScenario
{
    public List<Unit> Units = new();
    public bool IsOver = false;
    public bool InvolvesPlayer => Player != null;
    public Unit? Player;
    public List<Gear> ThrownWeapons = new();
    private int _currentAIIndex;
    public CombatScenario(List<Unit> team1, List<Unit> team2, Unit? player)
    {
        Team1 = team1;
        Team2 = team2;
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

        if (action == CombatActions.Move)
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
        else if (ActionNeedsTarget(action))
        {
            var target = CombatAI.DetermineTarget(unit, this);
            ExecuteAction(action, unit, target);
            narrative = GetActionNarrative(action, unit, target);
        }
        else
        {
            ExecuteAction(action, unit, null);
            narrative = GetActionNarrative(action, unit, null);
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

    private string GetActionNarrative(CombatActions action, Unit actor, Unit? target)
    {
        string actorName = $"The {actor.actor.Name.ToLower()}";
        string targetName = target != null ? $"the {target.actor.Name.ToLower()}" : "";

        return action switch
        {
            CombatActions.Attack => $"{actorName} attacks {targetName}!",
            CombatActions.Throw => $"{actorName} throws at {targetName}!",
            CombatActions.Dodge => $"{actorName} readies to dodge.",
            CombatActions.Block => $"{actorName} raises its guard.",
            CombatActions.Shove => $"{actorName} shoves {targetName}!",
            CombatActions.Intimidate => $"{actorName} tries to intimidate.",
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
    public void Attack(Unit attacker, Unit defender)
    {
        double hitChance = 0.9;
        if (Utils.DetermineSuccess(hitChance))
        {
            ApplyDamage(attacker, defender);
        }
    }
    public void RangedAttack(Unit attacker, Unit target)
    {
        var weapon = attacker.GetWeapon() ?? throw new InvalidOperationException("No weapon");
        double dist = attacker.Position.DistanceTo(target.Position);
        double maxRange = 20.0;
        double baseHitChance = .9;
        double hitChance = baseHitChance * (1 - dist / maxRange);

        if (Utils.DetermineSuccess(hitChance))
        {
            ApplyDamage(attacker, target);
        }
        attacker.UnequipWeapon(); // do this after for damage calculations
        ThrownWeapons.Add(weapon); // for retrieval after
        // todo spawn ranged weapon on ground
    }
    private void ApplyDamage(Unit attacker, Unit defender)
    {
        if (defender.DodgeSet && ResolveDodge(defender, attacker)) return;

        var damage = attacker.actor.GetAttackDamage();
        if (defender.BlockSet) damage.Amount = ResolveBlock(defender, attacker, damage.Amount);

        defender.actor.Damage(damage);

        attacker.ApplyBoldnessChange(MoraleEvent.DealtDamage, defender);
        defender.ApplyBoldnessChange(MoraleEvent.TookDamage, attacker);

        defender.allies.ForEach(x => x.ApplyBoldnessChange(MoraleEvent.AllyDamaged, defender));
        attacker.allies.ForEach(x => x.ApplyBoldnessChange(MoraleEvent.AllyDealtDamage, attacker));
        if (!defender.actor.IsAlive)
        {
            HandleUnitDeath(defender);
            CheckIfOver();
        }
    }
    public void Dodge(Unit unit) { unit.DodgeSet = true; }
    public void Block(Unit unit) { unit.BlockSet = true; }
    // public void Brace(Unit unit) { unit.BraceSet = true; } // todo

    public void Shove(Unit attacker, Unit target)
    {
        double strengthRatio = attacker.actor.Strength / target.actor.Strength;
        double weightRatio = attacker.actor.Body.WeightKG / target.actor.Body.WeightKG;
        double baseChance = .5;
        double successChance = baseChance * strengthRatio * weightRatio;
        if (target.BlockSet || target.BraceSet) successChance /= 2;
        if (Utils.DetermineSuccess(successChance))
        {
            if (target.DodgeSet && ResolveDodge(target, attacker)) return;

            float pushDistance = (float)(1.0 + (2 * strengthRatio) + weightRatio);
            var direction = attacker.Position.DirectionTo(target.Position);
            target.Position = target.Position.Move(direction, pushDistance);
        }
    }
    public void Intimidate(Unit unit)
    {
        unit.enemies.ForEach(e => e.ApplyBoldnessChange(MoraleEvent.Intimidated, unit));
    }

    public void ExecuteAction(CombatActions action, Unit unit, Unit? target = null)
    {
        if (ActionNeedsTarget(action) && target == null)
            throw new InvalidOperationException($"Target required for {action}");

        switch (action)
        {
            case CombatActions.Attack: Attack(unit, target!); break;
            case CombatActions.Throw: RangedAttack(unit, target!); break;
            case CombatActions.Dodge: Dodge(unit); break;
            case CombatActions.Block: Block(unit); break;
            case CombatActions.Shove: Shove(unit, target!); break;
            // case CombatActions.Brace: Brace(unit); break;
            case CombatActions.Intimidate: Intimidate(unit); break;
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
            defender.Position = defender.Position.Move(direction, 1.0f);
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
    private const int MAP_SIZE = 25;
}

public enum CombatActions { Move, Attack, Throw, Dodge, Block, Shove, Intimidate, Advance, Retreat }

public enum Zone { close, near, mid, far }