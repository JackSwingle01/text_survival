using System.Text.Json.Serialization;
using text_survival.Actions;
using text_survival.Actors.Animals.Behaviors;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Items;

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
public class Herd : IMovable
{
    private static readonly Random _rng = new();

    #region Identity

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AnimalTypeEnum AnimalType { get; set; }

    public List<Animal> Members { get; set; } = [];

    public int MemberCount { get; set; }

    #endregion

    #region Position & Territory

    public Location CurrentLocation { get; set; } = null!;
    public GameMap Map { get; set; } = null!;

    /// <summary>Derived from CurrentLocation via Map. Use for spatial math only.</summary>
    [JsonIgnore]
    public GridPosition Position => Map?.GetPosition(CurrentLocation) ?? default;

    public List<GridPosition> HomeTerritory { get; set; } = [];
    public int TerritoryIndex { get; set; }
    public Location? TravelDestination { get; set; }
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

    /// <summary>True when the herd stands on its den tile (the first tile of its territory).</summary>
    [JsonIgnore]
    public bool AtDen => HomeTerritory.Count > 0 && Position == HomeTerritory[0];

    #endregion

    #region Boldness

    /// <summary>
    /// Chance (0-1) that this herd engages the target right now. The one boldness formula:
    /// species temperament, pack size, hunger, what it is defending, the target's vulnerability,
    /// the hour, and learned fear. Used both to decide whether an encounter starts and to seed
    /// the animals' morale when it does, so the wolf that approached boldly also fights boldly.
    /// </summary>
    public double BoldnessToward(Actor target, GameContext ctx, bool defending = false)
    {
        var t = AnimalType.Temperament();
        if (t.Cap <= 0) return 0;

        double bold = t.Base + Count * t.PerPackMember;
        if (Hunger > t.HungryAt) bold += t.HungerBonus;
        if (Hunger > t.StarvingAt) bold += t.HungerBonus;
        if (defending) bold += t.DefendBonus;

        // Target vulnerability
        bool bleeding = target.EffectRegistry.HasEffect("Bleeding") ||
                        target.EffectRegistry.GetSeverity("Bloody") > 0.3;
        if (bleeding) bold += 0.15;
        var inventory = target.Inventory;
        if (inventory != null && (inventory.Count(Resource.RawMeat) > 0 || inventory.Count(Resource.CookedMeat) > 0))
            bold += 0.1;
        if (target.GetCapacities().Moving < 0.5) bold += 0.2;
        if (target.Vitality < 0.7) bold += 0.1;
        if (ctx.GetTimeOfDay() == GameContext.TimeOfDay.Night) bold += 0.1;

        // Learned fear scales the whole thing down
        if (Fear > 0) bold *= 1.0 - Fear;

        return Math.Clamp(bold, 0, t.Cap);
    }

    #endregion

    #region Constructor

    public Herd() { }

