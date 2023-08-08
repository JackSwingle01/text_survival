namespace text_survival.Items
{
    public partial class EquipableItem : Item
    {
        public float Strength { get; set; }
        public float Defense { get; set; }
        public float Speed { get; set; }
        public float Warmth { get; set; }

        public EquipSpots EquipSpot { get; set; }

        public EquipableItem(string name, int strength = 0, int defense = 0, int speed = 0, float warmth = 0) :
            base(name)
        {
            Strength = strength;
            Defense = defense;
            Speed = speed;
            Warmth = warmth;
            UseEffect = player => player.Equip(this);
        }

        public override string ToString()
        {
            return Name;
        }

        public override void Write()
        {
            Utils.Write(this, ": ", Description, "\n");
            Utils.Write("Weight: ", Weight);
            Utils.Write(", Strength: ", Strength);
            Utils.Write(", Defense: ", Defense);
            Utils.Write(", Speed: ", Speed);
            Utils.Write(", Warmth: ", Warmth, "\n");
        }
    }
}