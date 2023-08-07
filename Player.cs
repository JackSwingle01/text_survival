using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Level;
namespace text_survival
{
    public class Player : IActor
    {
        private const float HungerRate = (2500F / (24F * 60F)); // calories per minute
        private const float ThirstRate = (4000F / (24F * 60F)); // mL per minute
        private const float ExhaustionRate = (480F / (24F * 60F)); // minutes per minute (8 hours per 24)

        private const float MaxHunger = 3000.0F; // calories
        private const float MaxThirst = 3000.0F; // mL
        private const float MaxExhaustion = 480.0F; // minutes (8 hours)

        public string Name { get; set; }
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public float Health { get; set; }
        public float Exhaustion { get; private set; }
        public float BodyTemperature { get; private set; }
        public TemperatureEnum TemperatureEffect { get; private set; }
        public Area CurrentArea { get; set; }

        public float WarmthBonus { get; set; }
        public float MaxHealth { get; set; }
        public Container Inventory { get; set; }
        private float BaseStrength => Strength - GearStrength;
        private float BaseDefense => Defense - GearDefense;
        private int BaseSpeed => Speed - GearSpeed;
        private float GearStrength => Gear.Sum(g => g.Strength);
        private float GearDefense => Gear.Sum(g => g.Defense);
        private int GearSpeed => Gear.Sum(g => g.Speed);
        public float Strength { get; set; }
        public float Defense { get; set; }
        public int Speed { get; set; }
        public List<EquipableItem> Gear { get; set; }

        public Level.Skills Skills { get; set; }

        public Player(Area area)
        {
            Name = "Player";
            Hunger = 0;
            Thirst = 0;
            MaxHealth = 100;
            Health = MaxHealth;
            Exhaustion = 0;
            BodyTemperature = 98.6F;
            Inventory = new Container("Backpack", 10);
            Strength = 10;
            Defense = 10;
            Speed = 10;
            Gear = new List<EquipableItem>();
            ItemFactory.MakeClothShirt().EquipTo(this);
            ItemFactory.MakeClothPants().EquipTo(this);
            ItemFactory.MakeBoots().EquipTo(this);
            EventAggregator.Subscribe<SkillLevelUpEvent>(OnSkillLeveledUp);
            CurrentArea = area;
            area.Enter(this);
            Skills = new Skills();

        }

        public void Attack(IActor target)
        {
            float damage = Combat.CalcDamage(this, target);
            if (Combat.DetermineDodge(this, target))
            {
                Utils.Write(target, " dodged the attack!\n");
                return;
            }
            Thread.Sleep(1000);
            Utils.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");
            EventAggregator.Publish(new GainExperienceEvent(1, SkillType.Strength));
            target.Damage(damage);
            Thread.Sleep(1000);
        }
        private void OnSkillLeveledUp(SkillLevelUpEvent e)
        {
            switch (e.Skill.Type)
            {
                case SkillType.Strength:
                    Strength += 1;
                    break;
                case SkillType.Defense:
                    Defense += 1;
                    break;
                case SkillType.Speed:
                    Speed += 1;
                    break;
                default:
                    Utils.WriteWarning("Error skill not found.");
                    break;
            }
        }

        public void WriteSurvivalStats()
        {
            Utils.Write("Health: ", (int)(Health), "%\n",
                "Hunger: ", (int)((Hunger / MaxHunger) * 100), "%\n",
                "Thirst: ", (int)((Thirst / MaxThirst) * 100), "%\n",
                "Exhaustion: ", (int)((Exhaustion / MaxExhaustion) * 100), "%\n",
                "Body Temperature: ", Math.Round(BodyTemperature, 1), "°F\n");
        }

        public void WriteCombatStats()
        {
            Utils.WriteLine("Strength: ", Strength, " (base: ", BaseStrength, ", gear: ", GearStrength, ")\n",
                               "Defense: ", Defense, " (base: ", BaseDefense, ", gear: ", GearDefense, ")\n",
                               "Speed: ", Speed, " (base: ", BaseSpeed, ", gear: ", GearSpeed, ")");
        }

        public void WriteEquippedItems()
        {
            foreach (EquipableItem item in Gear)
            {
                Utils.Write(item.EquipSpot, " => ");
                item.Write();
            }
        }

