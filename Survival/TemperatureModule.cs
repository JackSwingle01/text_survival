namespace text_survival.Survival
{
    public class TemperatureModule
    {
        public const double BaseBodyTemperature = 98.6F;
        public double BodyTemperature { get; private set; }
        public TemperatureEnum TemperatureEffect { get; private set; }
        private Player Player { get; set; }

        public TemperatureModule(Player player)
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

        //public void Update(int minutes)
        //{
        //    TemperatureEnum oldTemperature = TemperatureEffect;
        //    for (int i = 0; i < minutes; i++)
        //    {
        //        UpdateTemperatureTick();
        //    }
        //    if (oldTemperature != TemperatureEffect)
        //    {
        //        WriteTemperatureEffectMessage(TemperatureEffect);
        //    }
        //}

        public void Update()
        {
            TemperatureEnum oldTemperature = TemperatureEffect;
            UpdateTemperatureTick();
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
        private void UpdateTemperatureTick()
        {
            BodyTemperature += .1;

            double skinTemp = BodyTemperature - 8.4;
            float rate = 1F / 120F;
            double feelsLike = Player.CurrentArea.GetTemperature();
            feelsLike += Player.WarmthBonus;
            double tempChange = (skinTemp - feelsLike) * rate;
            BodyTemperature -= tempChange;

            UpdateTemperatureEffect();

            if (BodyTemperature < 89.6)
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
