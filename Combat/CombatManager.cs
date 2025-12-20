using text_survival.Actors;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Combat;

public class CombatManager
{
    private const double MELEE_HIT_RATE = 0.90;

    public CombatManager(Actor owner)
    {
        Owner = owner;
    }

    public double DetermineDamage(double baseDamage)
    {
        double skillBonus = 0;
        if (Owner is Player player)
        {
            skillBonus = player.Skills.Fighting.Level;
        }

        // modifiers
        double strengthModifier = (Owner.Strength / 2) + .5; // str determines up to 50%
        double vitalityModifier = 0.7 + (0.3 * Owner.Vitality);
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
        return Math.Clamp(chance, 0, .95);
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

    public bool DetermineHit()
    {
        // Flat 90% hit rate for melee
        if (!Utils.DetermineSuccess(MELEE_HIT_RATE))
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
        double blockChance = target.BlockChance + (target.Strength / 2) + skillBonus;

        if (Utils.DetermineSuccess(blockChance))
        {
            GameDisplay.AddNarrative($"{target} blocked the attack!");
            return true;
        }
        return false;
    }

    public void Attack(Actor target, Tool? weapon = null, string? targetedPart = null)
    {
        // Get attack stats from weapon if provided, otherwise from attacker's properties
        double baseDamage = weapon?.Damage ?? Owner.AttackDamage;
        string attackName = weapon?.Name ?? Owner.AttackName;
        DamageType damageType = weapon != null
            ? GetDamageType(weapon.WeaponClass)
            : Owner.AttackType;

        bool isDodged = DetermineDodge(target);
        if (isDodged)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, null, false, true, false);
            GameDisplay.AddNarrative(description);
            return;
        }

        bool isHit = DetermineHit();
        if (!isHit)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, null, false, false, false);
            GameDisplay.AddNarrative(description);
            return;
        }

        bool isBlocked = DetermineBlock(target);
        if (isBlocked)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, null, true, false, true);
            GameDisplay.AddNarrative(description);
            return;
        }

        double damage = DetermineDamage(baseDamage);

        DamageInfo damageInfo = new(
            amount: damage,
            source: Owner.Name,
            type: damageType,
            targetPartName: targetedPart
        );

        DamageResult damageResult = DamageProcessor.DamageBody(damageInfo, target.Body);

        string attackDescription = CombatNarrator.DescribeAttack(Owner, target, damageResult, true, false, false);
        GameDisplay.AddNarrative(attackDescription);

        // Add damage effect descriptions
        if (damageResult.TotalDamageDealt > 0)
        {
            AddDamageEffectDescription(damageType, damageResult.TotalDamageDealt);
        }

        if (target is Player player)
        {
            player.Skills.Fighting.GainExperience(1);
        }

        Thread.Sleep(1000);
    }

    private static DamageType GetDamageType(WeaponClass? weaponClass)
    {
        return weaponClass switch
        {
            WeaponClass.Blade or WeaponClass.Claw => DamageType.Sharp,
            WeaponClass.Pierce => DamageType.Pierce,
            _ => DamageType.Blunt
        };
    }

    private static void AddDamageEffectDescription(DamageType damageType, double damage)
    {
        if (damageType == DamageType.Sharp && damage > 10)
        {
            GameDisplay.AddDanger("Blood sprays from the wound!");
        }
        else if (damageType == DamageType.Blunt && damage > 12)
        {
            GameDisplay.AddDanger("You hear a sickening crack!");
        }
        else if (damageType == DamageType.Pierce && damage > 15)
        {
            GameDisplay.AddDanger("The attack pierces deep into the flesh!");
        }
    }

    public Actor Owner { get; }
}
