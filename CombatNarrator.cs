using text_survival.Actors;
using text_survival.Items;

namespace text_survival;

public static class CombatNarrator
{
    private static readonly Dictionary<WeaponClass, List<string>> AttackVerbs = new()
    {
        { WeaponClass.Blade, new List<string> { "slashes", "cuts", "slices", "carves" } },
        { WeaponClass.Blunt, new List<string> { "bashes", "strikes", "smashes", "cracks" } },
        { WeaponClass.Pierce, new List<string> { "stabs", "pierces", "impales", "punctures" } },
        { WeaponClass.Claw, new List<string> { "tears", "rips", "rends", "shreds" } },
        { WeaponClass.Unarmed, new List<string> { "punches", "strikes", "hits", "pounds" } }
    };

    private static readonly Dictionary<WeaponType, List<string>> SpecialAttackDescriptions = new()
    {
        { WeaponType.Spear, new List<string> { "lunges with", "thrusts forward with", "strikes with" } },
        { WeaponType.Claws, new List<string> { "swipes with", "tears with", "mauls with" } },
        { WeaponType.Fangs, new List<string> { "bites with", "snaps with", "sinks" } },
        { WeaponType.Knife, new List<string> { "slashes with", "jabs with", "slices with" } },
        { WeaponType.Club, new List<string> { "swings", "brings down", "thumps with" } }
    };

    public static string DescribeAttack(Actor attacker, Actor target, double damage, string targetPart, bool isHit, bool isDodged, bool isBlocked)
    {
        var sb = new System.Text.StringBuilder();

        // 1. Attack Initiation
        sb.Append(DescribeAttackAttempt(attacker, target, targetPart));

        // 2. Attack Resolution
        if (isDodged)
            sb.Append(DescribeDodge(attacker, target));
        else if (isBlocked)
            sb.Append(DescribeBlock(attacker, target));
        else if (!isHit)
            sb.Append(DescribeMiss(attacker, target));
        else
            sb.Append(DescribeHit(attacker, target, damage, targetPart));

        return sb.ToString();
    }

    private static string DescribeAttackAttempt(Actor attacker, Actor target, string targetPart)
    {
        WeaponType weaponType = attacker.ActiveWeapon.Type;
        string weaponName = attacker.ActiveWeapon.Name;

        if (attacker is Player)
        {
            // Get special description based on weapon type if available
            if (SpecialAttackDescriptions.TryGetValue(weaponType, out var specialDescs))
            {
                string desc = specialDescs[Utils.RandInt(0, specialDescs.Count - 1)];
                return $"You {desc} your {weaponName}, at {target.Name}'s {targetPart}. ";
            }
            else
            {
                return $"You attack with your {weaponName}, aiming at {target.Name}'s {targetPart}. ";
            }
        }
        else
        {
            // Enemy attacking player
            if (SpecialAttackDescriptions.TryGetValue(weaponType, out var specialDescs))
            {
                string desc = specialDescs[Utils.RandInt(0, specialDescs.Count - 1)];
                return $"The {attacker.Name} {desc} its {weaponName} at your {targetPart}. ";
            }
            else
            {
                return $"The {attacker.Name} attacks with its {weaponName}, lunging at your {targetPart}. ";
            }
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
            return $"You raise your {target.ActiveWeapon.Name} and deflect the attack!";
        else
            return $"The {target.Name} blocks your blow with its {target.ActiveWeapon.Name}!";
    }

    private static string DescribeMiss(Actor attacker, Actor target)
    {
        if (attacker is Player)
            return "Your attack misses, throwing you slightly off balance!";
        else
            return $"The {attacker.Name}'s attack whistles past you, missing entirely!";
    }

    private static string DescribeHit(Actor attacker, Actor target, double damage, string targetPart)
    {
        WeaponClass weaponClass = attacker.ActiveWeapon.Class;

        // Get appropriate attack verb based on weapon class
        string attackVerb;
        if (AttackVerbs.TryGetValue(weaponClass, out var verbs))
            attackVerb = verbs[Utils.RandInt(0, verbs.Count - 1)];
        else
            attackVerb = "strikes";

        // Describe damage severity
        string damageDesc = GetDamageSeverity(damage);

        // Describe hit
        if (attacker is Player)
        {
            return $"Your attack {attackVerb} the {target.Name}'s {targetPart}, dealing {damageDesc} damage! ({Math.Round(damage, 1)})";
        }
        else
        {
            return $"The {attacker.Name}'s attack {attackVerb} your {targetPart}, dealing {damageDesc} damage! ({Math.Round(damage, 1)})";
        }
    }

    private static string GetDamageSeverity(double damage)
    {
        if (damage <= 2) return "minimal";
        if (damage <= 5) return "light";
        if (damage <= 10) return "moderate";
        if (damage <= 15) return "severe";
        if (damage <= 25) return "critical";
        return "devastating";
    }

    public static string DescribeTargetStatus(string partName, double healthPercent)
    {
        if (healthPercent <= 0)
            return $"The {partName} is completely destroyed!";
        if (healthPercent < 0.15)
            return $"The {partName} is maimed and barely functioning!";
        if (healthPercent < 0.35)
            return $"The {partName} is gravely injured!";
        if (healthPercent < 0.6)
            return $"The {partName} is wounded.";
        if (healthPercent < 0.9)
            return $"The {partName} is lightly injured.";

        return "";
    }

    public static string DescribeCombatStart(Actor player, Actor enemy)
    {
        List<string> possibleStarts = new()
        {
            $"A {enemy.Name} emerges, its {enemy.ActiveWeapon.Name} at the ready!",
            $"A {enemy.Name} appears and prepares to attack!",
            $"You find yourself face to face with a {enemy.Name}!",
            $"A hostile {enemy.Name} blocks your path!",
            $"A {enemy.Name} lunges towards you suddenly!"
        };

        // Add special descriptions for certain enemy types
        if (enemy.Name.Contains("Wolf"))
            possibleStarts.Add("You hear growling as a wolf steps out from the shadows, fangs bared!");
        else if (enemy.Name.Contains("Bear"))
            possibleStarts.Add("A massive bear rears up on its hind legs, letting out a terrifying roar!");
        else if (enemy.Name.Contains("Snake"))
            possibleStarts.Add("A venomous snake rises up, hood flared, ready to strike!");

        return possibleStarts[Utils.RandInt(0, possibleStarts.Count - 1)];
    }
}