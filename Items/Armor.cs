using text_survival.Actors;
using text_survival.Magic;

namespace text_survival.Items
{
    public class Armor : Item, IEquippable
    {
        public EquipSpots EquipSpot { get; set; }
        public double Rating { get; set; }
        public double Warmth { get; set; }
        public ArmorClass Type { get; set; }
        private List<Buff> EquipBuffs { get; }
        public List<Buff> GetEquipBuffs() => EquipBuffs;
        public void AddEquipBuff(Buff buff) => EquipBuffs.Add(buff);
        public void RemoveEquipBuff(Buff buff) => EquipBuffs.Remove(buff);

        public Armor(string name, double rating, EquipSpots equipSpot, double warmth = 0, ArmorClass type = ArmorClass.Light) : base(name)
        {
            EquipBuffs = new List<Buff>();
            Rating = rating;
            EquipSpot = equipSpot;
            UseEffect = player => player.Equip(this);
            Warmth = warmth;
            Type = type;
            if (warmth > 0)EquipBuffs.Add(CommonBuffs.Warmth(warmth));

        }

            
    }
}