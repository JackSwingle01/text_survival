using System.Text.Json.Serialization;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals
{
    public enum AnimalActivity
    {
        Grazing,
        Moving,
        Resting,
        Alert
    }

    public class Animal : Actor
    {
        private static readonly Random _rng = new();
        #region Combat Properties

        [JsonInclude] private double _attackDamage;
        [JsonInclude] private double _blockChance;
        [JsonInclude] private string _attackName = "";
        [JsonInclude] private DamageType _attackType;

        public override double AttackDamage => _attackDamage;
        public override double BlockChance => _blockChance;
        public override string AttackName => _attackName;
        public override DamageType AttackType => _attackType;

        #endregion

        #region Properties

        public AnimalType AnimalType { get; set; }

        public override double BaseThreat { get; set; }
        public override double StartingBoldness { get; set; }
        public override double BaseAggression { get; set; }
        public override double BaseCohesion { get; set; }

        public string Description { get; set; } = "";
        public bool IsHostile { get; set; } = true;
        public AnimalBehaviorType BehaviorType { get; set; }
        public AnimalSize Size { get; set; }
        public int FailedStealthChecks { get; set; }
        public int TrackingDifficulty { get; set; }
        public bool IsBleeding { get; set; }
        public DateTime? WoundedTime { get; set; }
        [JsonInclude] private double _speedMps;
        [JsonInclude] private double _pursuitCommitmentSeconds;
        public double SpeedMps => _speedMps;
        public double PursuitCommitmentSeconds => _pursuitCommitmentSeconds;
        public double DisengageAfterMaul { get; set; }

        /// <summary>How badly hurt this animal is (0-1): its worst tissue or its blood, whichever is worse.</summary>
        [JsonIgnore]
        public double WoundLevel => Math.Max(1 - Body.Parts.Min(p => p.Condition), 1 - Body.Blood.Condition);
        public List<(Resource resource, double kgYield)> SpecialYields { get; init; } = [];
        public bool IsMegafauna => Body.WeightKG > 500;

        #endregion

        #region Individual Traits

        public double SizeModifier { get; set; } = 1.0;
        public double Condition { get; set; } = 1.0;
        public double Nervousness { get; set; } = 0.5;
        public List<string> DistinguishingMarks { get; set; } = [];
        public AnimalActivity CurrentActivity { get; set; } = AnimalActivity.Resting;
        private int _activityRemainingMinutes = 10;

        #endregion

        #region Constructor

        /// <summary>Parameterless constructor for JSON deserialization.</summary>
        public Animal() : base("", Body.BaselineHumanStats, null!, null!) { }

        public Animal(
            AnimalType animalType,
            BodyCreationInfo bodyStats,
            AnimalBehaviorType behaviorType,
            AnimalSize size,
            double attackDamage,
            string attackName,
            DamageType attackType,
            Location location,
            GameMap map,
            double speedMps = 6.0,
            double pursuitCommitment = 45.0,
            bool isHostile = true,
            double blockChance = 0.01,
            double disengageAfterMaul = 0.1)
            : base(animalType.DisplayName(), bodyStats, location, map)
        {
            // Core identity
            AnimalType = animalType;

            // Combat stats
            _attackDamage = attackDamage;
            _attackName = attackName;
            _attackType = attackType;
            _blockChance = blockChance;

            // Behavior
            BehaviorType = behaviorType;
            Size = size;
            IsHostile = isHostile;
            _speedMps = speedMps;
            _pursuitCommitmentSeconds = pursuitCommitment;
            DisengageAfterMaul = disengageAfterMaul;

            // Defaults
            FailedStealthChecks = 0;
            TrackingDifficulty = 5;
        }

        #endregion

        #region Trait and Activity Methods

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

        public string GetFullDescription()
        {
            return $"{GetTraitDescription()}, {GetActivityDescription()}";
        }

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

        public void UpdateActivity(int minutes)
        {
            _activityRemainingMinutes -= minutes;

            if (_activityRemainingMinutes <= 0)
            {
                CycleActivity();
            }
        }

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
