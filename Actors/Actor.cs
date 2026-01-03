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

    public double GetMovementFactor()
    {
        var capacities = GetCapacities();

        // Base time multiplier from moving capacity
        // Moving 1.0 → 1.0x time (normal)
        // Moving 0.5 → 2.0x time (half speed = double time)
        // Moving 0.1 → 10.0x time (barely moving)
        double movingMultiplier = Math.Max(0.1, capacities.Moving); // Floor to prevent infinity
        double timeMultiplier = 1.0 / movingMultiplier;

        // Breathing impairment adds +10% time
        // Labored breathing slows pace
        if (AbilityCalculator.IsBreathingImpaired(capacities.Breathing))
        {
            timeMultiplier *= 1.10;
        }

        // Blood pumping impairment adds +25% time
        if (AbilityCalculator.IsBloodPumpingImpaired(capacities.BloodPumping))
        {
            timeMultiplier *= 1.25;
        }

        return timeMultiplier;
    }

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


