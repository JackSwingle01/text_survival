using text_survival.Actions.Expeditions;
using text_survival.Actions.Handlers;
using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;
using text_survival.Web.Dto;

namespace text_survival.Actions;

/// <summary>
/// Result of an interactive hunt.
/// </summary>
public enum HuntOutcome
{
    Success,        // Kill + butcher completed
    PreyFled,       // Animal escaped
    PlayerAbandoned,// Player gave up
    TurnedCombat,   // Prey became aggressive, encounter ran
    PlayerDied      // Player died during hunt
}

/// <summary>
/// Internal state for tracking hunt progress across turns.
/// </summary>
internal class HuntState
{
    public Animal Target { get; }
    public Location Location { get; }
    public GameContext Context { get; }
    public Guid? HerdId { get; }  // Set when animal came from persistent herd
    public int MinutesSpent { get; set; }
    public double? PreviousDistanceMeters { get; set; }
    public string? StatusMessage { get; set; }
    public bool JustApproached { get; set; }

    public HuntState(Animal target, Location location, GameContext ctx, Guid? herdId = null)
    {
        Target = target;
        Location = location;
        Context = ctx;
        HerdId = herdId;
        MinutesSpent = 0;
        PreviousDistanceMeters = null;
        StatusMessage = null;
        JustApproached = false;
    }

    public bool IsActive => Target != null && Target.IsAlive && !Target.IsEngaged;

    public bool HasSpear => Context.Inventory.Weapon?.ToolType == ToolType.Spear;
    public bool HasStones => Context.Inventory.Count(Resource.Stone) > 0;

    public bool InSpearRange
    {
        get
        {
            if (!HasSpear || Context.Inventory.Weapon == null) return false;
            double range = HuntHandler.GetSpearRange(Context.Inventory.Weapon);
            return Target.DistanceFromPlayer <= range;
        }
    }

    public bool InStoneRange => HasStones && Target.Size == AnimalSize.Small &&
                                 Target.DistanceFromPlayer <= 15;

    public bool InMeleeRange => Target.DistanceFromPlayer <= 5 &&
                                Target.State != AnimalState.Detected;
}

/// <summary>
/// Result of processing a hunt choice.
/// </summary>
internal record HuntChoiceResult(
    bool HuntEnded,
    bool TransitionToCombat,
    HuntOutcome Outcome,
    string? StatusMessage,
    Inventory? Loot = null
);

/// <summary>
/// Handles the interactive hunting phase after an animal is found.
/// Uses overlay pattern for UI presentation.
/// </summary>
public static class HuntRunner
{
    /// <summary>
    /// Run an interactive hunt against a found animal.
    /// Prompts player to stalk, then runs the approach/attack loop.
    /// </summary>
    /// <param name="herdId">Optional ID of persistent herd this animal belongs to.</param>
    /// <returns>Outcome of the hunt and minutes elapsed</returns>
    public static (HuntOutcome outcome, int minutesElapsed) Run(Animal target, Location location, GameContext ctx, Guid? herdId = null)
    {
        // Initial "Stalk?" prompt
        if (!PromptInitialStalk(target, ctx))
        {
            return (HuntOutcome.PlayerAbandoned, 0);
        }

        // Initialize hunt state
        var state = new HuntState(target, location, ctx, herdId);

        // Auto-equip spear if available
        var spear = ctx.Inventory.GetOrEquipWeapon(ctx, ToolType.Spear);
        if (!state.HasSpear && !state.HasStones)
        {
            state.StatusMessage = "You don't have a ranged weapon so you need to get very close. This will be difficult.";
        }

        // Main turn loop
        while (state.IsActive)
        {
            var huntDto = BuildHuntDto(state);
            string choiceId = WebIO.WaitForHuntChoice(ctx, huntDto);

            // Clear animation flag after first frame
            state.JustApproached = false;

            var result = ProcessChoice(choiceId, state);

            if (result.TransitionToCombat)
            {
                // Show transition message
                state.StatusMessage = $"The {target.Name} attacks!";
                var transitionDto = BuildHuntDto(state);
                WebIO.RenderHunt(ctx, transitionDto);

                // Hand off to combat runner
                var combatResult = CombatRunner.RunCombat(ctx, target);
                WebIO.ClearHunt(ctx);

                return TranslateCombatResult(combatResult, state.MinutesSpent);
            }

            if (result.HuntEnded)
            {
                // Show outcome overlay
                ShowHuntOutcome(state, result);
                WebIO.ClearHunt(ctx);

                // Record successful hunt if applicable
                if (result.Outcome == HuntOutcome.Success)
                {
                    var territory = location.GetFeature<AnimalTerritoryFeature>();
                    territory?.RecordSuccessfulHunt();
                }

                return (result.Outcome, state.MinutesSpent);
            }

            // Update status message from result
            state.StatusMessage = result.StatusMessage;
        }

        // Hunt ended due to state becoming invalid
        WebIO.ClearHunt(ctx);

        if (!ctx.player.IsAlive)
            return (HuntOutcome.PlayerDied, state.MinutesSpent);

        if (!target.IsAlive)
            return (HuntOutcome.Success, state.MinutesSpent);

        return (HuntOutcome.PreyFled, state.MinutesSpent);
    }

