namespace text_survival
{
    public class Player
    {
        private float HUNGER_RATE = (2500F / (24F * 60F)); // calories per minute
        private float THIRST_RATE = (4000F / (24F * 60F)); // mL per minute

        private const float MAX_HEALTH = 100.0F; // percent
        private const float MAX_HUNGER = 3000.0F; // calories
        private const float MAX_THIRST = 3000.0F; // mL

        private List<Item> _inventory = new List<Item>();

        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Health { get; private set; }
        public float BodyTemperature { get; private set; }
        public Place Location { get; set; }
        public float ClothingInsulation { get; set; }

        public Player(Place location)
        {
            Hunger = MAX_HUNGER;
            Thirst = MAX_THIRST;
            Health = MAX_HEALTH;
            BodyTemperature = 98.6F;
            Location = location;
            ClothingInsulation = 10;
        }

        public List<Item> Inventory { get => _inventory; private set => _inventory = value; }


        public string GetStats()
        {
            string stats = "";
            stats += "Health: " + (int)Health + "%";
            stats += "\n";
            stats += "Hunger: " + (int)((Hunger / MAX_HUNGER) * 100) + "%";
            stats += "\n";
            stats += "Thirst: " + (int)((Thirst / MAX_THIRST) * 100) + "%";
            stats += "\n";
            stats += "Body Temperature: " + BodyTemperature + "°F";
            stats += "\n";
            return stats;
        }

        public void Eat(FoodItem food)
        {
            if (Hunger + food.Calories > MAX_HUNGER)
            {
                Utils.Write("You are too full to finish it.");
                food.Calories -= (int)(MAX_HUNGER - Hunger);
                Hunger = MAX_HUNGER;
                return;
            }
            Hunger += food.Calories;
            Thirst += food.WaterContent;
            Inventory.Remove(food);
            Update(1);
        }
        private void UpdateHungerAndThirst(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                UpdateHungerAndThirstTick();
            }
        }

        private void UpdateHungerAndThirstTick()
        {
            Hunger -= HUNGER_RATE;
            Thirst -= THIRST_RATE;
            if (Hunger <= 0)
            {
                Hunger = 0;
                this.Damage(1);
            }
            if (Thirst <= 0)
            {
                Thirst = 0;
                this.Damage(1);
            }
        }


        public void Update(int minutes)
        {
            World.Update(minutes);
            UpdateHungerAndThirst(minutes);
            UpdateTemperature(minutes);
        }

        public Item? GetItemFromInventory(int index)
        {
            if (index < 0 || index >= Inventory.Count)
            {
                return null;
            }
            return Inventory[index];
        }

        public string GetInventoryToString()
        {
            if (Inventory.Count == 0)
            {
                return "Inventory is empty!";
            }
            string str = "";
            int count = 1;
            foreach (Item item in Inventory)
            {
                str += count + ". ";
                str += item.ToString();
                str += "\n";
                count++;
            }
            return str;
        }
        public Item? OpenInventory()
        {
            Utils.Write("Inventory:");
            Utils.Write(GetInventoryToString());
            if (Inventory.Count == 0)
            {
                return null;
            }
            Utils.Write("Enter the number of the item you want to use or type 'exit' to exit");
            string? input = Console.ReadLine();
            if (input == "exit" || input == null)
            {
                return null;
            }
            int index = int.Parse(input) - 1;
            return GetItemFromInventory(index);

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
                System.Environment.Exit(0);
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
                BodyTemperature += CalcTemperatureChange(HUNGER_RATE);
            }
            float skinTemp = BodyTemperature - 8.4F;
            float rate = 1F / 100F;
            float feelsLike = Location.GetTemperature();
            feelsLike += ClothingInsulation;  
            float tempChange = (skinTemp - feelsLike) * rate;
            BodyTemperature -= tempChange;

           
            if (BodyTemperature < 82.4)
            {
                // Severe hypothermia effects
                Damage(1);
            }
            else if (BodyTemperature >= 104.0)
            {
                // Heat stroke effects
                Damage(1);
            }
        }
        private float ConvertCelsiusToFahrenheit(float celsius)
        {
            return (celsius * (9.0F / 5.0F));
        }
        private float CalcTemperatureChange(float calories)
        {
            // Q = m * c * deltaT
            // Q = calories
            // m = mass (kg)
            // c = specific heat capacity (J/kg*C)
            // deltaT = change in temperature (C)
            // 1 kcal = 4184 J
            float m = 70F; // kg
            float c = 3600.0F; // Specific heat of human body
            float Q = calories * 4184.0F; // J
            float deltaT = Q / (m * c);
            return ConvertCelsiusToFahrenheit(deltaT);
        }


    }
}
