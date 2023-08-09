using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Level;
using text_survival.Survival;

namespace text_survival
{
    public class Player : ICombatant
    {
        public string Name { get; set; }
        // Health and Energy
        public double Health { get; set; }
        public double MaxHealth { get; set; }
        public double Energy { get; set; }
        public double MaxEnergy { get; set; }
        public double EnergyRegen { get; set; }
        public double Psych { get; set; }
        public double MaxPsych { get; set; }
        public double PsychRegen { get; set; }


        // Survival stats
        public Hunger Hunger { get; set; }
        public Thirst Thirst { get; set; }
        public Exhaustion Exhaustion { get; set; }
        public Temperature Temperature { get; set; }
        public double WarmthBonus => Armor.Sum(a => a.Warmth);

        // area
        public Area CurrentArea { get; set; }

        // inventory
        public Container Inventory { get; set; }
        public List<Armor> Armor { get; set; }
        public Gear? HeldItem { get; set; }
        public Weapon Weapon { get; set; }
        public Weapon Unarmed { get; set; }

        // skills and level
        public int Level { get; set; }
        public int Experience { get; set; }
        public int ExperienceToNextLevel => (Level + 1) * 10;
        public int SkillPoints { get; set; }

        public Attributes Attributes { get; set; }
        public Skills Skills { get; set; }

        // stats
        public double ArmorRating
        {
            get
            {
                double rating = 0;
                foreach (Armor armor in Armor)
                {
                    rating += armor.Rating;
                    if (armor.Type == ArmorClass.Light)
                        rating += Skills.LightArmor.Level * .01;
                    else if (armor.Type == ArmorClass.Heavy)
                        rating += Skills.HeavyArmor.Level * .01;
                }
                return rating;
            }
        }


        public Player(Area area)
        {
            Name = "Player";
            Attributes = new Attributes();
            MaxEnergy = 100;
            Energy = MaxEnergy;
            EnergyRegen = 1;
            Psych = 100;
            MaxPsych = 100;
            PsychRegen = 1;
            Hunger = new Hunger(this);
            Thirst = new Thirst(this);
            Exhaustion = new Exhaustion(this);
            MaxHealth = 100;
            Health = MaxHealth;
            Temperature = new Temperature(this);
            Inventory = new Container("Backpack", 10);
            Armor = new List<Armor>();
            Equip(ItemFactory.MakeClothShirt());
            Equip(ItemFactory.MakeClothPants());
            Equip(ItemFactory.MakeBoots());
            EventAggregator.Subscribe<SkillLevelUpEvent>(OnSkillLeveledUp);
            CurrentArea = area;
            area.Enter(this);
            Skills = new Skills();
            Level = 0;
            Experience = 0;
            SkillPoints = 0;
            Unarmed = ItemFactory.MakeFists();
            Weapon = Unarmed;

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
                Unequip(Weapon);
                Weapon = weapon;
                Inventory.Remove(weapon);
                return;
            }
            else if (item is Armor armor)
            {
                var oldItem = Armor.FirstOrDefault(i => i.EquipSpot == armor.EquipSpot);
                if (oldItem != null)
                {
                    Unequip(oldItem);
                }
                Armor.Add(armor);
                Inventory.Remove(item);
                return;
            }
            else if (item is Gear gear)
            {
                if (HeldItem != null)
                {
                    Unequip(HeldItem);
                }
                HeldItem = gear;
                Inventory.Remove(gear);
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
                Weapon = Unarmed;
                if (weapon != Unarmed)
                    Inventory.Add(weapon);
            }
            else if (item is Armor armor)
            {
                Armor.Remove(armor);
                Inventory.Add(armor);
            }
            else if (item is Gear gear)
            {
                HeldItem = null;
                Inventory.Add(gear);
            }
            else
            {
                Utils.WriteLine("You can't unequip that.");
            }

        }

        // COMBAT //

        public double DetermineDamage(ICombatant defender)
        {
            // strength and exhaustion modifiers
            double strengthModifier = (Attributes.Strength + 50) / 100;
            double exhaustionModifier = (2 - Exhaustion.Amount / Exhaustion.Max) / 2 + .1;

            // skill bonus
            double skillBonus = 0;
            if (Weapon == Unarmed)
                skillBonus = Skills.HandToHand.Level;
            else if (Weapon.WeaponClass == WeaponClass.Blade)
                skillBonus = Skills.Blade.Level;
            else if (Weapon.WeaponClass == WeaponClass.Blunt)
                skillBonus = Skills.Blunt.Level;
            
            // weapon and armor modifiers
            double weaponDamage = Weapon.Damage + skillBonus; // add skill modifier
            double defenderDefense = defender.ArmorRating;
            double damage = weaponDamage * strengthModifier * exhaustionModifier * (1 - defenderDefense);

            // random multiplier
            damage *= Utils.RandDouble(.5, 2);

            if (damage < 0)
                damage = 0;

            return damage;
        }

        public double DetermineHitChance(ICombatant attacker)
        {
            return 1;
        }

        public double DetermineDodgeChance(ICombatant attacker)
        {
            double baseDodge = (Skills.Dodge.Level + Attributes.Agility / 2 + Attributes.Luck / 10) / 200;
            double speedDiff = this.Attributes.Speed - attacker.Attributes.Speed;
            double chance = baseDodge + speedDiff;
            return chance;
        }



        public void Attack(ICombatant target)
        {
            // base damage - defense percentage
            double damage = DetermineDamage(target);
            double hitChance = DetermineHitChance(target);
            double dodgeChance = target.DetermineDodgeChance(this);
            int roll = Utils.RandInt(0, 100);
            if (roll > hitChance * 100)
            {
                Utils.WriteLine(this, " missed ", target, "!");
                return;
            }
            if (roll > (1 - dodgeChance) * 100)
            {
                Utils.Write(target, " dodged the attack!\n");
                return;
            }
            Utils.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");
            target.Damage(damage);
            switch (Weapon.WeaponClass)
            {
                case WeaponClass.Blade:
                    EventAggregator.Publish(new GainExperienceEvent(1, SkillType.Blade));
                    break;
                case WeaponClass.Blunt:
                    EventAggregator.Publish(new GainExperienceEvent(1, SkillType.Blunt));
                    break;
                case WeaponClass.Unarmed:
                    EventAggregator.Publish(new GainExperienceEvent(1, SkillType.HandToHand));
                    break;
                default:
                    Utils.WriteDanger("Unknown weapon type.");
                    break;
            }
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
            MaxHealth += Attributes.Endurance / 10;
            Health += Attributes.Endurance / 10;
            Utils.WriteWarning("You leveled up to level " + Level + "!");
            Utils.WriteLine("You gained ", Attributes.Endurance / 10, " health!");
            Utils.WriteLine("You gained 3 skill points!");
            SkillPoints += 3;
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
            GainExperience(1 * e.Skill.Level);
        }

        /// OTHER ///

        public override string ToString()
        {
            return Name;
        }


    }
}
