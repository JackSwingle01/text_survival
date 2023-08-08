using text_survival.Actors;
using text_survival.Items;

namespace text_survival
{
    public static class Examine
    {
        public static void ExamineCombatant(ICombatant c)
        {
            Utils.WriteLine(c, " => HP: ", c.Health, "/", c.MaxHealth);

        }
        public static void ExamineGear(Player player)
        {
            if (player.Weapon is not null)
            {
                Utils.Write("Weapon => ");
                ExamineItem(player.Weapon);
            }
            foreach (Armor armor in player.Armor)
            {
                Utils.Write(armor.EquipSpot, " => ");
                ExamineItem(armor);
            }

            if (player.HeldItem is not null)
            {
                Utils.Write("Held Item => ");
                ExamineItem(player.HeldItem);
            }
        }
        public static void ExamineSurvivalStats(Player player)
        {
            Utils.WriteLine("Health: ", (int)(player.Health), "%");
            Utils.WriteLine("Hunger: ", (int)((player.Hunger.Amount / player.Hunger.Max) * 100), "%");
            Utils.WriteLine("Thirst: ", (int)((player.Thirst.Amount / player.Thirst.Max) * 100), "%");
            Utils.WriteLine("Exhaustion: ", (int)((player.Exhaustion.Amount / player.Exhaustion.Max) * 100), "%");
            Utils.WriteLine("Body Temperature: ", Math.Round(player.Temperature.BodyTemperature, 1), "°F (", player.Temperature.TemperatureEffect, ")");
        }
        public static void ExamineItem(Item item)
        {
            Utils.Write(item, " => ");
            Utils.Write("Weight: ", item.Weight);
            if (item is Weapon weapon)
            {
                Utils.Write(", Damage: ", weapon.Damage);
                Utils.Write(", Accuracy: ", weapon.Accuracy);
                if (weapon.BlockChance != 0)
                {
                    Utils.Write(", BlockChance: ", weapon.BlockChance);
                }
            }
            else if (item is Armor armor)
            {
                if (armor.Rating != 0)
                    Utils.Write(", Defense: ", armor.Rating);

                if (armor.Warmth != 0)
                    Utils.Write(", Warmth: ", armor.Warmth);
            }
            Utils.WriteLine();
            //Utils.Write(", Strength: ", item.Strength);
            //Utils.Write(", Defense: ", Defense);
            //Utils.Write(", Speed: ", Speed);
            //Utils.Write(", Warmth: ", Warmth, "\n");
        }

        public static void ExamineLevel(Player player)
        {
            Utils.WriteLine("Level: ", player.Level);
            Utils.WriteLine("XP: ", player.Experience, "/", player.ExperienceToNextLevel);
        }
        public static void ExaminePrimaryAttributes(Player player)
        {
            Utils.WriteLine("Primary Attributes: ");
            Utils.WriteLine("STR: ", player.Attributes.Strength);
            Utils.WriteLine("INT: ", player.Attributes.Intelligence);
            Utils.WriteLine("WIL: ", player.Attributes.Willpower);
            Utils.WriteLine("AGI: ", player.Attributes.Agility);
            Utils.WriteLine("SPD: ", player.Attributes.Speed);
            Utils.WriteLine("END: ", player.Attributes.Endurance);
            Utils.WriteLine("PER: ", player.Attributes.Personality);
            Utils.WriteLine("LUC: ", player.Attributes.Luck);
        }

        public static void ExamineSecondaryAttributes(Player player)
        {
            Utils.WriteLine("Secondary Attributes: ");
            Utils.WriteLine("Max HP: ", player.MaxHealth);
            Utils.WriteLine("Max Energy: ", player.MaxEnergy);
            Utils.WriteLine("Max Psych: ", player.MaxPsych);
        }

        public static void ExamineSkills(Player player)
        {
            Utils.WriteLine("Skills: ");
            foreach (var skill in player.Skills.All)
            {
                Utils.WriteLine(skill.Type, ": ", skill.Level, " (", skill.Xp, "/", skill.LevelUpThreshold, ")");
            }
        }

    }
}