        public void Eat(FoodItem food)
        {
            Utils.Write("You eat the ", food, ".\n");
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
                if (!(Exhaustion <= 0)) continue;
                Utils.Write("You wake up feeling refreshed.\n");
                Heal(i / 6);
                return;
            }
            Heal(minutes / 6);
        }
        private void SleepTick()
        {
            Exhaustion -= 1 + ExhaustionRate; // 1 minute plus negate exhaustion rate for update
            Update(1);
        }

        public void Damage(float damage)
        {
            Health -= damage;
            if (!(Health <= 0)) return;
            Utils.WriteLine("You died!");
            Health = 0;
            // end program
            Environment.Exit(0);
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
            for (int i = 0; i < minutes; i++)
            {
                UpdateThirstTick();
                UpdateHungerTick();
                UpdateExhaustionTick();
            }
            UpdateTemperature(minutes);
        }




        private void UpdateHungerTick()
        {
            Hunger += HungerRate;
            if (!(Hunger >= MaxHunger)) return;
            Hunger = MaxHunger;
            this.Damage(1);

        }
        private void UpdateThirstTick()
        {
            Thirst += ThirstRate;
            if (!(Thirst >= MaxThirst)) return;
            Thirst = MaxThirst;
            this.Damage(1);
        }


        private void UpdateExhaustionTick()
        {
            Exhaustion += ExhaustionRate;

            if (!(Exhaustion >= MaxExhaustion)) return;
            Exhaustion = MaxExhaustion;
            this.Damage(1);
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
            if (BodyTemperature >= 97.7 && BodyTemperature <= 99.5)
            {
                // Normal body temperature, no effects
                TemperatureEffect = TemperatureEnum.Warm;
            }
            else if (BodyTemperature >= 95.0 && BodyTemperature < 97.7)
            {
                // Mild hypothermia effects
                TemperatureEffect = TemperatureEnum.Cool;
            }
            else if (BodyTemperature >= 89.6 && BodyTemperature < 95.0)
            {
                // Moderate hypothermia effects
                TemperatureEffect = TemperatureEnum.Cold;
            }
            else if (BodyTemperature < 89.6)
            {
                // Severe hypothermia effects
                TemperatureEffect = TemperatureEnum.Freezing;
            }
            else if (BodyTemperature > 99.5 && BodyTemperature <= 104.0)
            {
                // Heat exhaustion effects
                TemperatureEffect = TemperatureEnum.Hot;
            }
            else if (BodyTemperature > 104.0)
            {
                // Heat stroke effects
                TemperatureEffect = TemperatureEnum.HeatExhaustion;
            }
        }

        public static void WriteTemperatureEffectMessage(TemperatureEnum tempEnum)
        {
            switch (tempEnum)
            {
                case TemperatureEnum.Warm:
                    Utils.WriteLine("You feel normal.");
                    break;
                case TemperatureEnum.Cool:
                    Utils.WriteWarning("You feel cool.");
                    break;
                case TemperatureEnum.Cold:
                    Utils.WriteWarning("You feel cold.");
                    break;
                case TemperatureEnum.Freezing:
                    Utils.WriteDanger("You are freezing cold.");
                    break;
                case TemperatureEnum.Hot:
                    Utils.WriteWarning("You feel hot.");
                    break;
                case TemperatureEnum.HeatExhaustion:
                    Utils.WriteDanger("You are burning up.");
                    break;
                default:
                    Utils.WriteDanger("Error: Temperature effect not found.");
                    break;
            }
        }
        private void UpdateTemperatureTick()
        {
            // body heats based on calories burned
            if (BodyTemperature < 98.6)
            {
                float joulesBurned = Physics.CaloriesToJoules(HungerRate);
                float specificHeatOfHuman = 3500F;
                float weight = 70F;
                float tempChangeCelsius = Physics.TempChange(weight, specificHeatOfHuman, joulesBurned);
                BodyTemperature += Physics.DeltaCelsiusToDeltaFahrenheit(tempChangeCelsius);
            }
            float skinTemp = BodyTemperature - 8.4F;
            float rate = 1F / 100F;
            float feelsLike = CurrentArea.GetTemperature();
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
