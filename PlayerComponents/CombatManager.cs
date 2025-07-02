using System.Runtime;
using Microsoft.VisualBasic;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.PlayerComponents;

public class CombatManager
{
    public CombatManager(Actor owner)
    {
        Owner = owner;
    }

    public double DetermineDamage()
    {
        // base weapon and skill
        double baseDamage = Owner.ActiveWeapon.Damage;

        double skillBonus = 0;
        if (Owner is Player player)
        {
            skillBonus = player.Skills.Fighting.Level;
        }

        // modifiers
        double strengthModifier = (AbilityCalculator.CalculateStrength(Owner.Body) / 2) + .5; // str determines up to 50%
        // A smaller health modifier up to 30%
        double healthModifier = 0.7 + (0.3 * (Owner.Body.Health / Owner.Body.MaxHealth));
        // todo factor in any effects like adrenaline, etc.
        // This could be expanded based on your EffectRegistry
        double effectsModifier = 1.0;
        double randomModifier = Utils.RandDouble(.5, 1.5);
        double totalModifier = strengthModifier * healthModifier * effectsModifier * randomModifier;

        double damage = (baseDamage + skillBonus) * totalModifier;
        return damage >= 0 ? damage : 0;
    }

    public double DetermineDodgeChance(Actor target)
    {

        double dodgeLevel = 0;
        if (target is Player player)
            dodgeLevel = player.Skills.Reflexes.Level;

        double baseDodge = dodgeLevel / 100;
        double speedDiff = AbilityCalculator.CalculateSpeed(target.Body) - AbilityCalculator.CalculateSpeed(Owner.Body);
        double chance = baseDodge + speedDiff;
        // Output.WriteLine("Debug: Dodge Chance = ", chance);
        chance = Math.Clamp(chance, 0, .95);
        return chance;
    }

    public bool DetermineDodge(Actor target)
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

    public bool DetermineBlock(Actor target)
    {
        double blockLevel = 0;
        if (target is Player player)
            blockLevel = player.Skills.Defense.Level;
        double skillBonus = blockLevel / 100;
        double attributeAvg = AbilityCalculator.CalculateStrength(target.Body); // todo 
        double blockAtbAvg = target.ActiveWeapon.BlockChance + attributeAvg / 2;
        double blockChance = blockAtbAvg + skillBonus;
        if (Utils.DetermineSuccess(blockChance))
        {
            Output.WriteLine($"{target} blocked the attack!");
            return true;
        }
        return false;
    }

    public void Attack(Actor target, string? targetedPart = null)
    {
        bool isDodged = DetermineDodge(target);
        if (isDodged)
        {
            // Use our narrator for rich descriptions
            string description = CombatNarrator.DescribeAttack(Owner, target, 0, null, false, true, false);
            Output.WriteLine(description);
            return;
        }

        bool isHit = DetermineHit();
        if (!isHit)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, 0, null, false, false, false);
            Output.WriteLine(description);
            return;
        }

        // Check for block
        bool isBlocked = DetermineBlock(target);
        if (isBlocked)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, 0, null, true, false, true);
            Output.WriteLine(description);
            return;
        }

        double damage = DetermineDamage();

        DamageType type = Owner.ActiveWeapon.Class switch
        {
            WeaponClass.Blade or WeaponClass.Claw => DamageType.Sharp,
            WeaponClass.Pierce => DamageType.Pierce,
            _ => DamageType.Blunt
        };

        DamageInfo damageInfo = new(
            amount: damage,
            source: Owner.Name,
            type: type,
            targetPartName: targetedPart
        );

        DamageResult damageResult = DamageProcessor.DamageBody(damageInfo, target.Body);

        string attackDescription = CombatNarrator.DescribeAttack(Owner, target, damageResult, true, false, false);
        Output.WriteLine(attackDescription);

        // Add weapon-specific effect descriptions
        if (damageResult.TotalDamageDealt > 0)
        {
            AddWeaponEffectDescription(Owner.ActiveWeapon.Class, damageResult.TotalDamageDealt);
        }

        if (target is Player player)
        {
            player.Skills.Fighting.GainExperience(1);
        }
        
        Thread.Sleep(1000);
    }

    private static void AddWeaponEffectDescription(WeaponClass weaponClass, double damage)
    {
        if (weaponClass == WeaponClass.Blade && damage > 10)
        {
            Output.WriteDanger("Blood sprays from the wound!");
        }
        else if (weaponClass == WeaponClass.Blunt && damage > 12)
        {
            Output.WriteDanger("You hear a sickening crack!");
        }
        else if (weaponClass == WeaponClass.Pierce && damage > 15)
        {
            Output.WriteDanger("The attack pierces deep into the flesh!");
        }
    }

    public Actor Owner { get; }
}
