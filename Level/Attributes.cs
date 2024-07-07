namespace text_survival.Level
{
    public class Attributes
    {
        public enum PrimaryAttributes
        {
            Strength,
            Speed,
            Endurance,
            Luck
        }
        // base
        private double _baseStrength;
        private double _baseSpeed;
        private double _baseEndurance;
        private double _baseLuck;

        // buffs
        private double StrengthBuff { get; set; }
        private double SpeedBuff { get; set; }
        private double EnduranceBuff { get; set; }
        private double LuckBuff { get; set; }

        // total
        public double Strength => _baseStrength + StrengthBuff;
        public double Speed => _baseSpeed + SpeedBuff;
        public double Endurance => _baseEndurance + EnduranceBuff;
        public double Luck => _baseLuck + LuckBuff;


        public Attributes(int STR = 40, int SPD = 40, int END = 40, int LUC = 50)
        {
            _baseStrength = STR;
            _baseSpeed = SPD;
            _baseEndurance = END;
            _baseLuck = LUC;
        }

        public void IncreaseBase(PrimaryAttributes primaryAttribute, int amount)
        {
            switch (primaryAttribute)
            {
                case PrimaryAttributes.Strength:
                    _baseStrength += amount;
                    break;
                case PrimaryAttributes.Speed:
                    _baseSpeed += amount;
                    break;
                case PrimaryAttributes.Endurance:
                    _baseEndurance += amount;
                    break;
                case PrimaryAttributes.Luck:
                    _baseLuck += amount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(primaryAttribute), primaryAttribute, null);
            }
        }


    }
}
