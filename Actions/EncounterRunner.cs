using text_survival.Actors.Animals;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Result of a predator encounter, used by callers to handle cleanup.
/// </summary>
public enum EncounterOutcome
{
    PredatorRetreated,  // Boldness dropped below 0.3, predator left
    PlayerEscaped,      // Player outran or distracted predator
    CombatVictory,      // Player killed predator
    PlayerDied          // Player was killed
}

public static class EncounterRunner
{
    /// <summary>
    /// Handles a predator encounter. Returns outcome for caller to handle cleanup.
    /// Does NOT call StealthManager - caller is responsible for that.
    /// Expedition is nullable for reuse from events/travel.
    /// </summary>
    public static EncounterOutcome HandlePredatorEncounter(Animal predator, GameContext ctx)
    {
        // Initialize boldness from observable context
        predator.EncounterBoldness = predator.CalculateBoldness(ctx.player, ctx.Inventory);

        while (predator.IsAlive && ctx.player.IsAlive)
        {
            // Display current state
            string boldnessDesc = predator.EncounterBoldness >= 0.7 ? "aggressive"
                : predator.EncounterBoldness > 0.3 ? "wary" : "hesitant";

            GameDisplay.AddNarrative($"\nThe {predator.Name} is {predator.DistanceFromPlayer:F0}m away, looking {boldnessDesc}.");

            // Show observable factors
            bool hasMeat = ctx.Inventory.HasMeat;
            if (hasMeat)
                GameDisplay.AddNarrative("It's eyeing the meat you're carrying.");
            if (ctx.player.Vitality < 0.7)
                GameDisplay.AddNarrative("It seems to sense your weakness.");

            GameDisplay.Render(ctx, statusText: "Alert.");

            // Player options
            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Stand your ground", "stand");
            choice.AddOption("Back away slowly", "back");
            choice.AddOption("Run", "run");
            if (predator.DistanceFromPlayer <= 20)
                choice.AddOption("Fight", "fight");
            if (hasMeat)
                choice.AddOption("Drop the meat", "drop_meat");

            string action = choice.GetPlayerChoice();

            switch (action)
            {
                case "stand":
                    GameDisplay.AddNarrative("You hold your position, facing the predator.");
                    predator.DistanceFromPlayer -= 10; // Predator closes
                    predator.EncounterBoldness -= 0.10; // But loses confidence

                    if (predator.EncounterBoldness < 0.3)
                    {
                        GameDisplay.AddNarrative($"The {predator.Name} hesitates... then slinks away.");
                        return EncounterOutcome.PredatorRetreated;
                    }
                    GameDisplay.AddNarrative($"The {predator.Name} moves closer, but seems less certain.");
                    break;

                case "back":
                    GameDisplay.AddNarrative("You slowly back away, keeping eyes on the predator.");
                    predator.DistanceFromPlayer += 5; // You gain distance
                    predator.EncounterBoldness += 0.05; // But it gets bolder
                    GameDisplay.AddNarrative($"Distance: {predator.DistanceFromPlayer:F0}m");
                    break;

                case "run":
                    var (escaped, narrative) = HuntingCalculator.CalculatePursuitOutcome(
                        ctx.player, predator, predator.DistanceFromPlayer);
                    GameDisplay.AddNarrative(narrative);
                    if (escaped)
                    {
                        return EncounterOutcome.PlayerEscaped;
                    }
                    // Caught — forced combat
                    predator.DistanceFromPlayer = 5;
                    return RunPredatorCombat(predator, ctx);

                case "fight":
                    return RunPredatorCombat(predator, ctx);

                case "drop_meat":
                    double meatDropped = ctx.Inventory.DropAllMeat();
                    GameDisplay.AddNarrative($"You drop {meatDropped:F1}kg of meat and back away.");
                    GameDisplay.AddNarrative($"The {predator.Name} goes for the meat. You slip away.");
                    return EncounterOutcome.PlayerEscaped;
            }

            // Boldness ceiling: very bold predator closes regardless of player action
            if (predator.EncounterBoldness >= 0.7)
            {
                GameDisplay.AddNarrative($"The {predator.Name} grows impatient and closes in.");
                predator.DistanceFromPlayer -= 10;
            }

            // Check if predator reaches attack range
            if (predator.DistanceFromPlayer <= 5)
            {
                GameDisplay.AddNarrative($"The {predator.Name} charges!");
                return RunPredatorCombat(predator, ctx);
            }

            ctx.Update(1, ActivityType.Encounter); // 1 minute per turn, no events during encounter
            Input.WaitForKey();
        }

        return ctx.player.IsAlive ? EncounterOutcome.PredatorRetreated : EncounterOutcome.PlayerDied;
    }

    private static EncounterOutcome RunPredatorCombat(Animal predator, GameContext ctx)
    {
        GameDisplay.AddNarrative($"Combat with {predator.Name}!");

        while (predator.IsAlive && ctx.player.IsAlive)
        {
            GameDisplay.AddNarrative($"\nYou: {ctx.player.Vitality:P0} | {predator.Name}: {predator.Vitality:P0}");
            GameDisplay.Render(ctx, statusText: "Fighting.");

            var choice = new Choice<string>("Your move:");
            choice.AddOption("Attack", "attack");
            choice.GetPlayerChoice();

            // Player attacks with equipped weapon
            ctx.player.Attack(predator, ctx.Inventory.Weapon);
            ctx.Update(1, ActivityType.Fighting); // No events during combat

            if (!predator.IsAlive) break;

            // Predator attacks
            predator.Attack(ctx.player);
        }

        if (!ctx.player.IsAlive)
        {
            return EncounterOutcome.PlayerDied;
        }

        // Victory
        GameDisplay.AddNarrative($"The {predator.Name} falls!");

        var butcherChoice = new Choice<bool>("Butcher the carcass?");
        butcherChoice.AddOption("Yes", true);
        butcherChoice.AddOption("No", false);

        if (butcherChoice.GetPlayerChoice())
        {
            var loot = ButcherRunner.ButcherAnimal(predator, ctx);
            ctx.Inventory.Add(loot);
            GameDisplay.AddNarrative($"You collect {loot.TotalWeightKg:F1}kg of resources.");

            // Butchering can be interrupted by events
            var result = ctx.Update(10, ActivityType.Hunting);
        }

        Input.WaitForKey();
        return EncounterOutcome.CombatVictory;
    }


    /// <summary>
    /// Creates an animal from an EncounterConfig, setting up initial distance and boldness.
    /// </summary>
    public static Animal? CreateAnimalFromConfig(EncounterConfig config)
    {
        var animal = config.AnimalType.ToLower() switch
        {
            "wolf" => AnimalFactory.MakeWolf(),
            "bear" => AnimalFactory.MakeBear(),
            "cave bear" => AnimalFactory.MakeCaveBear(),
            "fox" => AnimalFactory.MakeFox(),
            _ => null
        };

        if (animal != null)
        {
            animal.DistanceFromPlayer = config.InitialDistance;
            animal.EncounterBoldness = config.InitialBoldness;
        }

        return animal;
    }
}