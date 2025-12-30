using System.Text.Json.Serialization;
using text_survival.Actions;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals;

/// <summary>
/// Behavioral state for a herd. All members share the same state.
/// </summary>
public enum HerdState
{
    /// <summary>Staying in place, low alertness.</summary>
    Resting,

    /// <summary>Moving slowly within territory, eating. Reduces hunger.</summary>
    Grazing,

    /// <summary>Predators only: moving through territory actively.</summary>
    Patrolling,

    /// <summary>Detected stimulus, assessing threat. Freezes in place.</summary>
    Alert,

    /// <summary>Prey response: moving away from threat quickly.</summary>
    Fleeing,

    /// <summary>Predator response: pursuing player.</summary>
    Hunting,

    /// <summary>Predators at a kill, consuming prey. Will defend aggressively.</summary>
    Feeding
}

/// <summary>
/// A group of animals that move and behave together.
/// Even a solo bear is a "herd of 1". All members share position, hunger, and behavioral state.
/// </summary>
public class Herd
{
    private static readonly Random _rng = new();

    #region Identity

    /// <summary>Unique identifier for this herd.</summary>
    [JsonInclude]
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Type of animals in this herd (Wolf, Bear, Caribou, etc.).</summary>
    [JsonInclude]
    public string AnimalType { get; private set; } = "";

    /// <summary>The animals in this herd. Reuses existing Animal class for combat stats.</summary>
    /// <remarks>Not serialized - recreated on load from MemberCount and AnimalType.</remarks>
    [JsonIgnore]
    public List<Animal> Members { get; private set; } = [];

    /// <summary>Number of members for serialization. Animals recreated on load.</summary>
    [JsonInclude]
    public int MemberCount { get; private set; }

    #endregion

    #region Position & Territory

    /// <summary>Current tile position on the map.</summary>
    [JsonInclude]
    public GridPosition Position { get; set; }

    /// <summary>Tiles this herd uses as its home range.</summary>
    [JsonInclude]
    public List<GridPosition> HomeTerritory { get; private set; } = [];

    /// <summary>Current index in territory patrol cycle.</summary>
    [JsonInclude]
    public int TerritoryIndex { get; set; }

    #endregion

    #region State Machine

    /// <summary>Current behavioral state.</summary>
    [JsonInclude]
    public HerdState State { get; set; } = HerdState.Resting;

    /// <summary>How long the herd has been in current state (minutes).</summary>
    [JsonInclude]
    public int StateTimeMinutes { get; set; }

    /// <summary>State before becoming Alert (for returning after threat passes).</summary>
    [JsonInclude]
    public HerdState PreviousState { get; set; } = HerdState.Resting;

    #endregion

    #region Shared Condition

    /// <summary>Shared hunger level (0 = full, 1 = starving). Drives grazing behavior.</summary>
    [JsonInclude]
    public double Hunger { get; set; }

    /// <summary>Whether this herd has a wounded member (affects behavior).</summary>
    [JsonInclude]
    public bool IsWounded { get; set; }

    /// <summary>Severity of wound (0-1) for wounded herds.</summary>
    [JsonInclude]
    public double WoundSeverity { get; set; }

    #endregion

    #region Behavior Strategy

    /// <summary>Type of behavior for serialization. Behavior is recreated from this on load.</summary>
    [JsonInclude]
    public HerdBehaviorType BehaviorType { get; private set; } = HerdBehaviorType.Prey;

    /// <summary>The behavior strategy implementation. Not serialized - recreated on load.</summary>
    [JsonIgnore]
    public IHerdBehavior? Behavior { get; private set; }

    /// <summary>
    /// Recreates the behavior strategy from BehaviorType.
    /// Called after deserialization.
    /// </summary>
    public void RecreateBehavior()
    {
        Behavior = BehaviorType switch
        {
            HerdBehaviorType.Prey => new PreyBehavior(),
            HerdBehaviorType.PackPredator => new PackPredatorBehavior(),
            HerdBehaviorType.SolitaryPredator => new SolitaryPredatorBehavior(),
            HerdBehaviorType.Scavenger => new ScavengerBehavior(),
            _ => new PreyBehavior()
        };
    }

    /// <summary>
    /// Sets the behavior type and creates the behavior instance.
    /// </summary>
    public void SetBehavior(HerdBehaviorType type)
    {
        BehaviorType = type;
        RecreateBehavior();
    }

    #endregion

    #region Derived Properties

    /// <summary>True if this is a predator herd (wolf, bear, etc.).</summary>
    [JsonIgnore]
    public bool IsPredator => AnimalType.ToLower() switch
    {
        "wolf" or "bear" or "cave bear" or "saber-tooth" or "hyena" => true,
        _ => false
    };

    /// <summary>Detection range in tiles based on animal type.</summary>
    [JsonIgnore]
    public int BaseDetectionRange => AnimalType.ToLower() switch
    {
        "wolf" => 3,
        "bear" or "cave bear" => 2,
        "saber-tooth" or "saber-tooth tiger" => 3,  // Stealthy ambush hunter
        "hyena" or "cave hyena" => 2,
        "mammoth" or "woolly mammoth" => 2,
        "caribou" or "megaloceros" or "bison" => 2,
        _ => 2
    };

