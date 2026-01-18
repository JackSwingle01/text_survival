using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;
using text_survival.Skills;
using text_survival.Survival;

namespace text_survival.Actors.Player;

public class Player : Actor
{
    public readonly SkillRegistry Skills;

    // Last survival delta and duration for UI trend display
    public SurvivalStatsDelta? LastSurvivalDelta { get; private set; }
    public int LastUpdateMinutes { get; private set; } = 1;

    // Combat - uses equipped weapon or unarmed defaults
    public override double AttackDamage => Inventory?.Weapon?.Damage ?? 0.15;
    public override double BlockChance => Inventory?.Weapon?.BlockChance ?? 0.05;
    public override string AttackName => Inventory?.Weapon?.Name ?? "fists";
    public override DamageType AttackType => Inventory?.Weapon?.WeaponClass switch
    {
        WeaponClass.Blade => DamageType.Sharp,
        WeaponClass.Pierce => DamageType.Pierce,
        _ => DamageType.Blunt
    };

    public override DamageInfo GetAttackDamage(BodyTarget target = BodyTarget.Random)
    {
        var damageInfo = base.GetAttackDamage(target);
        damageInfo.Amount += Skills.Fighting.Level;
        return damageInfo;
    }

    public override void Update(int minutes, SurvivalContext context)
    {
        var (result, messages) = ProcessSurvivalTick(minutes, context);
        messages.ForEach(AddLog);

        LastSurvivalDelta = result.StatsDelta;
        LastUpdateMinutes = minutes;

        Body.ApplyResult(result);
    }

    private (SurvivalProcessorResult, List<string>) ProcessSurvivalTick(int minutes, SurvivalContext context)
    {
        var messages = new List<string>();

        // 1. Calculate base survival
        var result = SurvivalProcessor.Process(Body, context, minutes);

        // 2. Add effects from survival (hypothermia, etc)
        foreach (var effect in result.Effects)
        {
            // Wet and Bloody effects can both increase and decrease, so use SetEffectSeverity
            string? msg = (effect.EffectKind == "Wet" || effect.EffectKind == "Bloody")
                ? EffectRegistry.SetEffectSeverity(effect)
                : EffectRegistry.AddEffect(effect);
            if (msg != null) messages.Add(msg);
        }
        messages.AddRange(result.Messages);

        // 3. Tick existing effects
        messages.AddRange(EffectRegistry.Update(minutes));

        // 4. Combine effect contributions
        result.StatsDelta.Combine(EffectRegistry.GetSurvivalDelta());
        result.DamageEvents.AddRange(EffectRegistry.GetDamagesPerMinute());

        return (result, messages);
    }

    public Player() : base("Player", Body.BaselinePlayerStats, null!, null!)
    {
        Name = "Player";
        Skills = new SkillRegistry();
        Inventory = Inventory.CreatePlayerInventory(15.0);
    }
}
