using text_survival_rpg_web.Magic;

namespace text_survival_rpg_web.Items
{
    public class Gear : Item, IEquippable
    {
        public Buff Buff { get; set; }

        public Gear(string name, double weight = 1) : base(name, weight)
        {
            UseEffect = (player) =>
            {
                player.Equip(this);
            };
            Buff = new Buff(name, -1);
        }

        public void OnEquip(Player player)
        {
            player.ApplyBuff(Buff);
        }

        public void OnUnequip(Player player)
        {
            player.RemoveBuff(Buff);

        }
    }

}

