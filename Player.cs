namespace text_survival
{
    public class Player
    {
        private float HUNGER_RATE = (2500F / (24F * 60F)); // calories per minute
        private float THIRST_RATE = (4000F / (24F * 60F)); // mL per minute
        private float EXAUSTION_RATE = (480F / (24F * 60F)); // minutes per minute (8 hours per 24)

        private const float MAX_HEALTH = 100.0F; // percent
        private const float MAX_HUNGER = 3000.0F; // calories
        private const float MAX_THIRST = 3000.0F; // mL
        private const float MAX_EXAUSTION = 480.0F; // minutes (8 hours)

        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Health { get; private set; }
        public float Exaustion { get; private set; }
        public float BodyTemperature { get; private set; }
        public Place Location { get; set; }
        public float ClothingInsulation { get; set; }
        public Container Inventory { get; set; }

        public Player(Place location)
        {
            Hunger = 0;
            Thirst = 0;
            Health = MAX_HEALTH;
            Exaustion = 0;
            BodyTemperature = 98.6F;
            Location = location;
            ClothingInsulation = 10;
            Inventory = new Container("Backpack", 10);
        }

        public string GetStats()
        {
            string stats = "";
            stats += "Health: " + (int)Health + "%";
            stats += "\n";
            stats += "Hunger: " + (int)((Hunger / MAX_HUNGER) * 100) + "%";
            stats += "\n";
            stats += "Thirst: " + (int)((Thirst / MAX_THIRST) * 100) + "%";
            stats += "\n";
            stats += "Exaustion: " + (int)((Exaustion / MAX_EXAUSTION) * 100) + "%";
            stats += "\n";
            stats += "Body Temperature: " + BodyTemperature.ToString("0.0") + "°F";
            stats += "\n";
            return stats;
        }

        public void Eat(FoodItem food)
        {
            if (Hunger + food.Calories < 0)
            {
                Utils.Write("You are too full to finish it.");
                food.Calories -= (int)(0 - Hunger);
                Hunger = 0;
                return;
            }
            Hunger -= food.Calories;
            Thirst -= food.WaterContent;
            Inventory.Remove(food);
            Update(1);
        }

        public void Sleep(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                SleepTick();
                if (Exaustion <= 0)
                {
                    Utils.Write("You wake up feeling refreshed.");
                    return;
                }
            }
        }
        private void SleepTick()
        {
            Exaustion -= 1 + EXAUSTION_RATE; // 1 minute plus negatet exaustion rate for update
            Update(1);
        }

        public void Damage(float damage)
        {
            Health -= damage;
            //Utils.Write("You took " + damage + " damage!");
            if (Health <= 0)
            {
                Utils.Write("You died!");
                Health = 0;
                // end program
                Environment.Exit(0);
            }
        }

        public void Heal(float heal)
        {
            Health += heal;
            if (Health > MAX_HEALTH)
            {
                Health = MAX_HEALTH;
            }
        }

        public void Update(int minutes)
        {
            World.Update(minutes);
            UpdateStat(UpdateThirstTick, minutes);
            UpdateStat(UpdateExaustionTick, minutes);
            UpdateStat(UpdateHungerTick, minutes);
            UpdateStat(UpdateTemperatureTick, minutes);
        }

        private void UpdateStat(Action statUpdater, int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                statUpdater.Invoke();
            }
        }


        private void UpdateHungerTick()
        {
            Hunger += HUNGER_RATE;
            if (Hunger >= MAX_HUNGER)
            {
                Hunger = MAX_HUNGER;
                this.Damage(1);
            }

        }
        private void UpdateThirstTick()
        {
            Thirst += THIRST_RATE;
            if (Thirst >= MAX_THIRST)
            {
                Thirst = MAX_THIRST;
                this.Damage(1);
            }
        }


        private void UpdateExaustionTick()
        {
            Exaustion += EXAUSTION_RATE;

            if (Exaustion >= MAX_EXAUSTION)
            {
                Exaustion = MAX_EXAUSTION;
                this.Damage(1);
            }
        }

        private void UpdateTemperature(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                UpdateTemperatureTick();
            }

            if (BodyTemperature >= 97.0 && BodyTemperature < 99.7)
            {
                // Normal body temperature, no effects
                Utils.Write("You feel warm.");
            }
            else if (BodyTemperature >= 95.0 && BodyTemperature < 97.0)
            {
                // Mild hypothermia effects
                Utils.Write("You feel cold.");
            }
            else if (BodyTemperature >= 82.4 && BodyTemperature < 95.0)
            {
                // Moderate hypothermia effects
                Utils.Write("You feel cold.");
            }
            else if (BodyTemperature < 82.4)
            {
                // Severe hypothermia effects
                Utils.Write("You are freezing cold.");
            }
            else if (BodyTemperature >= 99.7 && BodyTemperature < 104.0)
            {
                //Heat exhaustion effects
                Utils.Write("You feel hot.");
            }
            else if (BodyTemperature >= 104.0)
            {
                // Heat stroke effects
                Utils.Write("You are burning up.");
            }
        }
        private void UpdateTemperatureTick()
        {
            // body heats based on calories burned
            if (BodyTemperature < 98.6)
            {
                float joulesBurned = Physics.CaloriesToJoules(HUNGER_RATE);
                float specificHeatOfHuman = 3500F;
                float weight = 70F;
                float tempChangeCelcius = Physics.TempChange(weight, specificHeatOfHuman, joulesBurned);
                BodyTemperature += Physics.DeltaCelsiusToDeltaFahrenheit(tempChangeCelcius);
            }
            float skinTemp = BodyTemperature - 8.4F;
            float rate = 1F / 100F;
            float feelsLike = Location.GetTemperature();
            feelsLike += ClothingInsulation;
            float tempChange = (skinTemp - feelsLike) * rate;
            BodyTemperature -= tempChange;

            if (BodyTemperature < 82.4)
            {
                Damage(1);
            }
            else if (BodyTemperature >= 104.0)
            {
                Damage(1);
            }
        }






    }
}
