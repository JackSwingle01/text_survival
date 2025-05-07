namespace text_survival.Items;

public class WeaponModifierItem : Item
{
    public double Damage { get; set; }
    public WeaponModifierItem(string name) : base(name)
    {
        Damage = 0;
    }

}

public class ArmorModifierItem : Item
{
    public List<EquipSpots> ValidArmorTypes;
    public double Warmth { get; set; }
    public double Rating { get; set; }
    public ArmorModifierItem(string name, List<EquipSpots> validArmorTypes) : base(name)
    {
        ValidArmorTypes = validArmorTypes;
        Warmth = 0;
        Rating = 0;
    }

}