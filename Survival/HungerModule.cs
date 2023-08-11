namespace text_survival.Survival
{
    public class HungerModule
    {
        public float Rate = 2500F / (24F * 60F); // calories per minute
        public float Max = 3000.0F; // calories
        public float Amount { get; set; }
        private Player Player { get; set; }

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
