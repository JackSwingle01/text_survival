using text_survival.Level;

namespace text_survival.Actors
{
    public class Animal : Npc
    {
        public Animal(string name, double baseDamage, Attributes? attributes = null) : base(name, attributes)
        {
            UnarmedDamage = baseDamage;
        }

    }
}
