using System.Text.Json.Serialization;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Survival;

namespace text_survival.Actors;

public abstract class Actor : IMovable
{
    public string Name = "";

    [JsonIgnore]
    public Location CurrentLocation { get; set; } = null!;

    [JsonIgnore]
    public GameMap Map { get; set; } = null!;

    // Inventory - set by subclasses that have one (Player, NPC). Animals leave null.
    public Inventory? Inventory { get; set; }

    // Combat interface - subclasses provide these from their weapon/natural attacks
    public abstract double AttackDamage { get; set; }
    public abstract double BlockChance { get; set; }
    public abstract string AttackName { get; set; }
    public abstract DamageType AttackType { get; set; }

    /// <summary>
    /// Creates DamageInfo for this actor's attack, including strength/vitality/RNG modifiers.
    /// </summary>
    public virtual DamageInfo GetAttackDamage(BodyTarget target = BodyTarget.Random)
    {
        double baseDamage = AttackDamage;
        double strengthMod = (Strength / 2) + 0.5;      // 50-100%
        double vitalityMod = 0.7 + (0.3 * Vitality);    // 70-100%
        double rngMod = 0.5 + Random.Shared.NextDouble(); // 50-150%

        double damage = baseDamage * strengthMod * vitalityMod * rngMod;
        return new DamageInfo(damage, AttackType, target);
    }

    /// <summary>
    /// Applies damage to this actor, including armor if available.
    /// </summary>
    public DamageResult Damage(DamageInfo damageInfo)
    {
        if (Inventory != null)
        {
            damageInfo.ArmorCushioning = Inventory.TotalCushioning;
            damageInfo.ArmorToughness = Inventory.TotalToughness;
        }

        var result = DamageProcessor.DamageBody(damageInfo, Body);
        foreach (var effect in result.TriggeredEffects)
        {
            EffectRegistry.AddEffect(effect);
        }
        return result;
    }

    // Combat stats - animals override with set values, NPCs/players use defaults
    public virtual double BaseThreat { get; set; } = 1.0;
    public virtual double StartingBoldness { get; set; } = 1.0;
    public virtual double BaseAggression { get; set; } = 1.0;
    public virtual double BaseCohesion { get; set; } = 0.5;

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

    public override string ToString() => Name;

    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// Location and Map must be restored after load via RestoreReferences().
    /// </summary>
    [JsonConstructor]
    protected Actor()
    {
        EffectRegistry = new EffectRegistry();
        Body = new Body();
    }

    protected Actor(string name, BodyCreationInfo stats, Location currentLocation, GameMap map)
    {
        Name = name;
        EffectRegistry = new EffectRegistry();
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

    // Context-aware abilities - use these when you have AbilityContext
    public double GetSpeed(AbilityContext context)
        => AbilityCalculator.CalculateSpeed(Body, GetEffectModifiers(), context);

    public double GetPerception(AbilityContext context)
        => AbilityCalculator.CalculatePerception(Body, GetEffectModifiers(), context);

    public double GetDexterity(AbilityContext context)
        => AbilityCalculator.CalculateDexterity(Body, GetEffectModifiers(), context);

    // Properties for backward compatibility (use Default context)
    public double Speed => GetSpeed(AbilityContext.Default);
    public double Dexterity => GetDexterity(AbilityContext.Default);

    #endregion


}


