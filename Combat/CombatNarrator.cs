using text_survival.Actors;
using text_survival.Actors.Player;
using text_survival.Bodies;

namespace text_survival.Combat;

public static class CombatNarrator
{
    private static readonly Dictionary<DamageType, List<string>> AttackVerbs = new()
    {
        { DamageType.Sharp, new List<string> { "slashes", "cuts", "slices", "carves" } },
        { DamageType.Blunt, new List<string> { "bashes", "strikes", "smashes", "cracks" } },
        { DamageType.Pierce, new List<string> { "stabs", "pierces", "impales", "punctures" } }
    };

    public static string DescribeAttack(Actor attacker, Actor target, DamageResult? damageResult, bool isHit, bool isDodged, bool isBlocked)
    {
        var sb = new System.Text.StringBuilder();

        string targetPart = damageResult?.HitPartName ?? "body";
        sb.Append(DescribeAttackAttempt(attacker, target, targetPart));

        if (isDodged)
            sb.Append(DescribeDodge(attacker, target));
        else if (isBlocked)
            sb.Append(DescribeBlock(attacker, target));
        else if (!isHit)
            sb.Append(DescribeMiss(attacker, target));
        else if (damageResult != null)
            sb.Append(DescribeHit(attacker, target, damageResult));

        return sb.ToString();
    }

    private static string DescribeAttackAttempt(Actor attacker, Actor target, string targetPart)
    {
        string attackName = attacker.AttackName;

        if (attacker is Player)
        {
            return $"You strike with your {attackName}, aiming at {target.Name}'s {targetPart}. ";
        }
        else
        {
            return $"The {attacker.Name} attacks with its {attackName}, lunging at your {targetPart}. ";
        }
    }

    private static string DescribeDodge(Actor attacker, Actor target)
    {
        if (target is Player)
            return $"You quickly sidestep the {attacker.Name}'s attack!";
        else
            return $"The {target.Name} nimbly evades your strike!";
    }

    private static string DescribeBlock(Actor attacker, Actor target)
    {
        if (target is Player)
            return $"You deflect the attack!";
        else
            return $"The {target.Name} blocks your blow!";
    }

    private static string DescribeMiss(Actor attacker, Actor target)
    {
        if (attacker is Player)
            return "Your attack misses!";
        else
            return $"The {attacker.Name}'s attack whistles past you, missing entirely!";
    }

    private static string DescribeHit(Actor attacker, Actor target, DamageResult damageResult)
    {
        DamageType damageType = attacker.AttackType;
        string attackVerb = GetAttackVerb(damageType);
        string damageDesc = GetDamageSeverity(damageResult.TotalDamageDealt);

        var sb = new System.Text.StringBuilder();

        if (attacker is Player)
        {
            sb.Append($"Your attack {attackVerb} the {target.Name}'s {damageResult.HitPartName}, dealing {damageDesc} damage! ");
        }
        else
        {
            sb.Append($"The {attacker.Name}'s attack {attackVerb} your {damageResult.HitPartName}, dealing {damageDesc} damage! ");
        }

        if (damageResult.WasPenetrating && damageResult.TotalDamageDealt > 5)
        {
            if (damageResult.OrganHit)
            {
                sb.Append($"The attack penetrates deep, striking the {damageResult.OrganHitName}! ");
            }
            else if (damageResult.TissuesDamaged.Any(t => t.TissueName == "Muscle"))
            {
                sb.Append("The attack tears through muscle tissue! ");
            }
        }

        if (damageResult.HitPartHealthAfter < 0.9)
        {
            string statusDesc = DescribeTargetStatus(damageResult.HitPartName, damageResult.HitPartHealthAfter);
            if (!string.IsNullOrEmpty(statusDesc))
            {
                sb.Append(statusDesc);
            }
        }

        sb.Append($"({Math.Round(damageResult.TotalDamageDealt, 1)})");

        return sb.ToString();
    }

    private static string GetAttackVerb(DamageType damageType)
    {
        if (AttackVerbs.TryGetValue(damageType, out var verbs))
            return verbs[Utils.RandInt(0, verbs.Count - 1)];
        return "strikes";
    }

    private static string GetDamageSeverity(double damage)
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

    public static string DescribeTargetStatus(string partName, double healthPercent)
    {
        return healthPercent switch
        {
            <= 0 => $"The {partName} is completely destroyed!",
            < 0.15 => $"The {partName} is maimed and barely functioning!",
            < 0.35 => $"The {partName} is gravely injured!",
            < 0.6 => $"The {partName} is wounded.",
            < 0.9 => $"The {partName} is lightly injured.",
            _ => ""
        };
    }

    public static string DescribeCombatStart(Actor player, Actor enemy)
    {
        List<string> possibleStarts =
        [
            $"A {enemy.Name} emerges, ready to attack!",
            $"A {enemy.Name} appears and prepares to attack!",
            $"You find yourself face to face with a {enemy.Name}!",
            $"A hostile {enemy.Name} blocks your path!",
            $"A {enemy.Name} lunges towards you suddenly!"
        ];

        if (enemy.Name.Contains("Wolf"))
            possibleStarts.Add("You hear growling as a wolf steps out from the shadows, fangs bared!");
        else if (enemy.Name.Contains("Bear"))
            possibleStarts.Add("A massive bear rears up on its hind legs, letting out a terrifying roar!");
        else if (enemy.Name.Contains("Snake"))
            possibleStarts.Add("A venomous snake rises up, hood flared, ready to strike!");

        return possibleStarts[Utils.RandInt(0, possibleStarts.Count - 1)];
    }
}
