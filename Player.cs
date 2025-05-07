using text_survival.Actors;
using text_survival.Actors.text_survival.Actors;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;
using text_survival.Level;
using text_survival.Magic;
using text_survival.PlayerComponents;

namespace text_survival
{
    public class Player : ICombatant, ISpellCaster
    {
        public string Name { get; set; }
        public bool IsAlive => SurvivalStats.IsAlive;
        private SurvivalManager SurvivalStats { get; }
        private InventoryManager InventoryManager { get; }

        public bool IsArmed => InventoryManager.IsArmed;
        public bool IsArmored => InventoryManager.IsArmored;

        public void Sleep(int minutes)
        {
            SurvivalStats.Sleep(minutes);
        }
        public void AddWarmthBonus(double warmth) => SurvivalStats.WarmthBonus += warmth;
        public void RemoveWarmthBonus(double warmth) => SurvivalStats.WarmthBonus -= warmth;

        public void ApplyEffect(IEffect e) => SurvivalStats.AddEffect(e);
        public void RemoveEffect(string effectType) => SurvivalStats.RemoveEffect(effectType);

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

        public Attributes Attributes { get; }
        public Skills Skills { get; }

        // // buffs
        // public List<Buff> Buffs { get; }
        // public bool HasBuff(BuffType type) => Buffs.Any(b => b.Type == type);
        // public Buff? GetBuff(BuffType type) => Buffs.FirstOrDefault(b => b.Type == type);


        // combat
        public bool IsEngaged { get; set; }

        // spells
        public List<Spell> Spells { get; }

        #region Constructor

        public Player(Location location)
        {
            // stats
            Name = "Player";
            // Level = 0;
            // Experience = 0;
            // SkillPoints = 0;

            SurvivalStats = new SurvivalManager(this, true, BodyPartFactory.CreateHumanBody("Player", 100));

            // lists
            // Buffs = [];

            Spells = [];
            // objects
            Attributes = new Attributes();
            Skills = new Skills();
            InventoryManager = new InventoryManager();

            // starting items

            // starting spells
            Spells.Add(SpellFactory.Bleeding);
            Spells.Add(SpellFactory.Poison);
            Spells.Add(SpellFactory.MinorHeal);
            // map
            Map = new WorldMap(location.ParentZone);
            _currentLocation = location;
            location.Visited = true;
            // events
            // EventHandler.Subscribe<SkillLevelUpEvent>(OnSkillLeveledUp);
        }

        #endregion Constructor


        public void EquipItem(IEquippable item)
        {
            Output.WriteLine("You equip the ", item);
            InventoryManager.Equip(item);
            foreach (IEffect effect in item.EquipEffects)
            {
                ApplyEffect(effect);
            }
        }
        public void UnequipItem(IEquippable item)
        {
            InventoryManager.Unequip(item);
            foreach (IEffect effect in item.EquipEffects)
            {
                SurvivalStats.RemoveEffect(effect);
            }
        }



        /// <summary>
        /// Removes an item from the player's inventory and adds it to the area's items
        /// </summary>
        /// <param name="item"></param>
        public void DropItem(Item item)
        {
            InventoryManager.RemoveFromInventory(item);
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
            InventoryManager.AddToInventory(item);
        }

        public double DetermineDamage()
        {
            // skill bonus
            double skillBonus = 0;
            if (!InventoryManager.IsArmed)
                skillBonus = Skills.Unarmed.Level;
            else if (InventoryManager.Weapon.Class == WeaponClass.Blade)
                skillBonus = Skills.Blade.Level;
            else if (InventoryManager.Weapon.Class == WeaponClass.Blunt)
                skillBonus = Skills.Blunt.Level;

            // other modifiers
            double conditionModifier = (2 - (SurvivalStats.OverallConditionPercent / 100)) / 2 + .1;

            double damage = Combat.CalculateAttackDamage(
                InventoryManager.Weapon.Damage, Attributes.Strength, skillBonus, conditionModifier);
            return damage;
        }

