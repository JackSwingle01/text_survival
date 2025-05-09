using text_survival.Actors;
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

        private SurvivalManager SurvivalSys { get; }
        private InventoryManager InventorySys { get; }
        private CombatManager CombatSys { get; }
        private LocationManager LocationSys { get; }
        private EffectRegistry EffectRegistry { get; }
        public Skills Skills { get; }



        // ICombatant implementation - delegate to components
        public bool IsEngaged { get; set; }
        public bool IsAlive => SurvivalSys.IsAlive;
        public double ConditionPercent => SurvivalSys.ConditionPercent;
        public Weapon ActiveWeapon => InventorySys.Weapon;
        public bool IsArmed => InventorySys.IsArmed;
        public bool IsArmored => InventorySys.IsArmored;
        public double EquipmentWarmth => InventorySys.EquipmentWarmth;

        // Delegate methods to appropriate components

        public void Heal(double amount) => SurvivalSys.Heal(amount);
        public void Attack(ICombatant target) => CombatSys.Attack(target);
        public void Sleep(int minutes) => SurvivalSys.Sleep(minutes);
        public void ApplyEffect(IEffect effect) => EffectRegistry.AddEffect(effect);
        public void RemoveEffect(string effectType) => EffectRegistry.RemoveEffect(effectType);
        public void RemoveEffect(IEffect effect) => EffectRegistry.RemoveEffect(effect);
        public void OpenInventory() => InventorySys.Open(this);

        // Location-related methods
        public Location CurrentLocation
        {
            get => LocationSys.CurrentLocation;
            set => LocationSys.CurrentLocation = value;
        }

        public Zone CurrentZone
        {
            get => LocationSys.CurrentZone;
            set => LocationSys.CurrentZone = value;
        }

        public void Update()
        {
            EffectRegistry.Update();
            SurvivalSys.Update();
        }



        public Attributes Attributes { get; }

        // spells
        public List<Spell> Spells { get; }

        #region Constructor

        public Player(Location location)
        {
            // stats
            Name = "Player";
            EffectRegistry = new(this);
            SurvivalSys = new SurvivalManager(this, EffectRegistry, true, BodyPartFactory.CreateHumanBody("Player", 100));
            InventorySys = new(EffectRegistry);
            CombatSys = new CombatManager(this);
            LocationSys = new LocationManager(location);
            Spells = [];

            Attributes = new Attributes();
            Skills = new Skills();

            // starting spells
            Spells.Add(SpellFactory.Bleeding);
            Spells.Add(SpellFactory.Poison);
            Spells.Add(SpellFactory.MinorHeal);
        }

        #endregion Constructor



        /// <summary>
        /// Removes an item from the player's inventory and adds it to the area's items
        /// </summary>
        /// <param name="item"></param>
        public void DropItem(Item item)
        {
            InventorySys.RemoveFromInventory(item);
            Output.WriteLine("You drop the ", item);
            LocationSys.AddItemToLocation(item);
        }

        public void TakeItem(Item item)
        {
            LocationSys.RemoveItemFromLocation(item);
            Output.WriteLine("You take the ", item);
            InventorySys.AddToInventory(item);
        }

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
            Skills.AddExperience("Shamanism", 2);
        }

        public void Damage(double amount)
        {
            SurvivalSys.Damage(amount);
            if (!SurvivalSys.IsAlive)
            {
                // end program
                Environment.Exit(0);
            }
        }

        internal void DescribeSurvivalStats() => SurvivalSys.Describe();


        public void UseItem(Item item)
        {
            // handle special logic for each item type
            if (item is FoodItem food)
            {
                string eating_type = food.WaterContent > food.Calories ? "drink" : "eat";
                Output.Write($"You {eating_type} the ", food, "...");
                SurvivalSys.ConsumeFood(food);
            }
            else if (item is ConsumableItem consumable)
            {
                foreach (IEffect e in consumable.Effects)
                {
                    ApplyEffect(e);
                }
            }
            else if (item is Gear gear)
            {
                Output.WriteLine("You equip the ", gear);
                InventorySys.Equip(gear);
                foreach (IEffect effect in gear.EquipEffects)
                {
                    ApplyEffect(effect);
                }
            }

            else if (item is WeaponModifierItem weaponMod)
            {
                if (ModifyWeapon(weaponMod.Damage))
                {
                    Output.WriteLine("You use the ", weaponMod, " to modify your ", InventorySys.Weapon);
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
                    InventorySys.RemoveFromInventory(item);
                }
            }
            World.Update(1);
        }

        public bool ModifyWeapon(double damage)
        {
            if (!IsArmed) return false;

            InventorySys.Weapon.Damage += damage;
            return true;
        }
        public bool ModifyArmor(EquipSpots spot, double rating = 0, double warmth = 0)
        {
            Armor? armor = InventorySys.GetArmorInSpot(spot);
            if (armor is null) return false;

            armor.Rating += rating;
            armor.Warmth += warmth;
            return true;
        }

        public string Name { get; set; }
        public override string ToString() => Name;

        public void Travel() => LocationSys.TravelToAdjacentZone();

        public Command<Player> LeaveCommand => new("Leave " + Name, p => LocationSys.Leave());


    }
}
