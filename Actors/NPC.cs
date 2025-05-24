using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;
using text_survival.Level;
using text_survival.Bodies;
using text_survival.Events;

namespace text_survival.Actors
{
    public class Npc : Actor
    {
        #region Properties

        // Basic properties
        public string Description { get; set; }
        public bool IsFound { get; set; }
        public bool IsHostile { get; private set; }
        public override Weapon ActiveWeapon { get; protected set; }

        // IPhysicalEntity implementation
        public double Health => Body.Health;
        public double MaxHealth => Body.MaxHealth;
        public bool IsDestroyed => Body.IsDestroyed;

        // Internal components

        private Container Loot { get; }

        #endregion

        #region Constructor

        public Npc(string name, Weapon weapon, BodyStats stats) : base(name, stats)
        {
            Description = "";
            IsHostile = true;
            ActiveWeapon = weapon;

            _skillRegistry = new SkillRegistry(false);
            Loot = new Container(name, 10);
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

        #endregion

        public override string ToString() => Name;
    }
}