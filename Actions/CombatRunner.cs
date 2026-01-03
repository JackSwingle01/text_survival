using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Web;
using text_survival.Web.Dto;

namespace text_survival.Actions;

/// <summary>
/// Result of combat, used by callers to handle cleanup.
/// </summary>
public enum CombatResult
{
    Victory,          // Player killed enemy
    Defeat,           // Player was killed
    Fled,             // Player escaped mid-combat
    AnimalDisengaged, // Animal left after incapacitating player
    AnimalFled,       // Animal retreated
    DistractedWithMeat // Player dropped meat, animal took it
}

/// <summary>
/// Distance-based strategic combat system.
/// Unified system that handles both pre-combat (encounter) and active combat.
///
/// Key Features:
/// - Distance zones (Melee, Close, Mid, Far) determine available actions
/// - Animal behavior states (Circling, Approaching, Threatening, Attacking, Recovering, Retreating, Disengaging)
/// - Defensive options (Dodge, Block, Brace, Give Ground)
/// - Descriptive health instead of HP bars
/// - Vulnerability windows for player lethality
/// </summary>
public static class CombatRunner
{
    private static readonly Random _rng = new();

    // Safety limit to prevent infinite loops
    private const int MaxCombatTurns = 100;

    // Damage constants
    private const double BasePlayerSpeedMps = 6.0;

    #region Targeting System (Close Range Only)

    /// <summary>
    /// Target options for Close range attacks.
    /// </summary>
    public enum AttackTarget
    {
        Legs,   // Cripples movement
        Torso,  // Standard damage
        Head    // High damage/lethal potential, risky
    }

    /// <summary>
    /// Base hit chances by target (before state modifiers).
    /// </summary>
    private static readonly Dictionary<AttackTarget, double> BaseTargetHitChances = new()
    {
        { AttackTarget.Legs, 0.70 },
        { AttackTarget.Torso, 0.80 },
        { AttackTarget.Head, 0.50 }
    };

    /// <summary>
    /// State modifiers to targeting hit chances.
    /// Player learns: "It's charging - go for the head" vs "It's circling - take the leg shot"
    /// </summary>
    private static double GetTargetingModifier(CombatBehavior behavior, AttackTarget target)
    {
        return (behavior, target) switch
        {
            // Attacking - committed, predictable. Head exposed
            (CombatBehavior.Attacking, AttackTarget.Head) => 0.15,
            (CombatBehavior.Attacking, AttackTarget.Torso) => 0.10,
            (CombatBehavior.Attacking, AttackTarget.Legs) => -0.15,

            // Approaching - closing distance, moving predictably
            (CombatBehavior.Approaching, AttackTarget.Head) => 0.05,
            (CombatBehavior.Approaching, AttackTarget.Torso) => 0.10,
            (CombatBehavior.Approaching, AttackTarget.Legs) => 0.05,

            // Circling - steady movement, legs visible
            (CombatBehavior.Circling, AttackTarget.Legs) => 0.10,
            (CombatBehavior.Circling, AttackTarget.Torso) => 0.00,
            (CombatBehavior.Circling, AttackTarget.Head) => -0.10,

            // Recovering - the opening, everything easier
            (CombatBehavior.Recovering, AttackTarget.Head) => 0.15,
            (CombatBehavior.Recovering, AttackTarget.Torso) => 0.15,
            (CombatBehavior.Recovering, AttackTarget.Legs) => 0.15,

            // Disengaging - desperate, exposed
            (CombatBehavior.Disengaging, AttackTarget.Head) => 0.10,
            (CombatBehavior.Disengaging, AttackTarget.Torso) => 0.15,
            (CombatBehavior.Disengaging, AttackTarget.Legs) => 0.10,

            // Threatening - ready position, slightly harder to hit head
            (CombatBehavior.Threatening, AttackTarget.Head) => -0.05,
            (CombatBehavior.Threatening, AttackTarget.Torso) => 0.00,
            (CombatBehavior.Threatening, AttackTarget.Legs) => 0.00,

            // Retreating - moving away, back exposed
            (CombatBehavior.Retreating, AttackTarget.Torso) => 0.10,
            (CombatBehavior.Retreating, AttackTarget.Legs) => 0.05,
            (CombatBehavior.Retreating, AttackTarget.Head) => -0.05,

            _ => 0.0
        };
    }

    /// <summary>
    /// Gets the hit chance for a specific target given current animal state.
    /// </summary>
    private static double GetTargetHitChance(CombatState state, AttackTarget target)
    {
        double baseChance = BaseTargetHitChances[target];
        double stateModifier = GetTargetingModifier(state.Behavior.CurrentBehavior, target);
        double vulnerabilityModifier = state.Behavior.GetHitChanceModifier() - 1.0; // -0.2 to +0.5

        return Math.Clamp(baseChance + stateModifier + (vulnerabilityModifier * 0.2), 0.1, 0.95);
    }

    /// <summary>
    /// Gets targeting options for the UI.
    /// </summary>
    public static List<CombatActionDto> BuildTargetingOptions(CombatState state, string attackType)
    {
        var options = new List<CombatActionDto>();
        string behaviorHint = GetBehaviorTargetingHint(state.Behavior.CurrentBehavior);

        foreach (AttackTarget target in Enum.GetValues<AttackTarget>())
        {
            double hitChance = GetTargetHitChance(state, target);
            string description = target switch
            {
                AttackTarget.Legs => "Cripples movement - slower charges, easier escape",
                AttackTarget.Torso => "Standard damage, animal stays mobile",
                AttackTarget.Head => "High damage potential, risky",
                _ => ""
            };

            options.Add(new CombatActionDto(
                Id: $"target_{target.ToString().ToLower()}",
                Label: target.ToString(),
                Description: description,
                Category: "targeting",
                IsAvailable: true,
                DisabledReason: null,
                HitChance: $"{hitChance * 100:F0}%"
            ));
        }

        return options;
    }

    private static string GetBehaviorTargetingHint(CombatBehavior behavior)
    {
        return behavior switch
        {
            CombatBehavior.Attacking => "Head exposed as it attacks",
            CombatBehavior.Approaching => "Moving predictably as it closes",
            CombatBehavior.Circling => "Legs are an easier target while it paces",
            CombatBehavior.Recovering => "It's off-balance - any target is easier",
            CombatBehavior.Threatening => "It's ready - no easy shots",
            CombatBehavior.Retreating => "Its back is exposed",
            CombatBehavior.Disengaging => "Desperate to escape - exposed",
            _ => ""
        };
    }

    #endregion

    #region Main Entry Points

    /// <summary>
    /// Run combat from an existing encounter setup.
    /// Uses animal's current DistanceFromPlayer and calculates boldness from context.
    /// </summary>
    public static CombatResult RunCombat(GameContext ctx, Animal enemy)
    {
        double terrainHazard = ctx.CurrentLocation.GetEffectiveTerrainHazard();
        double boldness = CalculateInitialBoldness(ctx, enemy);
        var state = new CombatState(enemy, enemy.DistanceFromPlayer, boldness, terrainHazard);
        state.InitializeGrid(ctx.player);
        return RunCombatLoop(ctx, state);
    }

