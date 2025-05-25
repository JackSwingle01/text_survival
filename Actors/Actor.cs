using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Level;
using text_survival.PlayerComponents;

namespace text_survival.Actors;

public abstract class Actor
{
    public string Name;
    public virtual Location? CurrentLocation { get; set; }
    public virtual void Attack(Actor target, string? bodyPart = null) => combatManager.Attack(target, bodyPart);

    public virtual BodyPart? Damage(DamageInfo damage) => Body.Damage(damage);
    public virtual void Heal(HealingInfo heal) => Body.Heal(heal);

    public bool IsEngaged { get; set; }
    public bool IsAlive => !Body.IsDestroyed;
    public abstract Weapon ActiveWeapon { get; protected set; }

    public virtual void ApplyEffect(Effect effect) => _effectRegistry.AddEffect(effect);
    public virtual void RemoveEffect(Effect effect) => _effectRegistry.RemoveEffect(effect);
    public virtual List<Effect> GetEffectsByKind(string kind) => _effectRegistry.GetEffectsByKind(kind);
    public virtual void Update()
    {
        _effectRegistry.Update();
        var context = new SurvivalContext
        {
            ActivityLevel = 2,
            LocationTemperature = CurrentLocation.GetTemperature(),
        };
        Body.Update(TimeSpan.FromMinutes(1), context);
    }

    public SkillRegistry _skillRegistry { get; init; }
    public Body Body { get; init; }
    protected EffectRegistry _effectRegistry { get; init; }
    protected CombatManager combatManager { get; init; }

    public override string ToString() => Name;

    protected Actor(string name, BodyStats stats)
    {
        Name = name;
        _effectRegistry = new EffectRegistry(this);
        _skillRegistry = new SkillRegistry();
        this.combatManager = new CombatManager(this);
        Body = new Body(Name, stats, _effectRegistry);
    }
}


