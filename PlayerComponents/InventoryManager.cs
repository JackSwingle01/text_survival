using text_survival.IO;
using text_survival.Items;

namespace text_survival.PlayerComponents;

public class InventoryManager
{
    public InventoryManager()
    {
        Armor = [];
        _unarmed = ItemFactory.MakeFists();
        Inventory = new Container("Bag", 10);
    }
    private Container Inventory { get; }
    public List<Armor> Armor { get; }
    public Gear? HeldItem { get; private set; }

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
    /// <summary>
    /// Simply adds the item, use TakeItem() if you want to take it from an area.
    /// </summary>
    /// <param name="item"></param>
    public void AddToInventory(Item item)
    {
        Output.WriteLine("You put the ", item, " in your ", Inventory);
        Inventory.Add(item);
    }

    /// <summary>
    /// Simply removes the item, use DropItem() if you want to drop it.
    /// </summary>
    /// <param name="item"></param>
    public void RemoveFromInventory(Item item)
    {
        Output.WriteLine("You take the ", item, " from your ", Inventory);
        Inventory.Remove(item);
    }

    public double ArmorRating
    {
        get
        {
            double rating = 0;
            foreach (Armor armor in Armor)
            {
                rating += armor.Rating;
                // rating += armor.Type switch
                // {
                //     ArmorClass.Light => Skills.LightArmor.Level * .01,
                //     ArmorClass.Heavy => Skills.HeavyArmor.Level * .01,
                //     _ => throw new ArgumentOutOfRangeException()
                // };
            }
            return rating;
        }
    }

    public double EquipmentWarmth => (HeldItem?.Warmth ?? 0) + Armor.Sum(a => a.Warmth);

    public void Equip(IEquippable item)
    {
        switch (item)
        {
            case Weapon weapon:
                Unequip(Weapon);
                Weapon = weapon;
                Inventory.Remove(weapon);
                break;
            case Armor armor:
                var oldItem = Armor.FirstOrDefault(i => i.EquipSpot == armor.EquipSpot);
                if (oldItem != null) Unequip(oldItem);
                Armor.Add(armor);
                Inventory.Remove(armor);
                break;
            case Gear gear:
                if (HeldItem != null) Unequip(HeldItem);
                HeldItem = gear;
                Inventory.Remove(gear);
                break;
            default:
                Output.WriteLine("You can't equip that.");
                return;
        }


    }
    public void Unequip(IEquippable item)
    {
        switch (item)
        {
            case Weapon weapon:
                {
                    Weapon = _unarmed;
                    if (weapon != _unarmed) Inventory.Add(weapon);
                    break;
                }
            case Armor armor:
                Armor.Remove(armor);
                Inventory.Add(armor);
                break;
            case Gear gear:
                HeldItem = null;
                Inventory.Add(gear);
                break;
            default:
                Output.WriteLine("You can't unequip that.");
                return;
        }

        if (item != _unarmed)
            Output.WriteLine("You unequip ", item);
    }
    public void CheckGear()
    {
        Describe.DescribeGear(this);
        Output.WriteLine("Would you like to unequip an item?");
        int choice = Input.GetSelectionFromList(new List<string> { "Yes", "No" });
        if (choice != 1) return;

        Output.WriteLine("Which item would you like to unequip?");
        // get list of all equipment
        var equipment = new List<IEquippable>();
        equipment.AddRange(Armor);
        if (IsArmed) equipment.Add(Weapon);
        if (HeldItem != null) equipment.Add(HeldItem);

        choice = Input.GetSelectionFromList(equipment, true);
        if (choice == 0) return;
        Unequip(equipment[choice - 1]);
    }

    //todo refactor this so that the logic is handled in the player class
    public void Open(Player player)
    {
        while (!Inventory.IsEmpty)
        {
            Output.WriteLine(Inventory, " (", Inventory.Weight(), "/", Inventory.MaxWeight, "):");
            var options = Inventory.GetStackedItemList();
            int index = Input.GetSelectionFromList(options, true, "Close " + Inventory) - 1;
            if (index == -1)
                return;

            string itemName = options[index];
            itemName = Inventory.ExtractStackedItemName(itemName);
            Item item = Inventory.GetItemByName(itemName); //Items.First(i => i.Name.StartsWith(itemName));
            Output.WriteLine("What would you like to do with ", item);
            int choice = Input.GetSelectionFromList(new List<string>() { "Use", "Inspect", "Drop" }, true);
            switch (choice)
            {
                case 0:
                    continue;
                case 1:
                    player.UseItem(item);
                    //item.Use(player);
                    break;
                case 2:
                    Describe.DescribeItem(item);
                    break;
                case 3:
                    player.DropItem(item);
                    break;
            }
        }
        Output.WriteLine(Inventory, " is empty.");

    }
}