    /// <summary>
    /// Show initial stalk prompt via hunt overlay.
    /// </summary>
    private static bool PromptInitialStalk(Animal target, GameContext ctx)
    {
        // Select a sighting variant based on animal behavior
        var sighting = HuntingSightingSelector.SelectForAnimal(target, ctx);
        var behavior = HuntingSightingSelector.MapActivityToBehavior(target);
        string behaviorHint = HuntingSightingSelector.GetBehaviorHint(behavior);

        var choices = new List<HuntChoiceDto>
        {
            new("stalk", $"Stalk the {target.Name}", null, true, null),
            new("leave", "Let it go", null, true, null)
        };

        // Use sighting description for richer flavor
        string statusMessage = $"You spot game. {sighting.Description}. {behaviorHint}";

        var huntDto = new HuntDto(
            AnimalName: target.Name,
            AnimalDescription: target.GetTraitDescription(),
            AnimalActivity: target.GetActivityDescription(),
            AnimalState: target.State.ToString().ToLower(),
            CurrentDistanceMeters: target.DistanceFromPlayer,
            PreviousDistanceMeters: null,
            IsAnimatingDistance: false,
            MinutesSpent: 0,
            StatusMessage: statusMessage,
            Choices: choices,
            Outcome: null
        );

        string choiceId = WebIO.WaitForHuntChoice(ctx, huntDto);
        WebIO.ClearHunt(ctx);

        return choiceId == "stalk";
    }

    /// <summary>
    /// Build HuntDto from current state.
    /// </summary>
    private static HuntDto BuildHuntDto(HuntState state)
    {
        return new HuntDto(
            AnimalName: state.Target.Name,
            AnimalDescription: state.Target.GetTraitDescription(),
            AnimalActivity: state.Target.GetActivityDescription(),
            AnimalState: state.Target.State.ToString().ToLower(),
            CurrentDistanceMeters: state.Target.DistanceFromPlayer,
            PreviousDistanceMeters: state.PreviousDistanceMeters,
            IsAnimatingDistance: state.JustApproached,
            MinutesSpent: state.MinutesSpent,
            StatusMessage: state.StatusMessage,
            Choices: BuildAvailableChoices(state),
            Outcome: null
        );
    }

