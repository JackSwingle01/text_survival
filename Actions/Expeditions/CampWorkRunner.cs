using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Handles work done at camp without leaving - foraging, scouting nearby areas
/// </summary>
public class CampWorkRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;

    public void Run()
    {
        var campLocation = _ctx.Camp.Location;

        var choice = new Choice<string>("What do you want to do?");

        if (campLocation.HasFeature<ForageFeature>())
        {
            var forage = campLocation.GetFeature<ForageFeature>()!;
            choice.AddOption($"Forage nearby ({forage.GetQualityDescription()})", "forage");
        }

        if (_ctx.Zone.HasUnrevealedLocations())
            choice.AddOption("Scout the area (discover new locations)", "scout");

        choice.AddOption("Cancel", "cancel");

        string action = choice.GetPlayerChoice();

        switch (action)
        {
            case "forage":
                DoForage();
                break;
            case "scout":
                DoScout();
                break;
        }
    }

    private void DoForage()
    {
        GameDisplay.Render(_ctx);
        var timeChoice = new Choice<int>("How long should you forage?");
        timeChoice.AddOption("Quick gather - 15 min", 15);
        timeChoice.AddOption("Standard search - 30 min", 30);
        timeChoice.AddOption("Thorough search - 60 min", 60);
        int workTime = timeChoice.GetPlayerChoice();

        GameDisplay.AddNarrative($"You search around camp for resources...");

        // Time passage with working activity (1.5) and moderate fire proximity (0.5)
        _ctx.Update(workTime, 1.5, 0.5);

        var feature = _ctx.Camp.Location.GetFeature<ForageFeature>()!;
        var found = feature.Forage(workTime / 60.0);
        _ctx.Inventory.Add(found);

        string quality = feature.GetQualityDescription();
        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative(GetForageFailureMessage(quality));
        }
        else
        {
            foreach (var desc in found.Descriptions)
            {
                GameDisplay.AddNarrative($"You found {desc}");
            }
            if (quality == "sparse" || quality == "picked over")
                GameDisplay.AddNarrative("Resources here are getting scarce.");
        }

        GameDisplay.Render(_ctx);
        Input.WaitForKey();
    }

    private void DoScout()
    {
        if (!_ctx.Zone.HasUnrevealedLocations())
        {
            GameDisplay.AddNarrative("You've already discovered all nearby areas.");
            return;
        }

        var location = _ctx.Camp.Location;
        double successChance = CalculateExploreChance(location);

        GameDisplay.Render(_ctx);
        var timeChoice = new Choice<int>($"How far should you scout? ({successChance:P0} chance to find something)");
        timeChoice.AddOption("Quick scout - 15 min", 15);
        timeChoice.AddOption("Standard scout - 30 min (+10%)", 30);
        timeChoice.AddOption("Thorough scout - 60 min (+20%)", 60);
        int scoutTime = timeChoice.GetPlayerChoice();

        // Longer scouting improves chances
        double timeBonus = scoutTime switch
        {
            30 => 0.10,
            60 => 0.20,
            _ => 0.0
        };
        double finalChance = Math.Min(0.95, successChance + timeBonus);

        GameDisplay.AddNarrative("You head out to scout the surrounding area...");

        // Scouting takes you away from fire (proximity = 0)
        _ctx.Update(scoutTime, 1.5, 0.0);

        // Roll for success
        if (Utils.RandDouble(0, 1) <= finalChance)
        {
            var newLocation = _ctx.Zone.RevealRandomLocation(location);

            if (newLocation != null)
            {
                GameDisplay.AddSuccess($"From higher ground, you spot something: {newLocation.Name}");
                if (!string.IsNullOrEmpty(newLocation.Description))
                    GameDisplay.AddNarrative($"It looks like {newLocation.Description.ToLower()}");
            }
            else
            {
                GameDisplay.AddNarrative("You scouted around but found no new paths.");
            }
        }
        else
        {
            GameDisplay.AddNarrative("You searched the area but couldn't find any new paths.");
        }

        GameDisplay.Render(_ctx);
        Input.WaitForKey();
    }

    /// <summary>
    /// Calculate chance to discover a new location.
    /// Decreases exponentially with existing connections.
    /// </summary>
    private static double CalculateExploreChance(Location location)
    {
        int connections = location.Connections.Count;
        double baseChance = 0.90;
        double decayFactor = 0.55; // Each connection multiplies chance by this

        return baseChance * Math.Pow(decayFactor, connections);
    }

    private static string GetForageFailureMessage(string quality)
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
}
