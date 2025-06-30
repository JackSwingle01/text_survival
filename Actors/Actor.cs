using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Items;
using text_survival.PlayerComponents;

namespace text_survival.Actors;

public abstract class Actor
{
    public string Name;
    public virtual Location? CurrentLocation { get; set; }
    public virtual void Attack(Actor target, IBodyPart? bodyPart = null) => combatManager.Attack(target, bodyPart);

    public virtual void Damage(DamageInfo damage) => Body.Damage(damage);
    public virtual void Heal(HealingInfo heal) => Body.Heal(heal);

    public bool IsEngaged { get; set; }
    public bool IsAlive => !Body.IsDestroyed;
    public abstract Weapon ActiveWeapon { get; protected set; }

    public virtual void Update()
    {
        EffectRegistry.Update();
        var context = new SurvivalContext
        {
            ActivityLevel = 2,
            LocationTemperature = CurrentLocation.GetTemperature(),
        };
        Body.Update(TimeSpan.FromMinutes(1), context);
    }
    public Body Body { get; init; }
    public EffectRegistry EffectRegistry { get; init; }
    protected CombatManager combatManager { get; init; }

    public override string ToString() => Name;

    protected Actor(string name, BodyStats stats)
    {
        Name = name;
        EffectRegistry = new EffectRegistry(this);
        this.combatManager = new CombatManager(this);
        Body = new Body(Name, stats, EffectRegistry);
    }
}


