using text_survival.Actors;
using text_survival.Bodies;
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

        private SurvivalManager survivalManager;
        private InventoryManager inventoryManager;
        private CombatManager combatManager;
        private LocationManager locationManager;
        private SpellManager spellManager;
        private EffectRegistry _effectRegistry;

        public SkillRegistry _skillRegistry { get; }



        // ICombatant implementation - delegate to components
        public bool IsEngaged { get; set; }
        public bool IsAlive => survivalManager.IsAlive;
        public double ConditionPercent => survivalManager.ConditionPercent;
        public Weapon ActiveWeapon => inventoryManager.Weapon;
        public bool IsArmed => inventoryManager.IsArmed;
        public bool IsArmored => inventoryManager.IsArmored;
        public double EquipmentWarmth => inventoryManager.EquipmentWarmth;

        // Delegate methods to appropriate components

        public void Heal(HealingInfo amount) => survivalManager.Heal(amount);
        public void Attack(ICombatant target) => combatManager.Attack(target);
        public void Sleep(int minutes) => survivalManager.Sleep(minutes);
        public void ApplyEffect(Effect effect) => _effectRegistry.AddEffect(effect);
        public void RemoveEffect(string effectType) => _effectRegistry.RemoveEffect(effectType);
        public void RemoveEffect(Effect effect) => _effectRegistry.RemoveEffect(effect);
        public void OpenInventory() => inventoryManager.Open(this);

        // Location-related methods
        public Location CurrentLocation
        {
            get => locationManager.CurrentLocation;
            set => locationManager.CurrentLocation = value;
        }

        public Zone CurrentZone
        {
            get => locationManager.CurrentZone;
            set => locationManager.CurrentZone = value;
        }

        public void Update()
        {
            _effectRegistry.Update();
            survivalManager.Update();
        }



        public Attributes Attributes { get; }


        #region Constructor

        public Player(Location startingLocation)
        {
            // stats
            Name = "Player";
            Attributes = new Attributes();

            _skillRegistry = new SkillRegistry();
            _effectRegistry = new(this);

            Body body = new(BodyPartFactory.CreateHumanBody("Player", 100), 70, 20, 60, _effectRegistry);
            survivalManager = new SurvivalManager(this, _effectRegistry, true, body);
            inventoryManager = new(_effectRegistry);
            combatManager = new CombatManager(this);
            locationManager = new LocationManager(startingLocation);
            spellManager = new(_skillRegistry);


        }

        #endregion Constructor

        public void DropItem(Item item)
        {
            inventoryManager.RemoveFromInventory(item);
            Output.WriteLine("You drop the ", item);
            locationManager.AddItemToLocation(item);
        }

        public void TakeItem(Item item)
        {
            locationManager.RemoveItemFromLocation(item);
            Output.WriteLine("You take the ", item);
            inventoryManager.AddToInventory(item);
        }

        public void SelectSpell()
        {
            List<ICombatant> targets = [this];
            CurrentLocation.Npcs.ForEach(targets.Add);
            spellManager.SelectSpell(targets);
        }



        public void Damage(DamageInfo damageInfo)
        {
            survivalManager.Damage(damageInfo);
            if (!survivalManager.IsAlive)
            {
                // end program
                Environment.Exit(0);
            }
        }

        internal void DescribeSurvivalStats() => survivalManager.Describe();
        public void UseItem(Item item)
        {
            Output.WriteLine($"DEBUG: Item '{item.Name}' has actual type: {item.GetType().FullName}");
            Output.WriteLine($"DEBUG: Base type: {item.GetType().BaseType?.FullName}");
            // handle special logic for each item type
            if (item is FoodItem food)
            {
                string eating_type = food.WaterContent > food.Calories ? "drink" : "eat";
                Output.Write($"You {eating_type} the ", food, "...");
                survivalManager.ConsumeFood(food);
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
                inventoryManager.Equip(gear);
                foreach (IEffect effect in gear.EquipEffects)
                {
                    ApplyEffect(effect);
                }
            }

            else if (item is WeaponModifierItem weaponMod)
            {
                if (ModifyWeapon(weaponMod.Damage))
                {
                    Output.WriteLine("You use the ", weaponMod, " to modify your ", inventoryManager.Weapon);
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
                    inventoryManager.RemoveFromInventory(item);
                }
            }
            World.Update(1);
        }

        public bool ModifyWeapon(double damage)
        {
            if (!IsArmed) return false;

            inventoryManager.Weapon.Damage += damage;
            return true;
        }
        public bool ModifyArmor(EquipSpots spot, double rating = 0, double warmth = 0)
        {
            Armor? armor = inventoryManager.GetArmorInSpot(spot);
            if (armor is null) return false;

            armor.Rating += rating;
            armor.Warmth += warmth;
            return true;
        }

        public string Name { get; set; }
        public override string ToString() => Name;

        public void Travel() => locationManager.TravelToAdjacentZone();

        public IReadOnlyDictionary<string, double> GetCapacities()
        {
            throw new NotImplementedException();
        }

        public void CastSpell(Spell spell, ICombatant target) => spellManager.CastSpell(spell, target);

        public bool IsDestroyed => throw new NotImplementedException();
        public Command<Player> LeaveCommand => new("Leave " + Name, p => locationManager.Leave());
    }
}
