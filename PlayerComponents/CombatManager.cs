using text_survival.Actors;
using text_survival.Effects;
using text_survival.IO;

namespace text_survival.PlayerComponents
{
    class CombatManager
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

        public double CalculateDodgeChance(double speed, double attackerSpeed, double luck, double dodgeLevel)
        {
            double baseDodge = (dodgeLevel + luck / 10) / 200;
            double speedDiff = speed - attackerSpeed;
            return baseDodge + speedDiff;
        }

        public double DetermineDamage()
        {
            double skillBonus = Owner.Skills.GetLevel("Fighting");

            double conditionModifier = (2 - (Owner.ConditionPercent / 100)) / 2 + 0.1;
            return CalculateAttackDamage(
                Owner.ActiveWeapon.Damage, Owner.Attributes.Strength, skillBonus, conditionModifier);
        }

        public double DetermineHitChance(ICombatant attacker) => Owner.ActiveWeapon.Accuracy;

        public double DetermineDodgeChance(ICombatant attacker)
        {
            double dodgeLevel = Owner.Skills != null ? Owner.Skills.GetLevel("Reflexes") : 0;
            return CalculateDodgeChance(
                Owner.Attributes.Speed, attacker.Attributes.Speed, Owner.Attributes.Luck, dodgeLevel);
        }

        public double DetermineBlockChance(ICombatant attacker)
        {
            double blockLevel = Owner.Skills != null ? Owner.Skills.GetLevel("Defense") : 0;
            return (Owner.ActiveWeapon.BlockChance * 100 + (Owner.Attributes.Luck + Owner.Attributes.Strength) / 3) / 2 +
                   blockLevel / 100;
        }

        public bool DetermineDodge(ICombatant attacker)
        {
            double dodgeChance = DetermineDodgeChance(attacker);
            double dodgeRoll = Utils.RandDouble(0, 100);
            if (dodgeRoll <= dodgeChance)
            {
                Output.WriteLine($"{Owner} dodged the attack!");
                return true;
            }
            return false;
        }

        public bool DetermineHit(ICombatant attacker)
        {
            double hitChance = DetermineHitChance(attacker);
            double roll = Utils.RandDouble(0, 1);
            if (roll > hitChance)
            {
                Output.WriteLine($"{Owner} missed!");
                return false;
            }
            return true;
        }

        public bool DetermineBlock(ICombatant attacker)
        {
            double blockChance = DetermineBlockChance(attacker);
            double roll = Utils.RandDouble(0, 1);
            if (roll < blockChance)
            {
                Output.WriteLine($"{Owner} blocked the attack!");
                return true;
            }
            return false;
        }

        public void Attack(ICombatant target)
        {
            double damage = DetermineDamage();
            if (DetermineDodge(target))
                return;
            if (!DetermineHit(target))
                return;
            if (DetermineBlock(target))
                return;

            Output.WriteLine($"{Owner} attacked {target} for {Math.Round(damage, 1)} damage!");
            target.Damage(damage);
            foreach (var effect in Owner.ActiveWeapon.EquipEffects)
                target.ApplyEffect(effect);
            if (Utils.RandFloat(0, 1) < 0.1) // 10% critical hit chance
                target.ApplyEffect(new BleedEffect(1, 3));

            Owner.Skills.AddExperience("Fighting", 1);
            Thread.Sleep(1000);
        }


        public ICombatant Owner { get; }
    }
}