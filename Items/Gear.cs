using text_survival.Effects;
using text_survival.Magic;

namespace text_survival.Items
{
    public class Gear : Item, IEquippable
    {
        // private List<Buff> EquipBuffs { get; }
        // public List<Buff> GetEquipBuffs() => EquipBuffs;
        // public void AddEquipBuff(Buff buff) => EquipBuffs.Add(buff);
        // public void RemoveEquipBuff(Buff buff) => EquipBuffs.Remove(buff);
        public List<Effect> EquipEffects { get; set; }
        public double Warmth { get; set; }
        public Gear(string name, double weight = 1, int quality = 50) : base(name, weight, quality)
        {
            EquipEffects = [];
        }
    }

}

