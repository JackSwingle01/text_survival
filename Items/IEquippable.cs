using text_survival.Actors;
using text_survival.Magic;

namespace text_survival.Items
{
    public interface IEquippable
    {
        public List<Buff> GetEquipBuffs();
        public void AddEquipBuff(Buff buff);
        public void RemoveEquipBuff(Buff buff);


        public virtual void OnEquip(IActor actor)
        {
            foreach (Buff buff in GetEquipBuffs())
            {
                buff.ApplyTo(actor);
            }
        }

        public virtual void OnUnequip(IActor actor)
        {
            foreach (Buff buff in GetEquipBuffs())
            {
                buff.Remove();
            }
        }
    }

}