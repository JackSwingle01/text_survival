using System.Text;
using text_survival.Actors;
using text_survival.Actors.Player;
using text_survival.Bodies;

namespace text_survival.Combat;

public static class CombatNarrator
{

    // Verbs by damage type and weight of the hit: light, solid, heavy. A random verb from one
    // list gave "smashes your abdomen, a glancing blow".
    private static readonly Dictionary<DamageType, string[][]> AttackVerbs = new()
    {
        { DamageType.Sharp, [["nicks", "scratches"], ["cuts", "slashes"], ["slices", "carves"]] },
        { DamageType.Blunt, [["grazes", "clips"], ["strikes", "bashes"], ["smashes", "cracks"]] },
        { DamageType.Pierce, [["pricks", "nicks"], ["stabs", "pierces"], ["impales", "punctures"]] }
    };

    private const double SolidHitDamage = 0.05;
    private const double HeavyHitDamage = 0.15;

    /// <summary>
    /// Unified method for describing any attack outcome.
    /// </summary>
    public static string DescribeAttack(Actor attacker, Actor target, CombatActionResult result)
    {
        var sb = new StringBuilder();

        if (result.Dodged)
        {
            sb.Append(DescribeDodge(attacker, target));
        }
        else if (result.Blocked)
        {
            sb.Append(DescribeBlock(attacker, target));
        }
        else if (!result.Hit)
        {
            sb.Append(DescribeMiss(attacker, target));
        }
        else if (result.Damage != null)
        {
            sb.Append(DescribeHit(attacker, target, result.Damage));
        }

        return sb.ToString();
    }

    private static string DescribeDodge(Actor attacker, Actor target)
    {
        if (target is Player)
            return $"You sidestep the {attacker.Name.ToLower()}'s attack!";
        else
            return $"The {target.Name.ToLower()} evades your strike!";
    }

    private static string DescribeBlock(Actor attacker, Actor target)
    {
        if (target is Player)
            return $"You deflect the {attacker.Name.ToLower()}'s attack!";
        else
            return $"The {target.Name.ToLower()} blocks your blow!";
    }

    private static string DescribeMiss(Actor attacker, Actor target)
    {
        if (attacker is Player)
            return $"Your attack misses the {target.Name.ToLower()}!";
        else
            return $"The {attacker.Name.ToLower()}'s attack misses!";
    }

    private static string DescribeHit(Actor attacker, Actor target, DamageResult damageResult)
    {
        DamageType damageType = attacker.AttackType;
        string attackVerb = GetAttackVerb(damageType, damageResult.TotalDamageDealt);
        string partName = FormatBodyPartName(damageResult.HitPartName);

        var sb = new StringBuilder();

        if (attacker is Player)
        {
            sb.Append($"Your attack {attackVerb} the {target.Name.ToLower()}'s {partName}");
        }
        else
        {
            sb.Append($"The {attacker.Name.ToLower()}'s attack {attackVerb} your {partName}");
        }

        // Add severity description
        sb.Append(damageResult.TotalDamageDealt > HeavyHitDamage ? "!" : ".");

        // Organ damage (critical info)
        if (damageResult.OrganHit && damageResult.OrganHitName != null)
        {
            sb.Append($" {damageResult.OrganHitName} damaged!");
        }

        // Bleeding effect
        if (damageResult.TriggeredEffects.Any(e => e.EffectKind == "Bleeding"))
        {
            sb.Append(" Blood flows from the wound.");
        }

        // Penetration detail for severe hits
        if (damageResult.WasPenetrating && damageResult.TotalDamageDealt > 0.1 && !damageResult.OrganHit)
        {
            if (damageResult.TissuesDamaged.Any(t => t.TissueName == "Muscle"))
            {
                sb.Append(" The blow tears muscle!");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Describes a shove attempt outcome.
    /// </summary>
    public static string DescribeShove(Actor attacker, Actor target, bool success, bool dodged)
    {
        if (dodged)
        {
            if (target is Player)
                return $"You dodge the {attacker.Name.ToLower()}'s shove!";
            else
                return $"The {target.Name.ToLower()} dodges your shove!";
        }

        if (success)
        {
            if (attacker is Player)
                return $"You shove the {target.Name.ToLower()} back!";
            else
                return $"The {attacker.Name.ToLower()} shoves you back!";
        }
        else
        {
            if (attacker is Player)
                return $"Your shove fails to move the {target.Name.ToLower()}.";
            else
                return $"The {attacker.Name.ToLower()}'s shove fails to move you.";
        }
    }

    /// <summary>
    /// Describes an intimidation attempt.
    /// </summary>
    public static string DescribeIntimidate(Actor attacker, bool isPlayer)
    {
        if (isPlayer)
            return "You shout and make yourself large!";
        else
            return $"The {attacker.Name.ToLower()} snarls threateningly.";
    }

    #region Utility Methods

    public static string FormatBodyPartName(string partName)
    {
        return partName.ToLower();
    }

    public static string GetAttackVerb(DamageType damageType, double damage)
    {
        if (!AttackVerbs.TryGetValue(damageType, out var tiers))
            return "strikes";
        int tier = damage > HeavyHitDamage ? 2 : damage > SolidHitDamage ? 1 : 0;
        var verbs = tiers[tier];
        return verbs[Utils.Rng.Next(verbs.Length)];
    }

    #endregion
}
