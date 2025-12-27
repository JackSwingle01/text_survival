using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for megafauna hunting (mammoth, cave bear, saber-tooth).
/// Multi-stage process: scout → track → approach.
/// Each stage creates/escalates hunt tensions and triggers appropriate events.
/// </summary>
public class MegafaunaStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var megafaunaFeature = location.GetFeature<MegafaunaPresenceFeature>();
        if (megafaunaFeature == null)
            return "There's no sign of megafauna here.";

        // Check respawn status
        if (megafaunaFeature.LastEncounterTime.HasValue)
        {
            var hoursSince = (DateTime.Now - megafaunaFeature.LastEncounterTime.Value).TotalHours;
            if (hoursSince < megafaunaFeature.RespawnHours)
            {
                var daysRemaining = (int)Math.Ceiling((megafaunaFeature.RespawnHours - hoursSince) / 24.0);
                return $"The {megafaunaFeature.MegafaunaType.ToLower()} haven't returned yet. (About {daysRemaining} days)";
            }
        }

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var megafaunaFeature = location.GetFeature<MegafaunaPresenceFeature>()!;
        string huntStage = megafaunaFeature.GetHuntStage(ctx);

        var choice = new Choice<int>("How long do you want to work?");

        switch (huntStage)
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

        // Megafauna tracking requires movement, perception, and focus
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,       // Need to track and follow
            checkBreathing: false,   // Not physically demanding
            effectRegistry: ctx.player.EffectRegistry
        );

        // Check perception impairment
        var perception = AbilityCalculator.CalculatePerception(
            ctx.player.Body, effectModifiers);
        if (AbilityCalculator.IsPerceptionImpaired(perception))
        {
            warnings.Add("Your dulled senses make tracking difficult.");
        }

        // Check consciousness impairment
        if (AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
        {
            warnings.Add("Your unfocused mind struggles to read the signs.");
        }

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Hunting;

    public string GetActivityName() => "tracking megafauna";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var megafaunaFeature = location.GetFeature<MegafaunaPresenceFeature>()!;
        string huntStage = megafaunaFeature.GetHuntStage(ctx);

        GameDisplay.AddNarrative(ctx, $"You search for signs of the {megafaunaFeature.MegafaunaType.ToLower()}...");

        // Events will handle the actual hunt progression
        // The work strategy just marks that we're working on megafauna hunting
        // Event system will check hunt stage and trigger appropriate events during time passage

        // Return standard work result - events will trigger during time passage in WorkRunner
        return WorkResult.Empty(actualTime);
    }
}