    /// <summary>
    /// Run combat with explicit initial conditions.
    /// </summary>
    public static CombatResult RunCombat(GameContext ctx, Animal enemy, double initialDistance, double initialBoldness)
    {
        double terrainHazard = ctx.CurrentLocation.GetEffectiveTerrainHazard();
        var state = new CombatState(enemy, initialDistance, initialBoldness, terrainHazard);
        state.InitializeGrid(ctx.player);
        return RunCombatLoop(ctx, state);
    }

    #endregion

    #region Boldness Calculation

    /// <summary>
    /// Calculates initial boldness from context using additive formula.
    /// Torch is a major deterrent (-0.3).
    /// </summary>
    private static double CalculateInitialBoldness(GameContext ctx, Animal enemy)
    {
        double boldness = 0.4; // Base boldness

        // Factors that increase boldness
        if (ctx.Inventory.HasMeat) boldness += 0.2;
        if (ctx.player.Vitality < 0.7) boldness += 0.15;
        if (ctx.player.EffectRegistry.HasEffect("Bloody")) boldness += 0.15;
        if (ctx.player.EffectRegistry.HasEffect("Bleeding")) boldness += 0.1;
        // Pack nearby would add 0.25 (checked via tensions)

        // Factors that decrease boldness
        if (ctx.Inventory.HasLitTorch) boldness -= 0.3; // Major deterrent
        if (enemy.Vitality < 0.7) boldness -= 0.1;
        if (enemy.Vitality < 0.5) boldness -= 0.1;
        if (enemy.Vitality < 0.3) boldness -= 0.1;

        // Size advantage (player vs animal)
        double weightRatio = ctx.player.Body.WeightKG / enemy.Body.WeightKG;
        if (weightRatio > 1.2) boldness -= 0.1;

        // Use animal's encounter boldness as additional modifier
        boldness += (enemy.EncounterBoldness - 0.5) * 0.2; // +-0.1 from base

        return Math.Clamp(boldness, 0.1, 0.95);
    }

    #endregion

    #region Main Combat Loop

    private static CombatResult RunCombatLoop(GameContext ctx, CombatState state)
    {
        // Defensive cleanup - clear ALL overlays to prevent stale overlay interference
        WebIO.ClearAllOverlays(ctx.SessionId!);

        double? prevDistance = null;
        bool isFirstTurn = true;

        while (state.Animal.IsAlive && ctx.player.IsAlive && state.TurnCount < MaxCombatTurns)
        {
            state.StartTurn();

            // === PHASE 1: Intro (first turn only) ===
            if (isFirstTurn)
            {
                var introDto = BuildCombatDto(ctx, state, prevDistance,
                    phase: CombatPhase.Intro,
                    narrative: $"A {state.Animal.Name.ToLower()} lunges at you!");
                WebIO.RenderCombat(ctx, introDto);
                WebIO.WaitForCombatContinue(ctx);
                isFirstTurn = false;
            }

            // === PHASE 2: Player Choice ===
            var choiceDto = BuildCombatDto(ctx, state, prevDistance,
                phase: CombatPhase.PlayerChoice);
            string actionId = WebIO.WaitForCombatChoice(ctx, choiceDto);

            // Save distance for animation
            prevDistance = state.DistanceMeters;

            // Process player action
            var actionResult = ProcessPlayerAction(ctx, state, actionId);

            // === PHASE 3: Player Action Result ===
            if (!string.IsNullOrEmpty(actionResult.Narrative))
            {
                var actionDto = BuildCombatDto(ctx, state, prevDistance,
                    phase: CombatPhase.PlayerAction,
                    narrative: actionResult.Narrative);
                WebIO.RenderCombat(ctx, actionDto);
                WebIO.WaitForCombatContinue(ctx);
            }

            // Check for immediate resolution (drop meat, disengage success, play dead)
            if (actionResult.ImmediateOutcome.HasValue)
            {
                return ShowOutcome(ctx, state, actionResult.ImmediateOutcome.Value, actionResult.Narrative);
            }

            // === PHASE 4: Animal Attack (if attacking) ===
            if (state.Behavior.WillAttackThisTurn())
            {
                // Animal attacks - must close distance to attack range
                if (state.Zone > DistanceZone.Close)
                {
                    state.SetToZone(DistanceZone.Close);
                }
                var chargeNarrative = ResolveAnimalCharge(ctx, state, actionResult.DefenseChosen);

                var chargeDto = BuildCombatDto(ctx, state, prevDistance,
                    phase: CombatPhase.AnimalAction,
                    narrative: chargeNarrative);
                WebIO.RenderCombat(ctx, chargeDto);
                WebIO.WaitForCombatContinue(ctx);
            }

            // Update time
            ctx.Update(1, ActivityType.Fighting);

            // End turn - update animal behavior
            state.EndTurn();

            // === PHASE 5: Behavior Transition (if changed) ===
            if (state.PreviousBehavior.HasValue &&
                state.PreviousBehavior.Value != state.Behavior.CurrentBehavior)
            {
                string transitionMsg = CombatNarrator.DescribeBehaviorTransition(
                    state.Animal.Name,
                    state.PreviousBehavior.Value,
                    state.Behavior.CurrentBehavior);
                if (!string.IsNullOrEmpty(transitionMsg))
                {
                    var transitionDto = BuildCombatDto(ctx, state, null,
                        phase: CombatPhase.BehaviorChange,
                        narrative: transitionMsg);
                    WebIO.RenderCombat(ctx, transitionDto);
                    WebIO.WaitForCombatContinue(ctx);
                }
            }

            // Check for combat end
            var outcome = state.CheckForEnd(ctx);
            if (outcome.HasValue)
            {
                return ShowOutcome(ctx, state, MapOutcome(outcome.Value), null);
            }

            // === PHASE 6: Animal Action (non-attacking) ===
            // Circling, threatening, retreating - show as its own narrative beat
            if (!state.Behavior.WillAttackThisTurn())
            {
                var animalActionNarrative = ProcessAnimalAction(ctx, state);
                if (!string.IsNullOrEmpty(animalActionNarrative))
                {
                    var animalDto = BuildCombatDto(ctx, state, null,
                        phase: CombatPhase.AnimalAction,
                        narrative: animalActionNarrative);
                    WebIO.RenderCombat(ctx, animalDto);
                    WebIO.WaitForCombatContinue(ctx);
                }
            }
        }

        // Safety fallback - should NEVER be reached now
        // CheckForEnd() now handles ALL exit conditions (player death, animal death, turn limit)
        // If we reach here, there's a bug in the combat loop logic
        throw new InvalidOperationException(
            $"[COMBAT BUG] Combat loop exited unexpectedly! " +
            $"Animal alive: {state.Animal.IsAlive}, " +
            $"Player alive: {ctx.player.IsAlive}, " +
            $"Turns: {state.TurnCount}, " +
            $"Zone: {state.Zone}, " +
            $"Behavior: {state.Behavior.CurrentBehavior}");
    }

