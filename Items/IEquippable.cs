using text_survival.Level;
using text_survival.Magic;

namespace text_survival.Items
{
    public interface IEquippable
    {
        Buff Buff { get; set; }
        public void OnEquip(Player player);
        public void OnUnequip(Player player);
    }
}