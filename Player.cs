﻿namespace text_survival
{
    public class Player : IActor
    {
        private float HUNGER_RATE = (2500F / (24F * 60F)); // calories per minute
        private float THIRST_RATE = (4000F / (24F * 60F)); // mL per minute
        private float EXAUSTION_RATE = (480F / (24F * 60F)); // minutes per minute (8 hours per 24)

        private const float MAX_HUNGER = 3000.0F; // calories
        private const float MAX_THIRST = 3000.0F; // mL
        private const float MAX_EXAUSTION = 480.0F; // minutes (8 hours)

        public string Name { get; set; }
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public float Health { get; set; }
        public float Exaustion { get; private set; }
        public float BodyTemperature { get; private set; }
        public TemperatureEnum TemperatureEffect { get; private set; }
        public Place Location { get; set; }
        public float WarmthBonus { get; set; }
        public float MaxHealth { get; set; }
        public Container Inventory { get; set; }
        public float Strength { get; set; }
        public float Defense { get; set; }
        public int Speed { get; set; }
        public List<EquipableItem> EquipedItems { get; set; }


        public Player(Place location)
        {
            Name = "Player";
            Hunger = 0;
            Thirst = 0;
            MaxHealth = 100;
            Health = MaxHealth;
            Exaustion = 0;
            BodyTemperature = 98.6F;
            Location = location;
            Inventory = new Container("Backpack", 10);
            Strength = 10;
            Defense = 10;
            Speed = 10;
            EquipedItems = new List<EquipableItem>();
            ItemFactory.MakeClothShirt().EquipTo(this);
            ItemFactory.MakeClothPants().EquipTo(this);
            ItemFactory.MakeBoots().EquipTo(this);

        }
        public void WriteSurvivalStats()
        {
            Utils.Write("Health: ", (int)(Health), "%\n",
                "Hunger: ", (int)((Hunger / MAX_HUNGER) * 100), "%\n",
                "Thirst: ", (int)((Thirst / MAX_THIRST) * 100), "%\n",
                "Exaustion: ", (int)((Exaustion / MAX_EXAUSTION) * 100), "%\n",
                "Body Temperature: ", Math.Round(BodyTemperature, 1), "°F\n");
        }

        public string EquipedItemsToString()
        {
            string items = "";
            foreach (EquipableItem item in EquipedItems)
            {
                items += item.EquipSpot + ": " + item.Name + " => ";
                if (item.Strength != 0)
                {
                    items += "Str: " + item.Strength;
                }
                if (item.Defense != 0)
                {
                    items += " Def: " + item.Defense;
                }
                if (item.Speed != 0)
                {
                    items += " Spd: " + item.Speed;
                }
                if (item.Warmth != 0)
                {
                    items += " Warmth: " + item.Warmth;
                }
                items += "\n";
            }
            return items;
        }

        public void Eat(FoodItem food)
        {
            if (Hunger + food.Calories < 0)
            {
                Utils.Write("You are too full to finish it.\n");
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
                    Utils.Write("You wake up feeling refreshed.\n");
                    Heal(i / 6);
                    return;
                }
            }
            Heal(minutes / 6);
        }
        private void SleepTick()
        {
            Exaustion -= 1 + EXAUSTION_RATE; // 1 minute plus negatet exaustion rate for update
            Update(1);
        }

        public void Damage(float damage)
        {
            Health -= damage;
            if (Health <= 0)
            {
                Utils.Write("You died!\n");
                Health = 0;
                // end program
                Environment.Exit(0);
            }
        }

        public void Heal(float heal)
        {
            Health += heal;
            if (Health > MaxHealth)
            {
                Health = MaxHealth;
            }
        }

        public void Update(int minutes)
        {
            World.Update(minutes);
            UpdateStat(UpdateThirstTick, minutes);
            UpdateStat(UpdateExaustionTick, minutes);
            UpdateStat(UpdateHungerTick, minutes);
            UpdateTemperature(minutes);
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

        public enum TemperatureEnum
        {
            Warm,
            Cool,
            Cold,
            Freezing,
            Hot,
            HeatExhaustion,
        }

        private void UpdateTemperature(int minutes)
        {
            TemperatureEnum oldTemperature = TemperatureEffect;
            for (int i = 0; i < minutes; i++)
            {
                UpdateTemperatureTick();
            }
            if (oldTemperature != TemperatureEffect)
            {
                WriteTemperatureEffectMessage(TemperatureEffect);
            }

        }
        private void UpdateTemperatureEffect()
        {
            if (BodyTemperature >= 97.0 && BodyTemperature < 99.7)
            {
                // Normal body temperature, no effects
                TemperatureEffect = TemperatureEnum.Warm;
            }
            else if (BodyTemperature >= 95.0 && BodyTemperature < 97.0)
            {
                // Mild hypothermia effects
                TemperatureEffect = TemperatureEnum.Cool;
            }
            else if (BodyTemperature >= 82.4 && BodyTemperature < 95.0)
            {
                // Moderate hypothermia effects
                TemperatureEffect = TemperatureEnum.Cold;
            }
            else if (BodyTemperature < 82.4)
            {
                // Severe hypothermia effects
                TemperatureEffect = TemperatureEnum.Freezing;
            }
            else if (BodyTemperature >= 99.7 && BodyTemperature < 104.0)
            {
                //Heat exhaustion effects
                TemperatureEffect = TemperatureEnum.Hot;
            }
            else if (BodyTemperature >= 104.0)
            {
                // Heat stroke effects
                TemperatureEffect = TemperatureEnum.HeatExhaustion;
            }
        }
        public static void WriteTemperatureEffectMessage(TemperatureEnum tempEnum)
        {
            if (tempEnum == TemperatureEnum.Warm)
            {
                Utils.Write("You feel normal.");
            }
            else if (tempEnum == TemperatureEnum.Cool)
            {
                Utils.WriteWarning("You feel cool.");
            }
            else if (tempEnum == TemperatureEnum.Cold)
            {
                Utils.WriteWarning("You feel cold.");
            }
            else if (tempEnum == TemperatureEnum.Freezing)
            {
                Utils.WriteDanger("You are freezing cold.");
            }
            else if (tempEnum == TemperatureEnum.Hot)
            {
                Utils.WriteWarning("You feel hot.");
            }
            else if (tempEnum == TemperatureEnum.HeatExhaustion)
            {
                Utils.WriteDanger("You are burning up.");
            }
            else
            {
                Utils.WriteDanger("Error: Temperature effect not found.");
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
            feelsLike += WarmthBonus;
            float tempChange = (skinTemp - feelsLike) * rate;
            BodyTemperature -= tempChange;

            UpdateTemperatureEffect();

            if (BodyTemperature < 82.4)
            {
                Damage(1);
            }
            else if (BodyTemperature >= 104.0)
            {
                Damage(1);
            }
        }

        public override string ToString()
        {
            return Name;
        }




    }
}
