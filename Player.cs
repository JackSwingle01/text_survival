using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Level;
namespace text_survival
{
    public class Player : ICombatant
    {
        public string Name { get; set; }
        // Health
        public float Health { get; set; }
        public float MaxHealth { get; set; }

        // Survival stats
        public Hunger Hunger { get; set; }
        public Thirst Thirst { get; set; }
        public Exhaustion Exhaustion { get; set; }
        public Temperature Temperature { get; set; }
        public float WarmthBonus { get; set; }

        // area
        public Area CurrentArea { get; set; }

        // inventory
        public Container Inventory { get; set; }
        public List<EquipableItem> Gear { get; set; }

        // skills
        public Level.Skills Skills { get; set; }

        // stats
        public float Strength { get; set; }
        public float Defense => Gear.Sum(g => g.Defense);
        public float Speed { get; set; }
        

        public Player(Area area)
        {
            Name = "Player";
            Hunger = new Hunger(this);
            Thirst = new Thirst(this);
            Exhaustion = new Exhaustion(this);
            MaxHealth = 100;
            Health = MaxHealth;
            Temperature = new Temperature(this);
            Inventory = new Container("Backpack", 10);
            Strength = 5;
            Speed = 10;
            Gear = new List<EquipableItem>();
            this.Equip(ItemFactory.MakeClothShirt());
            this.Equip(ItemFactory.MakeClothPants());
            this.Equip(ItemFactory.MakeBoots());
            EventAggregator.Subscribe<SkillLevelUpEvent>(OnSkillLeveledUp);
            CurrentArea = area;
            area.Enter(this);
            Skills = new Skills();
            Weapon weapon = Weapon.GenerateRandomWeapon();
            this.Equip(weapon);
        }


        // ACTIONS //

        public void Eat(FoodItem food)
        {
            Utils.Write("You eat the ", food, ".\n");
            if (Hunger.Amount - food.Calories < 0)
            {
                Utils.Write("You are too full to finish it.\n");
                food.Calories -= (int)(0 - Hunger.Amount);
                Hunger.Amount = 0;
                return;
            }
            Hunger.Amount -= food.Calories;
            Thirst.Amount -= food.WaterContent;
            Inventory.Remove(food);
            Update(1);
        }

        public void Sleep(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                Exhaustion.Amount -= 1 + Exhaustion.Rate; // 1 minute plus negate exhaustion rate for update
                Update(1);
                if (!(Exhaustion.Amount <= 0)) continue;
                Utils.Write("You wake up feeling refreshed.\n");
                Heal(i / 6);
                return;
            }
            Heal(minutes / 6);
        }

        public void Equip(EquipableItem item)
        {
            var oldItem = this.Gear.FirstOrDefault(i => i.EquipSpot == item.EquipSpot);
            if (oldItem != null)
            {
                this.Unequip(oldItem);
            }
            this.Gear.Add(item);
            this.Inventory.Remove(item);
        }


        public void Unequip(EquipableItem item)
        {
            this.Gear.Remove(item);
            this.Inventory.Add(item);
        }

        // COMBAT //

        public void Attack(ICombatant target)
        {
            // base damage - defense percentage
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

        // UPDATE //

        public void Update(int minutes)
        {
            World.Update(minutes);
            for (int i = 0; i < minutes; i++)
            {
                Hunger.Update();
                Thirst.Update();
                Exhaustion.Update();
            }
            Temperature.Update(minutes);
        }

        // EVENTS //

        private void OnSkillLeveledUp(SkillLevelUpEvent e)
        {
            switch (e.Skill.Type)
            {
                //case SkillType.Strength:
                //    Strength += 1;
                //    break;
                //case SkillType.Defense:
                //    Defense += 1;
                //    break;
                //case SkillType.Speed:
                //    Speed += 1;
                //    break;
                default:
                    Utils.WriteWarning("Error skill not found.");
                    break;
            }
        }

        /// OTHER ///

        public override string ToString()
        {
            return Name;
        }

        
    }
}