    #endregion

    #region Player Action Processing

    private record ActionResult(
        string Narrative,
        CombatResult? ImmediateOutcome,
        CombatPlayerAction DefenseChosen
    );

    private static ActionResult ProcessPlayerAction(GameContext ctx, CombatState state, string actionId)
    {
        state.RecordPlayerAction(MapActionId(actionId));

        return actionId switch
        {
            // Movement actions
            "hold_ground" => ProcessHoldGround(ctx, state),
            "close_distance" => ProcessCloseDistance(ctx, state),
            "back_away" => ProcessBackAway(ctx, state),
            "careful_retreat" => ProcessCarefulRetreat(ctx, state),

            // Offensive actions
            "strike" => ProcessStrike(ctx, state),
            "thrust" => ProcessThrust(ctx, state),
            "throw" => ProcessThrow(ctx, state),
            "shove" => ProcessShove(ctx, state),
            "grapple" => ProcessGrapple(ctx, state),

            // Defensive actions
            "dodge" => ProcessDodge(ctx, state),
            "block" => ProcessBlock(ctx, state),
            "brace" => ProcessBrace(ctx, state),
            "give_ground" => ProcessGiveGround(ctx, state),

            // Special actions
            "intimidate" => ProcessIntimidate(ctx, state),
            "disengage" => ProcessDisengage(ctx, state),
            "drop_meat" => ProcessDropMeat(ctx, state),
            "go_down" => ProcessGoDown(ctx, state),
            "retrieve_weapon" => ProcessRetrieveWeapon(ctx, state),

            // Intro confirmation - no action, just acknowledge
            "confirm" => new ActionResult("", null, CombatPlayerAction.None),

            _ => new ActionResult("You hesitate.", null, CombatPlayerAction.None)
        };
    }

    private static CombatPlayerAction MapActionId(string actionId)
    {
        return actionId switch
        {
            "hold_ground" => CombatPlayerAction.HoldGround,
            "close_distance" => CombatPlayerAction.CloseDistance,
            "back_away" => CombatPlayerAction.BackAway,
            "careful_retreat" => CombatPlayerAction.GiveGround,
            "strike" => CombatPlayerAction.Strike,
            "thrust" => CombatPlayerAction.Thrust,
            "throw" => CombatPlayerAction.Throw,
            "shove" => CombatPlayerAction.Shove,
            "grapple" => CombatPlayerAction.Grapple,
            "dodge" => CombatPlayerAction.Dodge,
            "block" => CombatPlayerAction.Block,
            "brace" => CombatPlayerAction.Brace,
            "give_ground" => CombatPlayerAction.GiveGround,
            "intimidate" => CombatPlayerAction.Intimidate,
            "disengage" => CombatPlayerAction.Disengage,
            "drop_meat" => CombatPlayerAction.DropMeat,
            "go_down" => CombatPlayerAction.GoDown,
            _ => CombatPlayerAction.None
        };
    }

    #region Movement Actions

    private static ActionResult ProcessHoldGround(GameContext ctx, CombatState state)
    {
        state.Behavior.ModifyBoldness(-0.05);
        string narrative = $"You hold your ground, facing the {state.Animal.Name}.";
        return new ActionResult(narrative, null, CombatPlayerAction.HoldGround);
    }

    private static ActionResult ProcessCloseDistance(GameContext ctx, CombatState state)
    {
        var capacities = ctx.player.GetCapacities();
        double movedMeters = state.PlayerClosesDistance(capacities.Moving);
        string narrative = $"You advance {movedMeters:F0}m toward the {state.Animal.Name}.";
        return new ActionResult(narrative, null, CombatPlayerAction.CloseDistance);
    }

    private static ActionResult ProcessBackAway(GameContext ctx, CombatState state)
    {
        var capacities = ctx.player.GetCapacities();
        double movedMeters = state.PlayerIncreasesDistance(capacities.Moving);
        state.Behavior.ModifyBoldness(0.05);
        string narrative = $"You back away {movedMeters:F0}m, keeping eyes on the predator.";
        return new ActionResult(narrative, null, CombatPlayerAction.BackAway);
    }

    private static ActionResult ProcessCarefulRetreat(GameContext ctx, CombatState state)
    {
        var capacities = ctx.player.GetCapacities();
        double movedMeters = state.PlayerIncreasesDistance(capacities.Moving * 0.8); // Slower but safer
        state.Behavior.ModifyBoldness(0.03);
        string narrative = $"You retreat {movedMeters:F0}m carefully, watching for any sudden moves.";
        return new ActionResult(narrative, null, CombatPlayerAction.GiveGround);
    }

    #endregion

    #region Offensive Actions

    private static ActionResult ProcessStrike(GameContext ctx, CombatState state)
    {
        var weapon = ctx.Inventory.Weapon;
        var (damage, narrative) = ExecutePlayerAttack(ctx, state, weapon);

        return new ActionResult(narrative, null, CombatPlayerAction.Strike);
    }

    private static ActionResult ProcessThrust(GameContext ctx, CombatState state)
    {
        var weapon = ctx.Inventory.Weapon;
        if (weapon == null)
        {
            return new ActionResult("You have nothing to thrust with!", null, CombatPlayerAction.Thrust);
        }

        // Two-stage menu: first thrust, then target selection
        var targetingOptions = BuildTargetingOptions(state, "thrust");
        string targetId = WebIO.WaitForTargetChoice(ctx, targetingOptions, state.Animal.Name);

        // Parse target selection
        AttackTarget target = targetId switch
        {
            "target_legs" => AttackTarget.Legs,
            "target_head" => AttackTarget.Head,
            _ => AttackTarget.Torso
        };

        var (damage, narrative) = ExecuteTargetedAttack(ctx, state, weapon, target);

        return new ActionResult(narrative, null, CombatPlayerAction.Thrust);
    }

