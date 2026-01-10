using System.Numerics;
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

    public void ProcessAITurns()
    {
        foreach (Unit unit in Units)
        {
            if (unit == Player) continue;
            if (unit.actor.IsAlive) // normally handled when killed by combat but the actor could die between turns via bleeding or hypothermia etc.
            {
                unit.ResetStateBeforeTurn();
                var move = CombatAI.DetermineAction(unit, this);
                if (move == CombatActions.Move)
                {
                    var newPosition = CombatAI.DetermineMovePosition(unit);
                    Move(unit, newPosition);
                }
                else if (ActionNeedsTarget(move))
                {
                    var target = CombatAI.DetermineTarget(unit, this);
                    ExecuteAction(move, unit, target);
                }
                else
                {
                    ExecuteAction(move, unit, null);
                }
                unit.ApplyBoldnessChange(MoraleEvent.RoundAdvanced, null);
                unit.ResetStateAfterTurn();
            }
            if (!unit.actor.IsAlive) HandleUnitDeath(unit);
            CheckIfOver();
            if (IsOver) return;
        }
    }

    private void CheckIfOver()
    {
        var team1Alive = Units.Any(u => Team1.Contains(u));
        var team2Alive = Units.Any(Team2.Contains);
        IsOver = !team1Alive || !team2Alive;
    }
    private void HandleUnitDeath(Unit unit)
    {
        unit.allies.ForEach(u=>u.ApplyBoldnessChange(MoraleEvent.AllyKilled, unit));
        unit.allies.ForEach(u=>u.allies.Remove(unit));
        unit.enemies.ForEach(u=>u.enemies.Remove(unit));
        Units.Remove(unit);
    }

    // primary actions
    private void Move(Unit unit, GridPosition destination)
    {
        var moveDirection = unit.Position.DirectionTo(destination);
        unit.Position = destination;
        foreach (var enemy in unit.enemies)
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
        if (pos.X >= MAP_SIZE || pos.Y >= MAP_SIZE || pos.X <= 0 || pos.Y <= 0) return true;
        return false;
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
        if (!defender.actor.IsAlive) ;
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
        foreach (Unit ally in unit.allies)
        {
            ally.allies.Remove(unit);
            ally.ApplyBoldnessChange(MoraleEvent.AllyFled, unit);
        }
        foreach (Unit enemy in unit.enemies)
        {
            enemy.enemies.Remove(unit);
        }
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
    public Zone GetZone(double dist) => dist switch
    {
        <= 1 => Zone.close,
        <= 3 => Zone.near,
        <= 15 => Zone.mid,
        _ => Zone.far
    };
    private const int MAP_SIZE = 25;
}

public enum CombatActions { Move, Attack, Throw, Dodge, Block, Shove, Intimidate }

public enum Zone { close, near, mid, far }