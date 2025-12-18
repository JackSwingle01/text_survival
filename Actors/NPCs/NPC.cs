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

        #endregion

        #region Constructor

        public Npc(string name, Weapon weapon, BodyCreationInfo stats) : base(name, stats)
        {
            Description = "";
            IsHostile = true;
            ActiveWeapon = weapon;
        }

        #endregion

        public override string ToString() => Name;
    }
}