    /// <summary>
    /// Build available choices based on current state.
    /// </summary>
    private static List<HuntChoiceDto> BuildAvailableChoices(HuntState state)
    {
        var choices = new List<HuntChoiceDto>();
        var target = state.Target;
        var ctx = state.Context;

        // Always available
        choices.Add(new HuntChoiceDto("approach", "Approach carefully", "~7 min, reduces distance", true, null));
        choices.Add(new HuntChoiceDto("wait", "Wait and watch", "5-10 min, may change activity", true, null));
        choices.Add(new HuntChoiceDto("assess", "Assess target", "~2 min, check detection risk", true, null));

        // Melee strike (if very close and undetected)
        if (state.InMeleeRange)
        {
            choices.Add(new HuntChoiceDto("strike", "Strike now!", "Guaranteed kill", true, null));
        }

        // Spear throw
        if (state.HasSpear && ctx.Inventory.Weapon != null)
        {
            double range = HuntHandler.GetSpearRange(ctx.Inventory.Weapon);
            if (target.DistanceFromPlayer <= range)
            {
                double hitChance = HuntHandler.CalculateSpearHitChance(ctx.Inventory.Weapon, target, ctx);
                choices.Add(new HuntChoiceDto("throw_spear",
                    $"Throw {ctx.Inventory.Weapon.Name}",
                    $"{hitChance:P0} hit chance", true, null));
            }
            else
            {
                choices.Add(new HuntChoiceDto("throw_spear",
                    $"Throw {ctx.Inventory.Weapon.Name}",
                    $"Out of range ({range:F0}m)", false, "Too far"));
            }
        }

        // Stone throw
        if (state.HasStones && target.Size == AnimalSize.Small)
        {
            if (target.DistanceFromPlayer <= 15)
            {
                double hitChance = HuntHandler.CalculateStoneHitChance(target, ctx);
                int stoneCount = ctx.Inventory.Count(Resource.Stone);
                choices.Add(new HuntChoiceDto("throw_stone",
                    $"Throw stone ({stoneCount} left)",
                    $"{hitChance:P0} hit chance", true, null));
            }
            else
            {
                choices.Add(new HuntChoiceDto("throw_stone",
                    "Throw stone",
                    "Out of range (15m)", false, "Too far"));
            }
        }

        choices.Add(new HuntChoiceDto("stop", "Give up this hunt", null, true, null));

        return choices;
    }

    /// <summary>
    /// Process the player's choice and update state.
    /// </summary>
    private static HuntChoiceResult ProcessChoice(string choiceId, HuntState state)
    {
        return choiceId switch
        {
            "approach" => ProcessApproach(state),
            "wait" => ProcessWait(state),
            "assess" => ProcessAssess(state),
            "strike" => ProcessMeleeStrike(state),
            "throw_spear" => ProcessRangedAttack(state, isSpear: true),
            "throw_stone" => ProcessRangedAttack(state, isSpear: false),
            "stop" => ProcessStop(state),
            _ => ProcessStop(state)
        };
    }

    private static HuntChoiceResult ProcessApproach(HuntState state)
    {
        var ctx = state.Context;
        var target = state.Target;

        // Store previous distance for animation
        state.PreviousDistanceMeters = target.DistanceFromPlayer;

        bool success = AttemptApproach(state.Location, state.Target, ctx);
        state.MinutesSpent += 7;
        state.JustApproached = true;

        ctx.player.Skills.GetSkill("Hunting").GainExperience(1);

        if (!success)
        {
            if (target.IsEngaged)
            {
                // Prey turned aggressive
                return new HuntChoiceResult(
                    HuntEnded: true,
                    TransitionToCombat: true,
                    Outcome: HuntOutcome.TurnedCombat,
                    StatusMessage: $"The {target.Name} attacks!"
                );
            }

            // Animal fled
            return new HuntChoiceResult(
                HuntEnded: true,
                TransitionToCombat: false,
                Outcome: HuntOutcome.PreyFled,
                StatusMessage: $"The {target.Name} fled!"
            );
        }

        // Check if close enough for melee
        if (state.InMeleeRange)
        {
            return new HuntChoiceResult(
                HuntEnded: false,
                TransitionToCombat: false,
                Outcome: HuntOutcome.Success,
                StatusMessage: "You're close enough to strike!"
            );
        }

        string statusMsg = target.State == AnimalState.Alert
            ? $"You move closer. The {target.Name} is alert."
            : "You remain undetected.";

        return new HuntChoiceResult(
            HuntEnded: false,
            TransitionToCombat: false,
            Outcome: HuntOutcome.Success,
            StatusMessage: statusMsg
        );
    }

