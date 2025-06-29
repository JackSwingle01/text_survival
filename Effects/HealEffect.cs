using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
namespace text_survival.Effects;
public class HealEffect : Effect
{
    private float _healAmountPerHour;
    private string _targetPartName;

    public HealEffect(string source, MajorBodyPart? targetPart, float healAmount, int durationMin = 60)
        : base("Healing", source, targetPart, 1.0f)
    {
        _healAmountPerHour = healAmount;
        _targetPartName = targetPart?.Name ?? "body";

        // Configure effect properties
        CanHaveMultiple = false; // Healing doesn't stack
        SeverityChangeRate = -1.0f / durationMin; // Decreases to 0 over duration
    }

    protected override void OnApply(Actor target)
    {
        string location = _targetPartName;
        Output.WriteLine($"{Source} begins healing {target}'s {location}.");
    }

    protected override void OnUpdate(Actor target)
    {
        // Calculate healing for one minute
        float healAmount = _healAmountPerHour / 60.0f;

        // Apply healing
        var healInfo = new HealingInfo
        {
            Amount = healAmount,
            Quality = 1.0,
            Source = Source,
            TargetOrgan = _targetPartName
        };

        target.Heal(healInfo);

        // Visual effect based on severity (how much healing is left)
        if (Utils.DetermineSuccess(0.05f))
        {
            string location = _targetPartName;
            if (Severity > 0.7f)
            {
                Output.WriteLine($"Fresh healing is occurring in {target}'s {location}.");
            }
            else if (Severity > 0.3f)
            {
                Output.WriteLine($"The healing in {target}'s {location} continues steadily.");
            }
            else
            {
                Output.WriteLine($"The healing in {target}'s {location} is nearly complete.");
            }
        }
    }

    protected override void OnRemove(Actor target)
    {
        string location = _targetPartName;
        Output.WriteLine($"The healing process on {target}'s {location} is complete.");
    }
}