using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actors;

class ConsumableItem : Item
{
    public ConsumableItem(string name, int numUses=1) : base(name)
    {
        Effects = [];
        NumUses = numUses;
    }

    public List<IEffect> Effects;
}
