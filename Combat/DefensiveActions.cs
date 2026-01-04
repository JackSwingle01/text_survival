using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Combat;

/// <summary>
/// Result of a defensive action attempt.
/// </summary>
public record DefenseResult(
    bool Success,
    double DamageReduction,  // 0-1, how much damage was avoided/reduced
    double EnergyCost,
    string Narrative,
    DistanceZone? NewZone = null  // If defense moved player to new zone
);

/// <summary>
/// Handles defensive action resolution in combat.
/// All defensive options have costs and effectiveness scaled by player state.
/// </summary>
public static class DefensiveActions
{
    private static readonly Random _rng = new();

    // Energy costs (small, as per user request)
    private const double DodgeEnergyCost = 0.03;
    private const double BlockEnergyCost = 0.02;
    private const double BraceEnergyCost = 0.01;
    private const double GiveGroundEnergyCost = 0.01;

    // Base success rates
    private const double BaseDodgeSuccess = 0.70;
    private const double BaseBlockAbsorption = 0.60;
    private const double BaseBraceMultiplier = 2.0;

    #region Availability Checks

    /// <summary>
    /// Checks if actor can dodge (requires movement capacity).
    /// </summary>
    public static bool CanDodge(ICombatActor actor)
    {
        var capacities = actor.ActorReference.GetCapacities();
        return capacities.Moving > 0.3;
    }

    /// <summary>
    /// Checks if actor can block (requires weapon or shield).
    /// </summary>
    public static bool CanBlock(ICombatActor actor)
    {
        var weapon = actor.Weapon;
        if (weapon == null) return false;

        var capacities = actor.ActorReference.GetCapacities();
        return capacities.Manipulation > 0.3;
    }

    /// <summary>
    /// Checks if actor can brace (requires spear-type weapon).
    /// </summary>
    public static bool CanBrace(ICombatActor actor)
    {
        var weapon = actor.Weapon;
        if (weapon == null) return false;

        // Spears can brace
        bool isSpear = weapon.WeaponClass == WeaponClass.Pierce;
        var capacities = actor.ActorReference.GetCapacities();
        return isSpear && capacities.Manipulation > 0.4;
    }

    /// <summary>
    /// Checks if player can give ground (requires space behind).
    /// </summary>
    public static bool CanGiveGround(DistanceZone currentZone)
    {
        // Can't give ground if already at Far zone
        return currentZone != DistanceZone.Far;
    }

    /// <summary>
    /// Gets the reason why a defensive action is unavailable.
    /// </summary>
    public static string? GetUnavailableReason(CombatPlayerAction action, ICombatActor actor, DistanceZone zone)
    {
        return action switch
        {
            CombatPlayerAction.Dodge => !CanDodge(actor) ? "Too injured to dodge" : null,
            CombatPlayerAction.Block => !CanBlock(actor) ? "No weapon to block with" : null,
            CombatPlayerAction.Brace => !CanBrace(actor) ? "Need a spear to brace" : null,
            CombatPlayerAction.GiveGround => !CanGiveGround(zone) ? "No room to retreat" : null,
            _ => null
        };
    }

    #endregion

    #region Dodge

    /// <summary>
    /// Attempt to dodge an incoming attack.
    /// Success avoids damage entirely but costs energy and pushes actor back.
    /// </summary>
    public static DefenseResult AttemptDodge(ICombatActor defender, CombatState state, double incomingDamage)
    {
        var capacities = defender.ActorReference.GetCapacities();
        double energy = defender.ActorReference.Body.Energy / 480.0;

        // Calculate dodge chance based on defender state
        double dodgeChance = CalculateDodgeChance(defender, state.Animal);

        // Energy affects effectiveness
        double energyFactor = 0.5 + (energy * 0.5); // 50-100% effectiveness
        dodgeChance *= energyFactor;

        bool success = _rng.NextDouble() < dodgeChance;

        // Energy cost is tracked in DefenseResult, drain happens via update

        // Determine new zone (dodging pushes you back)
        DistanceZone? newZone = null;
        if (success)
        {
            var fartherZone = DistanceZoneHelper.GetFartherZone(state.Zone);
            if (fartherZone.HasValue)
            {
                newZone = fartherZone.Value;
            }
        }

        string narrative = success
            ? GetDodgeSuccessNarrative(defender.Name, state.Animal.Name)
            : GetDodgeFailNarrative(defender.Name, state.Animal.Name);

        return new DefenseResult(
            Success: success,
            DamageReduction: success ? 1.0 : 0.0,
            EnergyCost: DodgeEnergyCost,
            Narrative: narrative,
            NewZone: newZone
        );
    }

    private static double CalculateDodgeChance(ICombatActor defender, Animal enemy)
    {
        var capacities = defender.ActorReference.GetCapacities();
        double baseChance = BaseDodgeSuccess;

        // Movement capacity strongly affects dodge availability (raw body function)
        baseChance *= capacities.Moving;

        // Reflexes skill bonus (Player-specific)
        if (defender.ActorReference is Player player)
        {
            baseChance += player.Skills.Reflexes.Level * 0.02;
        }

        // Speed difference matters - use actor speed property
        double defenderSpeed = defender.Speed * 6.0; // Scale to ~m/s
        double speedRatio = defenderSpeed / enemy.SpeedMps;
        baseChance *= Math.Min(1.3, speedRatio);

        return Math.Clamp(baseChance, 0.1, 0.9);
    }

