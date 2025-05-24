using text_survival;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

namespace text_survival
{
    public static class Combat
    {
        public static void CombatLoop(Player player, Actor enemy)
        {
            // Dramatic combat intro
            Output.WriteLine("!");
            Output.WriteLine(CombatNarrator.DescribeCombatStart(player, enemy));

            player.IsEngaged = true;
            enemy.IsEngaged = true;

            // First strike determination
            bool enemyFirstStrike = enemy.Body.CalculateSpeed() > player.Body.CalculateSpeed();

            if (enemyFirstStrike)
            {
                Output.WriteLine($"The {enemy.Name} moves with surprising speed!");
                Thread.Sleep(500);
                enemy.Attack(player);
            }
            else
            {
                Output.WriteLine("You're quick to react, giving you the initiative!");
                Thread.Sleep(500);
            }

            // Combat loop
            while (player.IsAlive && enemy.IsAlive)
            {
                if (!player.IsEngaged || !player.IsAlive) break;
                PlayerTurn(player, enemy);

                if (!enemy.IsEngaged || !enemy.IsAlive) break;
                Thread.Sleep(500); // Pause before enemy attack
                enemy.Attack(player);

                World.Update(1);
            }

            player.IsEngaged = false;
            enemy.IsEngaged = false;

            // Combat end
            if (!player.IsAlive)
            {
                Output.WriteDanger("Your vision fades to black as you collapse... You have died!");
            }
            else if (!enemy.IsAlive)
            {
                string[] victoryMessages = {
                    $"The {enemy.Name} collapses, defeated!",
                    $"You stand victorious over the fallen {enemy.Name}!",
                    $"With a final blow, you bring down the {enemy.Name}!"
                };
                Output.WriteLine(victoryMessages[Utils.RandInt(0, victoryMessages.Length - 1)]);

                // Calculate experience based on enemy difficulty
                int xpGain = CalculateExperienceGain(enemy);
                Output.WriteLine($"You've gained {xpGain} fighting experience!");
                player._skillRegistry.AddExperience("Fighting", xpGain);
            }
        }

        private static int CalculateExperienceGain(Actor enemy)
        {
            // Base XP
            int baseXP = 5;

            // Adjust based on enemy weight/size
            double sizeMultiplier = Math.Clamp(enemy.Body.Weight / 50, 0.5, 3.0);

            // Adjust based on enemy weapon damage
            double weaponMultiplier = Math.Clamp(enemy.ActiveWeapon.Damage / 8, 0.5, 2.0);

            return (int)(baseXP * sizeMultiplier * weaponMultiplier);
        }


        public static void PlayerTurn(Player player, Actor enemy)
        {
            Output.WriteLine("─────────────────────────────────────");

            // Display combat status
            DisplayCombatStatus(player, enemy);

            Output.WriteLine("What do you want to do?");

            List<string> options = ["Attack", "Target Attack", "Cast Spell", "Flee"];

            string? choice = Input.GetSelectionFromList(options);

            switch (choice)
            {
                case "Attack":
                    player.Attack(enemy);
                    break;
                case "Target Attack":
                    string? targetPart = SelectTargetPart(enemy, player._skillRegistry.GetLevel("Fighting"));
                    if (targetPart != null)
                    {
                        player.Attack(enemy, targetPart);
                    }
                    else
                    {
                        // Player changed their mind
                        PlayerTurn(player, enemy);
                    }
                    break;
                case "Cast Spell":
                    player.SelectSpell();
                    break;
                case "Flee":
                    if (SpeedCheck(player, enemy))
                    {
                        Output.WriteLine("You got away!");
                        enemy.IsEngaged = false;
                        player.IsEngaged = false;
                    }
                    else
                    {
                        Output.WriteLine("You weren't fast enough to get away from ", enemy, "!");
                        player._skillRegistry.AddExperience("Agility", 1); // XP for flee attempt
                    }
                    break;
                default:
                    throw new InvalidOperationException("Invalid Selection");
            }
        }

        private static string? SelectTargetPart(Actor enemy, int depth)
        {
            if (depth <= 0)
            {
                Output.WriteWarning("You don't have enough skill to target an attack");
                return null;
            }
            Output.WriteLine($"Where do you want to target your attack on the {enemy.Name}?");

            // Group body parts by region for better organization
            var allParts = enemy.Body.GetPartsToNDepth(depth)!;

            BodyPart? choice = Input.GetSelectionFromList(allParts, true);
            if (choice == null)
                return null;

            // todo return part itself
            return choice.Name;
        }

        public static void DisplayCombatStatus(Player player, Actor enemy)
        {
            ConsoleColor oldColor = Console.ForegroundColor;

            // Player status
            Console.ForegroundColor = GetHealthColor(player.Body.Health / player.Body.MaxHealth);
            Output.WriteLine($"You: {Math.Round(player.Body.Health, 0)}/{Math.Round(player.Body.MaxHealth, 0)} HP");

            // Enemy status
            Console.ForegroundColor = GetHealthColor(enemy.Body.Health / enemy.Body.MaxHealth);
            Output.WriteLine($"{enemy.Name}: {Math.Round(enemy.Body.Health, 0)}/{Math.Round(enemy.Body.MaxHealth, 0)} HP");

            Console.ForegroundColor = oldColor;
        }

        private static ConsoleColor GetHealthColor(double healthPercentage)
        {
            if (healthPercentage < 0.2) return ConsoleColor.Red;
            if (healthPercentage < 0.5) return ConsoleColor.Yellow;
            return ConsoleColor.Green;
        }
        public static bool SpeedCheck(Player player, Actor? enemy = null)
        {
            if (player.CurrentLocation.Npcs.Count == 0) return true;

            enemy ??= GetFastestNpc(player.CurrentLocation);

            double playerCheck = CalcSpeedCheck(player);
            double enemyCheck = CalcSpeedCheck(enemy);

            return playerCheck >= enemyCheck;
        }

        public static Npc GetFastestNpc(Location location)
        {
            double enemyCheck = 0;
            Npc fastestNpc = location.Npcs.First();
            foreach (Npc npc in location.Npcs)
            {
                if (npc == fastestNpc) continue;
                if (!npc.IsAlive) continue;
                var currentNpcCheck = CalcSpeedCheck(npc);
                if (currentNpcCheck < enemyCheck) continue;
                fastestNpc = npc;
                enemyCheck = currentNpcCheck;
            }
            return fastestNpc;
        }

        public static double CalcSpeedCheck(Actor actor)
        {
            double athleticsBonus = actor._skillRegistry.GetLevel("Agility");
            return actor.Body.CalculateSpeed() + athleticsBonus;
        }
    }
}
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