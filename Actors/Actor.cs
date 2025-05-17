using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Level;
using text_survival.PlayerComponents;

namespace text_survival.Actors;

public abstract class Actor
{
    public string Name = "";

    public abstract Location CurrentLocation { get; set; }
    public abstract Zone CurrentZone { get; set; }
    public virtual void Attack(Actor target) => combatManager.Attack(target);

    public virtual void Damage(DamageInfo damage) => survivalManager.Damage(damage);
    public virtual void Heal(HealingInfo heal) => survivalManager.Heal(heal);

    public bool IsEngaged { get; set; }
    public bool IsAlive => survivalManager.IsAlive;
    public double ConditionPercent => survivalManager.ConditionPercent;
    public Weapon ActiveWeapon => inventoryManager.Weapon;
    public bool IsArmed => inventoryManager.IsArmed;
    public bool IsArmored => inventoryManager.IsArmored;
    public double EquipmentWarmth => inventoryManager.EquipmentWarmth;

    public virtual void ApplyEffect(Effect effect) => _effectRegistry.AddEffect(effect);
    public virtual void RemoveEffect(Effect effect) => _effectRegistry.RemoveEffect(effect);

    public virtual void Update()
    {
        survivalManager.Update();
        _effectRegistry.Update();
    }

    public SkillRegistry _skillRegistry { get; init; }
    public Body Body { get; init; }
    protected EffectRegistry _effectRegistry { get; init; }
    protected SurvivalManager survivalManager { get; init; }
    protected InventoryManager inventoryManager { get; init; }
    protected CombatManager combatManager { get; init; }

    public override string ToString() => Name;

    // protected Actor(EffectRegistry effectRegistry,
    //               SkillRegistry skillRegistry,
    //               SurvivalManager survivalManager,
    //               InventoryManager inventoryManager,
    //               CombatManager combatManager,
    //               Body body)
    // {
    //     _effectRegistry = effectRegistry;
    //     _skillRegistry = skillRegistry;
    //     this.survivalManager = survivalManager;
    //     this.inventoryManager = inventoryManager;
    //     this.combatManager = combatManager;
    //     Body = body;
    // }
}


