using text_survival.Actions;
using text_survival.Actors;
using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Combat;

/// <summary>
/// Result of an attack, returned by CombatManager.Attack().
/// </summary>
public record AttackResult(
    bool Hit,
    double DamageDealt,
    List<string> Narratives
);

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
        return Utils.DetermineSuccess(dodgeChance);
    }

    public bool DetermineHit()
    {
        // Flat 90% hit rate for melee
        return Utils.DetermineSuccess(MELEE_HIT_RATE);
    }

    public bool DetermineBlock(Actor target)
    {
        double blockLevel = 0;
        if (target is Player player)
            blockLevel = player.Skills.Defense.Level;

        double skillBonus = blockLevel / 100;
        double blockChance = target.BlockChance + (target.Strength / 2) + skillBonus;

        return Utils.DetermineSuccess(blockChance);
    }

    public AttackResult Attack(Actor target, Gear? weapon = null, string? targetedPart = null, GameContext? ctx = null)
    {
        var narratives = new List<string>();

        // Get attack stats from weapon if provided, otherwise from attacker's properties
        double baseDamage = weapon?.Damage ?? Owner.AttackDamage;
        DamageType damageType = weapon != null
            ? GetDamageType(weapon.WeaponClass)
            : Owner.AttackType;

        bool isDodged = DetermineDodge(target);
        if (isDodged)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, null, false, true, false);
            narratives.Add(description);
            return new AttackResult(Hit: false, DamageDealt: 0, Narratives: narratives);
        }

        bool isHit = DetermineHit();
        if (!isHit)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, null, false, false, false);
            narratives.Add(description);
            return new AttackResult(Hit: false, DamageDealt: 0, Narratives: narratives);
        }

        bool isBlocked = DetermineBlock(target);
        if (isBlocked)
        {
            string description = CombatNarrator.DescribeAttack(Owner, target, null, true, false, true);
            narratives.Add(description);
            return new AttackResult(Hit: false, DamageDealt: 0, Narratives: narratives);
        }

        double damage = DetermineDamage(baseDamage);

        DamageInfo damageInfo = new(
            amount: damage,
            type: damageType
        );

        // Set target directly by name (internal access for combat targeting)
        if (targetedPart != null)
            damageInfo.TargetPartName = targetedPart;

        // Set armor values if attacking the player (who has equipment)
        if (target is Player && ctx != null)
        {
            damageInfo.ArmorCushioning = ctx.Inventory.TotalCushioning;
            damageInfo.ArmorToughness = ctx.Inventory.TotalToughness;
        }

        DamageResult damageResult = DamageProcessor.DamageBody(damageInfo, target.Body);

        // Apply triggered effects (bleeding, pain, etc.)
        foreach (var effect in damageResult.TriggeredEffects)
        {
            target.AddLog(target.EffectRegistry.AddEffect(effect));
        }

        string attackDescription = CombatNarrator.DescribeAttack(Owner, target, damageResult, true, false, false);
        narratives.Add(attackDescription);

        // Add damage effect descriptions
        string? damageEffectNarrative = GetDamageEffectDescription(damageType, damageResult.TotalDamageDealt);
        if (damageEffectNarrative != null)
        {
            narratives.Add(damageEffectNarrative);
        }

        if (Owner is Player player)
        {
            player.Skills.Fighting.GainExperience(1);
        }

        Thread.Sleep(1000);

        return new AttackResult(Hit: true, DamageDealt: damageResult.TotalDamageDealt, Narratives: narratives);
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

    private static string? GetDamageEffectDescription(DamageType damageType, double damage)
    {
        if (damageType == DamageType.Sharp && damage > 10)
            return "Blood sprays from the wound!";
        if (damageType == DamageType.Blunt && damage > 12)
            return "You hear a sickening crack!";
        if (damageType == DamageType.Pierce && damage > 15)
            return "The attack pierces deep into the flesh!";
        return null;
    }

    public Actor Owner { get; }
}
