namespace text_survival.Items;

public class WeaponModifierItem : Item
{
    public double Damage { get; set; }
    public WeaponModifierItem(string name) : base(name)
    {
        Damage = 0;
    }
}

// Note: ArmorModifierItem was removed during inventory system migration
// Equipment modifiers can be added later if needed