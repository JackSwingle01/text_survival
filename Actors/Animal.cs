using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actors
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

        #endregion

        #region Constructor

        public Animal(string name, Weapon weapon, BodyCreationInfo bodyStats, AnimalBehaviorType behaviorType, bool isHostile = true)
            : base(name, weapon, bodyStats)
        {
            Name = name;
            ActiveWeapon = weapon;
            BehaviorType = behaviorType;
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
        /// Returns true if player appears stronger (higher health + better weapon).
        /// </summary>
        private bool AssessIfOutmatched(Actor player)
        {
            // Simple heuristic: compare health and weapon damage
            double playerThreat = player.Body.Health + player.ActiveWeapon.Damage;
            double animalThreat = this.Body.Health + this.ActiveWeapon.Damage;

            return playerThreat > animalThreat * 1.2; // Needs to be clearly outmatched
        }

        #endregion

        public override string ToString() => Name;
    }
}