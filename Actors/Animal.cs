using text_survival_rpg_web.Level;

namespace text_survival_rpg_web.Actors
{
    public class Animal : Npc
    {
        public Animal(string name, double baseDamage, Attributes? attributes = null) : base(name, attributes)
        {
            UnarmedDamage = baseDamage;
        }

    }
}
