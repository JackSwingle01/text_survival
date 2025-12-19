using text_survival.Actors;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Combat;

public class CombatManager
{
    public CombatManager(Actor owner)
    {
        Owner = owner;
    }

    public double DetermineDamage(Weapon weapon)
    {
        // base weapon and skill
        double baseDamage = weapon.Damage;

        double skillBonus = 0;
        if (Owner is Player player)
        {
            skillBonus = player.Skills.Fighting.Level;
        }

        // modifiers
        double strengthModifier = (Owner.Strength / 2) + .5; // str determines up to 50%
        // Vitality affects damage output - low organ function = weaker attacks
        double vitalityModifier = 0.7 + (0.3 * Owner.Vitality);
        // todo factor in any effects like adrenaline, etc.
        // This could be expanded based on your EffectRegistry
        double effectsModifier = 1.0;
        double randomModifier = Utils.RandDouble(.5, 1.5);
        double totalModifier = strengthModifier * vitalityModifier * effectsModifier * randomModifier;

        double damage = (baseDamage + skillBonus) * totalModifier;
        return damage >= 0 ? damage : 0;
    }

    public double DetermineDodgeChance(Actor target)
    {
        double dodgeLevel = 0;
        if (target is Player player)
            dodgeLevel = player.Skills.Reflexes.Level;

        double baseDodge = dodgeLevel / 100;
        double speedDiff = target.Speed - Owner.Speed;
        double chance = baseDodge + speedDiff;
        // GameDisplay.AddNarrative("Debug: Dodge Chance = ", chance);
        chance = Math.Clamp(chance, 0, .95);
        return chance;
    }

    public bool DetermineDodge(Actor target)
    {
        double dodgeChance = DetermineDodgeChance(target);
        if (Utils.DetermineSuccess(dodgeChance))
        {
            GameDisplay.AddNarrative($"{Owner} dodged the attack!");
            return true;
        }
        return false;
    }

    public bool DetermineHit(Weapon weapon)
    {
        // GameDisplay.AddNarrative("Debug: hit Chance: ", weapon.Accuracy);
        double hitChance = Math.Clamp(weapon.Accuracy, .01, .95);
        if (!Utils.DetermineSuccess(hitChance))
        {
            GameDisplay.AddNarrative($"{Owner} missed!");
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
        double attributeAvg = target.Strength; // todo 
        double blockAtbAvg = target.ActiveWeapon.BlockChance + attributeAvg / 2;
        double blockChance = blockAtbAvg + skillBonus;
        if (Utils.DetermineSuccess(blockChance))
        {
            GameDisplay.AddNarrative($"{target} blocked the attack!");
            return true;
        }
        return false;
    }

    public void Attack(Actor target, Weapon? weaponOverride = null, string? targetedPart = null)
    {
        Weapon weapon = weaponOverride ?? Owner.ActiveWeapon;

        bool isDodged = DetermineDodge(target);
        if (isDodged)
        {
            // Use our narrator for rich descriptions
            string description = CombatNarrator.DescribeAttack(Owner, target, null, false, true, false);
            GameDisplay.AddNarrative(description);
            return;
        }

        bool isHit = DetermineHit(weapon);
        if (!isHit)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, null, false, false, false);
            GameDisplay.AddNarrative(description);
            return;
        }

        // Check for block
        bool isBlocked = DetermineBlock(target);
        if (isBlocked)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, null, true, false, true);
            GameDisplay.AddNarrative(description);
            return;
        }

        double damage = DetermineDamage(weapon);

        DamageType type = weapon.Class switch
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
        GameDisplay.AddNarrative(attackDescription);

        // Add weapon-specific effect descriptions
        if (damageResult.TotalDamageDealt > 0)
        {
            AddWeaponEffectDescription(weapon.Class, damageResult.TotalDamageDealt);
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
            GameDisplay.AddDanger("Blood sprays from the wound!");
        }
        else if (weaponClass == WeaponClass.Blunt && damage > 12)
        {
            GameDisplay.AddDanger("You hear a sickening crack!");
        }
        else if (weaponClass == WeaponClass.Pierce && damage > 15)
        {
            GameDisplay.AddDanger("The attack pierces deep into the flesh!");
        }
    }

    public Actor Owner { get; }
}