    /// <summary>
    /// Executes a targeted attack with specific body part targeting.
    /// </summary>
    private static (double damage, string narrative) ExecuteTargetedAttack(
        GameContext ctx, CombatState state, Gear weapon, AttackTarget target)
    {
        DamageType damageType = GetDamageType(weapon.WeaponClass);

        // Hit chance based on target and animal state
        double hitChance = GetTargetHitChance(state, target);

        bool hit = _rng.NextDouble() < hitChance;

        if (!hit)
        {
            string targetName = target.ToString().ToLower();
            string missNarrative = CombatNarrator.DescribePlayerMiss(state.Animal.Name, targetName);
            return (0, missNarrative);
        }

        // Check for critical hit (vulnerability window + head targeting bonus)
        double critChance = state.Behavior.GetCriticalChance();
        if (target == AttackTarget.Head) critChance *= 2.0; // Double crit chance on head shots

        bool isCritical = _rng.NextDouble() < critChance;

        // Calculate damage
        double baseDamage = weapon.Damage ?? 10;
        double damage = CalculatePlayerDamage(ctx, baseDamage);

        if (isCritical)
        {
            damage *= target == AttackTarget.Head ? 3.0 : 2.5; // Even more damage on crit headshot
        }

        // Apply damage to specific body region
        var damageResult = ApplyTargetedDamageToAnimal(ctx, state, damage, weapon, target, isCritical);

        // Build narrative from actual damage result using CombatNarrator
        string hitNarrative = CombatNarrator.DescribePlayerAttackHit(
            state.Animal.Name, weapon.Name, damageResult, damageType, isCritical);

        return (damage, hitNarrative);
    }

    private static DamageResult ApplyTargetedDamageToAnimal(
        GameContext ctx, CombatState state, double damage, Gear weapon,
        AttackTarget target, bool isCritical)
    {
        DamageType damageType = GetDamageType(weapon.WeaponClass);

        // Map target to body region
        string bodyRegion = target switch
        {
            AttackTarget.Legs => "Left Hind Leg", // Quadruped leg
            AttackTarget.Head => "Head",
            AttackTarget.Torso => "Chest",
            _ => "Chest"
        };

        var damageInfo = new DamageInfo(damage, damageType)
        {
            TargetPartName = bodyRegion
        };

        var result = DamageProcessor.DamageBody(damageInfo, state.Animal.Body);

        foreach (var effect in result.TriggeredEffects)
        {
            state.Animal.EffectRegistry.AddEffect(effect);
        }

        // Leg hits reduce animal speed for the encounter
        if (target == AttackTarget.Legs && damage > 5)
        {
            // Crippling effect - reduces chase/charge effectiveness
            state.AddMessage($"The {state.Animal.Name} favors its wounded leg.");
        }

        return result;
    }

    private static ActionResult ProcessThrow(GameContext ctx, CombatState state)
    {
        var weapon = ctx.Inventory.Weapon;
        if (weapon == null)
        {
            return new ActionResult("You have nothing to throw!", null, CombatPlayerAction.Throw);
        }

        // Calculate hit chance based on distance
        double hitChance = CalculateThrowHitChance(ctx, state);
        bool hit = _rng.NextDouble() < hitChance;

        // Remove weapon from inventory (thrown) and track where it lands
        ctx.Inventory.UnequipWeapon();
        double weaponLandingDistance = state.DistanceMeters + (hit ? 0 : 2); // Lands at target or past
        state.RecordWeaponThrow(weaponLandingDistance);

        if (hit)
        {
            // Apply damage with vulnerability modifier
            double baseDamage = weapon.Damage ?? 10;
            double damage = baseDamage * state.Behavior.GetHitChanceModifier();

            // Check for critical
            bool isCritical = _rng.NextDouble() < state.Behavior.GetCriticalChance();
            if (isCritical)
            {
                damage *= 2.5;
            }

            ApplyDamageToAnimal(ctx, state, damage, weapon);

            string narrative = isCritical
                ? $"Your {weapon.Name} strikes true! A devastating hit!"
                : $"Your {weapon.Name} hits the {state.Animal.Name}!";

            return new ActionResult(narrative, null, CombatPlayerAction.Throw);
        }
        else
        {
            string narrative = $"Your {weapon.Name} misses! It lands {weaponLandingDistance:F0}m away.";
            return new ActionResult(narrative, null, CombatPlayerAction.Throw);
        }
    }

    private static ActionResult ProcessRetrieveWeapon(GameContext ctx, CombatState state)
    {
        if (!state.CanRetrieveWeapon)
        {
            return new ActionResult("Your weapon is too far away to retrieve!", null, CombatPlayerAction.None);
        }

        // Risky action - animal gets a free attack if close enough
        bool animalCanAttack = state.Zone <= DistanceZone.Close;
        string narrative;

        if (animalCanAttack)
        {
            // Animal gets free attack during retrieval
            double damage = state.Animal.AttackDamage * 0.7; // Glancing blow while you grab weapon
            var damageResult = ApplyDamageToPlayer(ctx, state, state.Animal, damage);
            string partName = CombatNarrator.FormatBodyPartName(damageResult.HitPartName);
            narrative = $"You dive for your weapon! The {state.Animal.Name} catches your {partName} as you grab it.";
        }
        else
        {
            narrative = "You quickly retrieve your weapon.";
        }

        // Weapon retrieved - would need to re-equip from ground
        // For simplicity, auto-equip
        state.WeaponRetrieved();

        return new ActionResult(narrative, null, CombatPlayerAction.None);
    }

    private static ActionResult ProcessShove(GameContext ctx, CombatState state)
    {
        var result = DefensiveActions.AttemptShove(ctx, state);

        if (result.NewZone.HasValue)
        {
            state.SetToZone(result.NewZone.Value);
        }

        return new ActionResult(result.Narrative, null, CombatPlayerAction.Shove);
    }

    private static ActionResult ProcessGrapple(GameContext ctx, CombatState state)
    {
        var capacities = ctx.player.GetCapacities();

        // Grappling is risky but can be decisive
        double successChance = 0.3 + (ctx.player.Strength * 0.2) + (capacities.Manipulation * 0.2);

        // Size matters a lot
        double weightRatio = ctx.player.Body.WeightKG / state.Animal.Body.WeightKG;
        successChance *= Math.Min(1.2, weightRatio);

        bool success = _rng.NextDouble() < successChance;

        if (success)
        {
            // Pin and execute - massive damage
            double damage = 30 + (ctx.player.Strength * 20);
            ApplyDamageToAnimal(ctx, state, damage, null, targetVital: true);

            string narrative = $"You grapple the {state.Animal.Name} and drive your weight down!";
            return new ActionResult(narrative, null, CombatPlayerAction.Grapple);
        }
        else
        {
            // Failed grapple - you take damage
            double damage = state.Animal.AttackDamage * 0.5;
            var damageResult = ApplyDamageToPlayer(ctx, state, state.Animal, damage);
            string partName = CombatNarrator.FormatBodyPartName(damageResult.HitPartName);

            string narrative = $"The {state.Animal.Name} slips free and bites your {partName}!";
            return new ActionResult(narrative, null, CombatPlayerAction.Grapple);
        }
    }