    /// <summary>Number of animals in the herd.</summary>
    [JsonIgnore]
    public int Count => Members.Count > 0 ? Members.Count : MemberCount;

    /// <summary>True if the herd has no members left.</summary>
    [JsonIgnore]
    public bool IsEmpty => Count == 0;

    /// <summary>Total mass of all herd members in kg.</summary>
    [JsonIgnore]
    public double TotalMassKg => Members.Sum(m => m.Body.WeightKG);

    /// <summary>The diet type for this herd based on animal type.</summary>
    [JsonIgnore]
    public AnimalDiet Diet => AnimalDietExtensions.GetDietForAnimal(AnimalType);

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new herd. Use static factory methods for convenience.
    /// </summary>
    public Herd() { }

    /// <summary>
    /// Creates a herd with specified type and starting position.
    /// </summary>
    public static Herd Create(string animalType, GridPosition startPosition, List<GridPosition> territory)
    {
        var behaviorType = animalType.ToLower() switch
        {
            "wolf" => HerdBehaviorType.PackPredator,
            "bear" or "cave bear" => HerdBehaviorType.SolitaryPredator,
            "saber-tooth" or "saber-tooth tiger" => HerdBehaviorType.SolitaryPredator,
            "hyena" or "cave hyena" => HerdBehaviorType.Scavenger,
            _ => HerdBehaviorType.Prey
        };

        var herd = new Herd
        {
            AnimalType = animalType,
            Position = startPosition,
            HomeTerritory = territory,
            TerritoryIndex = 0,
            BehaviorType = behaviorType,
            State = behaviorType switch
            {
                HerdBehaviorType.PackPredator => HerdState.Patrolling,
                HerdBehaviorType.SolitaryPredator => HerdState.Resting,
                HerdBehaviorType.Scavenger => HerdState.Patrolling,  // Always searching
                _ => HerdState.Grazing
            },
            Hunger = _rng.NextDouble() * 0.3 // Start slightly hungry
        };

        herd.RecreateBehavior();
        return herd;
    }

    #endregion

    #region Member Management

    /// <summary>
    /// Adds an animal to the herd.
    /// </summary>
    public void AddMember(Animal animal)
    {
        Members.Add(animal);
        MemberCount = Members.Count;
    }

    /// <summary>
    /// Removes an animal from the herd (after kill).
    /// </summary>
    public void RemoveMember(Animal animal)
    {
        Members.Remove(animal);
        MemberCount = Members.Count;
    }

    /// <summary>
    /// Recreates animal members from MemberCount and AnimalType.
    /// Called after deserialization.
    /// </summary>
    public void RecreateMembers()
    {
        if (Members.Count > 0 || MemberCount == 0) return;

        for (int i = 0; i < MemberCount; i++)
        {
            var animal = AnimalFactory.FromName(AnimalType);
            if (animal != null)
            {
                Members.Add(animal);
            }
        }
    }

    /// <summary>
    /// Gets a random member from the herd (for hunt target selection).
    /// </summary>
    public Animal? GetRandomMember()
    {
        if (Members.Count == 0) return null;
        return Members[_rng.Next(Members.Count)];
    }

    /// <summary>
    /// Splits off a wounded animal into its own herd of size 1.
    /// Returns the new herd containing the wounded animal.
    /// </summary>
    public Herd SplitOffWounded(Animal animal, GridPosition fleeDirection)
    {
        Members.Remove(animal);
        MemberCount = Members.Count;

        var newHerd = new Herd
        {
            AnimalType = AnimalType,
            Position = Position,
            HomeTerritory = [Position, fleeDirection], // Small territory around where it fled
            TerritoryIndex = 0,
            BehaviorType = BehaviorType, // Inherit behavior type from parent herd
            State = HerdState.Fleeing,
            IsWounded = true,
            WoundSeverity = animal.CurrentWoundSeverity,
            Hunger = Hunger
        };
        newHerd.AddMember(animal);
        newHerd.RecreateBehavior();

        return newHerd;
    }

    #endregion

    #region State Machine Update

    /// <summary>
    /// Updates the herd using behavior strategy. Called each game minute.
    /// New signature takes GameContext instead of individual parameters.
    /// </summary>
    /// <param name="elapsedMinutes">Minutes elapsed since last update.</param>
    /// <param name="ctx">Game context for behavior processing.</param>
    /// <returns>Result containing any encounters, narratives, or carcass creations.</returns>
    public HerdUpdateResult Update(int elapsedMinutes, GameContext ctx)
    {
        // Ensure behavior is initialized
        if (Behavior == null)
        {
            RecreateBehavior();
        }

        // Delegate to behavior strategy
        return Behavior!.Update(this, elapsedMinutes, ctx);
    }

