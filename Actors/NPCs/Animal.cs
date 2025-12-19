using text_survival.Actors.Player;
using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actors.NPCs
{
    /// <summary>
    /// Represents an animal NPC with hunting-specific behavior and state tracking.
    /// Extends NPC with distance-based detection, behavior types, and stealth mechanics.
    /// </summary>
    public class Animal : Npc
    {
        #region Properties

        public override Weapon ActiveWeapon { get; protected set; }

        /// <summary>
        /// Defines how this animal responds to player detection.
        /// Prey flee, Predators attack, Scavengers assess threat.
        /// </summary>
        public AnimalBehaviorType BehaviorType { get; set; }

        /// <summary>
        /// Size category affecting weapon effectiveness.
        /// Small game: stones work, spears have accuracy penalty.
        /// Large game: spears required, stones ineffective.
        /// </summary>
        public AnimalSize Size { get; set; }

        /// <summary>
        /// Current awareness state: Idle (unaware) → Alert (suspicious) → Detected (knows player is there)
        /// </summary>
        public AnimalState State { get; set; }

        /// <summary>
        /// Distance from player in meters. Default 100m (safe range).
        /// Used for detection difficulty and ranged weapon effectiveness.
        /// </summary>
        public double DistanceFromPlayer { get; set; }

        /// <summary>
        /// Number of failed stealth checks. Each failure makes subsequent checks harder.
        /// Resets when animal is Detected or flees.
        /// </summary>
        public int FailedStealthChecks { get; set; }

        /// <summary>
        /// Tracking difficulty for this animal (0-10 scale).
        /// Affects blood trail tracking success rate.
        /// </summary>
        public int TrackingDifficulty { get; set; }

        /// <summary>
        /// Whether this animal is currently bleeding from a wound (Phase 4).
        /// </summary>
        public bool IsBleeding { get; set; }

        /// <summary>
        /// Time when the animal was wounded (Phase 4).
        /// Used to calculate bleed-out timing.
        /// </summary>
        public DateTime? WoundedTime { get; set; }

        /// <summary>
        /// Severity of the current wound (0.0-1.0) (Phase 4).
        /// Affects bleed-out rate and blood trail visibility.
        /// </summary>
        public double CurrentWoundSeverity { get; set; }

        /// <summary>
        /// Current boldness during a predator encounter. Initialized from CalculateBoldness(),
        /// then modified by player actions (standing ground reduces, backing away increases).
        /// </summary>
        public double EncounterBoldness { get; set; }

        #endregion

        #region Constructor

        public Animal(string name, Weapon weapon, BodyCreationInfo bodyStats, AnimalBehaviorType behaviorType, AnimalSize size, bool isHostile = true)
            : base(name, weapon, bodyStats)
        {
            Name = name;
            ActiveWeapon = weapon;
            BehaviorType = behaviorType;
            Size = size;
            State = AnimalState.Idle;
            DistanceFromPlayer = 100.0; // Start at safe range
            FailedStealthChecks = 0;
            TrackingDifficulty = 5; // Default medium difficulty
            IsHostile = isHostile; // Set hostility based on animal type (prey = false, predator = true)
        }

        #endregion

        #region Behavior Methods

        /// <summary>
        /// Transitions to Alert state, making detection more likely.
        /// Called when player fails stealth check but isn't fully detected yet.
        /// </summary>
        public void BecomeAlert()
        {
            if (State == AnimalState.Idle)
            {
                State = AnimalState.Alert;
            }
        }

        /// <summary>
        /// Animal becomes fully aware of player presence.
        /// Triggers behavior-specific response: Prey flees, Predators attack, etc.
        /// </summary>
        public void BecomeDetected()
        {
            State = AnimalState.Detected;
        }

        /// <summary>
        /// Resets animal to unaware state (used after fleeing or time passes).
        /// </summary>
        public void ResetState()
        {
            State = AnimalState.Idle;
            FailedStealthChecks = 0;
            DistanceFromPlayer = 100.0;
        }

        /// <summary>
        /// Returns true if animal should flee based on its behavior type and state.
        /// Prey always flee when detected. Scavengers flee if outmatched.
        /// </summary>
        public bool ShouldFlee(Actor player)
        {
            if (State != AnimalState.Detected)
                return false;

            return BehaviorType switch
            {
                AnimalBehaviorType.Prey => true,
                AnimalBehaviorType.Scavenger => AssessIfOutmatched(player),
                AnimalBehaviorType.Predator => false,
                AnimalBehaviorType.DangerousPrey => false, // V2: will flee unless cornered/wounded
                _ => false
            };
        }

        /// <summary>
        /// Simple threat assessment for scavengers.
        /// Returns true if player appears stronger.
        /// </summary>
        private bool AssessIfOutmatched(Actor player)
        {
            // Simple heuristic: compare vitality and weapon damage
            double playerThreat = player.Vitality * player.ActiveWeapon.Damage * player.Body.WeightKG;
            double animalThreat = this.Vitality * this.ActiveWeapon.Damage * this.Body.WeightKG;

            return playerThreat > animalThreat * .8; // Needs to clearly outmatch
        }

        /// <summary>
        /// Calculates initial boldness for a predator encounter based on observable factors.
        /// All factors are visible to the player so they can reason about outcomes.
        /// </summary>
        public double CalculateBoldness(Player.Player player, Inventory inventory)
        {
            double boldness = 0.4; // Base boldness

            // Factors that increase boldness (observable to player)
            bool hasMeat = inventory.RawMeat.Count > 0 || inventory.CookedMeat.Count > 0;
            if (hasMeat) boldness += 0.20;
            if (player.Vitality < 0.7) boldness += 0.15;

            // Factors that decrease boldness (observable to player)
            if (player.Body.WeightKG > this.Body.WeightKG) boldness -= 0.10;
            // TODO Phase 4: fire/torch check (-0.30)

            return Math.Clamp(boldness, 0.0, 1.0);
        }

        #endregion

        public override string ToString() => Name;
    }
}