    private static (double damage, string narrative) ExecutePlayerAttack(
        GameContext ctx, CombatState state, Gear? weapon, bool isThrust = false)
    {
        double baseDamage = weapon?.Damage ?? ctx.player.AttackDamage;
        DamageType damageType = weapon != null
            ? GetDamageType(weapon.WeaponClass)
            : DamageType.Blunt;

        // Hit chance modified by animal behavior
        double hitModifier = state.Behavior.GetHitChanceModifier();
        double baseHitChance = 0.85;
        double hitChance = baseHitChance * hitModifier;

        bool hit = _rng.NextDouble() < hitChance;

        if (!hit)
        {
            string missNarrative = isThrust
                ? $"Your thrust misses as the {state.Animal.Name} dodges."
                : $"Your attack misses the {state.Animal.Name}.";
            return (0, missNarrative);
        }

        // Check for critical hit (vulnerability window)
        bool isCritical = _rng.NextDouble() < state.Behavior.GetCriticalChance();

        // Calculate damage
        double damage = CalculatePlayerDamage(ctx, baseDamage);
        if (isCritical)
        {
            damage *= 2.5;
        }

        // Apply vulnerability modifier
        damage *= hitModifier;

        ApplyDamageToAnimal(ctx, state, damage, weapon);

        string hitNarrative;
        if (isCritical)
        {
            hitNarrative = $"Your {weapon?.Name ?? "attack"} finds a vital spot! Critical hit!";
        }
        else if (damage > 15)
        {
            hitNarrative = $"You land a solid hit on the {state.Animal.Name}!";
        }
        else
        {
            hitNarrative = $"You strike the {state.Animal.Name}.";
        }

        return (damage, hitNarrative);
    }

    #endregion

    #region Defensive Actions

    private static ActionResult ProcessDodge(GameContext ctx, CombatState state)
    {
        // Dodge is prepared - actual resolution happens when animal attacks
        return new ActionResult("You ready yourself to dodge.", null, CombatPlayerAction.Dodge);
    }

    private static ActionResult ProcessBlock(GameContext ctx, CombatState state)
    {
        // Block is prepared - actual resolution happens when animal attacks
        var weapon = ctx.Inventory.Weapon;
        string narrative = weapon != null
            ? $"You raise your {weapon.Name} to block."
            : "You brace yourself.";
        return new ActionResult(narrative, null, CombatPlayerAction.Block);
    }

    private static ActionResult ProcessBrace(GameContext ctx, CombatState state)
    {
        state.PlayerBraced = true;
        var weapon = ctx.Inventory.Weapon;
        string narrative = $"You plant your {weapon?.Name ?? "weapon"} and brace for a charge.";
        return new ActionResult(narrative, null, CombatPlayerAction.Brace);
    }

    private static ActionResult ProcessGiveGround(GameContext ctx, CombatState state)
    {
        var result = DefensiveActions.AttemptGiveGround(ctx, state, 0);

        if (result.NewZone.HasValue)
        {
            state.SetToZone(result.NewZone.Value);
        }

        return new ActionResult(result.Narrative, null, CombatPlayerAction.GiveGround);
    }

    #endregion

    #region Special Actions

    private static ActionResult ProcessIntimidate(GameContext ctx, CombatState state)
    {
        // Intimidation effectiveness based on player state and weapon
        double intimidateChance = 0.3;
        intimidateChance += ctx.player.Vitality * 0.2;
        if (ctx.Inventory.Weapon != null) intimidateChance += 0.15;
        if (state.Animal.Vitality < 0.5) intimidateChance += 0.2;

        // Reduce animal boldness
        double boldnessReduction = _rng.NextDouble() < intimidateChance ? 0.2 : 0.05;
        state.Behavior.ModifyBoldness(-boldnessReduction);

        string narrative = boldnessReduction > 0.1
            ? $"You shout and make yourself large. The {state.Animal.Name} flinches!"
            : $"You try to intimidate the {state.Animal.Name}. It watches you warily.";

        return new ActionResult(narrative, null, CombatPlayerAction.Intimidate);
    }

    private static ActionResult ProcessDisengage(GameContext ctx, CombatState state)
    {
        if (!state.CanDisengage(ctx))
        {
            return new ActionResult("You can't disengage from here!", null, CombatPlayerAction.Disengage);
        }

        // At Far range, disengage always succeeds
        state.AttemptDisengage(ctx);
        return new ActionResult(
            $"You break away and sprint to safety. The {state.Animal.Name} doesn't follow.",
            CombatResult.Fled,
            CombatPlayerAction.Disengage
        );
    }

    private static ActionResult ProcessDropMeat(GameContext ctx, CombatState state)
    {
        double meatDropped = ctx.Inventory.DropAllMeat();
        if (meatDropped > 0)
        {
            return new ActionResult(
                $"You drop {meatDropped:F1}kg of meat and back away. The {state.Animal.Name} goes for the meat.",
                CombatResult.DistractedWithMeat,
                CombatPlayerAction.DropMeat
            );
        }
        else
        {
            return new ActionResult("You have no meat to drop!", null, CombatPlayerAction.DropMeat);
        }
    }

    private static ActionResult ProcessGoDown(GameContext ctx, CombatState state)
    {
        // Play dead - risky but might work
        double successChance = state.Animal.DisengageAfterMaul + 0.2;
        if (state.Animal.Vitality < 0.5) successChance += 0.15;

        bool success = _rng.NextDouble() < successChance;
        if (success)
        {
            return new ActionResult(
                $"You collapse and go still. The {state.Animal.Name} sniffs at you... then loses interest and wanders off.",
                CombatResult.AnimalDisengaged,
                CombatPlayerAction.GoDown
            );
        }
        else
        {
            // Didn't work - animal attacks
            double damage = state.Animal.AttackDamage * 1.5; // Vulnerable position
            var damageResult = ApplyDamageToPlayer(ctx, state, state.Animal, damage);
            string partName = CombatNarrator.FormatBodyPartName(damageResult.HitPartName);

            return new ActionResult(
                $"You go down, but the {state.Animal.Name} isn't fooled. It mauls your {partName}!",
                null,
                CombatPlayerAction.GoDown
            );
        }
    }

    #endregion

    #endregion

    #region Animal Action Processing

    private static string? ProcessAnimalAction(GameContext ctx, CombatState state)
    {
        // Animal action depends on behavior state
        switch (state.Behavior.CurrentBehavior)
        {
            case CombatBehavior.Circling:
                // Just positioning, no damage - might close slightly
                if (_rng.NextDouble() < 0.3)
                {
                    double moved = state.AnimalClosesDistance() * 0.3; // Slight repositioning
                    return $"The {state.Animal.Name} shifts position.";
                }
                return null;

            case CombatBehavior.Threatening:
                // Building to attack, might close distance
                if (_rng.NextDouble() < 0.4)
                {
                    state.AnimalClosesDistance();
                    return $"The {state.Animal.Name} edges closer.";
                }
                return null;

            case CombatBehavior.Recovering:
                // Vulnerable, can't attack
                return null;

            case CombatBehavior.Retreating:
                // Moving away using continuous distance
                double retreatDistance = state.AnimalIncreasesDistance();
                return $"The {state.Animal.Name} backs away {retreatDistance:F0}m.";

            default:
                return null;
        }
    }