    private static HuntChoiceResult ProcessWait(HuntState state)
    {
        var target = state.Target;
        int waitTime = Utils.RandInt(5, 10);
        state.MinutesSpent += waitTime;

        string statusMsg;
        if (target.CheckActivityChange(waitTime, out var newActivity) && newActivity.HasValue)
        {
            statusMsg = $"The {target.Name} shifts—now {target.GetActivityDescription()}.";
        }
        else
        {
            statusMsg = $"The {target.Name} continues {target.GetActivityDescription()}.";
        }

        return new HuntChoiceResult(
            HuntEnded: false,
            TransitionToCombat: false,
            Outcome: HuntOutcome.Success,
            StatusMessage: statusMsg
        );
    }

    private static HuntChoiceResult ProcessAssess(HuntState state)
    {
        var ctx = state.Context;
        var target = state.Target;
        state.MinutesSpent += 2;

        // Calculate detection chance for next approach
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting").Level;
        double nextApproachDistance = target.DistanceFromPlayer - 25;
        double impairedMultiplier = 1.0;
        if (AbilityCalculator.IsConsciousnessImpaired(ctx.player.GetCapacities().Consciousness))
            impairedMultiplier *= 1.3;
        if (AbilityCalculator.IsPerceptionImpaired(
            AbilityCalculator.CalculatePerception(ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers())))
            impairedMultiplier *= 1.3;
        double detectionChance = HuntingCalculator.CalculateDetectionChanceWithTraits(
            nextApproachDistance,
            target,
            huntingSkill,
            target.FailedStealthChecks,
            impairedMultiplier
        );

        string hint = target.CurrentActivity == AnimalActivity.Grazing
            ? "It's distracted—a good time to approach."
            : target.CurrentActivity == AnimalActivity.Alert
                ? "It's very alert—approaching now is risky."
                : "";

        string statusMsg = $"Detection risk: {detectionChance * 100:F0}%. {hint}";

        return new HuntChoiceResult(
            HuntEnded: false,
            TransitionToCombat: false,
            Outcome: HuntOutcome.Success,
            StatusMessage: statusMsg
        );
    }

