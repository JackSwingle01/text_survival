
using text_survival.Items;
using text_survival.Level;

namespace text_survival.Actors
{
    public interface ICombatant : IActor
    {
        bool IsAlive { get; }
        bool IsEngaged { get; set; }
        Weapon ActiveWeapon { get; }
        Attributes Attributes { get; }
        Skills Skills {get;}
        double ConditionPercent { get; }
        void Attack(ICombatant target);
        // double DetermineDamage();
        // double DetermineHitChance(ICombatant defender);
        // double DetermineDodgeChance(ICombatant attacker);
        // double DetermineBlockChance(ICombatant attacker);

    }
}
