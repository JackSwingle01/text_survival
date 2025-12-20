using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Unified work execution for all locations (camp or expedition).
/// Returns WorkResult - caller handles logging and expedition tracking.
/// </summary>
public class WorkRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private bool PlayerDied => !_ctx.player.IsAlive;

    public WorkResult DoForage(Location location)
    {
        var feature = location.GetFeature<ForageFeature>();
        if (feature == null)
        {
            GameDisplay.AddNarrative("There's nothing to forage here.");
            return WorkResult.Empty(0);
        }

        GameDisplay.Render(_ctx);
        var workTimeChoice = new Choice<int>("How long should you forage?");
        workTimeChoice.AddOption("Quick gather - 15 min", 15);
        workTimeChoice.AddOption("Standard search - 30 min", 30);
        workTimeChoice.AddOption("Thorough search - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice();

        GameDisplay.AddNarrative("You search the area for resources...");

        bool died = RunWorkWithProgress(location, workTime, "Foraging");
        if (died) return WorkResult.Died(workTime);

        var found = feature.Forage(workTime / 60.0);
        _ctx.Inventory.Add(found);

        var collected = new List<string>();
        string quality = feature.GetQualityDescription();

        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative(GetForageFailureMessage(quality));
        }
        else
        {
            GameDisplay.AddNarrative("You found:");
            var grouped = found.Descriptions.GroupBy(d => d).Select(g => (g.Key, g.Count()));
            foreach (var (desc, count) in grouped)
            {
                string line = count > 1 ? $"{desc} ({count})" : desc;
                GameDisplay.AddNarrative($"  - {line}");
            }
            collected.AddRange(found.Descriptions);
            if (quality == "sparse" || quality == "picked over")
                GameDisplay.AddNarrative("Resources here are getting scarce.");
        }

        GameDisplay.Render(_ctx);
        Input.WaitForKey();

        // Check weight limit and force drop if needed
        ForceDropIfOverweight();

        return new WorkResult(collected, null, workTime, false);
    }

    public WorkResult DoHarvest(Location location)
    {
        var harvestables = location.Features
            .OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources())
            .ToList();

        if (harvestables.Count == 0)
        {
            GameDisplay.AddNarrative("There's nothing to harvest here.");
            return WorkResult.Empty(0);
        }

        HarvestableFeature target;
        if (harvestables.Count == 1)
        {
            target = harvestables[0];
        }
        else
        {
            GameDisplay.Render(_ctx);
            var harvestChoice = new Choice<HarvestableFeature>("What do you want to harvest?");
            foreach (var h in harvestables)
            {
                harvestChoice.AddOption($"{h.DisplayName} - {h.GetStatusDescription()}", h);
            }
            target = harvestChoice.GetPlayerChoice();
        }

        GameDisplay.Render(_ctx);
        var workTimeChoice = new Choice<int>($"How long should you harvest {target.DisplayName}?");
        workTimeChoice.AddOption("Quick work - 15 min", 15);
        workTimeChoice.AddOption("Standard work - 30 min", 30);
        workTimeChoice.AddOption("Thorough work - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice();

        bool died = RunWorkWithProgress(location, workTime, "Harvesting");
        if (died) return WorkResult.Died(workTime);

        var found = target.Harvest(workTime);
        _ctx.Inventory.Add(found);

        var collected = new List<string>();

        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative("You didn't get anything.");
        }
        else
        {
            foreach (var desc in found.Descriptions)
            {
                GameDisplay.AddNarrative($"You harvested {desc}");
                collected.Add(desc);
            }
        }

        GameDisplay.AddNarrative($"{target.DisplayName}: {target.GetStatusDescription()}");
        GameDisplay.Render(_ctx);
        Input.WaitForKey();

        // Check weight limit and force drop if needed
        ForceDropIfOverweight();

        return new WorkResult(collected, null, workTime, false);
    }

    public WorkResult DoExplore(Location location)
    {
        if (!_ctx.Zone.HasUnrevealedLocations())
        {
            GameDisplay.AddNarrative("You've explored everything reachable from here.");
            return WorkResult.Empty(0);
        }

        double successChance = CalculateExploreChance(location);

        GameDisplay.Render(_ctx);
        var timeChoice = new Choice<int>($"How thoroughly should you scout? ({successChance:P0} chance to find something)");
        timeChoice.AddOption("Quick scout - 15 min", 15);
        timeChoice.AddOption("Standard scout - 30 min (+10%)", 30);
        timeChoice.AddOption("Thorough scout - 60 min (+20%)", 60);
        int exploreTime = timeChoice.GetPlayerChoice();

        double timeBonus = exploreTime switch
        {
            30 => 0.10,
            60 => 0.20,
            _ => 0.0
        };
        double finalChance = Math.Min(0.95, successChance + timeBonus);

        GameDisplay.AddNarrative("You scout the area, looking for new paths...");

        // Scouting takes you away from fire - use 0.0 proximity regardless of location
        bool died = RunWorkWithProgress(location, exploreTime, "Exploring", fireProximityOverride: 0.0);
        if (died) return WorkResult.Died(exploreTime);

        Location? discovered = null;

        if (Utils.RandDouble(0, 1) <= finalChance)
        {
            var newLocation = _ctx.Zone.RevealRandomLocation(location);

            if (newLocation != null)
            {
                GameDisplay.AddSuccess($"You discovered a new area: {newLocation.Name}!");
                if (!string.IsNullOrEmpty(newLocation.Description))
                    GameDisplay.AddNarrative(newLocation.Description);
                discovered = newLocation;
            }
            else
            {
                GameDisplay.AddNarrative("You scouted the area but found no new paths.");
            }
        }
        else
        {
            GameDisplay.AddNarrative("You searched the area but couldn't find any new paths.");
        }

        GameDisplay.Render(_ctx);
        Input.WaitForKey();

        return new WorkResult([], discovered, exploreTime, false);
    }

    // === PROGRESS AND TIME PASSAGE ===

    /// <summary>
    /// Runs work with progress bar and event checks.
    /// Returns true if player died during work.
    /// </summary>
    private bool RunWorkWithProgress(Location location, int workMinutes, string workType, double? fireProximityOverride = null)
    {
        int elapsed = 0;
        GameEvent? triggeredEvent = null;
        bool died = false;

        double fireProximity = fireProximityOverride ?? GetFireProximity(location);

        while (elapsed < workMinutes && !died)
        {
            Output.Progress($"{workType} at {location.Name}...", workMinutes, task =>
            {
                task?.Increment(elapsed);

                while (elapsed < workMinutes && triggeredEvent == null && !died)
                {
                    var tickResult = GameEventRegistry.RunTicks(_ctx, 1);
                    _ctx.Update(tickResult.MinutesElapsed, 1.5, fireProximity);
                    elapsed += tickResult.MinutesElapsed;
                    task?.Increment(tickResult.MinutesElapsed);

                    if (PlayerDied)
                    {
                        died = true;
                        break;
                    }

                    if (tickResult.TriggeredEvent != null)
                        triggeredEvent = tickResult.TriggeredEvent;

                    Thread.Sleep(100);
                }
            });

            if (died) break;

            if (triggeredEvent != null)
            {
                GameEventRegistry.HandleEvent(_ctx, triggeredEvent);
                triggeredEvent = null;
            }
        }

        return died;
    }

    // === WORK OPTIONS (used by GameRunner and ExpeditionRunner) ===

    /// <summary>
    /// Check if any work is available at a location.
    /// </summary>
    public static bool HasWorkOptions(GameContext ctx, Location location, bool includeHunt = false)
    {
        if (location.HasFeature<ForageFeature>())
            return true;

        var harvestables = location.Features
            .OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources());
        if (harvestables.Any())
            return true;

        if (includeHunt && location.HasFeature<AnimalTerritoryFeature>())
            return true;

        if (ctx.Zone.HasUnrevealedLocations())
            return true;

        return false;
    }

    /// <summary>
    /// Get work options menu for a location. Returns null if no options available.
    /// </summary>
    public static Choice<string>? GetWorkOptions(GameContext ctx, Location location, bool includeHunt = false)
    {
        var choice = new Choice<string>("What work do you want to do?");
        bool hasOptions = false;

        if (location.HasFeature<ForageFeature>())
        {
            var forage = location.GetFeature<ForageFeature>()!;
            choice.AddOption($"Forage for resources ({forage.GetQualityDescription()})", "forage");
            hasOptions = true;
        }

        var harvestables = location.Features
            .OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources());
        if (harvestables.Any())
        {
            choice.AddOption("Harvest resources", "harvest");
            hasOptions = true;
        }

        if (includeHunt && location.HasFeature<AnimalTerritoryFeature>())
        {
            var territory = location.GetFeature<AnimalTerritoryFeature>()!;
            choice.AddOption($"Hunt ({territory.GetQualityDescription()})", "hunt");
            hasOptions = true;
        }

        if (ctx.Zone.HasUnrevealedLocations())
        {
            choice.AddOption("Explore the area (discover new locations)", "explore");
            hasOptions = true;
        }

        choice.AddOption("Cancel", "cancel");
        return hasOptions ? choice : null;
    }

    /// <summary>
    /// Get labels for available work types (for menu display).
    /// </summary>
    public static List<string> GetWorkLabels(GameContext ctx, Location location)
    {
        var labels = new List<string>();

        if (location.HasFeature<ForageFeature>())
            labels.Add("Forage");

        if (ctx.Zone.HasUnrevealedLocations())
            labels.Add("Scout");

        return labels;
    }

    // === HELPERS ===

    /// <summary>
    /// Get fire proximity based on whether location has an active fire or embers.
    /// </summary>
    private static double GetFireProximity(Location location)
    {
        var fire = location.GetFeature<HeatSourceFeature>();
        return fire switch
        {
            { IsActive: true } => 0.5,
            { HasEmbers: true } => 0.25,
            _ => 0.0
        };
    }

    /// <summary>
    /// Calculate chance to discover a new location.
    /// Decreases exponentially with existing connections.
    /// </summary>
    public static double CalculateExploreChance(Location location)
    {
        int connections = location.Connections.Count;
        double baseChance = 0.90;
        double decayFactor = 0.55;
        return baseChance * Math.Pow(decayFactor, connections);
    }

    public static string GetForageFailureMessage(string quality)
    {
        string[] messages = quality switch
        {
            "abundant" => [
                "You find plenty, but it's all frozen solid or rotted through. The area is rich - just not this haul.",
                "Fresh snow buries everything. You dig, but there's more here than you had time to uncover.",
                "A rich area, but everything usable is just out of reach. A longer search would help.",
                "You find things, but they crumble apart - frozen and brittle. Plenty more here though.",
                "Ice coats everything. Resources are visible beneath but locked away. The area is clearly bountiful."
            ],
            "decent" => [
                "You find a few scraps, but nothing worth keeping. The area still has potential.",
                "You turn up a few things, but nothing quite usable. There's more here with patience.",
                "Resources here take more effort to find. A more thorough search might turn something up.",
                "You turn up some possibilities, but nothing usable. More thorough searching might help.",
                "A modest area. You didn't find much this time, but it's not exhausted."
            ],
            "sparse" => [
                "Slim pickings. Most of what was here has already been taken.",
                "You find traces of what this place once offered. It's nearly spent.",
                "Hardly anything left. You'd need luck to find something useful here.",
                "The area is almost picked clean. Time to look elsewhere.",
                "Scraps and remnants. This place won't sustain you much longer."
            ],
            _ => [
                "Nothing. This place has been stripped bare.",
                "You search thoroughly and find nothing. Whatever was here is gone.",
                "Completely exhausted. You're wasting time here.",
                "Barren. Not a single useful thing remains.",
                "Empty. There's nothing left to find."
            ]
        };

        return messages[Random.Shared.Next(messages.Length)];
    }

    /// <summary>
    /// Check if player is over carry capacity and force them to drop items.
    /// </summary>
    private void ForceDropIfOverweight()
    {
        var inv = _ctx.Inventory;

        if (inv.RemainingCapacityKg >= 0)
            return;

        GameDisplay.ClearNarrative();
        GameDisplay.AddWarning($"You're carrying too much! ({inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0} kg)");
        GameDisplay.AddNarrative("You must drop some items.");
        GameDisplay.Render(_ctx);
        Input.WaitForKey();

        // Create a dummy "drop target" that just discards items
        var dropTarget = new Items.Inventory { MaxWeightKg = -1 };

        while (inv.RemainingCapacityKg < 0)
        {
            var items = inv.GetTransferableItems(dropTarget);
            if (items.Count == 0)
                break;

            var options = items.Select(i => $"{i.Description}").ToList();

            GameDisplay.ClearNarrative();
            GameDisplay.AddWarning($"Over capacity by {-inv.RemainingCapacityKg:F1} kg. Drop something.");
            GameDisplay.Render(_ctx);

            string selected = Input.Select("Drop which item?", options);
            int idx = options.IndexOf(selected);

            items[idx].TransferTo();
            GameDisplay.AddNarrative($"Dropped {items[idx].Description}");
        }

        GameDisplay.AddNarrative("You adjust your load and continue.");
        GameDisplay.Render(_ctx);
        Input.WaitForKey();
    }
}
