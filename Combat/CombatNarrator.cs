using System.Text;
using text_survival.Actors;
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

    #region Player Attack Narratives (used by CombatRunner)

    /// <summary>
    /// Describes the result of a player's attack on an animal.
    /// Merges best features from CombatRunner and CombatNarrator.
    /// </summary>
    public static string DescribePlayerAttackHit(
        string animalName,
        string weaponName,
        DamageResult result,
        DamageType damageType,
        bool isCritical)
    {
        string partName = FormatBodyPartName(result.HitPartName);
        string verb = GetAttackVerb(damageType);

        var sb = new StringBuilder();

        // Main hit description
        if (isCritical)
        {
            sb.Append($"Your {weaponName} {verb} deep into the {animalName}'s {partName}!");
        }
        else if (result.TotalDamageDealt > 0.15)
        {
            sb.Append($"Your {weaponName} {verb} the {animalName}'s {partName}.");
        }
        else if (result.TotalDamageDealt > 0.05)
        {
            sb.Append($"You land a hit on the {animalName}'s {partName}.");
        }
        else
        {
            sb.Append($"Your {weaponName} grazes the {animalName}'s {partName}.");
        }

        // Organ damage (critical info for player)
        if (result.OrganHit && result.OrganHitName != null)
        {
            sb.Append($" {result.OrganHitName} damaged!");
        }

        // Bleeding effect
        if (result.TriggeredEffects.Any(e => e.EffectKind == "Bleeding"))
        {
            sb.Append(" Blood flows from the wound.");
        }

        // Penetration detail for severe hits
        if (result.WasPenetrating && result.TotalDamageDealt > 0.1 && !result.OrganHit)
        {
            if (result.TissuesDamaged.Any(t => t.TissueName == "Muscle"))
            {
                sb.Append(" The attack tears through muscle!");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Describes a player's attack missing.
    /// </summary>
    public static string DescribePlayerMiss(string animalName, string? targetPart = null)
    {
        if (targetPart != null)
        {
            return $"Your thrust at the {animalName}'s {targetPart} misses!";
        }
        return $"Your attack misses the {animalName}.";
    }

    #endregion

    #region Animal Attack Narratives (used by CombatRunner)

    /// <summary>
    /// Describes the result of an animal's attack on the player.
    /// </summary>
    public static string DescribeAnimalAttackHit(string animalName, DamageResult damageResult)
    {
        string partName = FormatBodyPartName(damageResult.HitPartName);
        string organInfo = damageResult.OrganHit && damageResult.OrganHitName != null
            ? $" {damageResult.OrganHitName} damaged!"
            : "";

        var options = new[]
        {
            $"The {animalName}'s attack catches your {partName}!{organInfo}",
            $"The {animalName}'s jaws find your {partName}!{organInfo}",
            $"The {animalName} strikes your {partName}!{organInfo}"
        };
        return options[_rng.Next(options.Length)];
    }

    #endregion

    #region Behavior Transitions (used by CombatRunner)

    /// <summary>
    /// Describes animal behavior state transitions.
    /// </summary>
    public static string DescribeBehaviorTransition(
        string animalName,
        CombatBehavior from,
        CombatBehavior to)
    {
        return (from, to) switch
        {
            (CombatBehavior.Circling, CombatBehavior.Retreating) =>
                $"The {animalName}'s nerve wavers - it begins backing away.",

            (CombatBehavior.Threatening, CombatBehavior.Retreating) =>
                $"The {animalName} breaks off its threat display and retreats.",

            (CombatBehavior.Circling, CombatBehavior.Threatening) =>
                $"The {animalName} stops circling and moves to attack.",

            (CombatBehavior.Threatening, CombatBehavior.Attacking) =>
                $"The {animalName} commits to the attack!",

            (CombatBehavior.Circling, CombatBehavior.Approaching) =>
                $"The {animalName} begins closing the distance.",

            (CombatBehavior.Approaching, CombatBehavior.Threatening) =>
                $"The {animalName} is close enough to strike.",

            (CombatBehavior.Attacking, CombatBehavior.Recovering) =>
                $"The {animalName} is off-balance after its attack.",

            (CombatBehavior.Recovering, CombatBehavior.Circling) =>
                $"The {animalName} recovers and resumes circling.",

            (CombatBehavior.Retreating, CombatBehavior.Circling) =>
                $"The {animalName} regains its nerve and circles back.",

            (CombatBehavior.Disengaging, CombatBehavior.Retreating) =>
                $"The {animalName} breaks free and retreats.",

            _ => "" // No message for other transitions
        };
    }

    /// <summary>
    /// Describes animal disengaging from combat.
    /// </summary>
    public static string DescribeDisengage(string animalName)
    {
        var messages = new[]
        {
            $"The {animalName} sniffs at your motionless form, then loses interest and lumbers away.",
            $"Satisfied that you're no threat, the {animalName} turns and disappears.",
            $"The {animalName} gives you one last look, then wanders off.",
            $"Your stillness convinces the {animalName} the fight is over. It leaves."
        };
        return messages[_rng.Next(messages.Length)];
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Formats body part names for narrative display (e.g., "Left Arm" → "left arm").
    /// </summary>
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

        return possibleStarts[Utils.RandInt(0, possibleStarts.Count - 1)];
    }
}
