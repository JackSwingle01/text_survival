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
        public double Health { get; set; }
        public double MaxHealth { get; set; }

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
        public List<Armor> Armor { get; set; }
        public Gear HeldItem { get; set; }
        public Weapon Weapon { get; set; }

        // skills and level
        public int Level { get; set; }
        public int Experience { get; set; }
        public int ExperienceToNextLevel => (Level + 1) * 20;

        public Attributes Attributes { get; set; }
        public Skills Skills { get; set; }

        // stats
        public double ArmorRating => Armor.Sum(g => g.Rating);


        public Player(Area area)
        {
            Name = "Player";
            Attributes = new Attributes();
            Hunger = new Hunger(this);
            Thirst = new Thirst(this);
            Exhaustion = new Exhaustion(this);
            MaxHealth = 100;
            Health = MaxHealth;
            Temperature = new Temperature(this);
            Inventory = new Container("Backpack", 10);
            Armor = new List<Armor>();
            this.Equip(ItemFactory.MakeClothShirt());
            this.Equip(ItemFactory.MakeClothPants());
            this.Equip(ItemFactory.MakeBoots());
            EventAggregator.Subscribe<SkillLevelUpEvent>(OnSkillLeveledUp);
            CurrentArea = area;
            area.Enter(this);
            Skills = new Skills();
            Weapon weapon = Weapon.GenerateRandomWeapon();
            this.Equip(weapon);
            Level = 0;
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

        public void Equip(Item item)
        {
            if (item is Weapon weapon)
            {
                if (Weapon != null)
                {
                    this.Unequip(Weapon);
                }
                Weapon = weapon;
                this.Inventory.Remove(weapon);
                return;
            }
            else if (item is Armor armor)
            {
                var oldItem = this.Armor.FirstOrDefault(i => i.EquipSpot == armor.EquipSpot);
                if (oldItem != null)
                {
                    this.Unequip(oldItem);
                }
                this.Armor.Add(armor);
                this.Inventory.Remove(item);
                return;
            }
            else if (item is Gear gear)
            {
                if (HeldItem != null)
                {
                    this.Unequip(HeldItem);
                }
                HeldItem = gear;
                this.Inventory.Remove(gear);
                return;
            }
            else
            {
                Utils.WriteLine("You can't equip that.");
            }

        }


        public void Unequip(Item item)
        {
            if (item is Weapon weapon)
            {
                Weapon = null;
                this.Inventory.Add(weapon);

            }
            else if (item is Armor armor)
            {
                this.Armor.Remove(armor);
                this.Inventory.Add(armor);
            }
            else if (item is Gear gear)
            {
                HeldItem = null;
                this.Inventory.Add(gear);
            }
            else
            {
                Utils.WriteLine("You can't unequip that.");
            }

        }

        // COMBAT //

        public double DetermineDamage()
        {
            double strengthModifier = (Attributes.Strength + 75) / 100;
            double exhaustionModifier = ((Exhaustion.Amount / Exhaustion.Max) + 1) / 2; ;
            double weaponDamage = Weapon?.Damage ?? 1; // change to unarmed skill once implemented
            double damage = weaponDamage * strengthModifier * exhaustionModifier;
            damage *= Utils.RandDouble(.5, 2);
            if (damage < 0)
            {
                damage = 0;
            }
            return damage;
        }

        public void Attack(ICombatant target)
        {
            // base damage - defense percentage
            double damage = DetermineDamage();
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

        public void Damage(double damage)
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

        // Leveling //
        public void GainExperience(int xp)
        {
            Experience += xp;
            if (Experience < ExperienceToNextLevel) return;
            Experience -= ExperienceToNextLevel;
            LevelUp();
        }

        public void LevelUp()
        {
            Level++;
            Health += (Attributes.Endurance / 10);
            Utils.WriteLine("You leveled up to level ", Level, "!");
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
            GainExperience(1 * e.Skill.Level);
        }

        /// OTHER ///

        public override string ToString()
        {
            return Name;
        }


    }
}
