using text_survival.Bodies;
using text_survival.Effects;
using text_survival.UI;

namespace text_survival.Actions.Events;

/// <summary>
/// Discrete stages for survival stat thresholds.
/// </summary>
public enum ThresholdStage
{
    Healthy = 0,    // > 50%
    Normal = 1,     // 25-50%
    Severe = 2,     // 10-25%
    Critical = 3    // < 10%
}

/// <summary>
/// Records a survival stat threshold crossing.
/// </summary>
public record ThresholdChange(
    string StatName,
    ThresholdStage? Previous,
    ThresholdStage Current
)
{
    public bool IsWorsening => Previous != null && Current > Previous;
    public bool IsImproving => Previous != null && Current < Previous;
    public bool IsInitial => Previous == null;
}

/// <summary>
/// Warns the player when a survival stat enters a crisis zone. There is nothing to decide
/// at the moment of crossing - the decision is what to do next - so these are log
/// warnings, not events that stop play.
/// </summary>
public static class ThresholdWarnings
{
    public static void Apply(ThresholdChange change, GameContext ctx)
    {
        // Only warn when entering worse stages (not improving), and only for Severe and Critical
        if (!change.IsWorsening)
            return;

        string? warning = (change.StatName, change.Current) switch
        {
            ("Energy", ThresholdStage.Severe) =>
                "Your legs are heavy. Your thoughts sluggish. You need rest soon.",
            ("Energy", ThresholdStage.Critical) =>
                "You can barely keep your eyes open. You're making mistakes. Dangerous mistakes. Rest now.",
            ("Calories", ThresholdStage.Severe) =>
                "Your stomach cramps. Your hands tremble. Your body is starting to eat itself.",
            ("Calories", ThresholdStage.Critical) =>
                "The world narrows. Without food soon, you won't have the strength to find any.",
            ("Hydration", ThresholdStage.Severe) =>
                "Your mouth is dry, your lips cracked. Dehydration kills faster than hunger.",
            ("Hydration", ThresholdStage.Critical) =>
                "Your head pounds. The world spins when you stand. Water is life or death now.",
            _ => null
        };
        if (warning == null)
            return;

        GameDisplay.AddWarning(ctx, warning);

        // Hunger and thirst already scale their own effects; exhaustion's crash is extra.
        if (change.StatName == "Energy" && change.Current == ThresholdStage.Critical)
            ctx.player.EffectRegistry.AddEffect(EffectFactory.Exhausted(0.4, 120));
    }
}
