using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;
using text_survival.Skills;
using text_survival.Survival;

namespace text_survival.Actors.Player;

public class Player : Actor
{
    public readonly StealthManager stealthManager;
    public readonly HuntingManager huntingManager;
    public readonly SkillRegistry Skills;

    // Last survival delta and duration for UI trend display
    public SurvivalStatsDelta? LastSurvivalDelta { get; private set; }
    public int LastUpdateMinutes { get; private set; } = 1;

    public override void Update(int minutes, SurvivalContext context)
    {
        var result = SurvivalProcessor.Process(Body, context, minutes);

        result.Effects.ForEach(EffectRegistry.AddEffect);
        result.Messages.ForEach(AddLog);

        EffectRegistry.Update(minutes);
        result.StatsDelta.Combine(EffectRegistry.GetSurvivalDelta());
        result.DamageEvents.AddRange(EffectRegistry.GetDamagesPerMinute());

        // Store delta and duration for UI trend display
        LastSurvivalDelta = result.StatsDelta;
        LastUpdateMinutes = minutes;

        Body.ApplyResult(result);
    }

    // Fallback weapon - actual weapon comes from ctx.Inventory.Weapon via Attack() override
    public override Weapon ActiveWeapon
    {
        get => Tool.Unarmed.ToWeapon();
        protected set { }  // No-op - weapons live in Inventory now
    }

    public Player() : base("Player", Body.BaselinePlayerStats)
    {
        Name = "Player";
        stealthManager = new(this);
        huntingManager = new(this);
        Skills = new SkillRegistry();
    }
}
