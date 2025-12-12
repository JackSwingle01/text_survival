using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Actors;

public abstract class Actor
{
    public string Name;
    public virtual Location? CurrentLocation { get; set; }
    public virtual void Attack(Actor target, string? bodyPart = null) => combatManager.Attack(target, bodyPart);

    public bool IsEngaged { get; set; }
    public bool IsAlive => !Body.IsDestroyed;
    public abstract Weapon ActiveWeapon { get; protected set; }

    public virtual void Update()
    {

        if (CurrentLocation == null)
        {
            // Actor not in a valid location, skip survival update
            return;
        }

        var context = new SurvivalContext
        {
            ActivityLevel = 2,
            LocationTemperature = CurrentLocation.GetTemperature(),
        };
        var result = SurvivalProcessor.Process(Body, context, 1);

        EffectRegistry.Update();
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

    protected Actor(string name, BodyCreationInfo stats)
    {
        Name = name;
        EffectRegistry = new EffectRegistry(this);
        this.combatManager = new CombatManager(this);
        Body = new Body(Name, stats);
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

    public double Strength => AbilityCalculator.CalculateStrength(Body, GetEffectModifiers());
    public double Speed => AbilityCalculator.CalculateSpeed(Body, GetEffectModifiers());
    public double Vitality => AbilityCalculator.CalculateVitality(Body, GetEffectModifiers());
    public double Perception => AbilityCalculator.CalculatePerception(Body, GetEffectModifiers());
    public double ColdResistance => AbilityCalculator.CalculateColdResistance(Body);
}


