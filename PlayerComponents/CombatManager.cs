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
        double strengthModifier = (Owner.Body.CalculateStrength() / 2) + .5; // str determines up to 50%
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
        double speedDiff = target.Body.CalculateSpeed() - Owner.Body.CalculateSpeed();
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
        double attributeAvg = target.Body.CalculateStrength(); // todo 
        double blockAtbAvg = target.ActiveWeapon.BlockChance + attributeAvg / 2;
        double blockChance = blockAtbAvg + skillBonus;
        if (Utils.DetermineSuccess(blockChance))
        {
            Output.WriteLine($"{target} blocked the attack!");
            return true;
        }
        return false;
    }

    public void Attack(Actor target, IBodyPart? targetedPart = null)
    {
        bool isDodged = DetermineDodge(target);
        string partName = targetedPart?.Name ?? "body";
        if (isDodged)
        {
            // Use our narrator for rich descriptions
            string description = CombatNarrator.DescribeAttack(Owner, target, 0, partName, false, true, false);
            Output.WriteLine(description);
            return;
        }

        bool isHit = DetermineHit();
        if (!isHit)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, 0, partName, false, false, false);
            Output.WriteLine(description);
            return;
        }

        // Check for block
        bool isBlocked = DetermineBlock(target);
        if (isBlocked)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, 0, partName, true, false, true);
            Output.WriteLine(description);
            return;
        }

        double damage = DetermineDamage();

        if (targetedPart != null)
        {
            AdjustAccuracyForTargeting(targetedPart);
        }

        DamageType type = DamageType.Blunt;
        if (Owner.ActiveWeapon.Class == WeaponClass.Blade || Owner.ActiveWeapon.Class == WeaponClass.Claw)
        {
            type = DamageType.Sharp;
        }
        else if (Owner.ActiveWeapon.Class == WeaponClass.Pierce)
        {
            type = DamageType.Pierce;
        }

        DamageInfo damageInfo = new(
            amount: damage,
            source: Owner.Name,
            type: type,
            accuracy: Owner.ActiveWeapon.Accuracy,
            targetPart: targetedPart
        );

        target.Damage(damageInfo);

        double partHealthPercent = 0;
    
        if (targetedPart != null)
        {
            partHealthPercent = targetedPart.Condition;
        }
        string attackDescription = CombatNarrator.DescribeAttack(Owner, target, damage, partName, true, false, false);
        Output.WriteLine(attackDescription);

        // Add part status if it's significantly damaged
        if (partHealthPercent < 0.9)
        {
            string statusDesc = CombatNarrator.DescribeTargetStatus(partName, partHealthPercent);
            if (!string.IsNullOrEmpty(statusDesc))
            {
                Output.WriteLine(statusDesc);
            }
        }

        // Add weapon-specific effect descriptions
        if (Owner.ActiveWeapon.Class == WeaponClass.Blade && damage > 10)
        {
            Output.WriteDanger("Blood sprays from the wound!");
        }
        else if (Owner.ActiveWeapon.Class == WeaponClass.Blunt && damage > 12)
        {
            Output.WriteDanger("You hear a sickening crack!");
        }
        else if (Owner.ActiveWeapon.Class == WeaponClass.Pierce && damage > 15)
        {
            Output.WriteDanger("The attack pierces deep into the flesh!");
        }

        if (target is Player player)
        {
            player.Skills.Fighting.GainExperience(1);
        }
        Thread.Sleep(1000);
    }

    private double AdjustAccuracyForTargeting(IBodyPart targetedPart)
    {
        return .8; // todo, for now slight penalty for targeting vs random swing
    }

    public Actor Owner { get; }
}
