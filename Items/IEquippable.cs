
using text_survival.Effects;

namespace text_survival.Items
{
    public interface IEquippable
    {
        public List<Effect> EquipEffects { get; }
    }

}