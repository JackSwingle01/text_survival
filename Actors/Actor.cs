using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;
using text_survival.Level;
using text_survival.PlayerComponents;

namespace text_survival.Actors;

public abstract class Actor
{
    public string Name = "";
    public virtual void Attack(Actor target) => combatManager.Attack(target);

    public virtual void Damage(DamageInfo damage) => survivalManager.Damage(damage);
    public virtual void Heal(HealingInfo heal) => survivalManager.Heal(heal);

    public bool IsEngaged { get; set; }
    public bool IsAlive => !Body.IsDestroyed;
    public double ConditionPercent => survivalManager.ConditionPercent;
    public abstract Weapon ActiveWeapon { get; protected set;}

    public virtual void ApplyEffect(Effect effect) => _effectRegistry.AddEffect(effect);
    public virtual void RemoveEffect(Effect effect) => _effectRegistry.RemoveEffect(effect);
    public virtual List<Effect> GetEffectsByKind(string kind) => _effectRegistry.GetEffectsByKind(kind);
    public virtual void Update()
    {
        survivalManager.Update();
        _effectRegistry.Update();
    }

    public SkillRegistry _skillRegistry { get; init; }
    public Body Body { get; init; }
    protected EffectRegistry _effectRegistry { get; init; }
    protected SurvivalManager survivalManager { get; init; }
    protected CombatManager combatManager { get; init; }

    public override string ToString() => Name;

    protected Actor(BodyStats stats)
    {
        _effectRegistry = new EffectRegistry(this);
        _skillRegistry = new SkillRegistry();
        this.survivalManager = new SurvivalManager(this, _effectRegistry);
        this.combatManager = new CombatManager(this);
        Body = new Body(stats, _effectRegistry);
    }
}


