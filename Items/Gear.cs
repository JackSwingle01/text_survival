using text_survival.Effects;

namespace text_survival.Items
{
    public class Gear : Item, IEquippable
    {
        // private List<Buff> EquipBuffs { get; }
        // public List<Buff> GetEquipBuffs() => EquipBuffs;
        // public void AddEquipBuff(Buff buff) => EquipBuffs.Add(buff);
        // public void RemoveEquipBuff(Buff buff) => EquipBuffs.Remove(buff);
        public List<Effect> EquipEffects { get; set; }

        /// <summary>
        /// Insulation should be a value between 0-1 of what percent of the skin-air temp differential it blocks
        /// </summary>
        public double Insulation { get; set; } 
        public Gear(string name, double weight = 1) : base(name, weight)
        {
            EquipEffects = [];
        }
    }

}