    private static string GetDodgeSuccessNarrative(string defenderName, string attackerName)
    {
        var options = new[]
        {
            $"{defenderName} twist aside as the {attackerName} lunges past.",
            $"{defenderName} dive clear of the {attackerName}'s attack.",
            $"{defenderName} sidestep the {attackerName}'s charge.",
            $"The {attackerName} snaps at empty air as {defenderName} duck away."
        };
        return options[_rng.Next(options.Length)];
    }

    private static string GetDodgeFailNarrative(string defenderName, string attackerName)
    {
        var options = new[]
        {
            $"{defenderName} try to dodge but the {attackerName} catches them.",
            $"Too slow! The {attackerName}'s attack connects with {defenderName}.",
            $"{defenderName} stumble trying to evade. The {attackerName} is on them."
        };
        return options[_rng.Next(options.Length)];
    }

    #endregion

    #region Block

    /// <summary>
    /// Attempt to block an incoming attack with weapon.
    /// Reduces damage but costs energy and weapon durability.
    /// </summary>
    public static DefenseResult AttemptBlock(ICombatActor defender, CombatState state, double incomingDamage)
    {
        var weapon = defender.Weapon;
        if (weapon == null)
        {
            return new DefenseResult(false, 0, 0, $"{defender.Name} have nothing to block with!");
        }

        var capacities = defender.ActorReference.GetCapacities();
        double energy = defender.ActorReference.Body.Energy / 480.0;

        // Calculate block effectiveness
        double blockAmount = CalculateBlockAmount(defender, weapon);

        // Energy affects effectiveness
        double energyFactor = 0.5 + (energy * 0.5);
        blockAmount *= energyFactor;

        // Energy cost is tracked in DefenseResult, drain happens via update

        // Damage weapon (apply wear)
        weapon.Use();

        string narrative = GetBlockNarrative(defender.Name, state.Animal.Name, weapon.Name, blockAmount);

        return new DefenseResult(
            Success: true,
            DamageReduction: Math.Clamp(blockAmount, 0, 0.8),
            EnergyCost: BlockEnergyCost,
            Narrative: narrative
        );
    }

    private static double CalculateBlockAmount(ICombatActor defender, Gear weapon)
    {
        double baseBlock = BaseBlockAbsorption;

        // Manipulation affects block
        var capacities = defender.ActorReference.GetCapacities();
        baseBlock *= capacities.Manipulation;

        // Defense skill bonus (Player-specific)
        if (defender.ActorReference is Player player)
        {
            baseBlock += player.Skills.Defense.Level * 0.02;
        }

        // Weapon condition affects block
        baseBlock *= weapon.ConditionPct;

        return Math.Clamp(baseBlock, 0.2, 0.8);
    }

    private static string GetBlockNarrative(string defenderName, string attackerName, string weaponName, double blockAmount)
    {
        if (blockAmount > 0.6)
        {
            return $"{defenderName} catch the {attackerName}'s attack on their {weaponName}, deflecting most of the force.";
        }
        else if (blockAmount > 0.4)
        {
            return $"{defenderName}'s {weaponName} absorbs some of the impact as the {attackerName} strikes.";
        }
        else
        {
            return $"{defenderName} partially block with their {weaponName}, but the blow still hits hard.";
        }
    }

    #endregion

    #region Brace

    /// <summary>
    /// Result of a brace defense against a charging animal.
    /// </summary>
    public record BraceResult(
        bool Success,
        double DamageReduction,
        double CounterDamage,  // Damage dealt to charging animal
        double EnergyCost,
        string Narrative
    );

    /// <summary>
    /// Resolve a brace against a charging animal.
    /// If animal charges into brace, defender takes reduced damage and deals counter-damage.
    /// </summary>
    public static BraceResult ResolveBrace(ICombatActor defender, CombatState state, double incomingDamage)
    {
        var weapon = defender.Weapon;
        if (weapon == null)
        {
            return new BraceResult(false, 0, 0, 0, $"{defender.Name} have nothing to brace with!");
        }

        // Was the animal attacking?
        bool wasAttacking = state.Behavior.CurrentBehavior == CombatBehavior.Attacking;

        // Energy cost is tracked in BraceResult, drain happens via update

        if (!wasAttacking)
        {
            // Brace wasted - animal didn't attack
            return new BraceResult(
                Success: false,
                DamageReduction: 0,
                CounterDamage: 0,
                EnergyCost: BraceEnergyCost,
                Narrative: $"{defender.Name} hold their {weapon.Name} ready, but the {state.Animal.Name} doesn't attack."
            );
        }

        // Animal attacked into the brace!
        var capacities = defender.ActorReference.GetCapacities();
        double energy = defender.ActorReference.Body.Energy / 480.0;

        // Calculate counter damage (weapon damage * brace multiplier)
        double baseDamage = weapon.Damage ?? 10;
        double counterDamage = baseDamage * BaseBraceMultiplier;

        // Energy and manipulation affect counter damage
        double effectiveFactor = (0.5 + (energy * 0.5)) * capacities.Manipulation;
        counterDamage *= effectiveFactor;

        // Defender still takes some damage (momentum)
        double damageReduction = 0.5; // Block half the damage when braced

        string narrative = GetBraceSuccessNarrative(defender.Name, state.Animal.Name, weapon.Name, counterDamage);

        return new BraceResult(
            Success: true,
            DamageReduction: damageReduction,
            CounterDamage: counterDamage,
            EnergyCost: BraceEnergyCost,
            Narrative: narrative
        );
    }

