using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Survival;

namespace text_survival.Actors;

public abstract class Actor : IMovable
{
    public string Name;
    public Location CurrentLocation { get; set; }
    public GameMap Map { get; set; }

    // Combat interface - subclasses provide these from their weapon/natural attacks
    public abstract double AttackDamage { get; }
    public abstract double BlockChance { get; }
    public abstract string AttackName { get; }
    public abstract DamageType AttackType { get; }

    public bool IsEngaged { get; set; }
    public bool IsAlive => Vitality > 0;

    public virtual void Update(int minutes, SurvivalContext context)
    {
        var result = SurvivalProcessor.Process(Body, context, minutes);

        // 2. Add effects from survival (hypothermia, etc)
        foreach (var effect in result.Effects)
        {
            // Wet and Bloody effects can both increase and decrease, so use SetEffectSeverity
            _ = (effect.EffectKind == "Wet" || effect.EffectKind == "Bloody")
                ? EffectRegistry.SetEffectSeverity(effect)
                : EffectRegistry.AddEffect(effect);
        }

        _ = EffectRegistry.Update(minutes);

        var delta = EffectRegistry.GetSurvivalDelta();
        var damages = EffectRegistry.GetDamagesPerMinute();

        result.StatsDelta.Combine(delta);
        result.DamageEvents.AddRange(damages);

        Body.ApplyResult(result);
    }

    public Body Body { get; init; }
    public EffectRegistry EffectRegistry { get; init; }
    protected CombatManager combatManager { get; init; }

    public override string ToString() => Name;

    protected Actor(string name, BodyCreationInfo stats, Location currentLocation, GameMap map)
    {
        Name = name;
        EffectRegistry = new EffectRegistry();
        this.combatManager = new CombatManager(this);
        Body = new Body(stats);
        CurrentLocation = currentLocation;
        Map = map;
    }

    private readonly List<string> _messageLog = [];
    public void AddLog(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _messageLog.Add(message);
        }
    }
    public List<string> GetFlushLogs()
    {
        var messages = _messageLog.ToList();
        _messageLog.Clear();
        return messages;
    }

    public CapacityModifierContainer GetEffectModifiers() => EffectRegistry.GetCapacityModifiers();

    public CapacityContainer GetCapacities() => CapacityCalculator.GetCapacities(Body, GetEffectModifiers());

    #region Abilities

    // Non-context abilities (no environmental factors)
    public double Vitality => AbilityCalculator.CalculateVitality(Body, GetEffectModifiers());
    public double Strength => AbilityCalculator.CalculateStrength(Body, GetEffectModifiers());
    public double ColdResistance => AbilityCalculator.CalculateColdResistance(Body);

    // Context-aware abilities - use these when you have AbilityContext
    public double GetSpeed(AbilityContext context)
        => AbilityCalculator.CalculateSpeed(Body, GetEffectModifiers(), context);

    public double GetPerception(AbilityContext context)
        => AbilityCalculator.CalculatePerception(Body, GetEffectModifiers(), context);

    public double GetDexterity(AbilityContext context)
        => AbilityCalculator.CalculateDexterity(Body, GetEffectModifiers(), context);

    // Properties for backward compatibility (use Default context)
    public double Speed => GetSpeed(AbilityContext.Default);
    public double Perception => GetPerception(AbilityContext.Default);
    public double Dexterity => GetDexterity(AbilityContext.Default);

    #endregion


}


