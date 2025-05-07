using text_survival.Actors;
using text_survival.Magic;

namespace text_survival.Items
{
    public interface IEquippable
    {
        public List<Buff> GetEquipBuffs();
        public void AddEquipBuff(Buff buff);
        public void RemoveEquipBuff(Buff buff);

    }

}