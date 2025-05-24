using text_survival.IO;

namespace text_survival.Survival
{
    public class ExhaustionModule
    {
        public bool IsExhausted => Amount >= Max;
        public bool IsFullyRested => Amount <= 0;
        public double ExhaustionPercent => (Amount / Max) * 100;
        private double Rate = 1;
        private double Max = 480.0F; // minutes (8 hours)
        public double Amount { get; private set; }

        public ExhaustionModule()
        {
            Amount = 0;
        }

        public void Rest(int minutes)
        {
            Amount -= minutes * Rate;
            if (Amount < 0)
            {
                Amount = 0;
            }
        }

        public void ModifyExhaustion(double minutes)
        {
            Amount += minutes;
            if (Amount > Max)
            {
                Amount = Max;
            }
        }

        public void Update()
        {
            Amount += Rate;
            if (Amount >= Max)
            {
                Amount = Max;
            }
        }

        public void Describe()
        {
            double percent = (int)((Amount / Max) * 100);
            Output.WriteLine("Exhaustion: ", percent, "%");
        }
    }
}