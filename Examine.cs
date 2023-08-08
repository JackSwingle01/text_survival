using text_survival.Items;

namespace text_survival
{
    public static class Examine
    {
        //public static void ExamineCombatStats(Player player)
        //{
        //    Utils.WriteLine("Strength: ", player.Strength, " (base: ", player.BaseStrength, ", gear: ", player.GearStrength, ")\n",
        //        "Defense: ", player.Defense, " (base: ", player.BaseDefense, ", gear: ", player.GearDefense, ")\n",
        //        "Speed: ", player.Speed, " (base: ", player.BaseSpeed, ", gear: ", player.GearSpeed, ")");
        //}
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
            Utils.WriteLine("Body Temperature: ", Math.Round(player.Temperature.BodyTemperature, 1), "°F (", player.Temperature.BodyTemperature, ")");
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
    }
}
