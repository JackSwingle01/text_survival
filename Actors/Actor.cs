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
        double factor = Speed;
        var capacities = GetCapacities();

        // Breathing impairment adds +10% time for all journeys
        // Labored breathing slows pace
        if (AbilityCalculator.IsBreathingImpaired(capacities.Breathing))
        {
            factor *= .9;
        }
        if (AbilityCalculator.IsBloodPumpingImpaired(capacities.BloodPumping))
        {
            factor *= .8;
        }
        return factor;
    }

    public double Strength => AbilityCalculator.CalculateStrength(Body, GetEffectModifiers());
    public double Speed => AbilityCalculator.CalculateSpeed(Body, GetEffectModifiers());
    public double Vitality => AbilityCalculator.CalculateVitality(Body, GetEffectModifiers());
    public double Perception => AbilityCalculator.CalculatePerception(Body, GetEffectModifiers());
    public double ColdResistance => AbilityCalculator.CalculateColdResistance(Body);


}


