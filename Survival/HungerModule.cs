namespace text_survival.Survival
{
    public class HungerModule
    {
        public double Rate = 2500.0 / (24.0 * 60.0); // calories per minute
        public double Max = 3000; // calories
        public double Amount { get; set; }
        private Player Player { get; }

        public HungerModule(Player player)
        {
            Amount = 0;
            Player = player;
        }

        public void Update()
        {
            Amount += Rate;
            if (!(Amount >= Max)) return;
            Amount = Max;
            Player.Damage(1);
        }

    }

}
