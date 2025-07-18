using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;
using text_survival.Level;
using text_survival.Magic;
using text_survival.PlayerComponents;


namespace text_survival;

public class Player : Actor
{
    private readonly LocationManager locationManager;
    public readonly InventoryManager inventoryManager;
    public readonly SkillRegistry Skills;
    public readonly List<Spell> _spells = [SpellFactory.Bleeding, SpellFactory.Poison, SpellFactory.MinorHeal];

    public override void Update()
    {
        EffectRegistry.Update();
        var context = GetSurvivalContext();
        Body.Update(TimeSpan.FromMinutes(1), context);
    }

    public SurvivalContext GetSurvivalContext() => new SurvivalContext
    {
        ActivityLevel = 2,
        LocationTemperature = locationManager.CurrentLocation.GetTemperature(),
        ClothingInsulation = inventoryManager.ClothingInsulation,
    };

    public override Weapon ActiveWeapon
    {
        get => inventoryManager.Weapon; protected set
        {
            inventoryManager.Weapon = value;
        }
    }

    // Location-related methods
    public override Location CurrentLocation
    {
        get => locationManager.CurrentLocation;
        set => locationManager.CurrentLocation = value;
    }

    public Zone CurrentZone => locationManager.CurrentZone;

    public Player(Location startingLocation) : base("Player", Body.BaselinePlayerStats)
    {
        Name = "Player";
        locationManager = new LocationManager(startingLocation);
        inventoryManager = new(EffectRegistry);
        Skills = new SkillRegistry();
    }

    public void DropItem(Item item)
    {
        inventoryManager.RemoveFromInventory(item);
        locationManager.AddItemToLocation(item);
    }

    public void TakeItem(Item item)
    {
        locationManager.RemoveItemFromLocation(item);

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
            string eating_type = food.WaterContent > food.Calories ? "drink" : "eat";
            Output.Write($"You {eating_type} the ", food, "...");
            Body.Consume(food);
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
            Output.WriteLine("You equip the ", gear);
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
                Output.WriteLine("You use the ", weaponMod, " to modify your ", inventoryManager.Weapon);
            }
            else
            {
                Output.WriteLine("You don't have a weapon equipped to modify.");
                return;
            }
        }
        else if (item is ArmorModifierItem armorMod)
        {
            if (ModifyArmor(armorMod.ValidArmorTypes[0], armorMod.Rating, armorMod.Warmth))
            {
                Output.WriteLine("You use the ", armorMod, " to modify your armor.");
            }
            else
            {
                Output.WriteLine("You don't have any armor you can use that on.");
                return;
            }
        }
        else
        {
            Output.Write("You don't know what to use the ", item, " for...\n");
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

    public void Travel() => locationManager.TravelToAdjacentZone();
}
