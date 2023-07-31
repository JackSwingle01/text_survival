namespace text_survival.Items
{
    public class EquipableItem : Item
    {
        public int Strength { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public float Warmth { get; set; }

        public EquipSpots EquipSpot { get; set; }

        public enum EquipSpots
        {
            Head,
            Chest,
            Legs,
            Feet,
            Hands,
            Weapon
        }

        public EquipableItem(string name, int strength = 0, int defense = 0, int speed = 0, float warmth = 0) : base(name)
        {
            Strength = strength;
            Defense = defense;
            Speed = speed;
            Warmth = warmth;
            UseEffect = (player) => EquipTo(player);
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
        public void EquipTo(Player player)
        {
            if (player.EquipedItems.Any(item => item.EquipSpot == EquipSpot))
            {
                EquipableItem? item = player.EquipedItems.Find(item => item.EquipSpot == EquipSpot);
                item?.Unequip(player);
            }
            player.EquipedItems.Add(this);
            ApplyStats(player);
            player.Inventory.Remove(this);
        }
        public void Unequip(Player player)
        {
            player.EquipedItems.Remove(this);
            RemoveStats(player);
            player.Inventory.Add(this);
        }
        private void ApplyStats(Player player)
        {
            player.Strength += Strength;
            player.Defense += Defense;
            player.Speed += Speed;
            player.WarmthBonus += Warmth;
        }
        private void RemoveStats(Player player)
        {
            player.Strength -= Strength;
            player.Defense -= Defense;
            player.Speed -= Speed;
            player.WarmthBonus -= Warmth;
        }
    }
}
