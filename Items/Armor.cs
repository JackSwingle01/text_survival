namespace text_survival.Items
{
    public class Armor : Item
    {
        public EquipSpots EquipSpot { get; set; }
        public double Rating { get; set; }
        public double Warmth { get; set; }
        public ArmorClass Type { get; set; }

        public Armor(string name, double rating, EquipSpots equipSpot, double warmth = 0, ArmorClass type = ArmorClass.Light) : base(name)
        {
            Rating = rating;
            EquipSpot = equipSpot;
            UseEffect = player => player.Equip(this);
            Type = type;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}