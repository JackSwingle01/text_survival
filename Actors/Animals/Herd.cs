using System.Text.Json.Serialization;
using text_survival.Actions;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Environments;
using text_survival.Environments.Grid;

// Avoid ambiguity between Herd.AnimalType property and AnimalType enum
using AnimalTypeEnum = text_survival.Actors.Animals.AnimalType;

namespace text_survival.Actors.Animals;

/// <summary>
/// Behavioral state for a herd. All members share the same state.
/// </summary>
public enum HerdState
{
    Resting,
    Grazing,
    Patrolling,
    Alert,
    Fleeing,
    Hunting,
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

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AnimalTypeEnum AnimalType { get; set; }

    [JsonIgnore]
    public List<Animal> Members { get; private set; } = [];

    public int MemberCount { get; set; }

    #endregion

    #region Position & Territory

    public GridPosition Position { get; set; }
    public List<GridPosition> HomeTerritory { get; set; } = [];
    public int TerritoryIndex { get; set; }
    public GridPosition? TravelDestination { get; set; }
    public int TravelTimeRemainingMinutes { get; set; }

    #endregion

    #region State Machine

    public HerdState State { get; set; } = HerdState.Resting;
    public int StateTimeMinutes { get; set; }

    #endregion

    #region Shared Condition

    public double Hunger { get; set; }
    public bool IsWounded { get; set; }
    public double WoundSeverity { get; set; }
    public double Fear { get; set; }
    public int LastCombatMinutes { get; set; } = -9999;

    #endregion

    #region Behavior Strategy

    public HerdBehaviorType BehaviorType { get; set; } = HerdBehaviorType.Prey;

    [JsonIgnore]
    public IHerdBehavior? Behavior { get; private set; }

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

    public void SetBehavior(HerdBehaviorType type)
    {
        BehaviorType = type;
        RecreateBehavior();
    }

    #endregion

    #region Derived Properties

    [JsonIgnore]
    public bool IsPredator => AnimalType.IsPredator();

    [JsonIgnore]
    public int BaseDetectionRange => AnimalType.BaseDetectionRange();

    [JsonIgnore]
    public int Count => Members.Count > 0 ? Members.Count : MemberCount;

    [JsonIgnore]
    public bool IsEmpty => Count == 0;

    [JsonIgnore]
    public double TotalMassKg => Members.Sum(m => m.Body.WeightKG);

    [JsonIgnore]
    public AnimalDiet Diet => AnimalType.GetDiet();

    [JsonIgnore]
    public bool IsTraveling => TravelDestination != null;

    #endregion

    #region Constructor

    public Herd() { }

    public static Herd Create(AnimalTypeEnum animalType, GridPosition startPosition, List<GridPosition> territory)
    {
        var behaviorType = animalType.GetBehaviorType();

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

    public void AddMember(Animal animal)
    {
        Members.Add(animal);
        MemberCount = Members.Count;
    }

    public void RemoveMember(Animal animal)
    {
        Members.Remove(animal);
        MemberCount = Members.Count;
    }

    public void RecreateMembers(GameMap map)
    {
        if (Members.Count > 0 || MemberCount == 0) return;

        var location = map.GetLocationAt(Position);

        for (int i = 0; i < MemberCount; i++)
        {
            var animal = AnimalFactory.FromType(AnimalType, location, map);
            if (animal != null)
            {
                Members.Add(animal);
            }
        }
    }

    public Animal? GetRandomMember()
    {
        if (Members.Count == 0) return null;
        return Members[_rng.Next(Members.Count)];
    }

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

    public HerdUpdateResult Update(int elapsedMinutes, GameContext ctx)
    {
        // Ensure behavior is initialized
        if (Behavior == null)
        {
            RecreateBehavior();
        }

        // Decay fear over time (fixed rate)
        if (Fear > 0)
        {
            const double DecayPerMinute = 0.015;  // ~67 minutes to fully decay from 1.0
            Fear = Math.Max(0, Fear - elapsedMinutes * DecayPerMinute);
        }

        // Delegate to behavior strategy
        return Behavior!.Update(this, elapsedMinutes, ctx);
    }

    #endregion

    #region Movement

    public bool StartTravelTo(GridPosition destination, GameMap map)
    {
        if (TravelDestination != null) return false; // Already traveling

        var origin = map.GetLocationAt(Position);
        var destLocation = map.GetLocationAt(destination);
        if (origin == null || destLocation == null || !destLocation.IsPassable) return false;

        // Use first member as representative for speed calculation
        var representative = Members.FirstOrDefault();
        if (representative == null) return false;

        int travelMinutes = TravelProcessor.GetTraversalMinutes(origin, destLocation, representative, inventory: null);

        TravelDestination = destination;
        TravelTimeRemainingMinutes = travelMinutes;
        return true;
    }

    public bool UpdateTravel(int elapsedMinutes)
    {
        if (TravelDestination == null) return false;

        TravelTimeRemainingMinutes -= elapsedMinutes;

        if (TravelTimeRemainingMinutes <= 0)
        {
            Position = TravelDestination.Value;
            TravelDestination = null;
            TravelTimeRemainingMinutes = 0;
            return true;
        }

        return false;
    }

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

        var displayName = AnimalType.DisplayName().ToLower();
        string animalName = Count == 1 ? displayName : displayName + "s";

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

    public string GetTrackDescription()
    {
        return AnimalType switch
        {
            AnimalTypeEnum.Wolf => "wolf tracks, moving in a pack",
            AnimalTypeEnum.Bear or AnimalTypeEnum.CaveBear => "large bear prints, deep in the snow",
            AnimalTypeEnum.Caribou => "caribou tracks, many hooves",
            AnimalTypeEnum.Megaloceros => "massive deer tracks",
            AnimalTypeEnum.Bison => "heavy bison tracks",
            AnimalTypeEnum.Mammoth => "enormous mammoth tracks",
            AnimalTypeEnum.SaberTooth => "large cat prints",
            AnimalTypeEnum.Hyena => "hyena tracks, scattered",
            _ => $"fresh {AnimalType.DisplayName().ToLower()} tracks"
        };
    }

    #endregion
}