    private static string ResolveAnimalCharge(GameContext ctx, CombatState state, CombatPlayerAction playerDefense)
    {
        double baseDamage = state.Animal.AttackDamage;
        string narrative;

        // Check if player had a brace set
        if (state.PlayerBraced)
        {
            var braceResult = DefensiveActions.ResolveBrace(ctx, state, baseDamage);

            // Apply counter-damage to animal
            if (braceResult.CounterDamage > 0)
            {
                ApplyDamageToAnimal(ctx, state, braceResult.CounterDamage, ctx.Inventory.Weapon);
            }

            // Reduced damage to player
            double playerDamage = baseDamage * (1 - braceResult.DamageReduction);
            if (playerDamage > 0)
            {
                ApplyDamageToPlayer(ctx, state, state.Animal, playerDamage);
            }

            state.PlayerBraced = false;
            return braceResult.Narrative;
        }

        // Check player's defensive action
        switch (playerDefense)
        {
            case CombatPlayerAction.Dodge:
                var dodgeResult = DefensiveActions.AttemptDodge(ctx, state, baseDamage);
                if (dodgeResult.Success)
                {
                    if (dodgeResult.NewZone.HasValue)
                    {
                        state.SetToZone(dodgeResult.NewZone.Value);
                    }
                    return dodgeResult.Narrative;
                }
                else
                {
                    ApplyDamageToPlayer(ctx, state, state.Animal, baseDamage);
                    return dodgeResult.Narrative;
                }

            case CombatPlayerAction.Block:
                var blockResult = DefensiveActions.AttemptBlock(ctx, state, baseDamage);
                double blockedDamage = baseDamage * (1 - blockResult.DamageReduction);
                ApplyDamageToPlayer(ctx, state, state.Animal, blockedDamage);
                return blockResult.Narrative;

            case CombatPlayerAction.GiveGround:
                var giveGroundResult = DefensiveActions.AttemptGiveGround(ctx, state, baseDamage);
                if (giveGroundResult.NewZone.HasValue)
                {
                    state.SetToZone(giveGroundResult.NewZone.Value);
                }
                double avoidedDamage = baseDamage * (1 - giveGroundResult.DamageReduction);
                ApplyDamageToPlayer(ctx, state, state.Animal, avoidedDamage);
                return giveGroundResult.Narrative;

            default:
                // No defense chosen - take full damage
                var damageResult = ApplyDamageToPlayer(ctx, state, state.Animal, baseDamage);
                narrative = CombatNarrator.DescribeAnimalAttackHit(state.Animal.Name, damageResult);
                return narrative;
        }
    }

    #endregion

    #region Damage Calculation

    /// <summary>
    /// Calculates damage dealt by player, accounting for strength, vitality, and skills.
    /// </summary>
    private static double CalculatePlayerDamage(GameContext ctx, double baseDamage)
    {
        double skillBonus = 0;
        if (ctx.player is Actors.Player.Player player)
        {
            skillBonus = player.Skills.Fighting.Level;
        }

        // Modifiers
        double strengthModifier = (ctx.player.Strength / 2) + 0.5; // strength determines up to 50%
        double vitalityModifier = 0.7 + (0.3 * ctx.player.Vitality);
        double randomModifier = 0.5 + _rng.NextDouble(); // 0.5 to 1.5

        double totalModifier = strengthModifier * vitalityModifier * randomModifier;
        double damage = (baseDamage + skillBonus) * totalModifier;

        return Math.Max(0, damage);
    }

    #endregion

    #region Damage Application

    private static void ApplyDamageToAnimal(GameContext ctx, CombatState state, double damage, Gear? weapon, bool targetVital = false)
    {
        DamageType damageType = weapon != null
            ? GetDamageType(weapon.WeaponClass)
            : DamageType.Blunt;

        var damageInfo = new DamageInfo(damage, damageType);

        if (targetVital)
        {
            // Target vital organ for critical hits
            damageInfo.TargetPartName = "Chest"; // Heart/Lungs area
        }

        var result = DamageProcessor.DamageBody(damageInfo, state.Animal.Body);

        foreach (var effect in result.TriggeredEffects)
        {
            state.Animal.EffectRegistry.AddEffect(effect);
        }
    }

    private static DamageResult ApplyDamageToPlayer(GameContext ctx, CombatState state, Animal attacker, double damage)
    {
        // Mark that animal has attacked
        state.RecordAnimalAttack();

        var damageInfo = new DamageInfo(damage, attacker.AttackType)
        {
            ArmorCushioning = ctx.Inventory.TotalCushioning,
            ArmorToughness = ctx.Inventory.TotalToughness
        };

        var result = DamageProcessor.DamageBody(damageInfo, ctx.player.Body);

        foreach (var effect in result.TriggeredEffects)
        {
            ctx.player.AddLog(ctx.player.EffectRegistry.AddEffect(effect));
        }

        return result;
    }

    private static DamageType GetDamageType(WeaponClass? weaponClass)
    {
        return weaponClass switch
        {
            WeaponClass.Blade or WeaponClass.Claw => DamageType.Sharp,
            WeaponClass.Pierce => DamageType.Pierce,
            _ => DamageType.Blunt
        };
    }

    #endregion

    #region Outcome Display

    private static CombatResult MapOutcome(CombatOutcome outcome)
    {
        return outcome switch
        {
            CombatOutcome.Victory => CombatResult.Victory,
            CombatOutcome.PlayerDied => CombatResult.Defeat,
            CombatOutcome.PlayerDisengaged => CombatResult.Fled,
            CombatOutcome.AnimalFled => CombatResult.AnimalFled,
            CombatOutcome.AnimalDisengaged => CombatResult.AnimalDisengaged,
            CombatOutcome.DistractedWithMeat => CombatResult.DistractedWithMeat,
            _ => CombatResult.Defeat
        };
    }

    private static CombatResult ShowOutcome(GameContext ctx, CombatState state, CombatResult result, string? lastMessage)
    {
        string outcomeType;
        string message;
        List<string>? rewards = null;

        switch (result)
        {
            case CombatResult.Victory:
                // Create carcass
                var carcass = new CarcassFeature(state.Animal);
                ctx.CurrentLocation.AddFeature(carcass);
                rewards = new List<string> { $"{state.Animal.Name} carcass available for butchering" };
                outcomeType = "victory";
                message = $"The {state.Animal.Name} falls!";
                break;

            case CombatResult.Defeat:
                outcomeType = "defeat";
                message = $"The {state.Animal.Name} has killed you.";
                break;

            case CombatResult.Fled:
                outcomeType = "fled";
                message = lastMessage ?? $"You escape from the {state.Animal.Name}.";
                break;

            case CombatResult.AnimalFled:
                outcomeType = "animal_fled";
                message = $"The {state.Animal.Name} retreats into the wilderness.";
                break;

            case CombatResult.AnimalDisengaged:
                outcomeType = "disengaged";
                message = CombatNarrator.DescribeDisengage(state.Animal.Name);
                break;

            case CombatResult.DistractedWithMeat:
                outcomeType = "distracted";
                message = lastMessage ?? $"The {state.Animal.Name} takes the bait.";
                break;

            default:
                outcomeType = "ended";
                message = "The encounter ends.";
                break;
        }

        // Show outcome
        var outcomeDto = new CombatOutcomeDto(outcomeType, message, rewards);
        var finalDto = BuildCombatDto(ctx, state, null,
            phase: CombatPhase.Outcome,
            narrative: message,
            outcome: outcomeDto);
        WebIO.RenderCombat(ctx, finalDto);
        WebIO.WaitForCombatContinue(ctx);

        return result;
    }

