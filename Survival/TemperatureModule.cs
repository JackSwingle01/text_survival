using text_survival.IO;

namespace text_survival.Survival
{
    public class TemperatureModule
    {
        public const double BaseBodyTemperature = 98.6F;
        public double BodyTemperature { get; private set; }
        public bool IsWarming { get; private set; }
        public TemperatureEnum TemperatureEffect { get; private set; }

        public bool IsDangerousTemperature { get; private set; }

        public TemperatureModule()
        {

            BodyTemperature = BaseBodyTemperature;
            TemperatureEffect = TemperatureEnum.Warm;
            IsDangerousTemperature = false;
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

        public void Update(double feelsLikeTemperature)
        {
            TemperatureEnum oldTemperature = TemperatureEffect;
            UpdateTemperatureTick(feelsLikeTemperature);
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
                    Output.WriteLine("You feel normal.");
                    break;
                case TemperatureEnum.Cool:
                    Output.WriteWarning("You feel cool.");
                    break;
                case TemperatureEnum.Cold:
                    Output.WriteWarning("You feel cold.");
                    break;
                case TemperatureEnum.Freezing:
                    Output.WriteDanger("You are freezing cold.");
                    break;
                case TemperatureEnum.Hot:
                    Output.WriteWarning("You feel hot.");
                    break;
                case TemperatureEnum.HeatExhaustion:
                    Output.WriteDanger("You are burning up.");
                    break;
                default:
                    Output.WriteDanger("Error: Temperature effect not found.");
                    break;
            }
        }
        private void UpdateTemperatureTick(double feelsLikeTemperature)
        {
            BodyTemperature += .1;

            double skinTemp = BodyTemperature - 8.4;
            float rate = 1F / 120F;

            double tempChange = (feelsLikeTemperature - skinTemp) * rate;
            BodyTemperature += tempChange;

            IsWarming = tempChange > 0;

            UpdateTemperatureEffect();

            if (BodyTemperature < 89.6)
            {
                IsDangerousTemperature = true;
            }
            else if (BodyTemperature >= 104.0)
            {
                IsDangerousTemperature = true;
            }
            else
            {
                IsDangerousTemperature = false;
            }
        }

        public void Describe()
        {
            string tempChange = IsWarming ? "Warming up" : "Getting colder";
            Output.WriteLine("Body Temperature: ", BodyTemperature, "°F (", TemperatureEffect, ")");
        }
    }
}
