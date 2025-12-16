using text_survival.Effects;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.Actors.Player;

public class InventoryManager
{
    public InventoryManager(EffectRegistry effectRegistry)
    {
        Armor = [];
        _unarmed = ItemFactory.MakeFists();
        Inventory = new Container("Bag", 10);
        _effectRegistry = effectRegistry;
    }
    private Container Inventory { get; }
    public List<ItemStack> Items => ItemStack.CreateStacksFromItems(Inventory.Items);
    public List<Armor> Armor { get; }
    public Gear? HeldItem { get; private set; }
    private EffectRegistry _effectRegistry { get; }
    // weapon
    private Weapon? _weapon;
    private readonly Weapon _unarmed;
    public bool IsArmed => Weapon != _unarmed;
    public bool IsArmored => Armor.Count != 0;

    public Armor? GetArmorInSpot(EquipSpots spot) => Armor.FirstOrDefault(i => i.EquipSpot == spot);
    public Weapon Weapon
    {
        get => _weapon ?? _unarmed;
        set => _weapon = value;
    }

    public void AddToInventory(Item item)
    {
        Output.WriteLine("You put the ", item, " in your ", Inventory);
        Inventory.Add(item);
    }

    public void RemoveFromInventory(Item item)
    {
        Output.WriteLine("You take the ", item, " from your ", Inventory);
        Inventory.Remove(item);
    }

    public double ArmorProtection
    {
        get
        {
            double rating = 0;
            foreach (Armor armor in Armor)
            {
                rating += armor.Rating;
            }
            return rating;
        }
    }

    public double ClothingInsulation
    {
        get
        {
            return Armor.Sum(a => a.Insulation);
        }
    }

    public void Equip(IEquippable item)
    {
        switch (item)
        {
            case Weapon weapon:
                Unequip(Weapon);
                Weapon = weapon;
                break;
            case Armor armor:
                var oldItem = Armor.FirstOrDefault(i => i.EquipSpot == armor.EquipSpot);
                if (oldItem != null) Unequip(oldItem);
                Armor.Add(armor);
                break;
            case Gear gear:
                if (HeldItem != null) Unequip(HeldItem);
                HeldItem = gear;
                break;
            default:
                Output.WriteLine("You can't equip that.");
                return;
        }
        Inventory.Remove((Item)item);
        item.EquipEffects.ForEach(_effectRegistry.AddEffect);
    }

    public bool CanAutoEquip(IEquippable item)
    {

        if (item is Weapon weapon && !IsArmed)
        {
            Output.WriteLine($"You equip the {weapon}");
            return true;
        }
        else if (item is Armor armor)
        {
            var existingItem = Armor.FirstOrDefault(i => i.EquipSpot == armor.EquipSpot);
            if (existingItem == null)
            {
                Output.WriteLine($"You equip the {armor}");
                return true;
            }
        }
        else if (item is Gear gear && HeldItem == null)
        {
            Output.WriteLine($"You didn't have anything in your hand yet so you equip the {item}");
            return true;
        }
        return false;
    }

    public void Unequip(IEquippable item)
    {
        if (item is not Gear gear) return;
        if (item == _unarmed) return;

        switch (gear)
        {
            case Weapon weapon:
                Weapon = _unarmed;
                break;
            case Armor armor:
                Armor.Remove(armor);
                break;
            case Gear g:
                HeldItem = null;
                break;
            default:
                Output.WriteLine("You can't unequip that.");
                return;
        }
        Output.WriteLine("You unequip ", gear);
        Inventory.Add(gear);
        gear.EquipEffects.ForEach(_effectRegistry.RemoveEffect);
    }
    public void CheckGear()
    {
        DescribeGear(this);
        Output.WriteLine("Would you like to unequip an item?");
        if (Input.ReadYesNo()) return;

        Output.WriteLine("Which item would you like to unequip?");
        // get list of all equipment
        var equipment = new List<IEquippable>();
        equipment.AddRange(Armor);
        if (IsArmed) equipment.Add(Weapon);
        if (HeldItem != null) equipment.Add(HeldItem);

        var choice = Input.GetSelectionFromList(equipment, true);
        if (choice == null) return;
        Unequip(choice);
    }

    public static void DescribeGear(InventoryManager inv)
    {
        if (inv.IsArmed)
        {
            Output.Write("Weapon => ");
            inv.Weapon.Describe();
        }
        foreach (Armor armor in inv.Armor)
        {
            Output.Write(armor.EquipSpot, " => ");
            armor.Describe();
        }
        if (inv.HeldItem is not null)
        {
            Output.Write("Held Item => ");
            inv.HeldItem.Describe();
        }
    }
    public void Describe()
    {
        Output.WriteLine(Inventory, " (", Inventory.Weight(), "/", Inventory.MaxWeight, "):");
    }

    internal double GetEncumbrance()
    {
        return 0;
    }
}
