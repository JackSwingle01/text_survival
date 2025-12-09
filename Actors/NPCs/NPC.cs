
using text_survival.Items;
using text_survival.Bodies;

namespace text_survival.Actors.NPCs
{
    public class Npc : Actor
    {
        #region Properties

        // Basic properties
        public string Description { get; set; }
        public bool IsFound { get; set; }
        public bool IsHostile { get; protected set; } // Protected set to allow Animal subclass to override
        public override Weapon ActiveWeapon { get; protected set; }

        // IPhysicalEntity implementation
        public double Health => Body.Health;
        public double MaxHealth => Body.MaxHealth;
        public bool IsDestroyed => Body.IsDestroyed;

        // Internal components

        public Container Loot { get; }

        #endregion

        #region Constructor

        public Npc(string name, Weapon weapon, BodyCreationInfo stats) : base(name, stats)
        {
            Description = "";
            IsHostile = true;
            ActiveWeapon = weapon;
            Loot = new Container(name, 10);
        }

        #endregion

        #region Inventory and Loot Methods

        public void AddLoot(Item item) => Loot.Add(item);

        #endregion

        public override string ToString() => Name;
    }
}