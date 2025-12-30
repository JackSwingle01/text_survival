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
    /// Checks if player can dodge (requires movement capacity).
    /// </summary>
    public static bool CanDodge(GameContext ctx)
    {
        var capacities = ctx.player.GetCapacities();
        return capacities.Moving > 0.3;
    }

    /// <summary>
    /// Checks if player can block (requires weapon or shield).
    /// </summary>
    public static bool CanBlock(GameContext ctx)
    {
        var weapon = ctx.Inventory.Weapon;
        if (weapon == null) return false;

        var capacities = ctx.player.GetCapacities();
        return capacities.Manipulation > 0.3;
    }

    /// <summary>
    /// Checks if player can brace (requires spear-type weapon).
    /// </summary>
    public static bool CanBrace(GameContext ctx)
    {
        var weapon = ctx.Inventory.Weapon;
        if (weapon == null) return false;

        // Spears can brace
        bool isSpear = weapon.WeaponClass == WeaponClass.Pierce;
        var capacities = ctx.player.GetCapacities();
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
    public static string? GetUnavailableReason(CombatPlayerAction action, GameContext ctx, DistanceZone zone)
    {
        return action switch
        {
            CombatPlayerAction.Dodge => !CanDodge(ctx) ? "Too injured to dodge" : null,
            CombatPlayerAction.Block => !CanBlock(ctx) ? "No weapon to block with" : null,
            CombatPlayerAction.Brace => !CanBrace(ctx) ? "Need a spear to brace" : null,
            CombatPlayerAction.GiveGround => !CanGiveGround(zone) ? "No room to retreat" : null,
            _ => null
        };
    }

    #endregion

    #region Dodge

    /// <summary>
    /// Attempt to dodge an incoming attack.
    /// Success avoids damage entirely but costs energy and pushes player back.
    /// </summary>
    public static DefenseResult AttemptDodge(GameContext ctx, CombatState state, double incomingDamage)
    {
        var capacities = ctx.player.GetCapacities();
        double energy = ctx.player.Body.Energy / 480.0;

        // Calculate dodge chance based on player state
        double dodgeChance = CalculateDodgeChance(ctx, state.Animal);

        // Energy affects effectiveness
        double energyFactor = 0.5 + (energy * 0.5); // 50-100% effectiveness
        dodgeChance *= energyFactor;

        bool success = _rng.NextDouble() < dodgeChance;

        // Energy cost is tracked in DefenseResult, drain happens via ctx.Update()

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
            ? GetDodgeSuccessNarrative(state.Animal)
            : GetDodgeFailNarrative(state.Animal);

        return new DefenseResult(
            Success: success,
            DamageReduction: success ? 1.0 : 0.0,
            EnergyCost: DodgeEnergyCost,
            Narrative: narrative,
            NewZone: newZone
        );
    }

    private static double CalculateDodgeChance(GameContext ctx, Animal enemy)
    {
        var capacities = ctx.player.GetCapacities();
        double baseChance = BaseDodgeSuccess;

        // Movement capacity strongly affects dodge
        baseChance *= capacities.Moving;

        // Reflexes skill bonus
        if (ctx.player is Player player)
        {
            baseChance += player.Skills.Reflexes.Level * 0.02;
        }

        // Speed difference matters
        double playerSpeed = 6.0 * capacities.Moving;
        double speedRatio = playerSpeed / enemy.SpeedMps;
        baseChance *= Math.Min(1.3, speedRatio);

        return Math.Clamp(baseChance, 0.1, 0.9);
    }

    private static string GetDodgeSuccessNarrative(Animal animal)
    {
        var options = new[]
        {
            $"You twist aside as the {animal.Name} lunges past.",
            $"You dive clear of the {animal.Name}'s attack.",
            $"You sidestep the {animal.Name}'s charge.",
            $"The {animal.Name} snaps at empty air as you duck away."
        };
        return options[_rng.Next(options.Length)];
    }

    private static string GetDodgeFailNarrative(Animal animal)
    {
        var options = new[]
        {
            $"You try to dodge but the {animal.Name} catches you.",
            $"Too slow! The {animal.Name}'s attack connects.",
            $"You stumble trying to evade. The {animal.Name} is on you."
        };
        return options[_rng.Next(options.Length)];
    }

    #endregion

    #region Block

    /// <summary>
    /// Attempt to block an incoming attack with weapon.
    /// Reduces damage but costs energy and weapon durability.
    /// </summary>
    public static DefenseResult AttemptBlock(GameContext ctx, CombatState state, double incomingDamage)
    {
        var weapon = ctx.Inventory.Weapon;
        if (weapon == null)
        {
            return new DefenseResult(false, 0, 0, "You have nothing to block with!");
        }

        var capacities = ctx.player.GetCapacities();
        double energy = ctx.player.Body.Energy / 480.0;

        // Calculate block effectiveness
        double blockAmount = CalculateBlockAmount(ctx, weapon);

        // Energy affects effectiveness
        double energyFactor = 0.5 + (energy * 0.5);
        blockAmount *= energyFactor;

        // Energy cost is tracked in DefenseResult, drain happens via ctx.Update()

        // Damage weapon (apply wear)
        weapon.Use();

        string narrative = GetBlockNarrative(state.Animal, weapon, blockAmount);

        return new DefenseResult(
            Success: true,
            DamageReduction: Math.Clamp(blockAmount, 0, 0.8),
            EnergyCost: BlockEnergyCost,
            Narrative: narrative
        );
    }

    private static double CalculateBlockAmount(GameContext ctx, Gear weapon)
    {
        double baseBlock = BaseBlockAbsorption;

        // Manipulation affects block
        var capacities = ctx.player.GetCapacities();
        baseBlock *= capacities.Manipulation;

        // Defense skill bonus
        if (ctx.player is Player player)
        {
            baseBlock += player.Skills.Defense.Level * 0.02;
        }

        // Weapon condition affects block
        baseBlock *= weapon.ConditionPct;

        return Math.Clamp(baseBlock, 0.2, 0.8);
    }

    private static string GetBlockNarrative(Animal animal, Gear weapon, double blockAmount)
    {
        if (blockAmount > 0.6)
        {
            return $"You catch the {animal.Name}'s attack on your {weapon.Name}, deflecting most of the force.";
        }
        else if (blockAmount > 0.4)
        {
            return $"Your {weapon.Name} absorbs some of the impact as the {animal.Name} strikes.";
        }
        else
        {
            return $"You partially block with your {weapon.Name}, but the blow still hits hard.";
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
    /// If animal charges into brace, player takes reduced damage and deals counter-damage.
    /// </summary>
    public static BraceResult ResolveBrace(GameContext ctx, CombatState state, double incomingDamage)
    {
        var weapon = ctx.Inventory.Weapon;
        if (weapon == null)
        {
            return new BraceResult(false, 0, 0, 0, "You have nothing to brace with!");
        }

        // Was the animal charging?
        bool wasCharging = state.Behavior.CurrentBehavior == CombatBehavior.Charging;

        // Energy cost is tracked in BraceResult, drain happens via ctx.Update()

        if (!wasCharging)
        {
            // Brace wasted - animal didn't charge
            return new BraceResult(
                Success: false,
                DamageReduction: 0,
                CounterDamage: 0,
                EnergyCost: BraceEnergyCost,
                Narrative: $"You hold your {weapon.Name} ready, but the {state.Animal.Name} doesn't charge."
            );
        }

        // Animal charged into the brace!
        var capacities = ctx.player.GetCapacities();
        double energy = ctx.player.Body.Energy / 480.0;

        // Calculate counter damage (weapon damage * brace multiplier)
        double baseDamage = weapon.Damage ?? 10;
        double counterDamage = baseDamage * BaseBraceMultiplier;

        // Energy and manipulation affect counter damage
        double effectiveFactor = (0.5 + (energy * 0.5)) * capacities.Manipulation;
        counterDamage *= effectiveFactor;

        // Player still takes some damage (momentum)
        double damageReduction = 0.5; // Block half the damage when braced

        string narrative = GetBraceSuccessNarrative(state.Animal, weapon, counterDamage);

        return new BraceResult(
            Success: true,
            DamageReduction: damageReduction,
            CounterDamage: counterDamage,
            EnergyCost: BraceEnergyCost,
            Narrative: narrative
        );
    }

    private static string GetBraceSuccessNarrative(Animal animal, Gear weapon, double damage)
    {
        if (damage > 30)
        {
            return $"The {animal.Name} impales itself on your {weapon.Name}! A devastating blow!";
        }
        else if (damage > 15)
        {
            return $"Your braced {weapon.Name} catches the charging {animal.Name}, driving deep.";
        }
        else
        {
            return $"The {animal.Name} hits your {weapon.Name}, wounding itself but knocking you back.";
        }
    }

    #endregion

    #region Give Ground

    /// <summary>
    /// Retreat to avoid an attack, increasing distance but showing weakness.
    /// </summary>
    public static DefenseResult AttemptGiveGround(GameContext ctx, CombatState state, double incomingDamage)
    {
        var capacities = ctx.player.GetCapacities();

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

        // Energy cost is tracked in DefenseResult, drain happens via ctx.Update()

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
            ? GetGiveGroundSuccessNarrative(state.Animal)
            : GetGiveGroundFailNarrative(state.Animal);

        return new DefenseResult(
            Success: success,
            DamageReduction: success ? 0.8 : 0.2, // Partial avoidance even on fail
            EnergyCost: GiveGroundEnergyCost,
            Narrative: narrative,
            NewZone: newZone
        );
    }

    private static string GetGiveGroundSuccessNarrative(Animal animal)
    {
        var options = new[]
        {
            $"You back away quickly, putting distance between you and the {animal.Name}.",
            $"You give ground, staying out of the {animal.Name}'s reach.",
            $"You retreat, the {animal.Name} snapping at empty air."
        };
        return options[_rng.Next(options.Length)];
    }

    private static string GetGiveGroundFailNarrative(Animal animal)
    {
        var options = new[]
        {
            $"You try to retreat but stumble. The {animal.Name} clips you.",
            $"You back away but not fast enough—the {animal.Name} catches you.",
            $"Your retreat is too slow. The {animal.Name}'s attack grazes you."
        };
        return options[_rng.Next(options.Length)];
    }

    #endregion

    #region Shove (Melee Zone)

    /// <summary>
    /// Shove the animal to create distance. Only available at melee range.
    /// </summary>
    public static DefenseResult AttemptShove(GameContext ctx, CombatState state)
    {
        var capacities = ctx.player.GetCapacities();

        // Need strength and manipulation
        double successChance = 0.4 + (ctx.player.Strength * 0.3) + (capacities.Manipulation * 0.2);

        // Smaller animals easier to shove
        double weightRatio = ctx.player.Body.WeightKG / state.Animal.Body.WeightKG;
        successChance *= Math.Min(1.5, weightRatio);

        bool success = _rng.NextDouble() < successChance;

        // Energy cost is tracked in DefenseResult, drain happens via ctx.Update()

        DistanceZone? newZone = null;
        double damageReduction = 0;

        if (success)
        {
            newZone = DistanceZone.Close; // Push to close range
            damageReduction = 0.5; // Avoid half the damage in the exchange
        }

        string narrative = success
            ? $"You shove the {state.Animal.Name} back, creating space!"
            : $"You try to push the {state.Animal.Name} away but it's too strong.";

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