    /// <summary>
    /// Legacy Update method for compatibility. Delegates to new behavior-based Update.
    /// </summary>
    /// <param name="elapsedMinutes">Minutes elapsed since last update.</param>
    /// <param name="playerPosition">Current player position (unused in new system).</param>
    /// <param name="playerCarryingMeat">Whether player is carrying meat (unused in new system).</param>
    /// <param name="playerBleeding">Whether player is bleeding (unused in new system).</param>
    /// <returns>True if the herd initiated an encounter with the player.</returns>
    [Obsolete("Use Update(int, GameContext) instead. This method is kept for compatibility.")]
    public bool Update(int elapsedMinutes, GridPosition playerPosition, bool playerCarryingMeat, bool playerBleeding)
    {
        // Legacy fallback - use inline behavior without GameContext
        StateTimeMinutes += elapsedMinutes;

        // Update hunger
        double hungerRate = IsPredator ? 0.002 : 0.003;
        Hunger = Math.Clamp(Hunger + elapsedMinutes * hungerRate, 0, 1);

        // Wounded herds heal over time
        if (IsWounded)
        {
            WoundSeverity = Math.Max(0, WoundSeverity - elapsedMinutes * 0.0002);
            if (WoundSeverity <= 0)
                IsWounded = false;
        }

        // Only trigger encounter if predator is at player position
        if (IsPredator && Position == playerPosition && State == HerdState.Patrolling)
        {
            // Random encounter chance based on hunger
            if (Hunger > 0.5 && new Random().NextDouble() < 0.3)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Movement

    /// <summary>
    /// Moves randomly within territory (for grazing).
    /// </summary>
    private void MoveWithinTerritory()
    {
        if (HomeTerritory.Count == 0) return;

        // 30% chance to move each update while grazing
        if (_rng.NextDouble() < 0.3)
        {
            // Pick a random territory tile
            Position = HomeTerritory[_rng.Next(HomeTerritory.Count)];
        }
    }

    /// <summary>
    /// Moves to next tile in territory patrol (for predators).
    /// </summary>
    private void MoveToNextTerritoryTile()
    {
        if (HomeTerritory.Count == 0) return;

        // Move every ~30 minutes of patrol time
        if (StateTimeMinutes > 0 && StateTimeMinutes % 30 == 0)
        {
            TerritoryIndex = (TerritoryIndex + 1) % HomeTerritory.Count;
            Position = HomeTerritory[TerritoryIndex];
        }
    }

    /// <summary>
    /// Moves toward a target position.
    /// </summary>
    private void MoveToward(GridPosition target)
    {
        // Move one tile toward target per update
        int dx = Math.Sign(target.X - Position.X);
        int dy = Math.Sign(target.Y - Position.Y);

        // Prefer the direction with greater distance
        if (Math.Abs(target.X - Position.X) > Math.Abs(target.Y - Position.Y))
        {
            Position = new GridPosition(Position.X + dx, Position.Y);
        }
        else if (dy != 0)
        {
            Position = new GridPosition(Position.X, Position.Y + dy);
        }
        else if (dx != 0)
        {
            Position = new GridPosition(Position.X + dx, Position.Y);
        }
    }

    /// <summary>
    /// Moves away from a threat position.
    /// </summary>
    private void MoveAwayFrom(GridPosition threat)
    {
        // Move one tile away from threat
        int dx = Math.Sign(Position.X - threat.X);
        int dy = Math.Sign(Position.Y - threat.Y);

        // If directly adjacent, pick a direction
        if (dx == 0 && dy == 0)
        {
            dx = _rng.Next(2) == 0 ? 1 : -1;
        }

        // Prefer the direction with greater distance
        if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
        {
            Position = new GridPosition(Position.X + dx, Position.Y);
        }
        else if (dy != 0)
        {
            Position = new GridPosition(Position.X, Position.Y + dy);
        }
    }

    #endregion

    #region Description

    /// <summary>
    /// Gets a description of this herd for player display.
    /// </summary>
    public string GetDescription()
    {
        string countDesc = Count switch
        {
            1 => "a lone",
            2 => "a pair of",
            <= 4 => "a small group of",
            <= 8 => "a group of",
            _ => "a large herd of"
        };

        string animalName = Count == 1 ? AnimalType.ToLower() : AnimalType.ToLower() + "s";

        string stateDesc = State switch
        {
            HerdState.Resting => "resting",
            HerdState.Grazing => "grazing",
            HerdState.Patrolling => "patrolling",
            HerdState.Alert => "alert",
            HerdState.Fleeing => "fleeing",
            HerdState.Hunting => "hunting",
            HerdState.Feeding => "feeding on a kill",
            _ => ""
        };

        return $"{countDesc} {animalName}, {stateDesc}";
    }

    /// <summary>
    /// Gets a track/sign description for foraging clues.
    /// </summary>
    public string GetTrackDescription()
    {
        return AnimalType.ToLower() switch
        {
            "wolf" => "wolf tracks, moving in a pack",
            "bear" => "large bear prints, deep in the snow",
            "caribou" => "caribou tracks, many hooves",
            "megaloceros" => "massive deer tracks",
            "bison" => "heavy bison tracks",
            _ => $"fresh {AnimalType.ToLower()} tracks"
        };
    }

    #endregion
}
