using text_survival.Effects;
using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;
using text_survival.Level;
using text_survival.PlayerComponents;
using text_survival.Bodies;

namespace text_survival.Actors
{
    public class Npc : Actor
    {
        #region Properties

        // Basic properties
        public string Description { get; set; }
        public bool IsFound { get; set; }
        public bool IsHostile { get; private set; }

        public override Location CurrentLocation {get; set;}
        public override Zone CurrentZone {get; set;}
        public Attributes Attributes { get; }

        // IPhysicalEntity implementation
        public double Health => Body.Health;
        public double MaxHealth => Body.MaxHealth;
        public bool IsDestroyed => Body.IsDestroyed;

        // Internal components

        private Container Loot { get; }

        #endregion

        #region Constructor

        public Npc(string name, Attributes? attributes = null)
        {
            // Basic initialization
            Name = name;
            Attributes = attributes ?? new Attributes();
            Description = "";
            IsHostile = true;

            // Component initialization
            _skillRegistry = new SkillRegistry(this is Humanoid);

            // Create the appropriate body type
            int baseHealth = (int)(((Attributes.Strength + Attributes.Endurance) / 10) * 2);
            BodyPart bodyPart;

            if (this is Humanoid)
            {
                bodyPart = BodyPartFactory.CreateHumanBody(name, baseHealth);
            }
            else if (this is Animal)
            {
                bodyPart = BodyPartFactory.CreateAnimalBody(name, baseHealth);
            }
            else
            {
                bodyPart = BodyPartFactory.CreateGenericBody(name, baseHealth);
            }

            // Create body from the generated body part with sensible defaults
            Body = new Body(bodyPart, 70, 20, 60, _effectRegistry);

            // Set up loot container
            Loot = new Container(name, 10);
        }

        #endregion

        #region IPhysicalEntity Interface Implementation

        public IReadOnlyDictionary<string, double> GetCapacities()
        {
            return Body.GetCapacities();
        }

        #endregion

  
        #region IInteractable Interface Implementation

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

        #endregion



        #region Inventory and Loot Methods

        public void AddLoot(Item item) => Loot.Add(item);

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
            location.Items.Add(item);
        }

        #endregion

        public override string ToString() => Name;
    }
}