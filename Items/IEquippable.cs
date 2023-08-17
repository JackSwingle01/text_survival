using text_survival_rpg_web.Magic;

namespace text_survival_rpg_web.Items
{
    public interface IEquippable
    {
        Buff Buff { get; set; }
        public void OnEquip(Player player);
        public void OnUnequip(Player player);
    }
}