using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Effects;

public class BleedEffect : Effect
{
    public BleedEffect(float damagePerHour, int durationMin) : base("Bleed", durationMin, severity: 1.0f)
    {
        DamagePerHour = damagePerHour;
    }


    public override void Apply(IActor target)
    {
        Output.WriteLine(target, " is bleeding!");
        IsActive = true;
    }


    public override void Update(IActor target)
    {
        if (!IsActive) return;

        double damage = DamagePerHour * Severity;
        target.Damage(damage);
        Output.WriteLine($"{target} takes {damage:F2} damage from bleeding.");

        RemainingDurationMin -= 1;
        if (RemainingDurationMin <= 0)
        {
            Remove(target);
        }
    }


    public override void Remove(IActor target)
    {
        if (!IsActive) return;
        IsActive = false;
        Output.WriteLine(target, " stopped bleeding.");
    }


    private float DamagePerHour;
}
