using System.Text;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Actors.Player;
using text_survival.Bodies;

namespace text_survival.Combat;

public static class CombatNarrator
{
    private static readonly Random _rng = new();

    private static readonly Dictionary<DamageType, List<string>> AttackVerbs = new()
    {
        { DamageType.Sharp, new List<string> { "slashes", "cuts", "slices", "carves" } },
        { DamageType.Blunt, new List<string> { "bashes", "strikes", "smashes", "cracks" } },
        { DamageType.Pierce, new List<string> { "stabs", "pierces", "impales", "punctures" } }
    };

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
        string attackVerb = GetAttackVerb(damageType);
        string partName = FormatBodyPartName(damageResult.HitPartName);
        string damageDesc = GetDamageSeverity(damageResult.TotalDamageDealt);

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
        if (damageResult.TotalDamageDealt > 0.15)
            sb.Append(" hard!");
        else if (damageResult.TotalDamageDealt > 0.05)
            sb.Append(".");
        else
            sb.Append(", a glancing blow.");

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

    public static string GetAttackVerb(DamageType damageType)
    {
        if (AttackVerbs.TryGetValue(damageType, out var verbs))
            return verbs[_rng.Next(verbs.Count)];
        return "strikes";
    }

    public static string GetDamageSeverity(double damage)
    {
        return damage switch
        {
            <= 0.05 => "minimal",
            <= 0.1 => "light",
            <= 0.25 => "moderate",
            <= 0.5 => "severe",
            <= 1 => "critical",
            _ => "devastating"
        };
    }

    #endregion
}
