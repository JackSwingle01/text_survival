namespace text_survival
{
    public class Attributes
    {
        public enum PrimaryAttributes
        {
            Strength,
            Intelligence,
            Willpower,
            Agility,
            Speed,
            Endurance,
            Personality,
            Luck
        }
        // base
        private double _baseStrength;
        private double _baseIntelligence;
        private double _baseWillpower;
        private double _baseAgility;
        private double _baseSpeed;
        private double _baseEndurance;
        private double _basePersonality;
        private double _baseLuck;

        // buffs
        private List<Buff> _buffs;
        private double StrengthBuff => _buffs.Sum(b => b.Strength);
        private double IntelligenceBuff => _buffs.Sum(b => b.Intelligence);
        private double WillpowerBuff => _buffs.Sum(b => b.Willpower);
        private double AgilityBuff => _buffs.Sum(b => b.Agility);
        private double SpeedBuff => _buffs.Sum(b => b.Speed);
        private double EnduranceBuff => _buffs.Sum(b => b.Endurance);
        private double PersonalityBuff => _buffs.Sum(b => b.Personality);
        private double LuckBuff => _buffs.Sum(b => b.Luck);

        // total
        public double Strength => _baseStrength + StrengthBuff;
        public double Intelligence => _baseIntelligence + IntelligenceBuff;
        public double Willpower => _baseWillpower + WillpowerBuff;
        public double Agility => _baseAgility + AgilityBuff;
        public double Speed => _baseSpeed + SpeedBuff;
        public double Endurance => _baseEndurance + EnduranceBuff;
        public double Personality => _basePersonality + PersonalityBuff;
        public double Luck => _baseLuck + LuckBuff;


        public Attributes(int STR = 40, int INT = 40, int WIL = 40, int AGI = 40, int SPD = 40, int END = 40, int PER = 40,
            int LUC = 50)
        {
            _baseStrength = STR;
            _baseIntelligence = INT;
            _baseWillpower = WIL;
            _baseAgility = AGI;
            _baseSpeed = SPD;
            _baseEndurance = END;
            _basePersonality = PER;
            _baseLuck = LUC;
            _buffs = new List<Buff>();
        }



        public void ApplyBuff(Buff buff)
        {
            _buffs.Add(buff);
        }

        public void IncreaseBase(PrimaryAttributes primaryAttribute, int amount)
        {
            switch (primaryAttribute)
            {
                case PrimaryAttributes.Strength:
                    _baseStrength += amount;
                    break;
                case PrimaryAttributes.Intelligence:
                    _baseIntelligence += amount;
                    break;
                case PrimaryAttributes.Willpower:
                    _baseWillpower += amount;
                    break;
                case PrimaryAttributes.Agility:
                    _baseAgility += amount;
                    break;
                case PrimaryAttributes.Speed:
                    _baseSpeed += amount;
                    break;
                case PrimaryAttributes.Endurance:
                    _baseEndurance += amount;
                    break;
                case PrimaryAttributes.Personality:
                    _basePersonality += amount;
                    break;
                case PrimaryAttributes.Luck:
                    _baseLuck += amount;
                    break;
            }
        }


    }
}