    private static string GetBraceSuccessNarrative(string defenderName, string attackerName, string weaponName, double damage)
    {
        if (damage > 30)
        {
            return $"The {attackerName} impales itself on {defenderName}'s {weaponName}! A devastating blow!";
        }
        else if (damage > 15)
        {
            return $"{defenderName}'s braced {weaponName} catches the charging {attackerName}, driving deep.";
        }
        else
        {
            return $"The {attackerName} hits {defenderName}'s {weaponName}, wounding itself but knocking {defenderName} back.";
        }
    }

    #endregion

    #region Give Ground

    /// <summary>
    /// Retreat to avoid an attack, increasing distance but showing weakness.
    /// </summary>
    public static DefenseResult AttemptGiveGround(ICombatActor defender, CombatState state, double incomingDamage)
    {
        var capacities = defender.ActorReference.GetCapacities();

        // Can't give ground at Far zone
        if (state.Zone == DistanceZone.Far)
        {
            return new DefenseResult(
                Success: false,
                DamageReduction: 0,
                EnergyCost: 0,
                Narrative: "There's nowhere left to retreat!"
            );
        }

        // Movement capacity affects success
        double successChance = 0.6 + (capacities.Moving * 0.3);

        bool success = _rng.NextDouble() < successChance;

        // Energy cost is tracked in DefenseResult, drain happens via update

        // Increase animal boldness (showing weakness)
        state.Behavior.ModifyBoldness(0.1);

        // Move to farther zone on success
        DistanceZone? newZone = null;
        if (success)
        {
            var fartherZone = DistanceZoneHelper.GetFartherZone(state.Zone);
            newZone = fartherZone;
        }

        string narrative = success
            ? GetGiveGroundSuccessNarrative(defender.Name, state.Animal.Name)
            : GetGiveGroundFailNarrative(defender.Name, state.Animal.Name);

        return new DefenseResult(
            Success: success,
            DamageReduction: success ? 0.8 : 0.2, // Partial avoidance even on fail
            EnergyCost: GiveGroundEnergyCost,
            Narrative: narrative,
            NewZone: newZone
        );
    }

    private static string GetGiveGroundSuccessNarrative(string defenderName, string attackerName)
    {
        var options = new[]
        {
            $"{defenderName} back away quickly, putting distance between them and the {attackerName}.",
            $"{defenderName} give ground, staying out of the {attackerName}'s reach.",
            $"{defenderName} retreat, the {attackerName} snapping at empty air."
        };
        return options[_rng.Next(options.Length)];
    }

    private static string GetGiveGroundFailNarrative(string defenderName, string attackerName)
    {
        var options = new[]
        {
            $"{defenderName} try to retreat but stumble. The {attackerName} clips them.",
            $"{defenderName} back away but not fast enough—the {attackerName} catches them.",
            $"{defenderName}'s retreat is too slow. The {attackerName}'s attack grazes them."
        };
        return options[_rng.Next(options.Length)];
    }

    #endregion

    #region Shove (Melee Zone)

    /// <summary>
    /// Shove the animal to create distance. Only available at melee range.
    /// </summary>
    public static DefenseResult AttemptShove(ICombatActor defender, CombatState state)
    {
        var capacities = defender.ActorReference.GetCapacities();

        // Need strength and manipulation
        double successChance = 0.4 + (defender.ActorReference.Strength * 0.3) + (capacities.Manipulation * 0.2);

        // Smaller animals easier to shove
        double weightRatio = defender.ActorReference.Body.WeightKG / state.Animal.Body.WeightKG;
        successChance *= Math.Min(1.5, weightRatio);

        bool success = _rng.NextDouble() < successChance;

        // Energy cost is tracked in DefenseResult, drain happens via update

        DistanceZone? newZone = null;
        double damageReduction = 0;

        if (success)
        {
            newZone = DistanceZone.Close; // Push to close range
            damageReduction = 0.5; // Avoid half the damage in the exchange
        }

        string narrative = success
            ? $"{defender.Name} shove the {state.Animal.Name} back, creating space!"
            : $"{defender.Name} try to push the {state.Animal.Name} away but it's too strong.";

        return new DefenseResult(
            Success: success,
            DamageReduction: damageReduction,
            EnergyCost: 0.02,
            Narrative: narrative,
            NewZone: newZone
        );
    }

    #endregion
}
