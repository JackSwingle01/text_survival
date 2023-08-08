namespace text_survival
{
    public class Exhaustion
    {
        public float Rate = (480F / (24F * 60F)); // minutes per minute (8 hours per 24)
        public float Max = 480.0F; // minutes (8 hours)
        public float Amount { get; set; }
        private Player Player { get; set; }
        public Exhaustion(Player player)
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