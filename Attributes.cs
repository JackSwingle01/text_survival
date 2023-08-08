namespace text_survival
{
    public class Attributes
    {
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


        public Attributes()
        {
            _baseStrength = 15;
            _baseIntelligence = 15;
            _baseWillpower = 15;
            _baseAgility = 15;
            _baseSpeed = 15;
            _baseEndurance = 15;
            _basePersonality = 15;
            _baseLuck = 15;
            _buffs = new List<Buff>();
        }

        public void ApplyBuff(Buff buff)
        {
            _buffs.Add(buff);
        }


    }
}
