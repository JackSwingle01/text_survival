
using text_survival.Effects;

namespace text_survival.Items
{
    public interface IEquippable
    {
        // public List<Buff> GetEquipBuffs();
        // public void AddEquipBuff(Buff buff);
        // public void RemoveEquipBuff(Buff buff);
        public List<IEffect> EquipEffects { get; }
    }

}