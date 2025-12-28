using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for foraging ambient resources from a location.
/// Requires ForageFeature. Impaired by moving and breathing capacity.
/// Yields reduced by perception impairment.
///
/// Shows environmental clues to help players make informed focus choices.
/// </summary>
public class ForageStrategy : IWorkStrategy
{
    // Store clues and focus between GetTimeOptions and Execute
    private List<ForageClue>? _clues;
    private ForageFocus _focus = ForageFocus.General;
    private ForageClue? _followedClue;

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var feature = location.GetFeature<ForageFeature>();
        if (feature == null)
            return "There's nothing to forage here.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var feature = location.GetFeature<ForageFeature>()!;
        string quality = feature.GetQualityDescription();

        // Generate environmental clues
        _clues = ClueSelector.GenerateClues(ctx, location);

        // Calculate perception for clue display
        var perception = AbilityCalculator.CalculatePerception(
            ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers());
        bool showHints = ClueSelector.ShouldShowHints(perception);

        // Display clues as narrative observations (not buttons)
        GameDisplay.ClearNarrative(ctx);
        GameDisplay.AddNarrative(ctx, $"You scan the area. Resources look {quality}.");
        if (_clues.Count > 0)
        {
            GameDisplay.AddNarrative(ctx, "You notice:");
            foreach (var clue in _clues)
            {
                // Show hint text only if perception is adequate
                string display = showHints
                    ? $"  • {clue.Description} {clue.HintText}"
                    : $"  • {clue.Description}";
                GameDisplay.AddNarrative(ctx, display);
            }
            if (!showHints)
                GameDisplay.AddWarning(ctx, "Your foggy senses make it hard to read the signs.");
        }

        // Show any penalties/bonuses
        bool isDark = location.IsDark || ctx.GetTimeOfDay() == GameContext.TimeOfDay.Night;
        if (isDark && !location.HasActiveHeatSource() && !ctx.Inventory.HasLitTorch)
            GameDisplay.AddWarning(ctx, "It's dark - your yield will be halved without light.");

        var axe = ctx.Inventory.GetTool(ToolType.Axe);
        var shovel = ctx.Inventory.GetTool(ToolType.Shovel);
        if (axe?.Works == true)
            GameDisplay.AddNarrative(ctx, "Your axe will help gather wood.");
        if (shovel?.Works == true)
            GameDisplay.AddNarrative(ctx, "Your shovel will help dig up roots.");

        GameDisplay.Render(ctx, statusText: "Reading the land.");

        // Get focus choice
        var focusChoice = new Choice<ForageFocus?>("What do you focus on?");
        focusChoice.AddOption("Fuel (sticks, bark, wood)", ForageFocus.Fuel);
        focusChoice.AddOption("Food (berries, roots, nuts)", ForageFocus.Food);
        focusChoice.AddOption("Medicine (fungi, moss, bark)", ForageFocus.Medicine);
        focusChoice.AddOption("Materials (stone, bone, fiber)", ForageFocus.Materials);
        focusChoice.AddOption("Search generally", ForageFocus.General);
        focusChoice.AddOption("Cancel", null);

        var focusResult = focusChoice.GetPlayerChoice(ctx);
        if (focusResult == null)
            return null; // Cancel work

        _focus = focusResult.Value;

        // Check if focus matches any visible clue (implicit clue-following)
        _followedClue = _clues.FirstOrDefault(c =>
            (_focus == ForageFocus.Fuel && c.SuggestedResources.Any(r => r.IsFuel())) ||
            (_focus == ForageFocus.Food && c.SuggestedResources.Any(r => r.IsFood())) ||
            (_focus == ForageFocus.Medicine && c.SuggestedResources.Any(r => r.IsMedicine())) ||
            (_focus == ForageFocus.Materials && c.SuggestedResources.Any(r => r.IsMaterial()))
        );

        // Tutorial for first clue experience
        if (_clues.Count > 0)
        {
            ctx.ShowTutorialOnce("You've learned to read the land. Signs like these tell you what might be found nearby.");
        }

