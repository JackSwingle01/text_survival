using text_survival.Actions.Tensions;
using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.UI;
using text_survival.Desktop;

namespace text_survival.Actions;

/// <summary>
/// Result of an interactive hunt.
/// </summary>
public enum HuntOutcome
{
    Success,        // Kill completed
    PreyFled,       // Animal escaped
    PlayerAbandoned,// Player gave up
    PlayerDied      // Player died during hunt
}

/// <summary>
/// Handles hunting by transitioning to the unified stealth-combat system.
/// </summary>
public static class HuntRunner
{
    /// <summary>
    /// Run an interactive hunt against a found animal.
    /// Shows initial prompt, then transitions to stealth-combat grid.
    /// </summary>
    /// <param name="sourceHerd">Optional persistent herd this animal belongs to.</param>
    /// <returns>Outcome of the hunt</returns>
    public static HuntOutcome Run(Animal target, Location location, GameContext ctx, Herd? sourceHerd = null)
    {
        // Show simple approach prompt
        if (!PromptApproach(target, ctx))
        {
            return HuntOutcome.PlayerAbandoned;
        }

        // Record animal encounter in Discovery Log
        ctx.RecordAnimalEncounter(target.AnimalType);

        // Auto-equip spear if available
        ctx.Inventory.GetOrEquipWeapon(ctx, Items.ToolType.Spear);

        // Transition to stealth-combat grid
        var combatResult = CombatOrchestrator.RunHunt(ctx, target);

        // Handle post-hunt cleanup
        HandlePostHunt(ctx, location, target, sourceHerd, combatResult);

        return TranslateOutcome(combatResult);
    }

    /// <summary>
    /// Show approach prompt with flavor text and behavioral hints.
    /// </summary>
    private static bool PromptApproach(Animal target, GameContext ctx)
    {
        // Get sighting variant for flavor
        var sighting = HuntingSightingSelector.SelectForAnimal(target, ctx);
        var behavior = HuntingSightingSelector.MapActivityToBehavior(target);
        string hint = HuntingSightingSelector.GetBehaviorHint(behavior);

        // Show flavor text + hint
        GameDisplay.AddNarrative(ctx, $"{sighting.Description}. {hint}");

        // Build prompt with trait info if available
        string traitDesc = target.GetTraitDescription();
        string message = string.IsNullOrEmpty(traitDesc)
            ? $"You spot a {target.Name.ToLower()}. Approach?"
            : $"You spot a {target.Name.ToLower()} ({traitDesc}). Approach?";

        var choices = new List<string> { "Approach", "Let it go" };
        string choice = DesktopIO.Select(ctx, message, choices, s => s);

        return choice == "Approach";
    }

    /// <summary>
    /// Handle post-hunt cleanup: herd management, territory tracking, wounded prey tensions.
    /// </summary>
    private static void HandlePostHunt(GameContext ctx, Location location, Animal target, Herd? sourceHerd, CombatResult result)
    {
        if (result == CombatResult.Victory)
        {
            // Record successful hunt for territory depletion
            var territory = location.GetFeature<AnimalTerritoryFeature>();
            territory?.RecordSuccessfulHunt();

            // Remove from persistent herd if applicable
            if (sourceHerd != null)
            {
                sourceHerd.RemoveMember(target);
                if (sourceHerd.IsEmpty)
                {
                    ctx.Herds.RemoveHerd(sourceHerd);
                }
            }

            // Grant hunting XP
            ctx.player.Skills.GetSkill("Hunting")?.GainExperience(5);
        }
        else if (result == CombatResult.AnimalFled)
        {
            // Create WoundedPrey tension if animal was wounded
            if (target.CurrentWoundSeverity > 0)
            {
                // Severity based on wound (0.3-0.8)
                double tensionSeverity = 0.3 + (target.CurrentWoundSeverity * 0.5);

                // Split wounded animal from herd if applicable
                Herd? woundedHerd = null;
                if (sourceHerd != null && ctx.Map != null)
                {
                    var fleeDirection = CalculateFleeDirection(ctx.Map.CurrentPosition, sourceHerd.Position);
                    woundedHerd = ctx.Herds.SplitWounded(sourceHerd, target, fleeDirection);
                }

                var tension = ActiveTension.WoundedPrey(
                    tensionSeverity,
                    target.AnimalType,
                    location,
                    woundedHerd
                );
                ctx.Tensions.AddTension(tension);

                string woundDesc = tensionSeverity > 0.6
                    ? "Bright red arterial spray—you could follow the blood trail."
                    : "Dark blood stains mark its escape route.";
                GameDisplay.AddNarrative(ctx, $"The wounded {target.Name.ToLower()} escapes. {woundDesc}");
            }
        }
    }

    /// <summary>
    /// Calculate flee direction away from player.
    /// </summary>
    private static GridPosition CalculateFleeDirection(GridPosition playerPos, GridPosition herdPos)
    {
        int dx = herdPos.X - playerPos.X;
        int dy = herdPos.Y - playerPos.Y;

        if (dx == 0 && dy == 0)
        {
            // At same position - pick random adjacent
            return new GridPosition(herdPos.X + Utils.RandInt(-1, 2), herdPos.Y + Utils.RandInt(-1, 2));
        }

        // Move away from player
        return new GridPosition(
            herdPos.X + (dx != 0 ? Math.Sign(dx) : 0),
            herdPos.Y + (dy != 0 ? Math.Sign(dy) : 0)
        );
    }

    /// <summary>
    /// Translate combat result to hunt outcome.
    /// Time is tracked by combat via GameContext.Update() - no separate tracking needed.
    /// </summary>
    private static HuntOutcome TranslateOutcome(CombatResult combatResult)
    {
        return combatResult switch
        {
            CombatResult.Victory => HuntOutcome.Success,
            CombatResult.Defeat => HuntOutcome.PlayerDied,
            CombatResult.Fled => HuntOutcome.PlayerAbandoned,
            CombatResult.AnimalFled => HuntOutcome.PreyFled,
            CombatResult.AnimalDisengaged => HuntOutcome.PreyFled,
            CombatResult.DistractedWithMeat => HuntOutcome.PreyFled,
            _ => HuntOutcome.PreyFled
        };
    }
}
