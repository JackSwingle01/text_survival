using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Magic
{
    public enum BuffType
    {
        Generic,
        Bleed,
        Poison,
        Heal,
        Warmth,
    }

    public class Buff
    {
        public string Name { get; set; }
        public Action<IBuffable> ApplyEffect { private get; set; }
        public Action<IBuffable> RemoveEffect { private get; set; }
        public BuffType Type { get; private set; }
        public IBuffable? Target { get; protected set; }

        public Buff(string name, BuffType type = BuffType.Generic)
        {
            Name = name;
            Type = type;
            ApplyEffect = (target) => { }; // applies once when applied
            RemoveEffect = (target) => { }; // should undo ApplyEffect     
        }

        public virtual void ApplyTo(IBuffable target)
        {
            Target = target;
            target.AddBuff(this);
            ApplyEffect?.Invoke(target);
        }

        public virtual void Remove()
        {
            if (Target == null)
            {
                Output.WriteLine("ERROR: Buff.Remove() called with no target.");
                return;
            }
            RemoveEffect?.Invoke(Target);

            IBuffable t = Target;
            Target = null;
            t.RemoveBuff(this);
        }
    }
}
