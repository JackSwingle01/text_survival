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
        public double Health { get; private set; }
        public double MaxHealth { get; private set; }
        public double Energy { get; private set; }
        public double MaxEnergy { get; private set; }
        public double EnergyRegen { get; private set; }
        public double Psych { get; private set; }
        public double MaxPsych { get; private set; }
        public double PsychRegen { get; private set; }


        // Survival stats
        // Hunger
        private HungerModule HungerModule { get; }
        public int HungerPercent => (int)(HungerModule.Amount / HungerModule.Max) * 100;
        // Thirst
        private ThirstModule ThirstModule { get; }
        public int ThirstPercent => (int)(ThirstModule.Amount / ThirstModule.Max) * 100;
        // Exhaustion
        private ExhaustionModule ExhaustionModule { get; }
        public double ExhaustionPercent => (int)(ExhaustionModule.Amount / ExhaustionModule.Max) * 100;
        // Temperature
        private TemperatureModule TemperatureModule { get; }
        public double Temperature => Math.Round(TemperatureModule.BodyTemperature, 1);
        public TemperatureModule.TemperatureEnum TemperatureStatus => TemperatureModule.TemperatureEffect;
        public double WarmthBonus { get; private set; }

        // area
        public Area CurrentArea { get; private set; }

        // inventory
        private Inventory Inventory { get; }
        public List<Armor> Armor { get; }
        public Gear? HeldItem { get; private set; }

        // weapon
        private Weapon? _weapon;
        private readonly Weapon _unarmed;
        public bool IsArmed => Weapon != _unarmed;
        public Weapon Weapon
        {
            get => _weapon ?? _unarmed;
            set => _weapon = value;
        }

        // skills and level
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int ExperienceToNextLevel => (Level + 1) * 5;
        public int SkillPoints { get; private set; }
        public Attributes Attributes { get; }
        public Skills Skills { get; }

        // buffs
        private List<Buff> Buffs { get; }

        // armor
        public double ArmorRating
        {
            get
            {
                double rating = 0;
                foreach (Armor armor in Armor)
                {
                    rating += armor.Rating;
                    rating += armor.Type switch
                    {
                        ArmorClass.Light => Skills.LightArmor.Level * .01,
                        ArmorClass.Heavy => Skills.HeavyArmor.Level * .01,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }
                return rating;
            }
        }

        // CONSTRUCTOR //

        public Player(Area area)
        {
            // stats
            Name = "Player";
            Level = 0;
            Experience = 0;
            SkillPoints = 0;
            MaxHealth = 100;
            Health = 100;
            MaxEnergy = 100;
            Energy = 100;
            EnergyRegen = 1;
            Psych = 100;
            MaxPsych = 100;
            PsychRegen = 1;
            // lists
            Buffs = new List<Buff>();
            Armor = new List<Armor>();
            // objects
            Attributes = new Attributes();
            Skills = new Skills();
            Inventory = new Inventory();
            HungerModule = new HungerModule(this);
            ThirstModule = new ThirstModule(this);
            ExhaustionModule = new ExhaustionModule(this);
            TemperatureModule = new TemperatureModule(this);
            // starting items
            Equip(ItemFactory.MakeClothShirt());
            Equip(ItemFactory.MakeClothPants());
            Equip(ItemFactory.MakeBoots());
            _unarmed = ItemFactory.MakeFists();
            // starting area
            CurrentArea = area;
            this.Enter(area);
            // events
            EventHandler.Subscribe<SkillLevelUpEvent>(OnSkillLeveledUp);
        }

        // Survival Actions //

        public void Eat(FoodItem food)
        {
            Utils.Write("You eat the ", food, ".\n");
            if (HungerModule.Amount - food.Calories < 0)
            {
                Utils.Write("You are too full to finish it.\n");
                food.Calories -= (int)(0 - HungerModule.Amount);
                HungerModule.Amount = 0;
                return;
            }
            HungerModule.Amount -= food.Calories;
            ThirstModule.Amount -= food.WaterContent;
            Inventory.Remove(food);
            World.Update(1);
        }

        public void Sleep(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                ExhaustionModule.Amount -= 1 + ExhaustionModule.Rate; // 1 minute plus negate exhaustion rate for update
                World.Update(1);
                if (!(ExhaustionModule.Amount <= 0)) continue;
                Utils.Write("You wake up feeling refreshed.\n");
                Heal(i / 6);
                return;
            }
            Heal(minutes / 6);
        }

        // Equipment //

        public void Equip(IEquippable item)
        {
            switch (item)
            {
                case Weapon weapon:
                    Unequip(Weapon);
                    Weapon = weapon;
                    Inventory.Remove(weapon);
                    break;
                case Armor armor:
                    {
                        var oldItem = Armor.FirstOrDefault(i => i.EquipSpot == armor.EquipSpot);
                        if (oldItem != null) Unequip(oldItem);
                        Armor.Add(armor);
                        Inventory.Remove(armor);
                        break;
                    }
                case Gear gear:
                    {
                        if (HeldItem != null) Unequip(HeldItem);
                        HeldItem = gear;
                        Inventory.Remove(gear);
                        break;
                    }
                default:
                    Utils.WriteLine("You can't equip that.");
                    break;
            }

            item.OnEquip(this);
        }

        public void Unequip(IEquippable item)
        {
            switch (item)
            {
                case Weapon weapon:
                    {
                        Weapon = _unarmed;
                        if (weapon != _unarmed) Inventory.Add(weapon);
                        break;
                    }
                case Armor armor:
                    Armor.Remove(armor);
                    Inventory.Add(armor);
                    break;
                case Gear gear:
                    HeldItem = null;
                    Inventory.Add(gear);
                    break;
                default:
                    Utils.WriteLine("You can't unequip that.");
                    break;
            }
            item.OnUnequip(this);
        }

        // Inventory //

        public int InventoryCount => Inventory.Count();

        public void AddToInventory(Item item)
        {
            Inventory.Add(item);
        }
        public void RemoveFromInventory(Item item)
        {
            Inventory.Remove(item);
            CurrentArea.Items.Add(item);
        }

        public void OpenInventory()
        {
            Inventory.Open(this);
        }

        // COMBAT //

        public double DetermineDamage(ICombatant defender)
        {

            // skill bonus
            double skillBonus = 0;
            if (!IsArmed)
                skillBonus = Skills.Unarmed.Level;
            else if (Weapon.WeaponClass == WeaponClass.Blade)
                skillBonus = Skills.Blade.Level;
            else if (Weapon.WeaponClass == WeaponClass.Blunt)
                skillBonus = Skills.Blunt.Level;

            // other modifiers
            double exhaustionModifier = (2 - ExhaustionModule.Amount / ExhaustionModule.Max) / 2 + .1;

            double damage = Combat.CalculateAttackDamage(
                Weapon.Damage, Attributes.Strength, defender.ArmorRating, skillBonus, exhaustionModifier);
            return damage;
        }

        public double DetermineHitChance(ICombatant attacker)
        {
            return 1;
        }

        public double DetermineDodgeChance(ICombatant attacker)
        {
            return Combat.CalculateDodgeChance(
                Attributes.Agility,
                Attributes.Speed,
                attacker.Attributes.Speed,
                Attributes.Luck,
                Skills.Dodge.Level);
        }

        public void Attack(ICombatant target)
        {
            // determine damage
            double damage = DetermineDamage(target);

            // check for dodge and miss
            if (Combat.DetermineDodge(this, target)) return; // if target dodges
            if (!Combat.DetermineHit(this, target)) return; // if attacker misses

            // apply damage
            Utils.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");
            target.Damage(damage);

            // apply xp
            HandleAttackXpGain();
            Thread.Sleep(1000);
        }

        // Effects //

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

        public void AddWarmthBonus(double warmth)
        {
            WarmthBonus += warmth;
        }

        public void RemoveWarmthBonus(double warmth)
        {
            WarmthBonus -= warmth;
        }

        // Buffs //

        public void ApplyBuff(Buff buff)
        {
            buff.ApplyEffect?.Invoke(this);
            Buffs.Add(buff);
        }

        public void RemoveBuff(Buff buff)
        {
            buff.RemoveEffect?.Invoke(this);
            Buffs.Remove(buff);
        }

        // Leveling //

        public void GainExperience(int xp)
        {
            Experience += xp;
            if (Experience < ExperienceToNextLevel) return;
            Experience -= ExperienceToNextLevel;
            LevelUp();
        }

        private void LevelUp()
        {
            Level++;
            MaxHealth += Attributes.Endurance / 10;
            Health += Attributes.Endurance / 10;
            Utils.WriteWarning("You leveled up to level " + Level + "!");
            Utils.WriteLine("You gained ", Attributes.Endurance / 10, " health!");
            Utils.WriteLine("You gained 3 skill points!");
            SkillPoints += 3;
        }

        public void SpendPointToUpgradeAttribute(Attributes.PrimaryAttributes attribute)
        {
            Attributes.IncreaseBase(attribute, 1);
            SkillPoints--;
        }

        private void HandleAttackXpGain()
        {
            switch (Weapon.WeaponClass)
            {
                case WeaponClass.Blade:
                    EventHandler.Publish(new GainExperienceEvent(1, SkillType.Blade));
                    break;
                case WeaponClass.Blunt:
                    EventHandler.Publish(new GainExperienceEvent(1, SkillType.Blunt));
                    break;
                case WeaponClass.Unarmed:
                    EventHandler.Publish(new GainExperienceEvent(1, SkillType.Unarmed));
                    break;
                default:
                    Utils.WriteDanger("Unknown weapon type.");
                    break;
            }
        }

        public void HandleArmorXpGain()
        {
            if (Armor.Any(a => a.Type == ArmorClass.Light))
                EventHandler.Publish(new GainExperienceEvent(1, SkillType.LightArmor));
            if (Armor.Any(a => a.Type == ArmorClass.Heavy))
                EventHandler.Publish(new GainExperienceEvent(1, SkillType.HeavyArmor));
        }

        private void OnSkillLeveledUp(SkillLevelUpEvent e)
        {
            GainExperience(1 * e.Skill.Level);
        }

        // UPDATE //

        public void Update()
        {
            foreach (var buff in Buffs)
            {
                buff.Tick(this);
            }
            HungerModule.Update();
            ThirstModule.Update();
            ExhaustionModule.Update();
            TemperatureModule.Update();
        }

        // Area //

        public void Enter(Area area)
        {
            Utils.WriteLine("You enter ", area);
            Utils.WriteLine(area.Description);
            if (!area.NearbyAreas.Contains(this.CurrentArea))
                this.CurrentArea.NearbyAreas.Add(this.CurrentArea);
            this.CurrentArea = area;
            if (!area.Visited) area.GenerateNearbyAreas();
            area.Visited = true;
            Utils.WriteLine("You should probably look around.");
        }

        /// OTHER ///

        public override string ToString()
        {
            return Name;
        }



    }
}