    private static HuntChoiceResult ProcessMeleeStrike(HuntState state)
    {
        var ctx = state.Context;
        var target = state.Target;

        target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, BodyTarget.Heart));

        // Create carcass for butchering (not immediate butcher)
        var carcass = new CarcassFeature(target);
        ctx.CurrentLocation.AddFeature(carcass);

        // Remove from persistent herd if applicable
        RemoveFromHerd(state);

        ctx.player.Skills.GetSkill("Hunting").GainExperience(5);

        return new HuntChoiceResult(
            HuntEnded: true,
            TransitionToCombat: false,
            Outcome: HuntOutcome.Success,
            StatusMessage: $"You strike! The {target.Name} falls. Its carcass awaits butchering.",
            Loot: null
        );
    }

    private static HuntChoiceResult ProcessRangedAttack(HuntState state, bool isSpear)
    {
        var ctx = state.Context;
        var target = state.Target;
        var weapon = ctx.Inventory.Weapon;

        state.MinutesSpent += 2;

        double hitChance = HuntHandler.CalculateThrownAccuracy(ctx, target, isSpear, weapon);

        // Consume stone immediately (it's thrown either way)
        if (!isSpear)
        {
            ctx.Inventory.Pop(Resource.Stone);
        }

        bool hit = Utils.RandDouble(0, 1) < hitChance;

        if (hit)
        {
            string weaponName = isSpear && weapon != null ? weapon.Name : "stone";
            target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, BodyTarget.Heart));

            // Create carcass for butchering (not immediate butcher)
            var carcass = new CarcassFeature(target);
            ctx.CurrentLocation.AddFeature(carcass);

            // Remove from persistent herd if applicable
            RemoveFromHerd(state);

            return new HuntChoiceResult(
                HuntEnded: true,
                TransitionToCombat: false,
                Outcome: HuntOutcome.Success,
                StatusMessage: $"Your {weaponName} strikes true! The {target.Name} falls. Its carcass awaits butchering.",
                Loot: null
            );
        }
        else
        {
            // Miss - check for glancing hit (wound)
            double roll = Utils.RandDouble(0, 1);
            bool isGlancingHit = roll < hitChance + (hitChance * 0.3) && isSpear;

            string weaponName = isSpear && weapon != null ? weapon.Name : "stone";

            if (isGlancingHit)
            {
                double woundSeverity = Utils.RandDouble(0.3, 0.8);
                string woundDesc = woundSeverity > 0.6
                    ? "Bright red arterial spray—you could follow the blood trail."
                    : "Dark blood, muscle wound—the animal barely slowed.";

                // Split wounded animal into its own herd if from persistent herd
                SplitWoundedFromHerd(state);

                // Create WoundedPrey tension
                var tension = Tensions.ActiveTension.WoundedPrey(
                    woundSeverity,
                    null,
                    ctx.CurrentLocation
                );
                ctx.Tensions.AddTension(tension);

                if (isSpear)
                {
                    ctx.Update(3, ActivityType.Hunting); // Time to retrieve spear
                    state.MinutesSpent += 3;
                }

                GameDisplay.AddNarrative(ctx, $"The {target.Name} escaped.");

                return new HuntChoiceResult(
                    HuntEnded: true,
                    TransitionToCombat: false,
                    Outcome: HuntOutcome.PreyFled,
                    StatusMessage: $"Your {weaponName} grazes the {target.Name}! {woundDesc}"
                );
            }
            else
            {
                // Clean miss
                if (isSpear)
                {
                    ctx.Update(3, ActivityType.Hunting);
                    state.MinutesSpent += 3;
                }

                GameDisplay.AddNarrative(ctx, $"The {target.Name} escaped.");

                return new HuntChoiceResult(
                    HuntEnded: true,
                    TransitionToCombat: false,
                    Outcome: HuntOutcome.PreyFled,
                    StatusMessage: $"Your {weaponName} misses! The {target.Name} bolts."
                );
            }
        }
    }

    private static HuntChoiceResult ProcessStop(HuntState state)
    {
        GameDisplay.AddNarrative(state.Context, "You abandon the hunt.");

        return new HuntChoiceResult(
            HuntEnded: true,
            TransitionToCombat: false,
            Outcome: HuntOutcome.PlayerAbandoned,
            StatusMessage: "You abandon the hunt."
        );
    }

    /// <summary>
    /// Show hunt outcome overlay.
    /// </summary>
    private static void ShowHuntOutcome(HuntState state, HuntChoiceResult result)
    {
        var ctx = state.Context;

        // Build items gained list
        var itemsGained = new List<string>();
        if (result.Loot != null)
        {
            // Iterate through resources to build summary
            foreach (Resource resource in Enum.GetValues<Resource>())
            {
                int count = result.Loot.Count(resource);
                if (count > 0)
                {
                    double weight = result.Loot.Weight(resource);
                    itemsGained.Add($"+{weight:F1}kg {resource.ToDisplayName()}");
                }
            }
        }

        // Determine result string
        string resultStr = result.Outcome switch
        {
            HuntOutcome.Success => "success",
            HuntOutcome.PreyFled => "fled",
            HuntOutcome.PlayerAbandoned => "abandoned",
            HuntOutcome.TurnedCombat => "combat",
            _ => "unknown"
        };

        var outcome = new HuntOutcomeDto(
            Result: resultStr,
            Message: result.StatusMessage ?? "The hunt is over.",
            TotalMinutesSpent: state.MinutesSpent,
            ItemsGained: itemsGained,
            EffectsApplied: [],
            TransitionToCombat: result.TransitionToCombat
        );

        var huntDto = new HuntDto(
            AnimalName: state.Target.Name,
            AnimalDescription: state.Target.GetTraitDescription(),
            AnimalActivity: state.Target.GetActivityDescription(),
            AnimalState: state.Target.State.ToString().ToLower(),
            CurrentDistanceMeters: state.Target.DistanceFromPlayer,
            PreviousDistanceMeters: state.PreviousDistanceMeters,
            IsAnimatingDistance: state.JustApproached,
            MinutesSpent: state.MinutesSpent,
            StatusMessage: null,
            Choices: [],
            Outcome: outcome
        );

        WebIO.RenderHunt(ctx, huntDto);
        WebIO.WaitForHuntContinue(ctx);
    }

    /// <summary>
    /// Translate combat result to hunt outcome.
    /// </summary>
    private static (HuntOutcome, int) TranslateCombatResult(CombatResult combatResult, int minutesSpent)
    {
        return combatResult switch
        {
            CombatResult.Victory => (HuntOutcome.Success, minutesSpent),
            CombatResult.Defeat => (HuntOutcome.PlayerDied, minutesSpent),
            CombatResult.Fled => (HuntOutcome.PreyFled, minutesSpent),
            CombatResult.AnimalFled => (HuntOutcome.PreyFled, minutesSpent),
            CombatResult.AnimalDisengaged => (HuntOutcome.PreyFled, minutesSpent),
            CombatResult.DistractedWithMeat => (HuntOutcome.PreyFled, minutesSpent),
            _ => (HuntOutcome.PreyFled, minutesSpent)
        };
    }

    /// <summary>
    /// Remove the killed animal from its persistent herd.
    /// </summary>
    private static void RemoveFromHerd(HuntState state)
    {
        if (state.HerdId == null) return;

        var herd = state.Context.Herds.GetHerdById(state.HerdId.Value);
        if (herd == null) return;

        herd.RemoveMember(state.Target);

        // Clean up empty herds
        if (herd.IsEmpty)
        {
            state.Context.Herds.RemoveHerd(herd);
        }
    }

    /// <summary>
    /// Split a wounded animal off from its herd into a new herd of size 1.
    /// The wounded animal flees away from the player.
    /// </summary>
    private static void SplitWoundedFromHerd(HuntState state)
    {
        if (state.HerdId == null) return;

        var herd = state.Context.Herds.GetHerdById(state.HerdId.Value);
        if (herd == null) return;

        var ctx = state.Context;

        // Calculate flee direction (away from player)
        GridPosition fleeDirection;
        if (ctx.Map != null)
        {
            var playerPos = ctx.Map.CurrentPosition;
            var herdPos = herd.Position;

            // Calculate direction away from player
            int dx = herdPos.X - playerPos.X;
            int dy = herdPos.Y - playerPos.Y;

            // Normalize and move 1-2 tiles away
            if (dx == 0 && dy == 0)
            {
                // Herd at player position - pick random direction
                var directions = herdPos.GetCardinalNeighbors().ToList();
                fleeDirection = directions.Count > 0
                    ? directions[Utils.RandInt(0, directions.Count - 1)]
                    : herdPos;
            }
            else
            {
                // Move away from player
                int fleeX = herdPos.X + (dx != 0 ? Math.Sign(dx) : 0);
                int fleeY = herdPos.Y + (dy != 0 ? Math.Sign(dy) : 0);
                fleeDirection = new GridPosition(fleeX, fleeY);
            }
        }
        else
        {
            // No map - just use herd's current position
            fleeDirection = herd.Position;
        }

        // Split the wounded animal off
        ctx.Herds.SplitWounded(herd, state.Target, fleeDirection);
    }


    /// <summary>
    /// Attempt to approach the target animal, reducing distance and checking for detection.
    /// </summary>
    /// <returns>True if approach successful (not detected), false if detected</returns>
    private static bool AttemptApproach(Location location, Animal animal, GameContext ctx)
    {
        if (!IsTargetValid(animal))
            return false;

        // Calculate approach distance
        double approachDistance = HuntingCalculator.CalculateApproachDistance();
        double newDistance = Math.Max(0, animal.DistanceFromPlayer - approachDistance);

        GameDisplay.AddNarrative(ctx, $"You carefully move {approachDistance:F0}m closer...");

        // Check for detection (uses traits + activity)
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting").Level;
        double impairedMultiplier = 1.0;
        if (AbilityCalculator.IsConsciousnessImpaired(ctx.player.GetCapacities().Consciousness))
            impairedMultiplier *= 1.3;  // +30% detection when mentally impaired
        if (AbilityCalculator.IsPerceptionImpaired(
            AbilityCalculator.CalculatePerception(ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers())))
            impairedMultiplier *= 1.3;  // +30% detection when senses are foggy
        double detectionChance = HuntingCalculator.CalculateDetectionChanceWithTraits(
            newDistance,
            animal,
            huntingSkill,
            animal.FailedStealthChecks,
            impairedMultiplier
        );

        double detectionRoll = Utils.RandDouble(0, 1);
        bool detected = detectionRoll < detectionChance;
        bool becameAlert = !detected && HuntingCalculator.ShouldBecomeAlert(detectionRoll, detectionChance);

        // Update distance and activity (time passed during approach)
        animal.DistanceFromPlayer = newDistance;
        if (animal.CheckActivityChange(7, out var newActivity) && newActivity.HasValue)
        {
            GameDisplay.AddNarrative(ctx, $"The {animal.Name} shifts—now {animal.GetActivityDescription()}.");
        }

        // Handle detection results
        if (detected)
        {
            animal.BecomeDetected();
            GameDisplay.AddNarrative(ctx, $"The {animal.Name} spots you!");
            HandleAnimalDetection(animal, location, ctx);
            return false;
        }
        else if (becameAlert)
        {
            animal.BecomeAlert();
            animal.FailedStealthChecks++;
            GameDisplay.AddNarrative(ctx, $"The {animal.Name} becomes alert - it senses something nearby.");
            GameDisplay.AddNarrative(ctx, $"New distance: {animal.DistanceFromPlayer:F0}m | State: {animal.State}");
            return true;
        }
        else
        {
            GameDisplay.AddNarrative(ctx, $"You remain undetected.");
            GameDisplay.AddNarrative(ctx, $"New distance: {animal.DistanceFromPlayer:F0}m | State: {animal.State}");
            return true;
        }
    }

    private static bool IsTargetValid(Animal? TargetAnimal)
    {
        if (TargetAnimal == null)
            return false;

        if (!TargetAnimal.IsAlive)
        {
            return false;
        }

        return true;
    }

    private static void HandleAnimalDetection(Animal animal, Location location, GameContext ctx)
    {
        // Check if animal should flee
        if (animal.ShouldFlee(ctx.player))
        {
            GameDisplay.AddNarrative(ctx, $"The {animal.Name} flees!");
            GameDisplay.AddNarrative(ctx, $"The {animal.Name} escaped.");
        }
        else
        {
            // Predator or cornered animal - attacks!
            GameDisplay.AddDanger(ctx, $"The {animal.Name} attacks!");
            ctx.player.IsEngaged = true;
            animal.IsEngaged = true;
            // Combat will be handled by existing combat system
            GameDisplay.AddDanger(ctx, $"Combat initiated with {animal.Name}!");
        }
    }

}
