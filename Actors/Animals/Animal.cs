using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actors.Animals
{
    /// <summary>
    /// What the animal is currently doing. Affects detection difficulty.
    /// Cycles automatically over time.
    /// </summary>
    public enum AnimalActivity
    {
        /// <summary>Feeding, head down. Easiest to approach.</summary>
        Grazing,

        /// <summary>Traveling between areas. Moderately alert.</summary>
        Moving,

        /// <summary>Bedded down or standing still. Relaxed but eyes open.</summary>
        Resting,

        /// <summary>Suspicious of something. Very difficult to approach.</summary>
        Alert
    }

    /// <summary>
    /// Represents an animal with hunting-specific behavior and state tracking.
    /// Extends Actor with distance-based detection, behavior types, and stealth mechanics.
    /// </summary>
    public class Animal : Actor
    {
        private static readonly Random _rng = new();
        #region Combat Properties

        public override double AttackDamage { get; }
        public override double BlockChance { get; }
        public override string AttackName { get; }
        public override DamageType AttackType { get; }

        #endregion

        #region Properties

        public string Description { get; set; } = "";
        public bool IsHostile { get; protected set; } = true;

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

        /// <summary>
        /// Speed in meters per second for pursuit calculations.
        /// </summary>
        public double SpeedMps { get; }

        /// <summary>
        /// How long this animal will chase before giving up (seconds).
        /// </summary>
        public double PursuitCommitmentSeconds { get; }

        /// <summary>
        /// Base chance (0-1) that this animal disengages after incapacitating prey.
        /// Bears often leave after mauling (0.5), wolves tend to finish kills (0.2).
        /// </summary>
        public double DisengageAfterMaul { get; set; }

        /// <summary>
        /// Special materials this animal yields beyond standard meat/bone/hide.
        /// Set during construction for animals with trophy materials (ivory, mammoth hide, etc.).
        /// </summary>
        public List<(Resource resource, double kgYield)> SpecialYields { get; init; } = [];

        /// <summary>
        /// Megafauna flag - affects yield scaling and capping during butchering.
        /// Large body weight (>500kg) indicates megafauna.
        /// </summary>
        public bool IsMegafauna => Body.WeightKG > 500;

        #endregion

        #region Individual Traits

        /// <summary>
        /// Size relative to species average (0.7 = small, 1.0 = average, 1.3 = large).
        /// Affects body weight and meat yield.
        /// </summary>
        public double SizeModifier { get; set; } = 1.0;

        /// <summary>
        /// Physical condition (0.3 = sickly, 1.0 = prime).
        /// Affects speed and detection difficulty.
        /// </summary>
        public double Condition { get; set; } = 1.0;

        /// <summary>
        /// Base nervousness (0.0 = bold, 1.0 = skittish).
        /// Directly modifies detection thresholds.
        /// </summary>
        public double Nervousness { get; set; } = 0.5;

        /// <summary>
        /// Obvious physical features like "scarred flank", "broken antler", "limping".
        /// </summary>
        public List<string> DistinguishingMarks { get; set; } = [];

        /// <summary>
        /// Current activity the animal is engaged in.
        /// </summary>
        public AnimalActivity CurrentActivity { get; private set; } = AnimalActivity.Resting;

        /// <summary>
        /// Minutes until activity changes.
        /// </summary>
        private int _activityRemainingMinutes = 10;

        #endregion

        #region Constructor

        public Animal(
            string name,
            BodyCreationInfo bodyStats,
            AnimalBehaviorType behaviorType,
            AnimalSize size,
            double attackDamage,
            string attackName,
            DamageType attackType,
            double speedMps = 6.0,
            double pursuitCommitment = 45.0,
            bool isHostile = true,
            double blockChance = 0.01,
            double disengageAfterMaul = 0.1)
            : base(name, bodyStats)
        {
            // Combat stats
            AttackDamage = attackDamage;
            AttackName = attackName;
            AttackType = attackType;
            BlockChance = blockChance;

            // Behavior
            BehaviorType = behaviorType;
            Size = size;
            IsHostile = isHostile;
            SpeedMps = speedMps;
            PursuitCommitmentSeconds = pursuitCommitment;
            DisengageAfterMaul = disengageAfterMaul;

            // Defaults
            State = AnimalState.Idle;
            DistanceFromPlayer = 100.0;
            FailedStealthChecks = 0;
            TrackingDifficulty = 5;
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
                AnimalBehaviorType.DangerousPrey => false,
                _ => false
            };
        }

        /// <summary>
        /// Simple threat assessment for scavengers.
        /// Returns true if player appears stronger.
        /// </summary>
        private bool AssessIfOutmatched(Actor player)
        {
            double playerThreat = player.Vitality * player.AttackDamage * player.Body.WeightKG;
            double animalThreat = this.Vitality * this.AttackDamage * this.Body.WeightKG;
            return playerThreat > animalThreat * .8;
        }

        /// <summary>
        /// Calculates initial boldness for a predator encounter based on observable factors.
        /// All factors are visible to the player so they can reason about outcomes.
        /// </summary>
        public double CalculateBoldness(Player.Player player, Inventory inventory)
        {
            double boldness = 0.4;

            bool hasMeat = inventory.Count(Resource.RawMeat) > 0 || inventory.Count(Resource.CookedMeat) > 0;
            if (hasMeat) boldness += 0.20;
            if (player.Vitality < 0.7) boldness += 0.15;
            if (player.Body.WeightKG > this.Body.WeightKG) boldness -= 0.10;

            // Blood scent attracts predators
            double bloodySeverity = player.EffectRegistry.GetSeverity("Bloody");
            if (bloodySeverity > 0)
                boldness += 0.15 * bloodySeverity;  // Up to +0.15 at full severity

            return Math.Clamp(boldness, 0.0, 1.0);
        }

        #endregion

        #region Trait and Activity Methods

        /// <summary>
        /// Generates randomized traits for this individual animal.
        /// Call after construction to make each animal unique.
        /// </summary>
        public void GenerateTraits()
        {
            // Size: most animals near average, with some outliers
            SizeModifier = Math.Clamp(GenerateNormalish(1.0, 0.15), 0.7, 1.3);
            Body.ApplySizeModifier(SizeModifier);

            // Condition: slightly favors healthy
            Condition = Math.Clamp(GenerateNormalish(0.75, 0.15), 0.3, 1.0);

            // Nervousness: based on behavior type
            double baseNervousness = BehaviorType switch
            {
                AnimalBehaviorType.Prey => 0.6,
                AnimalBehaviorType.Predator => 0.3,
                AnimalBehaviorType.Scavenger => 0.5,
                AnimalBehaviorType.DangerousPrey => 0.4,
                _ => 0.5
            };
            Nervousness = Math.Clamp(GenerateNormalish(baseNervousness, 0.2), 0.1, 0.9);

            // Distinguishing marks based on condition and chance
            DistinguishingMarks = [];
            if (Condition < 0.5 && _rng.NextDouble() < 0.6)
            {
                DistinguishingMarks.Add(_rng.NextDouble() < 0.5 ? "a limp" : "visible ribs");
            }
            if (_rng.NextDouble() < 0.10)
            {
                DistinguishingMarks.Add(_rng.NextDouble() < 0.5 ? "a scarred flank" : "old scars");
            }

            // Start with a random activity
            CurrentActivity = (AnimalActivity)_rng.Next(0, 3); // Grazing, Moving, or Resting
            _activityRemainingMinutes = GetActivityDuration(CurrentActivity);
        }

        /// <summary>
        /// Gets a player-facing description of this animal's observable traits.
        /// Example: "a lean doe with a scarred flank"
        /// </summary>
        public string GetTraitDescription()
        {
            var adjectives = new List<string>();

            // Size
            if (SizeModifier < 0.85) adjectives.Add("small");
            else if (SizeModifier > 1.15) adjectives.Add("large");

            // Condition
            if (Condition < 0.5) adjectives.Add("thin");
            else if (Condition > 0.85) adjectives.Add("healthy");

            // Nervousness (behavioral observation)
            if (Nervousness > 0.7) adjectives.Add("nervous");
            else if (Nervousness < 0.3) adjectives.Add("calm");

            string adjectiveStr = adjectives.Count > 0 ? string.Join(", ", adjectives) + " " : "";
            string result = $"a {adjectiveStr}{Name.ToLower()}";

            if (DistinguishingMarks.Count > 0)
            {
                result += $" with {string.Join(" and ", DistinguishingMarks)}";
            }

            return result;
        }

        /// <summary>
        /// Gets a description of current activity for player.
        /// Example: "grazing, head down"
        /// </summary>
        public string GetActivityDescription()
        {
            return CurrentActivity switch
            {
                AnimalActivity.Grazing => "grazing, head down",
                AnimalActivity.Moving => "on the move",
                AnimalActivity.Resting => "resting, but watchful",
                AnimalActivity.Alert => "alert, scanning the area",
                _ => ""
            };
        }

        /// <summary>
        /// Gets full description combining traits and current activity.
        /// Example: "a lean doe with a scarred flank, grazing, head down"
        /// </summary>
        public string GetFullDescription()
        {
            return $"{GetTraitDescription()}, {GetActivityDescription()}";
        }

        /// <summary>
        /// Gets detection modifier based on current activity.
        /// Lower = easier to approach undetected.
        /// </summary>
        public double GetActivityDetectionModifier()
        {
            return CurrentActivity switch
            {
                AnimalActivity.Grazing => 0.7,   // Head down, less aware
                AnimalActivity.Moving => 1.2,    // More alert while moving
                AnimalActivity.Resting => 0.9,   // Relaxed but not distracted
                AnimalActivity.Alert => 2.0,     // Very hard to approach
                _ => 1.0
            };
        }

        /// <summary>
        /// Updates animal activity state based on elapsed time.
        /// Called during approach/wait actions.
        /// </summary>
        public void UpdateActivity(int minutes)
        {
            _activityRemainingMinutes -= minutes;

            if (_activityRemainingMinutes <= 0)
            {
                CycleActivity();
            }
        }

        /// <summary>
        /// Checks if activity changed and returns true if it did.
        /// Useful for notifying player of changes during wait.
        /// </summary>
        public bool CheckActivityChange(int minutes, out AnimalActivity? newActivity)
        {
            var oldActivity = CurrentActivity;
            UpdateActivity(minutes);

            if (CurrentActivity != oldActivity)
            {
                newActivity = CurrentActivity;
                return true;
            }

            newActivity = null;
            return false;
        }

        private void CycleActivity()
        {
            // Weighted transition based on current activity
            CurrentActivity = CurrentActivity switch
            {
                AnimalActivity.Grazing => PickWeighted(
                    (AnimalActivity.Moving, 0.3),
                    (AnimalActivity.Resting, 0.4),
                    (AnimalActivity.Grazing, 0.25),
                    (AnimalActivity.Alert, 0.05)),
                AnimalActivity.Moving => PickWeighted(
                    (AnimalActivity.Grazing, 0.4),
                    (AnimalActivity.Resting, 0.4),
                    (AnimalActivity.Moving, 0.15),
                    (AnimalActivity.Alert, 0.05)),
                AnimalActivity.Resting => PickWeighted(
                    (AnimalActivity.Grazing, 0.5),
                    (AnimalActivity.Moving, 0.35),
                    (AnimalActivity.Resting, 0.1),
                    (AnimalActivity.Alert, 0.05)),
                AnimalActivity.Alert => PickWeighted(
                    (AnimalActivity.Grazing, 0.3),
                    (AnimalActivity.Moving, 0.3),
                    (AnimalActivity.Resting, 0.35),
                    (AnimalActivity.Alert, 0.05)),
                _ => AnimalActivity.Resting
            };

            _activityRemainingMinutes = GetActivityDuration(CurrentActivity);
        }

        private static int GetActivityDuration(AnimalActivity activity)
        {
            return activity switch
            {
                AnimalActivity.Grazing => _rng.Next(10, 30),
                AnimalActivity.Moving => _rng.Next(5, 15),
                AnimalActivity.Resting => _rng.Next(15, 40),
                AnimalActivity.Alert => _rng.Next(2, 8),
                _ => 10
            };
        }

        private static AnimalActivity PickWeighted(params (AnimalActivity activity, double weight)[] options)
        {
            double total = options.Sum(o => o.weight);
            double roll = _rng.NextDouble() * total;
            double cumulative = 0;

            foreach (var (activity, weight) in options)
            {
                cumulative += weight;
                if (roll < cumulative)
                    return activity;
            }

            return options[0].activity;
        }

        /// <summary>
        /// Generates values clustered around mean with given spread.
        /// </summary>
        private static double GenerateNormalish(double mean, double spread)
        {
            // Average of two uniform randoms approximates normal distribution
            double u1 = _rng.NextDouble();
            double u2 = _rng.NextDouble();
            return mean + (((u1 + u2) / 2.0) - 0.5) * 2.0 * spread;
        }

        #endregion

        public override string ToString() => Name;
    }

    
}
