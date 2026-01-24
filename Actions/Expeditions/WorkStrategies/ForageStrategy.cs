using Raylib_cs;
using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Desktop;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using DesktopIO = text_survival.Desktop.DesktopIO;

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
    private List<string> _impairmentWarnings = [];

    // Accumulated loot from RunCustomProgress (calculated minute-by-minute)
    private Inventory? _foundItems;

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
            // Generate environmental clues using seed for deterministic results
            _clues = ClueSelector.GenerateClues(ctx, location, feature.ClueSeed);

            // Show overlay and get selection - pass domain objects directly
            var (selectedFocus, selectedMinutes) = DesktopIO.SelectForageOptions(ctx, feature, _clues);

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

        // Store impairment warnings for later use in Execute
        _impairmentWarnings = warnings;

        return ((int)(workTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() =>
        FollowingAnimalSigns() ? ActivityType.Tracking : ActivityType.Foraging;

    private bool FollowingAnimalSigns() =>
        _gameClue != null || _scavengeClue != null;

    public string GetActivityName() => "foraging";

    public bool AllowedInDarkness => true;

    /// <summary>
    /// Custom progress: minute-by-minute foraging with items revealed as found.
    /// Game logic owns the loop; display only renders what it's told.
    /// Events pause foraging; aborting events stop it entirely.
    /// </summary>
    public (int elapsed, bool interrupted)? RunCustomProgress(GameContext ctx, Location location, int minutes)
    {
        if (_cancelled)
            return (0, false);

        var feature = location.GetFeature<ForageFeature>();
        if (feature == null)
            return null; // Fall back to standard progress

        // Calculate modifiers once at the start (for consistency during session)
        var negativeClue = _clues?.FirstOrDefault(c => c.Category == ClueCategory.Negative);
        double negativeMultiplier = negativeClue?.YieldModifier ?? 1.0;

        double perception = AbilityCalculator.GetPerception(ctx.player, ctx);
        double perceptionMultiplier = perception < 1.0 ? perception : 1.0;

        var axe = ctx.Inventory.GetTool(ToolType.Axe);
        double axeMultiplier = axe?.Works == true ? 1.10 : 1.0;

        var shovel = ctx.Inventory.GetTool(ToolType.Shovel);
        double shovelMultiplier = shovel?.Works == true ? 1.10 : 1.0;

        double totalMultiplier = negativeMultiplier * perceptionMultiplier * axeMultiplier * shovelMultiplier;

        // Animation timing: ~0.3 seconds per in-game minute, clamped to reasonable bounds
        float animDurationSeconds = Math.Clamp(minutes * 0.3f, 1.0f, 30.0f);
        float elapsedSeconds = 0;
        int simulatedMinutes = 0;

        var accumulated = new Inventory();
        var activity = GetActivityType();
        string statusText = "Foraging...";

        // Game logic owns the loop
        while (simulatedMinutes < minutes && !Raylib.WindowShouldClose() && ctx.player.IsAlive)
        {
            float deltaTime = Raylib.GetFrameTime();
            elapsedSeconds += deltaTime;

            // Calculate how many minutes to simulate this frame
            int targetMinutes = Math.Min((int)(elapsedSeconds * minutes / animDurationSeconds), minutes);

            // GAME LOGIC: simulate pending minutes
            while (simulatedMinutes < targetMinutes && ctx.player.IsAlive)
            {
                ctx.Update(1, activity);
                simulatedMinutes++;

                // Check for event interruption
                if (ctx.EventOccurredLastUpdate)
                {
                    if (ctx.LastEventAborted)
                    {
                        // Aborting event - stop foraging, return partial loot
                        _foundItems = accumulated;
                        return (simulatedMinutes, true);
                    }
                    // Non-aborting event - continue foraging after event resolves
                }

                // GAME LOGIC: forage this minute
                var found = feature.Forage(1.0 / 60.0);
                if (!found.IsEmpty)
                {
                    FocusProcessor.ApplyFocus(found, _focus, _followedClue);
                    if (totalMultiplier < 1.0)
                        found.ApplyMultiplier(totalMultiplier);
                    accumulated.Combine(found);
                }
            }

            // DISPLAY: render one frame (no callbacks, no game logic)
            DesktopRuntime.RenderForagingFrame(
                ctx,
                accumulated.GetLootItems(),
                (float)accumulated.CurrentWeightKg,
                simulatedMinutes,
                minutes,
                statusText);
        }

        // Store accumulated loot for Execute()
        _foundItems = accumulated;

        return (simulatedMinutes, false);
    }

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        // Early return if user cancelled
        if (_cancelled)
            return new WorkResult([], null, 0, false);

        var feature = location.GetFeature<ForageFeature>()!;

        // Collect narrative and warnings for overlay display
        var narrative = new List<string>();
        var warnings = new List<string>();

        // Use accumulated items from RunCustomProgress (already shown during progress bar)
        var found = _foundItems ?? new Inventory();

        // Check for perception warnings (always needed for UI)
        double currentPerception = AbilityCalculator.GetPerception(ctx.player, ctx);
        if (currentPerception < 1.0)
        {
            var abilityContext = AbilityContext.FromFullContext(
                ctx.player, ctx.Inventory, ctx.player.CurrentLocation, ctx.GameTime.Hour);

            if (abilityContext.DarknessLevel > 0.5 && !abilityContext.HasLightSource)
                warnings.Add("The darkness limits what you can find.");
            else if (currentPerception < 0.7)
                warnings.Add("Your foggy senses cause you to miss some resources.");
        }

        // Tool tutorials
        var axeTool = ctx.Inventory.GetTool(ToolType.Axe);
        var shovelTool = ctx.Inventory.GetTool(ToolType.Shovel);
        if (axeTool?.Works == true)
            ctx.ShowTutorialOnce("Your axe helps break branches and strip bark. (+10% yield)");
        if (shovelTool?.Works == true)
            ctx.ShowTutorialOnce("Your shovel helps dig up roots and turn soil. (+10% yield)");

        var collected = new List<string>();
        string quality = feature.GetQualityDescription();

        if (found.IsEmpty)
        {
            // Show failure message (items weren't shown during progress since there were none)
            string failureMessage = WorkRunner.GetForageFailureMessage(quality);
            DesktopIO.ShowWorkResult(ctx, "Foraging", failureMessage, [], [], []);
        }
        else
        {
            // Add to inventory
            InventoryCapacityHelper.CombineAndReport(ctx, found);
            collected.Add(found.GetDescription());

            // Show loot summary
            DesktopIO.ShowLootReveal(ctx, found);
        }

        // Process exploration progress and reveal discoveries
        var discoveries = ProcessExploration(ctx, location, actualTime);
        foreach (var discovery in discoveries)
        {
            // EventTriggerFeature: queue event and remove feature
            if (discovery.Feature is EventTriggerFeature trigger)
            {
                var evt = DiscoveryEventFactory.Create(trigger.EventId, ctx);
                ctx.EventQueue.Enqueue(evt);
                location.RemoveFeature(trigger);
                continue;
            }

            string message = GetDiscoveryMessage(discovery);
            if (discovery.Category == DiscoveryCategory.Minor)
            {
                narrative.Add(message);
            }
            else // Major
            {
                // Major discoveries get a special popup
                DesktopIO.ShowMajorDiscovery(ctx, message);
            }
        }

        // Handle Game clues - apply hunt bonus to territory
        if (_gameClue != null)
        {
            var territory = location.GetFeature<AnimalTerritoryFeature>();
            if (territory != null)
            {
                territory.ApplyHuntBonus(_gameClue.HuntBonus);
                narrative.Add("You've spotted signs of game. Hunting here might be more fruitful.");
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
                narrative.Add("You find some bone fragments.");
            }
            else
            {
                // Select animal from weighted pool
                AnimalType animalType = scenario.Animals.Select();

                // Create carcass with appropriate harvestedPct
                double harvestedPct = FreshnessHelper.RollHarvestedPct(scenario.Freshness);
                var carcass = CarcassFeature.FromAnimal(AnimalFactory.FromType(animalType, location, ctx.Map), harvestedPct);
                location.AddFeature(carcass);

                string description = $"{FreshnessHelper.GetDescription(scenario.Freshness)} - {animalType.DisplayName().ToLower()}";
                collected.Add(description);
                narrative.Add($"You find a {description}. It could be butchered for resources.");

                // Roll for predator encounter with freshness modifier
                double riskMod = FreshnessHelper.GetRiskModifier(scenario.Freshness);
                double encounterChance = scenario.BaseEncounterChance * riskMod;

                if (encounterChance > 0 && Utils.DetermineSuccess(encounterChance))
                {
                    AnimalType? predator = scenario.Predators.Select();
                    if (predator != null)
                    {
                        double boldness = FreshnessHelper.GetBoldness(scenario.Freshness);
                        ctx.QueueEncounter(new EncounterConfig(predator.Value, 30, boldness));
                        warnings.Add("Something is watching from the brush...");
                    }
                }
            }
        }

        // Combine impairment warnings with activity-specific warnings
        var allWarnings = new List<string>();
        allWarnings.AddRange(_impairmentWarnings);
        allWarnings.AddRange(warnings);

        // Show follow-up dialog if there are special finds, narrative, or warnings
        bool hasSpecialFinds = _gameClue != null || _scavengeClue?.Scenario != null;
        bool hasWarnings = allWarnings.Count > 0;

        if (hasSpecialFinds || hasWarnings)
        {
            DesktopIO.ShowWorkResult(ctx, "Foraging", "", [], narrative, allWarnings);
        }

        // Tutorial: Show fuel progress on Day 1
        double totalFuelGathered = found.GetWeight(ResourceCategory.Fuel);
        if (ctx.DaysSurvived == 0 && totalFuelGathered > 0)
        {
            double currentFuel = ctx.Inventory.GetWeight(ResourceCategory.Fuel);
            if (currentFuel < 8.0)
            {
                double needed = 8.0 - currentFuel;
                ctx.ShowTutorialOnce($"You'll want about {needed:F0} more kg of fuel for tonight.");
            }
        }

        return new WorkResult(collected, null, actualTime, false);
    }

    /// <summary>
    /// Add exploration progress and check for discoveries.
    /// Perception affects how quickly you notice things.
    /// Progress is tracked on ForageFeature (single source of truth).
    /// </summary>
    private List<HiddenFeature> ProcessExploration(GameContext ctx, Location location, int minutes)
    {
        var forage = location.GetFeature<ForageFeature>();
        if (forage == null) return [];

        double hours = minutes / 60.0;
        double perception = AbilityCalculator.GetPerception(ctx.player, ctx);

        // Better perception means noticing things faster
        forage.DiscoveryProgress += hours * perception;

        return location.RevealDiscoveries(forage.DiscoveryProgress);
    }

    /// <summary>
    /// Generate a discovery message based on the feature type.
    /// Uses varied text pools for more interesting presentation.
    /// </summary>
    private static string GetDiscoveryMessage(HiddenFeature discovery)
    {
        var feature = discovery.Feature;

        return feature switch
        {
            HarvestableFeature h => GetHarvestableDiscoveryMessage(h),
            ShelterFeature s => GetShelterDiscoveryMessage(s),
            EnvironmentalDetail d => GetEnvironmentalDetailDiscoveryMessage(d),
            _ => $"You discover something interesting: {feature.Name}."
        };
    }

    // todo - move these to their respective feature classes for better organization
    private static string GetEnvironmentalDetailDiscoveryMessage(EnvironmentalDetail d)
    {
        return d.Name switch
        {
            "fallen_log" => SelectRandom(
                "A fallen log, half-buried in snow. Might yield some sticks.",
                "You notice a rotting log nearby. Worth investigating.",
                "Deadfall - a log that could provide some kindling."
            ),
            "animal_tracks" => SelectRandom(
                "Fresh tracks in the snow. Something passed through here.",
                "Animal tracks - worth examining more closely.",
                "You spot tracks. The snow holds their shape well."
            ),
            "animal_droppings" => SelectRandom(
                "Animal scat. Fresh enough to tell you something lives here.",
                "Droppings nearby. You're not alone in this area.",
                "You notice animal scat - a sign of recent activity."
            ),
            "bent_branches" => SelectRandom(
                "Bent branches catch your eye. Something pushed through here.",
                "Low branches, broken and bent. A trail of sorts.",
                "You notice disturbed undergrowth. Worth a closer look."
            ),
            "stone_pile" => SelectRandom(
                "Loose stones gathered at the base of an outcrop.",
                "A pile of rocks. Might find something useful.",
                "Stones, naturally collected by erosion. Could be handy."
            ),
            "old_campfire" => SelectRandom(
                "The remains of an old fire ring. Long cold.",
                "A fire pit, abandoned. Someone camped here before.",
                "You find evidence of an old campfire."
            ),
            "hollow_tree" => SelectRandom(
                "A hollow tree catches your eye. Dry material inside.",
                "Dead wood with a hollow center. Good for tinder.",
                "You spot a hollow tree - might be worth checking."
            ),
            "scattered_bones" => SelectRandom(
                "Bones, picked clean by scavengers. An old kill site.",
                "You find scattered bones. Something died here.",
                "Weathered bones in the snow. Still useful."
            ),
            "dry_grass" => SelectRandom(
                "A tussock of dry grass, brown and brittle.",
                "Dead grass poking through the snow. Good fiber.",
                "You notice a clump of dry grass nearby."
            ),
            "animal_burrow" => SelectRandom(
                "A small burrow entrance. Something lives down there.",
                "You spot a hole in the ground - an animal den.",
                "A burrow. Fresh digging at the entrance."
            ),
            "frozen_puddle" or "forest_puddle" => SelectRandom(
                "A small frozen puddle. The ice looks thin.",
                "You find a puddle of water, frozen over.",
                "Ice covers a small pool. Drinkable water, perhaps."
            ),
            "old_nest" => SelectRandom(
                "An abandoned nest, woven from grass and twigs.",
                "You find an old bird's nest. Useful materials.",
                "A nest from last season. Dry nesting material."
            ),
            _ => $"You notice something: {d.DisplayName.ToLower()}."
        };
    }

    // todo - move to HarvestableFeature class
    private static string GetHarvestableDiscoveryMessage(HarvestableFeature h)
    {
        var name = h.DisplayName.ToLower();

        // Select variant based on feature name/type
        return h.Name switch
        {
            "berry_bush" => SelectRandom(
                $"You notice a {name} growing in a sheltered spot.",
                $"Berries! A {name} with fruit still clinging to the branches.",
                $"A {name} you almost walked past. Worth remembering."
            ),
            "deadfall" or "massive_deadfall" => SelectRandom(
                $"You find a {name}. Dry wood, ready to use.",
                $"A {name} - nature's kindling pile.",
                $"Wind-felled timber. This {name} could fuel a fire for days."
            ),
            "flint_outcrop" => SelectRandom(
                "Sharp flint nodules catch the light. A knapping site.",
                "Flint! The rock here breaks clean and sharp.",
                "Quality stone for tools. This spot is worth remembering."
            ),
            "pyrite_seam" => SelectRandom(
                "Golden glints in the rock - iron pyrite. Sparks well against flint.",
                "Fire-stone. A seam of pyrite runs through the rock here.",
                "You find pyrite - 'fool's gold' that strikes true sparks."
            ),
            "bone_pile" => SelectRandom(
                "Old bones scattered across the ground. Still useful.",
                "Something died here long ago. The bones remain.",
                "Weathered bone, picked clean by time and scavengers."
            ),
            "cattails" => SelectRandom(
                "Cattails! Good for fiber and tinder both.",
                "A stand of cattails. Useful plants, if you know how.",
                "Cattail reeds - their fluff catches sparks easily."
            ),
            _ => h.Description != "" ? $"You find {name}. {h.Description}" : $"You discover {name}."
        };
    }

    private static string GetShelterDiscoveryMessage(ShelterFeature s)
    {
        var name = s.Name.ToLower();

        return s.ShelterType switch
        {
            ShelterType.NaturalShelter or ShelterType.RockOverhang => SelectRandom(
                $"A natural overhang - this {name} could block the wind.",
                $"You find a {name}. Not much, but it's something.",
                $"Shelter! A {name} that would keep the worst of the weather off."
            ),
            _ => $"You find shelter: {name}."
        };
    }

    private static string SelectRandom(params string[] options)
    {
        return options[Random.Shared.Next(options.Length)];
    }
}
