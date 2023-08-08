using text_survival.Actors;
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
            foreach (EquipableItem item in player.Gear)
            {
                Utils.Write(item.EquipSpot, " => ");
                item.Write();
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

        public static void ExamineNpcCombatStats(Npc c)
        {
            Utils.Write(c, " => ",
                "HP: ", Math.Round(c.Health, 2), "/", c.MaxHealth,
                "\nStr: ", c.Strength,
                ", Def: ", c.Defense,
                ", Spd: ", c.Speed, "\n");
        }
        public static void ExaminePlayerCombatStats(Player c)
        {
            Utils.Write(c, " => ",
                "HP: ", Math.Round(c.Health, 2), "/", c.MaxHealth,
                "\nStr: ", c.Strength,
                ", Def: ", c.Defense,
                ", Spd: ", c.Speed, "\n");
        }
    }
}
