using text_survival.Actors;
using text_survival.Actors.text_survival.Actors;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Interfaces;
using text_survival.IO;
using text_survival.Items;
using text_survival.Level;
using text_survival.PlayerComponents;

namespace text_survival.Actors
{
    public class Npc : ICombatant, IInteractable, IClonable<Npc>
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double UnarmedDamage { get; protected set; }
        public double ArmorRating { get; private set; }
        public bool IsHostile { get; private set; }
        private Container Loot { get; }
        public bool IsAlive => SurvivalManager.IsAlive;
        public bool IsFound { get; set; }
        public bool IsEngaged { get; set; }
        public Attributes Attributes { get; }
        public Skills Skills { get; }
        public double ConditionPercent => SurvivalManager.OverallConditionPercent;
        public IClonable<Npc>.CloneDelegate Clone { get; set; }
        private SurvivalManager SurvivalManager { get; }
        private InventoryManager InventoryManager { get; }
        public bool IsArmed => InventoryManager.IsArmed;
        public Weapon ActiveWeapon => InventoryManager.Weapon;
        public double EquipmentWarmth => InventoryManager.EquipmentWarmth;

        public Npc(string name, Attributes? attributes = null)
        {
            Name = name;
            Attributes = attributes ?? new Attributes();
            int health = (int)(((Attributes.Strength + Attributes.Endurance) / 10) * 2);
            BodyPart body;
            if (this is Humanoid)
            {
                body = BodyPartFactory.CreateHumanBody(name, health);
            }
            else if (this is Animal)
            {
                body = BodyPartFactory.CreateAnimalBody(name, health);
            }
            else
            {
                body = BodyPartFactory.CreateGenericBody(name, health);
            }
            SurvivalManager = new SurvivalManager(this, false, body);
            InventoryManager = new InventoryManager();
            InventoryManager.Weapon = new Weapon(WeaponType.Unarmed, WeaponMaterial.Other);
            Skills = new Skills(this is Humanoid); 
            Loot = new Container(name, 10);
            IsHostile = true;
            Description = "";
            UnarmedDamage = 2;
            Clone = () => new Npc(name, attributes);
        }

        // Interact
        public void Interact(Player player)
        {
            if (IsAlive)
            {
                Combat.CombatLoop(player, this);
            }
            else
            {
                if (Loot.IsEmpty)
                {
                    Output.WriteLine("There is nothing to loot.");
                    return;
                }
                Loot.Open(player);
            }
        }

        public Command<Player> InteractCommand
        {
            get
            {
                string name = IsAlive ? "Fight " + Name : "Loot " + Name;
                return new Command<Player>(name, Interact);
            }
        }

        // Location (placeholder)
        public Location CurrentLocation => throw new NotImplementedException();
        public Zone CurrentZone => throw new NotImplementedException();

        // Update
        public void Update()
        {
            SurvivalManager.Update();
        }

        // Combat
        private CombatManager CombatManager => new CombatManager(this);

        public void Attack(ICombatant target)
        {
            CombatManager.Attack(target);
        }

        // Inventory
        public void DropInventory(Location location)
        {
            while (!Loot.IsEmpty)
            {
                Item item = Loot.GetItem(0);
                Output.WriteLine(this, " dropped ", item, "!");
                DropItem(item, location);
            }
        }

        private void DropItem(Item item, Location location)
        {
            item.IsFound = true;
            Loot.Remove(item);
            location.PutThing(item);
        }

        public void AddLoot(Item item) => Loot.Add(item);
        public void AddLoot(List<Item> items)
        {
            foreach (Item item in items)
            {
                Loot.Add(item);
            }
        }

        public void ApplyEffect(IEffect effect) => SurvivalManager.AddEffect(effect);
        public void RemoveEffect(string effectType) => SurvivalManager.RemoveEffect(effectType);

        public override string ToString() => Name;

        public void Damage(double damage) => SurvivalManager.Damage(damage);
        public void Heal(double heal) => SurvivalManager.Heal(heal);
    }
}