namespace text_survival.Items
{
    public class Armor : Item
    {
        public EquipSpots EquipSpot { get; set; }
        public double Rating { get; set; }
        public double Warmth { get; set; }

        public Armor(string name, double rating, EquipSpots equipSpot, double warmth = 0) : base(name)
        {
            Rating = rating;
            EquipSpot = equipSpot;
            UseEffect = player => player.Equip(this);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}