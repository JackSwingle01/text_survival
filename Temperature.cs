using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public class Temperature
    {
        public const float BaseBodyTemperature = 98.6F;
        public float BodyTemperature { get; private set; }
        public TemperatureEnum TemperatureEffect { get; private set; }
        private Player Player { get; set; }

        public Temperature(Player player)
        {
            Player = player;
            BodyTemperature = BaseBodyTemperature;
            TemperatureEffect = TemperatureEnum.Warm;
        }
        public enum TemperatureEnum
        {
            Warm,
            Cool,
            Cold,
            Freezing,
            Hot,
            HeatExhaustion,
        }

        public void Update(int minutes)
        {
            TemperatureEnum oldTemperature = TemperatureEffect;
            for (int i = 0; i < minutes; i++)
            {
                UpdateTemperatureTick();
            }
            if (oldTemperature != TemperatureEffect)
            {
                WriteTemperatureEffectMessage(TemperatureEffect);
            }

        }
        private void UpdateTemperatureEffect()
        {
            if (BodyTemperature >= 97.7 && BodyTemperature <= 99.5)
            {
                // Normal body temperature, no effects
                TemperatureEffect = TemperatureEnum.Warm;
            }
            else if (BodyTemperature >= 95.0 && BodyTemperature < 97.7)
            {
                // Mild hypothermia effects
                TemperatureEffect = TemperatureEnum.Cool;
            }
            else if (BodyTemperature >= 89.6 && BodyTemperature < 95.0)
            {
                // Moderate hypothermia effects
                TemperatureEffect = TemperatureEnum.Cold;
            }
            else if (BodyTemperature < 89.6)
            {
                // Severe hypothermia effects
                TemperatureEffect = TemperatureEnum.Freezing;
            }
            else if (BodyTemperature > 99.5 && BodyTemperature <= 104.0)
            {
                // Heat exhaustion effects
                TemperatureEffect = TemperatureEnum.Hot;
            }
            else if (BodyTemperature > 104.0)
            {
                // Heat stroke effects
                TemperatureEffect = TemperatureEnum.HeatExhaustion;
            }
        }

        public static void WriteTemperatureEffectMessage(TemperatureEnum tempEnum)
        {
            switch (tempEnum)
            {
                case TemperatureEnum.Warm:
                    Utils.WriteLine("You feel normal.");
                    break;
                case TemperatureEnum.Cool:
                    Utils.WriteWarning("You feel cool.");
                    break;
                case TemperatureEnum.Cold:
                    Utils.WriteWarning("You feel cold.");
                    break;
                case TemperatureEnum.Freezing:
                    Utils.WriteDanger("You are freezing cold.");
                    break;
                case TemperatureEnum.Hot:
                    Utils.WriteWarning("You feel hot.");
                    break;
                case TemperatureEnum.HeatExhaustion:
                    Utils.WriteDanger("You are burning up.");
                    break;
                default:
                    Utils.WriteDanger("Error: Temperature effect not found.");
                    break;
            }
        }
        private void UpdateTemperatureTick()
        {
            // body heats based on calories burned
            if (BodyTemperature < 98.6)
            {
                float joulesBurned = Physics.CaloriesToJoules(Player.Hunger.HungerRate);
                float specificHeatOfHuman = 3500F;
                float weight = 70F;
                float tempChangeCelsius = Physics.TempChange(weight, specificHeatOfHuman, joulesBurned);
                BodyTemperature += Physics.DeltaCelsiusToDeltaFahrenheit(tempChangeCelsius);
            }
            float skinTemp = BodyTemperature - 8.4F;
            float rate = 1F / 100F;
            float feelsLike = Player.CurrentArea.GetTemperature();
            feelsLike += Player.WarmthBonus;
            float tempChange = (skinTemp - feelsLike) * rate;
            BodyTemperature -= tempChange;

            UpdateTemperatureEffect();

            if (BodyTemperature < 82.4)
            {
                Player.Damage(1);
            }
            else if (BodyTemperature >= 104.0)
            {
                Player.Damage(1);
            }
        }
    }
}
