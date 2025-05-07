using text_survival.IO;

namespace text_survival.Survival
{
    public class ThirstModule
    {
        public bool IsParched => Amount >= Max;
        private double Rate = 4000F / (24F * 60F); // mL per minute
        private double Max = 3000.0F; // mL
        private double Amount { get; set; }
        public ThirstModule()
        {
            Amount = 0;
        }
        public void AddHydration(double mL){
            Amount -= mL;
            if (Amount < 0){
                Amount = 0;
            }
        }
        public void Update()
        {
            Amount += Rate;
            if (Amount >= Max)
                Amount = Max;
        }
        public void Describe()
        {
            double percent = (int)((Amount / Max) * 100);
            Output.WriteLine("Thirst: ", percent, "%");
        }
    }
}