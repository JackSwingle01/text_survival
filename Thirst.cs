namespace text_survival
{
    public class Thirst
    {
        public float Rate = (4000F / (24F * 60F)); // mL per minute
        public float Max = 3000.0F; // mL
        public float Amount { get; set; }
        private Player Player { get; set; }
        public Thirst(Player player)
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