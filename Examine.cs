using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.Items;

namespace text_survival
{
    public static class Examine
    {
        public static void ExamineCombatStats(Player player)
        {
            Utils.WriteLine("Strength: ", player.Strength, " (base: ", player.BaseStrength, ", gear: ", player.GearStrength, ")\n",
                "Defense: ", player.Defense, " (base: ", player.BaseDefense, ", gear: ", player.GearDefense, ")\n",
                "Speed: ", player.Speed, " (base: ", player.BaseSpeed, ", gear: ", player.GearSpeed, ")");
        }
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
            Utils.WriteLine("Hunger: ", (int)((player.Hunger.Amount / player.Hunger.MaxHunger) * 100), "%");
            Utils.WriteLine("Thirst: ", (int)((player.Thirst.Amount / player.Thirst.MaxThirst) * 100), "%");
            Utils.WriteLine("Exhaustion: ", (int)((player.Exhaustion.Amount / player.Exhaustion.MaxExhaustion) * 100), "%");
            Utils.WriteLine("Body Temperature: ", Math.Round(player.Temperature.BodyTemperature, 1), "°F (", player.Temperature.BodyTemperature, ")");
        }
    }
}
