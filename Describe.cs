using text_survival.Actors;
using text_survival.IO;
using text_survival.Items;

namespace text_survival
{
    public static class Describe
    {
        public static void DescribeCombatant(ICombatant c)
        {
            //Output.WriteLine(c, " => HP: ", c.Health, "/", c.MaxHealth);
            Output.WriteLine(c);
        }
        public static void DescribeGear(Player player)
        {
            if (player.IsArmed)
            {
                Output.Write("Weapon => ");
                DescribeItem(player.Weapon);
            }
            foreach (Armor armor in player.Armor)
            {
                Output.Write(armor.EquipSpot, " => ");
                DescribeItem(armor);
            }
            if (player.HeldItem is not null)
            {
                Output.Write("Held Item => ");
                DescribeItem(player.HeldItem);
            }
        }
        public static void DescribeSurvivalStats(Player player)
        {
            //Output.WriteLine("Health: ", (int)(player.Health), "%");
            Output.WriteLine("Hunger: ", player.HungerPercent, "%");
            Output.WriteLine("Thirst: ", player.ThirstPercent, "%");
            Output.WriteLine("Exhaustion: ", player.ExhaustionPercent, "%");
            string tempChange = player.IsWarming ? "Warming up" : "Getting colder";
            Output.WriteLine("Body Temperature: ", player.Temperature, "°F (", player.TemperatureStatus, ")");
            Output.WriteLine("Feels like: ", player.FeelsLikeTemperature, "°F -> ", tempChange);
        }
        public static void DescribeItem(Item item)
        {
            Output.Write(item, " => ", item.Description, " ");
            if (item is Weapon weapon)
            {
                Output.Write("Damage: ", weapon.Damage, " hp, ");
                Output.Write("Hit Chance: ", weapon.Accuracy * 100, "%, ");
                if (weapon.BlockChance != 0)
                {
                    Output.Write(", BlockChance: ", weapon.BlockChance * 100, "%, ");
                }
            }
            else if (item is Armor armor)
            {
                if (armor.Rating != 0)
                    Output.Write("Defense: ", armor.Rating * 100, "%, ");

                if (armor.Warmth != 0)
                    Output.Write("Warmth: ", armor.Warmth, "F, ");
            }

            if (item.Weight != 0)
            {
                Output.Write("Weight: ", item.Weight, "kg");
            }
            Output.WriteLine();
        }

        public static void DescribeLevel(Player player)
        {
            Output.WriteLine("Level: ", player.Level);
            Output.WriteLine("XP: ", player.Experience, "/", player.ExperienceToNextLevel);
        }
        public static void DescribePrimaryAttributes(Player player)
        {
            Output.WriteLine("Primary Attributes: ");
            Output.WriteLine("STR: ", player.Attributes.Strength);
            Output.WriteLine("SPD: ", player.Attributes.Speed);
            Output.WriteLine("END: ", player.Attributes.Endurance);
            Output.WriteLine("LUC: ", player.Attributes.Luck);
        }

        public static void DescribeSecondaryAttributes(Player player)
        {
            Output.WriteLine("Secondary Attributes: ");
            Output.WriteLine("Max HP: ", player.Body.MaxHealth);
            Output.WriteLine("Max Energy: ", player.MaxEnergy);
            Output.WriteLine("Max Psych: ", player.MaxPsych);
        }

        public static void DescribeSkills(Player player)
        {
            Output.WriteLine("Skills: ");
            foreach (var skill in player.Skills.All)
            {
                Output.WriteLine(skill.Type, ": ", skill.Level, " (", skill.Xp, "/", skill.LevelUpThreshold, ")");
            }
        }

    }
}