        // Now show time choice
        var timeChoice = new Choice<int>("How long should you search?");
        timeChoice.AddOption("Quick - 15 min", 15);
        timeChoice.AddOption("Standard - 30 min", 30);
        timeChoice.AddOption("Thorough - 60 min", 60);
        timeChoice.AddOption("Cancel", 0);
        return timeChoice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkBreathing: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() =>
        FollowingAnimalSigns() ? ActivityType.Tracking : ActivityType.Foraging;

    private bool FollowingAnimalSigns() =>
        _followedClue?.SuggestedResources.Any(r =>
            r == Resource.Bone || r == Resource.RawMeat) == true;

    public string GetActivityName() => "foraging";

    public bool AllowedInDarkness => true;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var feature = location.GetFeature<ForageFeature>()!;

        // Narrative based on focus
        if (_followedClue != null)
        {
            GameDisplay.AddNarrative(ctx, $"You follow the signs... {_followedClue.Description.ToLower()}");
        }
        else if (_focus != ForageFocus.General)
        {
            GameDisplay.AddNarrative(ctx, $"You search for {FocusProcessor.GetFocusDescription(_focus)}...");
        }
        else
        {
            GameDisplay.AddNarrative(ctx, "You search the area for resources...");
        }

        var found = feature.Forage(actualTime / 60.0);

        // Apply focus to results (self-contained in FocusProcessor)
        FocusProcessor.ApplyFocus(found, _focus, _followedClue);

        // Darkness penalty: limited visibility reduces yield (-50%)
        bool isDark = location.IsDark || ctx.GetTimeOfDay() == GameContext.TimeOfDay.Night;
        if (isDark && !location.HasActiveHeatSource() && !ctx.Inventory.HasLitTorch)
        {
            found.ApplyMultiplier(0.5);
            GameDisplay.AddWarning(ctx, "The darkness limits what you can find.");
        }

        // Perception impairment reduces yield (-15%)
        var perception = AbilityCalculator.CalculatePerception(
            ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers());
        if (AbilityCalculator.IsPerceptionImpaired(perception))
        {
            found.ApplyMultiplier(0.85);
            GameDisplay.AddWarning(ctx, "Your foggy senses cause you to miss some resources.");
        }

        // Tool bonuses - help gather more efficiently (+10% each)
        bool hasAxeBonus = false;
        bool hasShovelBonus = false;

        var axe = ctx.Inventory.GetTool(ToolType.Axe);
        if (axe != null && axe.Works)
        {
            found.ApplyMultiplier(1.10);
            hasAxeBonus = true;
        }

        var shovel = ctx.Inventory.GetTool(ToolType.Shovel);
        if (shovel != null && shovel.Works)
        {
            found.ApplyMultiplier(1.10);
            hasShovelBonus = true;
        }

        // Tutorial: Tool bonus explanation (once per tool type)
        if (hasAxeBonus)
            ctx.ShowTutorialOnce("Your axe helps break branches and strip bark. (+10% yield)");
        if (hasShovelBonus)
            ctx.ShowTutorialOnce("Your shovel helps dig up roots and turn soil. (+10% yield)");

        var collected = new List<string>();
        string quality = feature.GetQualityDescription();
        string resultMessage;

        if (found.IsEmpty)
        {
            resultMessage = WorkRunner.GetForageFailureMessage(quality);
        }
        else
        {
            collected.Add(found.GetDescription());
            InventoryCapacityHelper.CombineAndReport(ctx, found);
            resultMessage = quality is "sparse" or "picked over"
                ? "Resources here are getting scarce."
                : "You gather what you can find.";
        }

        // Show results in popup overlay
        WebIO.ShowWorkResult(ctx, "Foraging", resultMessage, collected);

        // Tutorial: Show fuel progress on Day 1
        double totalFuelGathered = found.GetWeight(ResourceCategory.Fuel);
        if (ctx.DaysSurvived == 0 && totalFuelGathered > 0)
        {
            double currentFuel = ctx.Inventory.GetWeight(ResourceCategory.Fuel);
            if (currentFuel < 8.0)
            {
                double needed = 8.0 - currentFuel;
                GameDisplay.AddNarrative(ctx, $"You'll want about {needed:F0}kg more for tonight.");
            }
        }

        return new WorkResult(collected, null, actualTime, false);
    }
}