    public static Herd Create(AnimalTypeEnum animalType, Location location, GameMap map, List<GridPosition> territory)
    {
        var behaviorType = animalType.GetBehaviorType();

        var herd = new Herd
        {
            AnimalType = animalType,
            CurrentLocation = location,
            Map = map,
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
            CurrentLocation = CurrentLocation,
            Map = Map,
            HomeTerritory = [Position, fleeDirection], // Small territory around where it fled
            TerritoryIndex = 0,
            BehaviorType = BehaviorType, // Inherit behavior type from parent herd
            State = HerdState.Fleeing,
            IsWounded = true,
            WoundSeverity = animal.WoundLevel,
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

        var destLocation = map.GetLocationAt(destination);
        if (destLocation == null || !destLocation.IsPassable) return false;

        // Use first member as representative for speed calculation
        var representative = Members.FirstOrDefault();
        if (representative == null) return false;

        int travelMinutes = TravelProcessor.GetTraversalMinutes(CurrentLocation, destLocation, representative, inventory: null);

        TravelDestination = destLocation;
        TravelTimeRemainingMinutes = travelMinutes;
        return true;
    }

    public bool UpdateTravel(int elapsedMinutes)
    {
        if (TravelDestination == null) return false;

        TravelTimeRemainingMinutes -= elapsedMinutes;

        if (TravelTimeRemainingMinutes <= 0)
        {
            CurrentLocation = TravelDestination;
            TravelDestination = null;
            TravelTimeRemainingMinutes = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get the best flee destination away from a threat (usually the player).
    /// Returns the passable neighbor tile furthest from the threat.
    /// </summary>
    public GridPosition? GetFleeTarget(GridPosition threat)
    {
        var options = Position.GetCardinalNeighbors()
            .Where(p => Map?.GetLocationAt(p)?.IsPassable ?? false)
            .OrderByDescending(p => p.ManhattanDistance(threat))
            .ToList();

        return options.FirstOrDefault();
    }

    /// <summary>
    /// Flee from a threat position. Starts travel to furthest passable neighbor.
    /// Returns a narrative message if the player can see the flee, or null.
    /// </summary>
    public string? FleeFrom(GridPosition threat)
    {
        var fleeTarget = GetFleeTarget(threat);

        if (fleeTarget != null && fleeTarget != Position)
        {
            var previousPosition = Position;

            if (!StartTravelTo(fleeTarget.Value, Map))
            {
                TransitionTo(HerdState.Resting);
                return null;
            }

            return null;
        }

        TransitionTo(HerdState.Resting);
        return null;
    }

    /// <summary>
    /// Try to move to a random territory tile. Used during grazing/patrolling.
    /// </summary>
    public void TryPatrolTerritory(int elapsedMinutes, double chancePerMinute)
    {
        if (HomeTerritory.Count == 0 || IsTraveling) return;

        double moveProbability = 1.0 - Math.Pow(1.0 - chancePerMinute, elapsedMinutes);

        if (_rng.NextDouble() < moveProbability)
        {
            TerritoryIndex = (TerritoryIndex + 1) % HomeTerritory.Count;
            StartTravelTo(HomeTerritory[TerritoryIndex], Map);
        }
    }

    /// <summary>
    /// Graze at current location, depleting forage resources.
    /// </summary>
    public void GrazeHere(int elapsedMinutes)
    {
        var forage = CurrentLocation?.Features.OfType<Environments.Features.ForageFeature>().FirstOrDefault();
        forage?.Graze(Diet, TotalMassKg, elapsedMinutes);
    }

    /// <summary>
    /// Get how grazed the current location is for this herd's diet (0-1).
    /// </summary>
    public double GetGrazedLevel()
    {
        var forage = CurrentLocation?.Features.OfType<Environments.Features.ForageFeature>().FirstOrDefault();
        return forage?.GetGrazingLevelForDiet(Diet) ?? 0;
    }

    /// <summary>
    /// Move one tile toward a target position. Starts travel if not already traveling.
    /// </summary>
    public void MoveToward(GridPosition target)
    {
        if (IsTraveling) return;

        int dx = Math.Sign(target.X - Position.X);
        int dy = Math.Sign(target.Y - Position.Y);

        GridPosition? newPos = null;
        if (Math.Abs(target.X - Position.X) >= Math.Abs(target.Y - Position.Y) && dx != 0)
        {
            newPos = new GridPosition(Position.X + dx, Position.Y);
        }
        else if (dy != 0)
        {
            newPos = new GridPosition(Position.X, Position.Y + dy);
        }
        else if (dx != 0)
        {
            newPos = new GridPosition(Position.X + dx, Position.Y);
        }

        if (newPos != null && Map?.GetLocationAt(newPos.Value)?.IsPassable == true)
        {
            StartTravelTo(newPos.Value, Map);
        }
    }

    /// <summary>
    /// Transition to a new state, resetting state timer.
    /// </summary>
    public void TransitionTo(HerdState newState)
    {
        State = newState;
        StateTimeMinutes = 0;
    }

    /// <summary>
    /// Whether the player is at the same position as this herd.
    /// </summary>
    [JsonIgnore]
    public bool IsPlayerHere => Map?.CurrentPosition == Position;

    /// <summary>
    /// Cardinal direction string from one position to another.
    /// </summary>
    public static string GetCardinalDirection(GridPosition from, GridPosition to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        return (dx, dy) switch
        {
            ( > 0, _) => "east",
            ( < 0, _) => "west",
            (_, > 0) => "south",
            (_, < 0) => "north",
            _ => "away"
        };
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
