using text_survival.Actors;
using text_survival.Items;

namespace text_survival
{
    public static class Examine
    {
        public static void ExamineCombatant(ICombatant c)
        {
            Output.WriteLine(c, " => HP: ", c.Health, "/", c.MaxHealth);

        }
        public static void ExamineGear(Player player)
        {
            if (player.IsArmed)
            {
                Output.Write("Weapon => ");
                ExamineItem(player.Weapon);
            }
            foreach (Armor armor in player.Armor)
            {
                Output.Write(armor.EquipSpot, " => ");
                ExamineItem(armor);
            }
            if (player.HeldItem is not null)
            {
                Output.Write("Held Item => ");
                ExamineItem(player.HeldItem);
            }
        }
        public static void ExamineSurvivalStats(Player player)
        {
            Output.WriteLine("Health: ", (int)(player.Health), "%");
            Output.WriteLine("Hunger: ", player.HungerPercent, "%");
            Output.WriteLine("Thirst: ", player.ThirstPercent, "%");
            Output.WriteLine("Exhaustion: ", player.ExhaustionPercent, "%");
            Output.WriteLine("Body Temperature: ", player.Temperature, "°F (", player.TemperatureStatus, ")");
        }
        public static void ExamineItem(Item item)
        {
            Output.Write(item, " => ", item.Description, " ");
            if (item is Weapon weapon)
            {
                Output.Write("Damage: ", weapon.Damage, ", ");
                Output.Write("Accuracy: ", weapon.Accuracy * 100, ", ");
                if (weapon.BlockChance != 0)
                {
                    Output.Write(", BlockChance: ", weapon.BlockChance * 100, ", ");
                }
            }
            else if (item is Armor armor)
            {
                if (armor.Rating != 0)
                    Output.Write("Defense: ", armor.Rating * 100, ", ");

                if (armor.Warmth != 0)
                    Output.Write("Warmth: ", armor.Warmth, ", ");
            }

            if (item.Weight != 0)
            {
                Output.Write("Weight: ", item.Weight);
            }
            Output.WriteLine();
        }

        public static void ExamineLevel(Player player)
        {
            Output.WriteLine("Level: ", player.Level);
            Output.WriteLine("XP: ", player.Experience, "/", player.ExperienceToNextLevel);
        }
        public static void ExaminePrimaryAttributes(Player player)
        {
            Output.WriteLine("Primary Attributes: ");
            Output.WriteLine("STR: ", player.Attributes.Strength);
            Output.WriteLine("INT: ", player.Attributes.Intelligence);
            Output.WriteLine("WIL: ", player.Attributes.Willpower);
            Output.WriteLine("AGI: ", player.Attributes.Agility);
            Output.WriteLine("SPD: ", player.Attributes.Speed);
            Output.WriteLine("END: ", player.Attributes.Endurance);
            Output.WriteLine("PER: ", player.Attributes.Personality);
            Output.WriteLine("LUC: ", player.Attributes.Luck);
        }

        public static void ExamineSecondaryAttributes(Player player)
        {
            Output.WriteLine("Secondary Attributes: ");
            Output.WriteLine("Max HP: ", player.MaxHealth);
            Output.WriteLine("Max Energy: ", player.MaxEnergy);
            Output.WriteLine("Max Psych: ", player.MaxPsych);
        }

        public static void ExamineSkills(Player player)
        {
            Output.WriteLine("Skills: ");
            foreach (var skill in player.Skills.All)
            {
                Output.WriteLine(skill.Type, ": ", skill.Level, " (", skill.Xp, "/", skill.LevelUpThreshold, ")");
            }
        }

    }
}
