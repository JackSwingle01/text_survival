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

    // Combat defaults (unarmed) - actual weapon passed to Attack()
    public override double AttackDamage => 2;
    public override double BlockChance => 0.01;
    public override string AttackName => "fists";
    public override DamageType AttackType => DamageType.Blunt;

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
            if (EffectRegistry.AddEffect(effect) is string msg) messages.Add(msg);
        messages.AddRange(result.Messages);

        // 3. Tick existing effects
        messages.AddRange(EffectRegistry.Update(minutes));

        // 4. Combine effect contributions
        result.StatsDelta.Combine(EffectRegistry.GetSurvivalDelta());
        result.DamageEvents.AddRange(EffectRegistry.GetDamagesPerMinute());

        return (result, messages);
    }

    public Player() : base("Player", Body.BaselinePlayerStats)
    {
        Name = "Player";
        stealthManager = new(this);
        huntingManager = new(this);
        Skills = new SkillRegistry();
    }
}
