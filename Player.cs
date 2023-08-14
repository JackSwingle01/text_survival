using text_survival.Actors;
using text_survival.Environments;
using text_survival.Items;
using text_survival.Level;
using text_survival.Magic;
using text_survival.Survival;

namespace text_survival
{
    public class Player : ICombatant, ISpellCaster
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
        public Area CurrentArea { get; private set; } = null!;

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

        // spells
        public List<Spell> Spells { get; }

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
            Spells = new List<Spell>();
            // objects
            Attributes = new Attributes();
            Skills = new Skills();
            Inventory = new Inventory();
            HungerModule = new HungerModule(this);
            ThirstModule = new ThirstModule(this);
            ExhaustionModule = new ExhaustionModule(this);
            TemperatureModule = new TemperatureModule(this);
            // starting items
            _unarmed = ItemFactory.MakeFists();
            // starting spells
            Spells.Add(SpellFactory.Bleeding);
            Spells.Add(SpellFactory.Poison);
            Spells.Add(SpellFactory.MinorHeal);
            // starting area
            CurrentArea = area;
            Enter(area);
            // events
            EventHandler.Subscribe<SkillLevelUpEvent>(OnSkillLeveledUp);
        }

        // Survival Actions //

        public void Eat(FoodItem food)
        {
            Output.Write("You eat the ", food, ".\n");
            if (HungerModule.Amount - food.Calories < 0)
            {
                Output.Write("You are too full to finish it.\n");
                int percentageEaten = (int)(HungerModule.Amount / food.Calories) * 100;
                food.Calories *= (100 - percentageEaten);
                food.WaterContent *= (100 - percentageEaten);
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
                Output.Write("You wake up feeling refreshed.\n");
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
                    Output.WriteLine("You can't equip that.");
                    return;
            }
            Output.WriteLine("You equip the ", item);
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
                    Output.WriteLine("You can't unequip that.");
                    return;
            }

            if (item != _unarmed) 
                Output.WriteLine("You unequip ", item);

            item.OnUnequip(this);
        }

        public void CheckGear()
        {
            Examine.ExamineGear(this);
            Output.WriteLine("Would you like to unequip an item?");
            int choice = Input.GetSelectionFromList(new List<string> { "Yes", "No" });
            if (choice != 1) return;

            Output.WriteLine("Which item would you like to unequip?");
            // get list of all equipment
            var equipment = new List<IEquippable>();
            equipment.AddRange(Armor);
            if (IsArmed) equipment.Add(Weapon);
            if (HeldItem != null) equipment.Add(HeldItem);

            choice = Input.GetSelectionFromList(equipment, true);
            if (choice == 0) return;
            Unequip(equipment[choice - 1]);
        }

        // Inventory //

        public int InventoryCount => Inventory.Count();

        public void AddToInventory(Item item)
        {
            Inventory.Add(item);
        }

        /// <summary>
        /// Simply removes the item, use DropItem() if you want to drop it.
        /// </summary>
        /// <param name="item"></param>
        public void RemoveFromInventory(Item item)
        {
            Inventory.Remove(item);
        }

        public void OpenInventory()
        {
            Inventory.Open(this);
        }

        /// <summary>
        /// Removes an item from the player's inventory and adds it to the area's items
        /// </summary>
        /// <param name="item"></param>
        public void DropItem(Item item)
        {
            Inventory.Remove(item);
            CurrentArea.PutThing(item);
        }

        public void TakeItem(Item item)
        {
            // recursively search 
            if (CurrentArea.Things.Contains(item))
                CurrentArea.Things.Remove(item);
            else
            {
                foreach (var thing in CurrentArea.Things)
                    if (thing is Container container)
                        if (container.Contains(item))
                        {
                            container.Remove(item);
                            break;
                        }
            }
            AddToInventory(item);
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

        /// <summary>
        /// Hit chance from 0-1
        /// </summary>
        /// <param name="attacker"></param>
        /// <returns></returns>
        public double DetermineHitChance(ICombatant attacker)
        {
            return Weapon.Accuracy;
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

        public double DetermineBlockChance(ICombatant attacker)
        {
            double block = (Weapon.BlockChance * 100 + (Attributes.Luck + Attributes.Agility + Attributes.Strength) / 6) / 2 + Skills.Block.Level;
            return block / 100;
        }

        public void Attack(ICombatant target)
        {
            // determine damage
            double damage = DetermineDamage(target);

            // check for dodge and miss
            if (Combat.DetermineDodge(this, target)) return; // if target dodges
            if (!Combat.DetermineHit(this, target)) return; // if attacker misses
            if (Combat.DetermineBlock(this, target)) return; // if target blocks
            // apply damage
            Output.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");
            target.Damage(damage);

            // apply xp
            HandleAttackXpGain();
            Thread.Sleep(1000);
        }

        // Spell //

        public void SelectSpell()
        {
            //get spell
            Output.WriteLine("Which spell would you like to cast?");
            var spellNames = new List<string>();
            Spells.ForEach(spell => spellNames.Add(spell.Name));
            var spell = Input.GetSelectionFromList(spellNames, true);
            if (spell == 0) return;

            // get target
            Output.WriteLine("Who would you like to cast ", Spells[spell - 1].Name, " on?");
            var targets = new List<string>();
            targets.Add("Yourself");
            CurrentArea.Npcs.ForEach(npc => targets.Add(npc.Name));
            var target = Input.GetSelectionFromList(targets, true);
            if (target == 0) return;

            // cast spell
            else if (target == 1) CastSpell(Spells[spell - 1], this);
            else
                CastSpell(Spells[spell - 1], CurrentArea.Npcs[target - 2]);

        }

        public void CastSpell(Spell spell, ICombatant target)
        {
            if (Psych < spell.PsychCost)
            {
                Output.WriteLine("You don't have enough psychic energy to cast that spell!");
                return;
            }
            Psych -= spell.PsychCost;
            spell.Cast(target);
            HandleSpellXpGain(spell);
        }

        // Effects //

        public void Damage(double damage)
        {
            Health -= damage;
            if (!(Health <= 0)) return;
            Output.WriteLine("You died!");
            Health = 0;
            // end program
            Environment.Exit(0);
        }

        public void Heal(double heal)
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
            Output.WriteWarning("You leveled up to level " + Level + "!");
            Output.WriteLine("You gained ", Attributes.Endurance / 10, " health!");
            Output.WriteLine("You gained 3 skill points!");
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
                    Output.WriteDanger("Unknown weapon type.");
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

        private static void HandleSpellXpGain(Spell spell)
        {
            switch (spell.Family)
            {
                case Spell.SpellFamily.Destruction:
                    EventHandler.Publish(new GainExperienceEvent(1, SkillType.Destruction));
                    break;
                case Spell.SpellFamily.Restoration:
                    EventHandler.Publish(new GainExperienceEvent(1, SkillType.Restoration));
                    break;
                default:
                    break;
            }
        }

        private void OnSkillLeveledUp(SkillLevelUpEvent e)
        {
            GainExperience(1 * e.Skill.Level);
        }

        // UPDATE //

        public void Update()
        {
            var buffs = new List<Buff>(Buffs);
            foreach (var buff in buffs)
            {
                buff.Tick(this);
            }
            HungerModule.Update();
            ThirstModule.Update();
            ExhaustionModule.Update();
            TemperatureModule.Update();
        }

        // Area //

        public void Enter(Area newArea)
        {
            Output.WriteLine("You enter ", newArea);
            Output.WriteLine(newArea.Description);
            if (!newArea.NearbyAreas.Contains(CurrentArea) && CurrentArea != newArea)
                newArea.NearbyAreas.Add(this.CurrentArea);
            CurrentArea = newArea;
            if (!newArea.Visited) newArea.GenerateNearbyAreas();
            newArea.Visited = true;
            Output.WriteLine("You should probably look around.");
        }

        /// OTHER ///

        public override string ToString()
        {
            return Name;
        }



    }
}
