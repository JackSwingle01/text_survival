using text_survival.Bodies;
using text_survival.Core;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;
using text_survival.Skills;
using text_survival.Survival;

namespace text_survival.Actors.Player;

public class Player : Actor
{
    public readonly CampManager Camp;
    private readonly LocationManager locationManager;
    public readonly InventoryManager inventoryManager;
    public readonly StealthManager stealthManager;
    public readonly AmmunitionManager ammunitionManager;
    public readonly HuntingManager huntingManager;
    public readonly SkillRegistry Skills;
    public override void Update(int minutes)
    {
        var context = GetSurvivalContext();

        var result = SurvivalProcessor.Process(Body, context, minutes);

        result.Effects.ForEach(EffectRegistry.AddEffect);
        result.Messages.ForEach(AddLog);

        EffectRegistry.Update(minutes);
        result.StatsDelta.Combine(EffectRegistry.GetSurvivalDelta());
        result.DamageEvents.AddRange(EffectRegistry.GetDamagesPerMinute());
        Output.WriteLine($"Debug temp delta: {result.StatsDelta.TemperatureDelta}");
        Body.ApplyResult(result);
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
        stealthManager = new(this);
        ammunitionManager = new(inventoryManager);
        huntingManager = new(this, ammunitionManager);
        Skills = new SkillRegistry();
        Camp = new CampManager(startingLocation);
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

    public void Travel() => locationManager.TravelToAdjacentZone();

    /// <summary>Gets the world map for map UI display</summary>
    public WorldMap GetWorldMap() => locationManager.GetWorldMap();

    /// <summary>Travel to a location within the current zone using directional navigation</summary>
    public void TravelToLocalLocation(string direction) => locationManager.TravelToLocalLocation(direction);

    /// <summary>Travel to an adjacent zone in the specified direction</summary>
    public void TravelToAdjacentZone(string direction)
    {
        // Calculate travel time
        int minutes = UI.MapController.CalculateZoneTravelTime();
        AddLog($"You travel {direction.ToLower()} for {minutes} minutes...");

        // Move to the zone in the chosen direction
        switch (direction)
        {
            case "N":
                locationManager.CurrentZone = locationManager.GetWorldMap().North;
                break;
            case "E":
                locationManager.CurrentZone = locationManager.GetWorldMap().East;
                break;
            case "S":
                locationManager.CurrentZone = locationManager.GetWorldMap().South;
                break;
            case "W":
                locationManager.CurrentZone = locationManager.GetWorldMap().West;
                break;
        }

        World.Update(minutes);
    }


}
