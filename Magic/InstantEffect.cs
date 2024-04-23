using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Actors;

namespace text_survival.Magic
{
    public class InstantEffectBuff : Buff
    {
        public InstantEffectBuff(string name, BuffType type = BuffType.Generic) : base(name, type)
        {
            ApplyEffect = (target) => { }; // applies once when applied
            RemoveEffect = (target) => { };   
        }

        public override void ApplyTo(IBuffable target)
        {
            base.ApplyTo(target);
            base.Remove();
        }
    }
}
