using text_survival.Magic;

namespace text_survival.Actors
{
    public interface IBuffable //: IActor
    {
        public List<Buff> Buffs { get; }

        public void AddBuff(Buff buff)
        { 
            buff.ApplyTo(this);
        }
        public void RemoveBuff(Buff buff)
        {
            buff.Remove();
        }

        public void RemoveBuff(string name)
        {
            Buffs.Find(buff => buff.Name == name)?.Remove();
        }

    }
}
