using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;
using text_survival.Skills;
using text_survival.Survival;

namespace text_survival.Actors.Player;

public class Player : Actor
{
    public readonly InventoryManager inventoryManager;
    public readonly StealthManager stealthManager;
    public readonly AmmunitionManager ammunitionManager;
    public readonly HuntingManager huntingManager;
    public readonly SkillRegistry Skills;
    public override void Update(int minutes, SurvivalContext context)
    {
        var result = SurvivalProcessor.Process(Body, context, minutes);

        result.Effects.ForEach(EffectRegistry.AddEffect);
        result.Messages.ForEach(AddLog);

        EffectRegistry.Update(minutes);
        result.StatsDelta.Combine(EffectRegistry.GetSurvivalDelta());
        result.DamageEvents.AddRange(EffectRegistry.GetDamagesPerMinute());
        Output.WriteLine($"Debug temp delta: {result.StatsDelta.TemperatureDelta}");
        Body.ApplyResult(result);
    }

    public SurvivalContext GetSurvivalContext(Location currentLocation) => new SurvivalContext
    {
        ActivityLevel = 1.5, // default - todo update
        LocationTemperature = currentLocation.GetTemperature(),
        ClothingInsulation = inventoryManager.ClothingInsulation,
    };

    public override Weapon ActiveWeapon
    {
        get => inventoryManager.Weapon; protected set
        {
            inventoryManager.Weapon = value;
        }
    }

    public Player() : base("Player", Body.BaselinePlayerStats)
    {
        Name = "Player";
        inventoryManager = new(EffectRegistry);
        stealthManager = new(this);
        ammunitionManager = new(inventoryManager);
        huntingManager = new(this, ammunitionManager);
        Skills = new SkillRegistry();
    }

    public void DropItem(Item item)
    {
        inventoryManager.RemoveFromInventory(item);
    }

    public void TakeItem(Item item)
    {
        // QOL - auto equip gear if you can
        if (item is IEquippable equipment && inventoryManager.CanAutoEquip(equipment))
        {
            inventoryManager.Equip(equipment);
            return;
        }
        inventoryManager.AddToInventory(item);
    }

    public void UseItem(Item item)
    {
        // handle special logic for each item type
        if (item is FoodItem food)
        {
            // Remove from inventory first so message order is correct
            inventoryManager.RemoveFromInventory(food);
            string eating_type = food.WaterContent > food.Calories ? "drink" : "eat";
            Output.Write($"You {eating_type} the ", food, "...");
            Body.Consume(food);
            return; // Early return since we already removed from inventory
        }
        else if (item is ConsumableItem consumable)
        {
            foreach (Effect e in consumable.Effects)
            {
                EffectRegistry.AddEffect(e);
            }
        }
        else if (item is Gear gear)
        {
            AddLog($"You equip the {gear}");
            inventoryManager.Equip(gear);
            foreach (Effect effect in gear.EquipEffects)
            {
                EffectRegistry.AddEffect(effect);
            }
        }

        else if (item is WeaponModifierItem weaponMod)
        {
            if (ModifyWeapon(weaponMod.Damage))
            {
                AddLog($"You use the {weaponMod} to modify your {inventoryManager.Weapon}");
            }
            else
            {
                AddLog("You don't have a weapon equipped to modify.");
                return;
            }
        }
        else if (item is ArmorModifierItem armorMod)
        {
            if (ModifyArmor(armorMod.ValidArmorTypes[0], armorMod.Rating, armorMod.Warmth))
            {
                AddLog($"You use the {armorMod} to modify your armor.");
            }
            else
            {
                AddLog("You don't have any armor you can use that on.");
                return;
            }
        }
        else
        {
            AddLog($"You don't know what to use the {item} for...");
            return;
        }
        // shared logic for all item types
        if (item.NumUses != -1)
        {
            item.NumUses -= 1;
            if (item.NumUses == 0)
            {
                inventoryManager.RemoveFromInventory(item);
            }
        }
    }

    public bool ModifyWeapon(double damage)
    {
        if (!inventoryManager.IsArmed) return false;

        inventoryManager.Weapon.Damage += damage;
        return true;
    }
    public bool ModifyArmor(EquipSpots spot, double rating = 0, double warmth = 0)
    {
        Armor? armor = inventoryManager.GetArmorInSpot(spot);
        if (armor is null) return false;

        armor.Rating += rating;
        armor.Insulation += warmth;
        return true;
    }
}