    #endregion

    #region DTO Building

    private static CombatDto BuildCombatDto(
        GameContext ctx,
        CombatState state,
        double? prevDistance,
        CombatPhase phase,
        string? narrative = null,
        CombatOutcomeDto? outcome = null)
    {
        // Build threat factors
        var factors = BuildThreatFactors(ctx, state);

        // Only include actions when in PlayerChoice phase
        var actions = phase == CombatPhase.PlayerChoice
            ? BuildAvailableActions(ctx, state)
            : new List<CombatActionDto>();

        // Get boldness level and descriptor
        var boldness = state.Behavior.Boldness;
        var boldnessDescriptor = boldness switch
        {
            >= 0.7 => "aggressive",
            >= 0.5 => "bold",
            >= 0.3 => "wary",
            _ => "cautious"
        };

        // Build grid data if available
        var grid = BuildGridData(ctx, state);

        return new CombatDto(
            AnimalName: state.Animal.Name,
            AnimalHealthDescription: state.GetAnimalHealthDescription(),
            AnimalConditionNarrative: state.GetAnimalConditionNarrative(),
            DistanceZone: DistanceZoneHelper.GetZoneName(state.Zone),
            DistanceMeters: state.DistanceMeters,
            PreviousDistanceMeters: prevDistance,
            BehaviorState: state.Behavior.GetBehaviorStatus(),
            BehaviorDescription: state.Behavior.GetBehaviorDescription(),
            PlayerVitality: ctx.player.Vitality,
            PlayerEnergy: ctx.player.Body.Energy / 480.0, // Normalize to 0-1
            PlayerBraced: state.PlayerBraced,
            BoldnessLevel: boldness,
            BoldnessDescriptor: boldnessDescriptor,
            Phase: phase,
            NarrativeMessage: narrative,
            Actions: actions,
            ThreatFactors: factors,
            Outcome: outcome,
            Grid: grid
        );
    }

    /// <summary>
    /// Build grid data for 2D visualization.
    /// </summary>
    private static CombatGridDto? BuildGridData(GameContext ctx, CombatState state)
    {
        if (state.Map == null) return null;

        // Get terrain info from current location
        var terrain = ctx.CurrentLocation?.Terrain.ToString();
        var locationX = ctx.Map?.CurrentPosition.X;
        var locationY = ctx.Map?.CurrentPosition.Y;

        var actors = new List<CombatGridActorDto>();

        foreach (var actor in state.Actors)
        {
            var pos = state.Map.GetPosition(actor);
            if (pos == null) continue;

            var team = actor.Team switch
            {
                CombatTeam.Player => "player",
                CombatTeam.Enemy => "enemy",
                CombatTeam.Ally => "ally",
                _ => "unknown"
            };

            actors.Add(new CombatGridActorDto(
                Id: actor.Id.ToString(),
                Name: actor.Name,
                Team: team,
                Position: new CombatGridPositionDto(pos.Value.X, pos.Value.Y),
                BehaviorState: actor.CurrentBehavior?.ToString(),
                Vitality: actor.IsPlayer ? null : actor.Vitality,
                IsAlpha: state.Alpha == actor
            ));
        }

        return new CombatGridDto(
            GridSize: CombatMap.GridSize,
            CellSizeMeters: CombatMap.CellSizeMeters,
            Actors: actors,
            Terrain: terrain,
            LocationX: locationX,
            LocationY: locationY
        );
    }

    private static List<ThreatFactorDto> BuildThreatFactors(GameContext ctx, CombatState state)
    {
        var factors = new List<ThreatFactorDto>();

        // Factors that embolden the animal (red/warning)
        if (ctx.Inventory.HasMeat)
            factors.Add(new ThreatFactorDto("meat", "Eyeing your meat", "restaurant"));

        if (ctx.player.Vitality < 0.7)
            factors.Add(new ThreatFactorDto("weakness", "Senses your weakness", "personal_injury"));

        if (ctx.player.EffectRegistry.HasEffect("Bloody"))
            factors.Add(new ThreatFactorDto("blood", "Smells blood on you", "water_drop"));

        if (ctx.player.EffectRegistry.HasEffect("Bleeding"))
            factors.Add(new ThreatFactorDto("bleeding", "You're bleeding", "bloodtype"));

        // Factors that deter the animal (green/positive)
        if (ctx.Inventory.HasLitTorch)
            factors.Add(new ThreatFactorDto("torch", "Wary of your flame", "local_fire_department"));

        if (state.Animal.Vitality < 0.5)
            factors.Add(new ThreatFactorDto("wounded", "It's wounded", "healing"));

        return factors;
    }

    private static List<CombatActionDto> BuildAvailableActions(GameContext ctx, CombatState state)
    {
        var actions = new List<CombatActionDto>();
        var zone = state.Zone;
        var weapon = ctx.Inventory.Weapon;
        bool hasSpear = weapon?.WeaponClass == WeaponClass.Pierce;
        var capacities = ctx.player.GetCapacities();

        // Zone-specific actions
        switch (zone)
        {
            case DistanceZone.Melee:
                AddMeleeActions(actions, ctx, state, weapon);
                break;
            case DistanceZone.Close:
                AddCloseActions(actions, ctx, state, weapon, hasSpear);
                break;
            case DistanceZone.Mid:
                AddMidActions(actions, ctx, state, weapon);
                break;
            case DistanceZone.Far:
                AddFarActions(actions, ctx, state);
                break;
        }

        // Universal defensive options (if animal is threatening/about to charge)
        if (state.Behavior.CurrentBehavior == CombatBehavior.Threatening ||
            state.Behavior.CurrentBehavior == CombatBehavior.Circling)
        {
            AddDefensiveOptions(actions, ctx, state, zone);
        }

        // Retrieve weapon (if thrown and reachable)
        if (state.CanRetrieveWeapon)
        {
            bool isRisky = zone <= DistanceZone.Close;
            actions.Add(new CombatActionDto(
                "retrieve_weapon", "Retrieve Weapon",
                isRisky ? "RISKY: Animal will attack while you grab it" : "Recover your thrown weapon",
                "special",
                true, null, null
            ));
        }

        return actions;
    }

