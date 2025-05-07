
using text_survival.IO;

namespace text_survival.Survival
{
    public class HungerModule
    {
        public bool IsStarving => Amount >= Max;
        private double Rate = 2500.0 / (24.0 * 60.0); // calories per minute
        private double Max = 3500; // calories
        private double Amount { get; set; }


        public HungerModule()
        {
            Amount = 0;
        }

        public void Update()
        {
            Amount += Rate;
            if (Amount >= Max)
                Amount = Max;
        }

        public void AddCalories(double calories)
        {
            Amount -= calories;
            if (Amount < 0)
            {
                Amount = 0;
            }
        }

        public void Describe()
        {
            double percent = (int)((Amount / Max) * 100);
            Output.WriteLine("Hunger: ", percent, "%");
        }
    }
}


