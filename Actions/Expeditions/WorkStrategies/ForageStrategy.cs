using text_survival.Actions;
using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;
using text_survival.Web.Dto;

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
    // Store clues, focus, and time between GetTimeOptions and Execute
    private List<ForageClue>? _clues;
    private ForageFocus _focus = ForageFocus.General;
    private ForageClue? _followedClue;
    private int _selectedMinutes;
    private bool _cancelled;

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

        while (true)
        {
            string quality = feature.GetQualityDescription();

            // Generate environmental clues using seed for deterministic results
            _clues = ClueSelector.GenerateClues(ctx, location, feature.ClueSeed);

            // Build clue DTOs - only Resource clues suggest a focus
            var clueDtos = _clues.Select((clue, i) => new ForageClueDto(
                Id: $"clue_{i}",
                Description: clue.Description,
                SuggestedFocusId: GetSuggestedFocusId(clue)
            )).ToList();

            // Build focus options - only show if resources of that type exist
            // Description shows actual available resources, not generic examples
            var focusOptions = new List<ForageFocusDto>();

            if (feature.HasResourcesForFocus(ForageFocus.Fuel))
                focusOptions.Add(new("fuel", "Fuel", feature.GetFocusDescription(ForageFocus.Fuel)));
            if (feature.HasResourcesForFocus(ForageFocus.Food))
                focusOptions.Add(new("food", "Food", feature.GetFocusDescription(ForageFocus.Food)));
            if (feature.HasResourcesForFocus(ForageFocus.Medicine))
                focusOptions.Add(new("medicine", "Medicine", feature.GetFocusDescription(ForageFocus.Medicine)));
            if (feature.HasResourcesForFocus(ForageFocus.Materials))
                focusOptions.Add(new("materials", "Materials", feature.GetFocusDescription(ForageFocus.Materials)));

            focusOptions.Add(new("general", "General", "balanced search"));

            // Build time options
            var timeOptions = new List<ForageTimeDto>
            {
                new("15", "Quick - 15 min", 15),
                new("30", "Standard - 30 min", 30),
                new("60", "Thorough - 60 min", 60)
            };

            // Build warnings
            var warnings = new List<string>();
            bool isDark = location.IsDark || ctx.GetTimeOfDay() == GameContext.TimeOfDay.Night;
            if (isDark && !location.HasActiveHeatSource() && !ctx.Inventory.HasLitTorch)
                warnings.Add("It's dark - your yield will be halved without light.");

            var axe = ctx.Inventory.GetTool(ToolType.Axe);
            var shovel = ctx.Inventory.GetTool(ToolType.Shovel);
            if (axe?.Works == true)
                warnings.Add("Your axe will help gather wood.");
            if (shovel?.Works == true)
                warnings.Add("Your shovel will help dig up roots.");

            // Capacity warning when pack is nearly full
            var inv = ctx.Inventory;
            if (inv.MaxWeightKg > 0)
            {
                double capacityPct = inv.CurrentWeightKg / inv.MaxWeightKg;
                if (capacityPct >= 0.8)
                {
                    double remaining = inv.RemainingCapacityKg;
                    warnings.Add($"Your pack is nearly full ({remaining:F1}kg remaining).");
                }
            }

            var forageDto = new ForageDto(
                LocationQuality: quality,
                Clues: clueDtos,
                FocusOptions: focusOptions,
                TimeOptions: timeOptions,
                Warnings: warnings
            );

            // Show overlay and get selection
            var (selectedFocus, selectedMinutes) = WebIO.SelectForageOptions(ctx, forageDto);

            // Handle "Keep Walking" - spend time to reroll clues
            if (selectedMinutes == -1)
            {
                ctx.Update(5, ActivityType.Traveling);
                feature.RerollClues();
                continue; // Show overlay again with new clues
            }

            if (selectedFocus == null)
            {
                _cancelled = true;
                return null; // Cancelled
            }

            _focus = selectedFocus.Value;

            // Check if focus matches any visible Resource clue (implicit clue-following)
            _followedClue = _clues.FirstOrDefault(c =>
                c.Category == ClueCategory.Resource && (
                    (_focus == ForageFocus.Fuel && c.SuggestedResources.Any(r => r.IsFuel())) ||
                    (_focus == ForageFocus.Food && c.SuggestedResources.Any(r => r.IsFood())) ||
                    (_focus == ForageFocus.Medicine && c.SuggestedResources.Any(r => r.IsMedicine())) ||
                    (_focus == ForageFocus.Materials && c.SuggestedResources.Any(r => r.IsMaterial()))
                ));

            // Also track if player is following a Game or Scavenge clue
            // (These don't match focus but affect results)
            var gameClue = _clues.FirstOrDefault(c => c.Category == ClueCategory.Game);
            var scavengeClue = _clues.FirstOrDefault(c => c.Category == ClueCategory.Scavenge);

            // Store for Execute() processing
            _gameClue = gameClue;
            _scavengeClue = scavengeClue;

            // Tutorial for first clue experience
            if (_clues.Count > 0)
            {
                ctx.ShowTutorialOnce("You've learned to read the land. Signs like these tell you what might be found nearby.");
            }

            // Store selected time - ApplyImpairments will return it
            _selectedMinutes = selectedMinutes;
            return null;
        }
    }

    // Additional clue storage for Execute()
    private ForageClue? _gameClue;
    private ForageClue? _scavengeClue;

    private static string? GetSuggestedFocusId(ForageClue clue)
    {
        // Only Resource clues suggest a focus category
        if (clue.Category != ClueCategory.Resource)
            return null;

        if (clue.SuggestedResources.Any(r => r.IsFuel()))
            return "fuel";
        if (clue.SuggestedResources.Any(r => r.IsFood()))
            return "food";
        if (clue.SuggestedResources.Any(r => r.IsMedicine()))
            return "medicine";
        if (clue.SuggestedResources.Any(r => r.IsMaterial()))
            return "materials";
        return null;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        // Use pre-selected time from popup (baseTime will be 0 since GetTimeOptions returns null)
        int workTime = _selectedMinutes;

        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkBreathing: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(workTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() =>
        FollowingAnimalSigns() ? ActivityType.Tracking : ActivityType.Foraging;

    private bool FollowingAnimalSigns() =>
        _gameClue != null || _scavengeClue != null;

    public string GetActivityName() => "foraging";

    public bool AllowedInDarkness => true;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        // Early return if user cancelled
        if (_cancelled)
            return new WorkResult([], null, 0, false);

        var feature = location.GetFeature<ForageFeature>()!;

        // Narrative based on clue types or focus
        if (_gameClue != null)
        {
            GameDisplay.AddNarrative(ctx, $"You follow the signs... {_gameClue.Description.ToLower()}");
        }
        else if (_scavengeClue != null)
        {
            GameDisplay.AddNarrative(ctx, $"You investigate... {_scavengeClue.Description.ToLower()}");
        }
        else if (_followedClue != null)
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

        // Apply negative clue yield modifier
        var negativeClue = _clues?.FirstOrDefault(c => c.Category == ClueCategory.Negative);
        if (negativeClue != null)
        {
            found.ApplyMultiplier(negativeClue.YieldModifier);
        }

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

        string activityHeader;
        if (found.IsEmpty)
        {
            activityHeader = "You didn't find anything";
            resultMessage = WorkRunner.GetForageFailureMessage(quality);
        }
        else
        {
            activityHeader = "Foraging";
            collected.Add(found.GetDescription());
            InventoryCapacityHelper.CombineAndReport(ctx, found);
            resultMessage = quality is "sparse" or "picked over"
                ? "Resources here are getting scarce."
                : "You gather what you can find.";
        }

        // Handle Game clues - apply hunt bonus to territory
        if (_gameClue != null)
        {
            var territory = location.GetFeature<AnimalTerritoryFeature>();
            if (territory != null)
            {
                territory.ApplyHuntBonus(_gameClue.HuntBonus);
                GameDisplay.AddNarrative(ctx, "You've spotted signs of game. Hunting here might be more fruitful.");
            }
        }

        // Handle Scavenge scenarios - spawn carcass with potential encounter
        if (_scavengeClue?.Scenario != null)
        {
            var scenario = _scavengeClue.Scenario;

            // Bones-only scenario (raptor pellets, old bones)
            if (scenario.Freshness == FreshnessCategory.BonesOnly &&
                scenario.Animals.Animals.Length == 0)
            {
                ctx.Inventory.Add(Resource.Bone, 0.1 + Random.Shared.NextDouble() * 0.2);
                collected.Add("bone scraps");
                GameDisplay.AddNarrative(ctx, "You find some bone fragments.");
            }
            else
            {
                // Select animal from weighted pool
                string animalName = scenario.Animals.Select();
                if (!string.IsNullOrEmpty(animalName))
                {
                    // Roll freshness and create carcass with appropriate harvestedPct
                    double harvestedPct = FreshnessHelper.RollHarvestedPct(scenario.Freshness);
                    var carcass = CarcassFeature.FromAnimalName(animalName, harvestedPct);
                    location.AddFeature(carcass);

                    string description = $"{FreshnessHelper.GetDescription(scenario.Freshness)} - {animalName.ToLower()}";
                    collected.Add(description);
                    GameDisplay.AddNarrative(ctx, $"You find a {description}. It could be butchered for resources.");

                    // Roll for predator encounter with freshness modifier
                    double riskMod = FreshnessHelper.GetRiskModifier(scenario.Freshness);
                    double encounterChance = scenario.BaseEncounterChance * riskMod;

                    if (encounterChance > 0 && Utils.DetermineSuccess(encounterChance))
                    {
                        string? predator = scenario.Predators.Select();
                        if (predator != null)
                        {
                            double boldness = FreshnessHelper.GetBoldness(scenario.Freshness);
                            ctx.QueueEncounter(new EncounterConfig(predator, 30, boldness));
                            GameDisplay.AddWarning(ctx, "Something is watching from the brush...");
                        }
                    }
                }
            }
        }

        // Show results in popup overlay
        WebIO.ShowWorkResult(ctx, activityHeader, resultMessage, collected);

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