    private static void AddMeleeActions(List<CombatActionDto> actions, GameContext ctx, CombatState state, Gear? weapon)
    {
        // Strike
        double hitChance = 0.85 * state.Behavior.GetHitChanceModifier();
        actions.Add(new CombatActionDto(
            "strike", "Strike",
            "Attack with your weapon",
            "offensive",
            weapon != null || ctx.player.AttackDamage > 0,
            weapon == null ? "No weapon" : null,
            $"{hitChance * 100:F0}% hit"
        ));

        // Shove
        actions.Add(new CombatActionDto(
            "shove", "Shove",
            "Push the animal back to create distance",
            "defensive",
            true, null, null
        ));

        // Grapple
        var capacities = ctx.player.GetCapacities();
        bool canGrapple = capacities.Manipulation > 0.5;
        actions.Add(new CombatActionDto(
            "grapple", "Grapple",
            "Risky but can be decisive - pin for a killing blow",
            "offensive",
            canGrapple,
            canGrapple ? null : "Too injured to grapple",
            null
        ));

        // Go down (play dead)
        actions.Add(new CombatActionDto(
            "go_down", "Play Dead",
            "Risky - the animal might lose interest",
            "special",
            true, null, null
        ));
    }

    private static void AddCloseActions(List<CombatActionDto> actions, GameContext ctx, CombatState state, Gear? weapon, bool hasSpear)
    {
        // Thrust (spear only)
        if (hasSpear)
        {
            double hitChance = 0.85 * state.Behavior.GetHitChanceModifier();
            actions.Add(new CombatActionDto(
                "thrust", "Thrust",
                "Attack at range with your spear",
                "offensive",
                true, null,
                $"{hitChance * 100:F0}% hit"
            ));
        }

        // Hold ground
        actions.Add(new CombatActionDto(
            "hold_ground", "Hold Ground",
            "Maintain distance, face the animal down",
            "movement",
            true, null, null
        ));

        // Set brace (spear only)
        if (hasSpear && !state.PlayerBraced)
        {
            actions.Add(new CombatActionDto(
                "brace", "Set Brace",
                "Ready your spear - devastating if it charges",
                "defensive",
                true, null, null
            ));
        }

        // Back away
        actions.Add(new CombatActionDto(
            "back_away", "Back Away",
            "Gain distance (animal may grow bolder)",
            "movement",
            true, null, null
        ));

        // Close distance
        actions.Add(new CombatActionDto(
            "close_distance", "Close In",
            "Move to melee range",
            "movement",
            true, null, null
        ));
    }

    private static void AddMidActions(List<CombatActionDto> actions, GameContext ctx, CombatState state, Gear? weapon)
    {
        // Throw weapon
        if (weapon != null)
        {
            double hitChance = CalculateThrowHitChance(ctx, state);
            actions.Add(new CombatActionDto(
                "throw", "Throw Weapon",
                "Ranged attack - you'll lose your weapon",
                "offensive",
                true, null,
                $"{hitChance * 100:F0}% hit"
            ));
        }

        // Intimidate
        actions.Add(new CombatActionDto(
            "intimidate", "Intimidate",
            "Try to scare the animal off",
            "special",
            true, null, null
        ));

        // Close distance
        actions.Add(new CombatActionDto(
            "close_distance", "Close In",
            "Move to close range",
            "movement",
            true, null, null
        ));

        // Careful retreat
        actions.Add(new CombatActionDto(
            "careful_retreat", "Careful Retreat",
            "Back toward far range",
            "movement",
            true, null, null
        ));
    }

    private static void AddFarActions(List<CombatActionDto> actions, GameContext ctx, CombatState state)
    {
        // Disengage
        bool canDisengage = state.CanDisengage(ctx);
        string? disengageReason = canDisengage ? null :
            (state.Zone != DistanceZone.Far ? "Too close to escape" : "Too slow to escape");
        actions.Add(new CombatActionDto(
            "disengage", "Disengage",
            "Attempt to end the encounter",
            "special",
            canDisengage,
            disengageReason,
            null
        ));

        // Hold position
        actions.Add(new CombatActionDto(
            "hold_ground", "Hold Position",
            "Wait and see what the animal does",
            "movement",
            true, null, null
        ));

        // Close distance
        actions.Add(new CombatActionDto(
            "close_distance", "Re-engage",
            "Move to mid range",
            "movement",
            true, null, null
        ));

        // Drop meat
        if (ctx.Inventory.HasMeat)
        {
            actions.Add(new CombatActionDto(
                "drop_meat", "Drop Meat",
                "Distract the animal and escape",
                "special",
                true, null, null
            ));
        }
    }

    private static void AddDefensiveOptions(List<CombatActionDto> actions, GameContext ctx, CombatState state, DistanceZone zone)
    {
        // Dodge
        if (DefensiveActions.CanDodge(ctx))
        {
            actions.Add(new CombatActionDto(
                "dodge", "Dodge",
                "Avoid the next attack (costs energy, pushes you back)",
                "defensive",
                true, null, null
            ));
        }
        else
        {
            actions.Add(new CombatActionDto(
                "dodge", "Dodge",
                "Avoid the next attack",
                "defensive",
                false, "Too injured to dodge", null
            ));
        }

        // Block
        if (DefensiveActions.CanBlock(ctx))
        {
            actions.Add(new CombatActionDto(
                "block", "Block",
                "Reduce incoming damage with your weapon",
                "defensive",
                true, null, null
            ));
        }

        // Give ground
        if (DefensiveActions.CanGiveGround(zone))
        {
            actions.Add(new CombatActionDto(
                "give_ground", "Give Ground",
                "Retreat to avoid attack (shows weakness)",
                "defensive",
                true, null, null
            ));
        }
    }

    private static double CalculateThrowHitChance(GameContext ctx, CombatState state)
    {
        var weapon = ctx.Inventory.Weapon;
        if (weapon == null) return 0;

        double baseAccuracy = 0.6;
        double range = state.DistanceMeters;
        double maxRange = 20.0;

        // Accuracy decreases with distance
        double distanceFactor = 1.0 - (range / maxRange);
        double hitChance = baseAccuracy * distanceFactor;

        // Apply vulnerability modifier
        hitChance *= state.Behavior.GetHitChanceModifier();

        return Math.Clamp(hitChance, 0.1, 0.8);
    }

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates an animal from an EncounterConfig, ready for combat.
    /// Logs a warning and returns null for unknown animal types.
    /// </summary>
    public static Animal? CreateAnimalFromConfig(EncounterConfig config, Location location, GameMap map)
    {
        var animal = AnimalFactory.FromType(config.AnimalType, location, map);

        if (animal == null)
        {
            Console.WriteLine($"[CombatRunner] WARNING: Unknown animal type '{config.AnimalType}', encounter skipped");
            return null;
        }

        animal.DistanceFromPlayer = config.InitialDistance;
        animal.EncounterBoldness = config.InitialBoldness;
        return animal;
    }

    #endregion
}