        public double DetermineHitChance(ICombatant attacker) => InventoryManager.Weapon.Accuracy;

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
            double block = (InventoryManager.Weapon.BlockChance * 100 + (Attributes.Luck + Attributes.Strength) / 3) / 2 + Skills.Block.Level;
            return block / 100;
        }

        public void Attack(ICombatant target)
        {
            // attack event
            var e = new CombatEvent(EventType.OnAttack, this, target);
            e.Weapon = InventoryManager.Weapon;
            EventHandler.Publish(e);

            // determine damage
            double damage = DetermineDamage();

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
            spell.Cast(target);
            HandleSpellXpGain(spell);
        }

        public void Damage(double amount)
        {
            SurvivalStats.Damage(amount);
            if (!SurvivalStats.IsAlive)
            {
                // end program
                Environment.Exit(0);
            }
        }
        public void Heal(double amount) => SurvivalStats.Heal(amount);
        public void OpenInventory() => InventoryManager.Open(this);
        internal void DescribeSurvivalStats() => SurvivalStats.Describe();


        private void HandleAttackXpGain()
        {
            switch (InventoryManager.Weapon.Class)
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
            if (InventoryManager.Armor.Any(a => a.Type == ArmorClass.Light))
                EventHandler.Publish(new GainExperienceEvent(1, SkillType.LightArmor));
            if (InventoryManager.Armor.Any(a => a.Type == ArmorClass.Heavy))
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


        public void Update()
        {
            SurvivalStats.Update();
        }

        public void UseItem(Item item)
        {
            // handle special logic for each item type
            if (item is FoodItem food)
            {
                string eating_type = food.WaterContent > food.Calories ? "drink" : "eat";
                Output.Write($"You {eating_type} the ", food, "...");
                SurvivalStats.ConsumeFood(food);
            }
            else if (item is ConsumableItem consumable)
            {
                foreach (IEffect e in consumable.Effects)
                {
                    ApplyEffect(e);
                }
            }
            else if (item is Armor armor)
            {
                EquipItem(armor);
            }
            else if (item is Weapon weapon)
            {
                EquipItem(weapon);
            }
            else if (item is WeaponModifierItem weaponMod)
            {
                if (ModifyWeapon(weaponMod.Damage))
                {
                    Output.WriteLine("You use the ", weaponMod, " to modify your ", InventoryManager.Weapon);
                }
                else
                {
                    Output.WriteLine("You don't have a weapon equipped to modify.");
                    return;
                }
            }
            else if (item is ArmorModifierItem armorMod)
            {
                if (ModifyArmor(armorMod.ValidArmorTypes[0], armorMod.Rating, armorMod.Warmth))
                {
                    Output.WriteLine("You use the ", armorMod, " to modify your armor.");
                }
                else
                {
                    Output.WriteLine("You don't have any armor you can use that on.");
                    return;
                }
            }
            else
            {
                Output.Write("You don't know what to use the ", item, " for...\n");
                return;
            }
            // shared logic for all item types
            if (item.NumUses != -1)
            {
                item.NumUses -= 1;
                if (item.NumUses == 0)
                {
                    InventoryManager.RemoveFromInventory(item);
                }
            }
            World.Update(1);
        }

        public bool ModifyWeapon(double damage)
        {
            if (!IsArmed) return false;

            InventoryManager.Weapon.Damage += damage;
            return true;
        }
        public bool ModifyArmor(EquipSpots spot, double rating = 0, double warmth = 0)
        {
            Armor? armor = InventoryManager.GetArmorInSpot(spot);
            if (armor is null) return false;

            armor.Rating += rating;
            armor.Warmth += warmth;
            return true;
        }

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

        public override string ToString() => Name;

        public Command<Player> LeaveCommand => new("Leave " + Name, Leave);
    }
    // public void Describe(){
    //     double feelsLikeTemperature = CurrentZone.GetTemperature() + 
    //     Output.WriteLine("Feels like: ", feelsLikeTemperature, "°F -> ", tempChange);
    // }
}
