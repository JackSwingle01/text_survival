using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.IO;

namespace text_survival.PlayerComponents;
class CombatManager : ICombatSystem
{
    public CombatManager(ICombatant owner)
    {
        Owner = owner;
    }

    public double CalculateAttackDamage(double baseDamage, double strength, double skillBonus, double otherModifiers)
    {
        double strengthModifier = (strength + 50) / 100;
        double damage = (baseDamage + skillBonus) * strengthModifier * otherModifiers;
        damage *= Utils.RandDouble(0.5, 2);
        return damage >= 0 ? damage : 0;
    }


    public double DetermineDamage()
    {
        double skillBonus = Owner._skillRegistry.GetLevel("Fighting");

        double conditionModifier = (2 - (Owner.ConditionPercent / 100)) / 2 + 0.1;
        return CalculateAttackDamage(
            Owner.ActiveWeapon.Damage, Owner.Attributes.Strength, skillBonus, conditionModifier);
    }

    public double DetermineDodgeChance(ICombatant target)
    {
        double dodgeLevel = target._skillRegistry != null ? target._skillRegistry.GetLevel("Reflexes") : 0;
        double baseDodge = (dodgeLevel + target.Attributes.Luck / 10) / 100;
        double speedDiff = (target.Attributes.Speed - Owner.Attributes.Speed) / 100;
        double chance = baseDodge + speedDiff;
        // Output.WriteLine("Debug: Dodge Chance = ", chance);
        chance = Math.Clamp(chance, 0, .95);
        return chance;
    }

    public bool DetermineDodge(ICombatant target)
    {
        double dodgeChance = DetermineDodgeChance(target);
        if (Utils.DetermineSuccess(dodgeChance))
        {
            Output.WriteLine($"{Owner} dodged the attack!");
            return true;
        }
        return false;
    }

    public bool DetermineHit()
    {
        // Output.WriteLine("Debug: hit Chance: ", Owner.ActiveWeapon.Accuracy);
        double hitChance = Math.Clamp(Owner.ActiveWeapon.Accuracy, .01, .95);
        if (!Utils.DetermineSuccess(hitChance))
        {
            Output.WriteLine($"{Owner} missed!");
            return false;
        }
        return true;
    }

    public bool DetermineBlock(ICombatant target)
    {
        double blockLevel = target._skillRegistry != null ? target._skillRegistry.GetLevel("Defense") : 0;
        double skillBonus = blockLevel / 100;
        double attributeAvg = (target.Attributes.Luck + target.Attributes.Strength) / 2 / 100;
        double blockAtbAvg = target.ActiveWeapon.BlockChance + attributeAvg / 2;
        double blockChance = blockAtbAvg + skillBonus;
        if (Utils.DetermineSuccess(blockChance))
        {
            Output.WriteLine($"{target} blocked the attack!");
            return true;
        }
        return false;
    }

    public void Attack(ICombatant target)
    {
        double damage = DetermineDamage();
        if (DetermineDodge(target))
            return;
        if (!DetermineHit())
            return;
        if (DetermineBlock(target))
            return;

        DamageInfo damageInfo = new(
            damage,
            source: Owner.Name,
            isSharp: Owner.ActiveWeapon.Class == Items.WeaponClass.Blade,
            isBlunt: Owner.ActiveWeapon.Class == Items.WeaponClass.Blunt || Owner.ActiveWeapon.Class == Items.WeaponClass.Unarmed,
            accuracy: Owner.ActiveWeapon.Accuracy
        );

        Output.WriteLine($"{Owner} attacked {target} for {Math.Round(damage, 1)} damage!");
        target.Damage(damageInfo);
        // if (Utils.RandFloat(0, 1) < 0.1) // 10% critical hit chance
        //     target.ApplyEffect(new BleedEffect(1, 3));


        Owner._skillRegistry.AddExperience("Fighting", 1);
        Thread.Sleep(1000);
    }


    public ICombatant Owner { get; }
}
