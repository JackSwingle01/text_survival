using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;
using text_survival.PlayerComponents;


namespace text_survival;

public class Player : Actor
{

    private readonly LocationManager locationManager;
    private readonly SpellManager spellManager;
    private readonly InventoryManager inventoryManager;
    public double EquipmentWarmth => inventoryManager.EquipmentWarmth;
    public void Sleep(int minutes)
    {
        bool fullyRested = Body.Rest(1);
        if (fullyRested)
        {
            Output.Write("You wake up feeling refreshed.\n");
        }

    }
    public void OpenInventory() => inventoryManager.Open(this);
    public override Weapon ActiveWeapon
    {
        get => inventoryManager.Weapon; protected set
        {
            inventoryManager.Weapon = value;
        }
    }

    // Location-related methods
    public Location CurrentLocation
    {
        get => locationManager.CurrentLocation;
        set => locationManager.CurrentLocation = value;
    }

    public Zone CurrentZone => locationManager.CurrentZone;

    #region Constructor

    public Player(Location startingLocation) : base("Player", Body.BaseLineHumanStats)
    {
        Name = "Player";
        locationManager = new LocationManager(startingLocation);
        spellManager = new(_skillRegistry);
        inventoryManager = new(_effectRegistry);
    }


    #endregion Constructor

    public void DropItem(Item item)
    {
        inventoryManager.RemoveFromInventory(item);
        Output.WriteLine("You drop the ", item);
        locationManager.AddItemToLocation(item);
    }

    public void TakeItem(Item item)
    {
        locationManager.RemoveItemFromLocation(item);
        Output.WriteLine("You take the ", item);
        inventoryManager.AddToInventory(item);
    }

    public void SelectSpell()
    {
        List<Actor> targets = [this];
        CurrentLocation.Npcs.ForEach(targets.Add);
        spellManager.SelectSpell(targets);
    }



    public override BodyPart? Damage(DamageInfo damageInfo)
    {
        var part = Body.Damage(damageInfo);
        if (!IsAlive)
        {
            // end program
            Output.WriteDanger("You died!");
            Environment.Exit(0);
        }
        return part;
    }

    public void DescribeSurvivalStats()
    {
        Body.DescribeSurvivalStats();
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
                ApplyEffect(e);
            }
        }
        else if (item is Gear gear)
        {
            Output.WriteLine("You equip the ", gear);
            inventoryManager.Equip(gear);
            foreach (Effect effect in gear.EquipEffects)
            {
                ApplyEffect(effect);
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
        World.Update(1);
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
        armor.Warmth += warmth;
        return true;
    }

    public void Travel() => locationManager.TravelToAdjacentZone();
}




public class SurvivalStatsUpdate
{
    public double Temperature;
    public double Calories;
    public double Hydration;
    public double Exhaustion;
}