using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for megafauna hunting (mammoth, saber-tooth).
/// Multi-stage process: scout → track → approach, staged by the hunt tension's severity.
/// Presence comes from the megafauna herd itself; when the herd is dead, the options are gone.
/// </summary>
public class MegafaunaStrategy(AnimalType megafauna) : IWorkStrategy
{
    private readonly AnimalType _megafauna = megafauna;

    public static string TensionNameFor(AnimalType type) => type switch
    {
        AnimalType.Mammoth => "MammothTracked",
        AnimalType.SaberTooth => "SaberToothStalked",
        _ => throw new ArgumentException($"{type} is not huntable megafauna")
    };

    /// <summary>
    /// Megafauna types with a living herd near the player: in whose territory the player stands,
    /// or (for the roaming mammoth herd) within calling distance.
    /// </summary>
    public static IEnumerable<AnimalType> MegafaunaNear(GameContext ctx)
    {
        if (AnimalPresence.OfTypeNear(ctx, AnimalType.SaberTooth))
            yield return AnimalType.SaberTooth;
        if (Situations.NearMammothHerd(ctx) || AnimalPresence.OfTypeNear(ctx, AnimalType.Mammoth))
            yield return AnimalType.Mammoth;
    }

    /// <summary>
    /// Work options for every megafauna herd near the player, gated by hunt progress.
    /// </summary>
    public static IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        foreach (var type in MegafaunaNear(ctx))
        {
            string name = type.DisplayName().ToLower();
            double severity = ctx.Tensions.GetTension(TensionNameFor(type))?.Severity ?? 0.0;

            if (severity < 0.5)
                yield return new WorkOption($"Scout for {name} signs", $"scout_{name}", new MegafaunaStrategy(type));
            if (severity >= 0.3 && severity < 0.6)
                yield return new WorkOption($"Track the {name}", $"track_{name}", new MegafaunaStrategy(type));
            if (severity >= 0.6)
                yield return new WorkOption("Approach for confrontation", $"approach_{name}", new MegafaunaStrategy(type));
        }
    }

    private string HuntStage(GameContext ctx)
    {
        double severity = ctx.Tensions.GetTension(TensionNameFor(_megafauna))?.Severity ?? 0.0;
        if (severity < 0.3) return "scout";
        if (severity < 0.6) return "track";
        return "approach";
    }

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        if (!MegafaunaNear(ctx).Contains(_megafauna))
            return $"There's no sign of {_megafauna.DisplayName().ToLower()} here.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var choice = new Choice<int>("How long do you want to work?");

        switch (HuntStage(ctx))
        {
            case "scout":
                choice.AddOption("Quick scouting - 15 min", 15);
                choice.AddOption("Thorough scouting - 30 min", 30);
                break;
            case "track":
                choice.AddOption("Track for a while - 45 min", 45);
                choice.AddOption("Extended tracking - 60 min", 60);
                break;
            case "approach":
                choice.AddOption("Cautious approach - 60 min", 60);
                choice.AddOption("Commit fully - 90 min", 90);
                break;
        }

        choice.AddOption("Cancel", 0);
        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkBreathing: false,
            effectRegistry: ctx.player.EffectRegistry
        );

        var perception = AbilityCalculator.CalculatePerception(ctx.player.Body, effectModifiers);
        if (AbilityCalculator.IsPerceptionImpaired(perception))
            warnings.Add("Your dulled senses make tracking difficult.");

        if (AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
            warnings.Add("Your unfocused mind struggles to read the signs.");

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Hunting;

    public string GetActivityName() => "tracking megafauna";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        string name = _megafauna.DisplayName().ToLower();

        if (ctx.Tensions.GetTension(TensionNameFor(_megafauna)) != null)
        {
            GameDisplay.AddNarrative(ctx, $"You're already tracking the {name}. The tension hangs heavy.");
            return WorkResult.Empty(actualTime);
        }

        GameDisplay.AddNarrative(ctx, $"You search for signs of the {name}...");

        // 40% chance per session to find the sign that starts the hunt arc
        if (Random.Shared.NextDouble() < 0.4)
        {
            var discoveryEvent = _megafauna switch
            {
                AnimalType.SaberTooth => GameEventRegistry.AncientPredator(ctx),
                _ => Random.Shared.NextDouble() < 0.5
                    ? GameEventRegistry.DistantTrumpeting(ctx)
                    : GameEventRegistry.TheHerd(ctx)
            };
            ctx.EventQueue.Enqueue(discoveryEvent);
            return WorkResult.Empty(actualTime);
        }

        GameDisplay.AddNarrative(ctx, "You find nothing conclusive. Old tracks, maybe.");
        return WorkResult.Empty(actualTime);
    }
}
