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
    public class Player : Actor
    {

        private LocationManager locationManager;
        private SpellManager spellManager;

        public void Sleep(int minutes) => survivalManager.Sleep(minutes);
        public void OpenInventory() => inventoryManager.Open(this);

        // Location-related methods
        public override Location CurrentLocation
        {
            get => locationManager.CurrentLocation;
            set => locationManager.CurrentLocation = value;
        }

        public override Zone CurrentZone
        {
            get => locationManager.CurrentZone;
            set => locationManager.CurrentZone = value;
        }


        public Attributes Attributes { get; }


        #region Constructor

        public Player(Location startingLocation)
        {
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
            List<Actor> targets = [this];
            CurrentLocation.Npcs.ForEach(targets.Add);
            spellManager.SelectSpell(targets);
        }



        public override void Damage(DamageInfo damageInfo)
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
                foreach (Effect e in consumable.Effects)
                {
                    ApplyEffect(e);
                }
            }
            else if (item is Gear gear)
            {
                Output.WriteLine("You equip the ", gear);
                inventoryManager.Equip(gear);
                foreach (Effect effect in gear.EquipEffects)
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



        public void Travel() => locationManager.TravelToAdjacentZone();
        public Command<Player> LeaveCommand => new("Leave " + Name, p => locationManager.Leave());
    }
}
