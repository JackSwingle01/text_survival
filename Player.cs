﻿using text_survival.Actors;
using text_survival.Actors.text_survival.Actors;
using text_survival.Environments;
using text_survival.IO;
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
        //public double Health { get; private set; }
        //public double MaxHealth { get; private set; }
        public BodyPart Body { get; }
        public bool IsAlive => !Body.IsDestroyed;
        public double Energy { get; private set; }
        public double MaxEnergy { get; private set; }
        public double EnergyRegen { get; private set; }
        public double Psych { get; private set; }
        public double MaxPsych { get; private set; }
        public double PsychRegen { get; private set; }


        // Survival stats
        // Hunger
        private HungerModule HungerModule { get; }
        public int HungerPercent => (int)((HungerModule.Amount / HungerModule.Max) * 100);
        // Thirst
        private ThirstModule ThirstModule { get; }
        public int ThirstPercent => (int)((ThirstModule.Amount / ThirstModule.Max) * 100);
        // Exhaustion
        private ExhaustionModule ExhaustionModule { get; }
        public int ExhaustionPercent => (int)((ExhaustionModule.Amount / ExhaustionModule.Max) * 100);
        // Temperature
        private TemperatureModule TemperatureModule { get; }
        public double Temperature => Math.Round(TemperatureModule.BodyTemperature, 1);
        public TemperatureModule.TemperatureEnum TemperatureStatus => TemperatureModule.TemperatureEffect;
        public bool IsWarming => TemperatureModule.IsWarming;
        public int FeelsLikeTemperature => (int)TemperatureModule.FeelsLike;
        public double WarmthBonus { get; private set; }

        // area
        public WorldMap Map { get; }

        public Location CurrentLocation
        {
            get
            {
                return _currentLocation;
            }
            set
            {
                Output.WriteLine("You go to the ", value);
                int minutes = Utils.RandInt(1, 10);
                World.Update(minutes);
                Output.WriteLine("You arrive at the ", value, " after walking ", minutes, " minutes.");
                _currentLocation = value;
                Output.WriteLine("You should probably look around.");
            }
        }
        private Location _currentLocation;
        public Zone CurrentZone
        {
            get
            {
                return Map.CurrentZone;
            }
            set
            {
                if (CurrentZone == value)
                {
                    Output.WriteLine("There's nowhere to leave. Travel instead.");
                    return;
                }
                if (Map.North == value)
                {
                    Output.WriteLine("You go north.");
                    Map.MoveNorth();
                }
                else if (Map.East == value)
                {
                    Output.WriteLine("You go east.");
                    Map.MoveEast();
                }
                else if (Map.South == value)
                {
                    Output.WriteLine("You go south.");
                    Map.MoveSouth();
                }
                else if (Map.West == value)
                {
                    Output.WriteLine("You go west.");
                    Map.MoveWest();
                }
                else
                    throw new Exception("Invalid zone.");
                Location? newLocation = Utils.GetRandomFromList(value.Locations);

                CurrentLocation = newLocation ?? throw new Exception("No Locations In Zone");
                Output.WriteLine("You enter ", value);
                Output.WriteLine(value.Description);
            }
        }
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
        public List<Buff> Buffs { get; }
        public bool HasBuff(BuffType type) => Buffs.Any(b => b.Type == type);
        public Buff? GetBuff(BuffType type) => Buffs.FirstOrDefault(b => b.Type == type);


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

        // combat
        public bool IsEngaged { get; set; }

        // spells
        public List<Spell> Spells { get; }

        #region Constructor

        public Player(Location location)
        {
            // stats
            Name = "Player";
            Level = 0;
            Experience = 0;
            SkillPoints = 0;
            Body = BodyPartFactory.CreateHumanBody(Name, 100);
            MaxEnergy = 100;
            Energy = 100;
            EnergyRegen = 1;
            Psych = 100;
            MaxPsych = 100;
            PsychRegen = 1;
            // lists
            Buffs = [];
            Armor = [];
            Spells = [];
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
            // map
            Map = new WorldMap(location.ParentZone);
            _currentLocation = location;
            location.Visited = true;
            // events
            EventHandler.Subscribe<SkillLevelUpEvent>(OnSkillLeveledUp);
        }

        #endregion Constructor
        // Survival Actions //

        public void Eat(FoodItem food)
        {
            Output.Write("You eat the ", food, ".\n");
            if (HungerModule.Amount - food.Calories < 0)
            {
                Output.Write("You are too full to finish it.\n");
                int percentageEaten = (int)(HungerModule.Amount / food.Calories) * 100;
                double calories = food.Calories * (100 - percentageEaten);
                double waterContent = food.WaterContent * (100 - percentageEaten);
                double weight = food.Weight * (100 - percentageEaten);
                food = new FoodItem(food.Name, (int)calories, (int)waterContent, weight);
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
                    var oldItem = Armor.FirstOrDefault(i => i.EquipSpot == armor.EquipSpot);
                    if (oldItem != null) Unequip(oldItem);
                    Armor.Add(armor);
                    Inventory.Remove(armor);
                    break;
                case Gear gear:
                    if (HeldItem != null) Unequip(HeldItem);
                    HeldItem = gear;
                    Inventory.Remove(gear);
                    break;
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
            Describe.DescribeGear(this);
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
        public void OpenInventory() => Inventory.Open(this);

        /// <summary>
        /// Simply adds the item, use TakeItem() if you want to take it from an area.
        /// </summary>
        /// <param name="item"></param>
        public void AddToInventory(Item item)
        {
            Output.WriteLine("You put the ", item, " in your ", Inventory);
            Inventory.Add(item);
        }

        /// <summary>
        /// Simply removes the item, use DropItem() if you want to drop it.
        /// </summary>
        /// <param name="item"></param>
        public void RemoveFromInventory(Item item)
        {
            Output.WriteLine("You take the ", item, " from your ", Inventory);
            Inventory.Remove(item);
        }


        /// <summary>
        /// Removes an item from the player's inventory and adds it to the area's items
        /// </summary>
        /// <param name="item"></param>
        public void DropItem(Item item)
        {
            RemoveFromInventory(item);
            Output.WriteLine("You drop the ", item);
            CurrentLocation.PutThing(item);
        }

        /// <summary>
        /// Removes an item from the area's items and adds it to the player's inventory
        /// </summary>
        /// <param name="item"></param>
        public void TakeItem(Item item)
        {
            if (CurrentLocation.ContainsThing(item))
                CurrentLocation.RemoveThing(item);
            Output.WriteLine("You take the ", item);
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
                Attributes.Speed,
                attacker.Attributes.Speed,
                Attributes.Luck,
                Skills.Dodge.Level);
        }

        public double DetermineBlockChance(ICombatant attacker)
        {
            double block = (Weapon.BlockChance * 100 + (Attributes.Luck + Attributes.Strength) / 3) / 2 + Skills.Block.Level;
            return block / 100;
        }

        public void Attack(ICombatant target)
        {
            // attack event
            var e = new CombatEvent(EventType.OnAttack, this, target);
            e.Weapon = Weapon;
            EventHandler.Publish(e);

            // determine damage
            double damage = DetermineDamage(target);

            // check for dodge and miss
            if (Combat.DetermineDodge(this, target)) return; // if target dodges
            if (!Combat.DetermineHit(this, target)) return; // if attacker misses
            if (Combat.DetermineBlock(this, target)) return; // if target blocks

            Output.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");

            // trigger hit event
            e = new CombatEvent(EventType.OnHit, this, target)
            {
                Damage = damage
            };
            EventHandler.Publish(e);

            // apply damage
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
            var targets = new List<string>
            {
                "Yourself"
            };
            CurrentLocation?.Npcs.ForEach(npc => targets.Add(npc.Name));
            var target = Input.GetSelectionFromList(targets, true);
            if (target == 0) return;

            // cast spell
            else if (target == 1) CastSpell(Spells[spell - 1], this);
            else if (CurrentLocation != null)
                CastSpell(Spells[spell - 1], CurrentLocation.Npcs[target - 2]);

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
            Body.Damage(damage);
            if (Body.IsDestroyed)
            {
                Output.WriteLine("You died!");
                // end program
                Environment.Exit(0);
            }
        }

        public void HealBodypart(double heal, BodyPart bodypart)
        {
            bodypart.Heal(heal);
        }
        public void Heal(double heal)
        {
            Body.Heal(heal);
        }

        public void AddWarmthBonus(double warmth) => WarmthBonus += warmth;
        public void RemoveWarmthBonus(double warmth) => WarmthBonus -= warmth;




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
            Body.MaxHealth += Attributes.Endurance / 10;
            Body.Heal(Attributes.Endurance / 10);
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
            foreach (Buff buff in buffs)
            {
                if (buff is TimedBuff timedBuff)
                {
                    timedBuff.Tick();
                }
            }
            buffs.Clear();
            HungerModule.Update();
            ThirstModule.Update();
            ExhaustionModule.Update();
            TemperatureModule.Update();
        }

        // Area //

        /// OTHER ///

        public override string ToString() => Name;


        public void Leave(Player player)
        {
            if (CurrentLocation.Parent is null)
            {
                Output.WriteLine("There's nowhere to leave. Travel instead.");
            }
            else if (CurrentLocation.Parent is Location l)
            {
                CurrentLocation = l;
            }
        }
        public Command<Player> LeaveCommand => new("Leave " + Name, Leave);
    }
}
