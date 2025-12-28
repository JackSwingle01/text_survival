using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actions.Tensions;
using text_survival.Actions.Variants;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Lightweight one-time interactable details for terrain tiles.
/// Adds world flavor and minor discoveries without the complexity of full features.
/// Examples: fallen logs, animal tracks, frozen puddles, old campsites.
/// </summary>
public class EnvironmentalDetail : LocationFeature, IWorkableFeature
{
    private static int _nextId = 1;

    /// <summary>
    /// Unique identifier for frontend reference.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Display name for the detail (shown in popup).
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Description text when discovered/examined.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Pool of examination variants for info-only details.
    /// When interacted, a random variant is selected for flavor text.
    /// </summary>
    public ExaminationVariant[]? ExaminationPool { get; init; }

    /// <summary>
    /// Material symbol for map display.
    /// </summary>
    public string? _mapIcon;
    public override string? MapIcon => !Interacted ? _mapIcon : null;
    public override int IconPriority => 0; // Low priority - other features show first

    /// <summary>
    /// Whether this detail has been discovered by the player.
    /// </summary>
    public bool _discovered = true; // Start visible for now
    public bool Discovered => _discovered;

    /// <summary>
    /// Whether this detail has been interacted with (one-time only).
    /// </summary>
    public bool _interacted = false;
    public bool Interacted => _interacted;

    /// <summary>
    /// Optional loot when interacted with.
    /// </summary>
    public Inventory? Loot { get; init; }

    /// <summary>
    /// Hint text shown in popup (e.g., "might find some sticks").
    /// </summary>
    public string? InteractionHint { get; init; }

    /// <summary>
    /// Time in minutes to interact with this detail.
    /// </summary>
    public int InteractionMinutes { get; init; } = 5;

    /// <summary>
    /// Optional tension created when this detail is examined.
    /// Factory function to create the tension (allows randomization).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<ActiveTension>? TensionOnInteract { get; init; }

    [System.Text.Json.Serialization.JsonConstructor]
    public EnvironmentalDetail() : base("environmental_detail")
    {
        Id = $"detail_{_nextId++}";
    }

    public EnvironmentalDetail(string name, string displayName, string description, string? icon = null)
        : base(name)
    {
        Id = $"detail_{_nextId++}";
        DisplayName = displayName;
        Description = description;
        _mapIcon = icon;
    }

    /// <summary>
    /// Interact with this detail.
    /// Returns loot (if any), examination text (for info-only details), and tension (if any).
    /// Returns (null, null, null) if already interacted.
    /// </summary>
    public (Inventory? loot, string? examinationText, ActiveTension? tension) Interact()
    {
        if (Interacted) return (null, null, null);

        _interacted = true;

        // Create tension if this detail produces one
        var tension = TensionOnInteract?.Invoke();

        // Loot-based detail
        if (Loot != null)
            return (Loot, null, tension);

        // Info-only detail with examination pool
        if (ExaminationPool?.Length > 0)
        {
            var variant = ExaminationVariants.SelectRandom(ExaminationPool);
            return (new Inventory(), variant.Description, tension);
        }

        // Fallback to base description
        return (new Inventory(), Description, tension);
    }

    /// <summary>
    /// Check if this detail can be interacted with.
    /// </summary>
    public bool CanInteract => Discovered && !Interacted && (Loot != null || InteractionHint != null || ExaminationPool != null || TensionOnInteract != null);

    /// <summary>
    /// Get status description for display.
    /// </summary>
    public string GetStatusDescription()
    {
        if (Interacted) return "examined";
        if (InteractionHint != null) return InteractionHint;
        return Description;
    }

    /// <summary>
    /// Returns work option for examining this detail.
    /// Part of IWorkableFeature - allows details to appear in the regular work menu.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!CanInteract) yield break;

        string label = InteractionHint != null
            ? $"{DisplayName} ({InteractionHint})"
            : DisplayName;

        yield return new WorkOption(label, $"examine_{Id}", new ExamineStrategy(Id));
    }

    // ===== Factory Methods for Common Details =====

    /// <summary>
    /// A fallen log with some gatherable sticks.
    /// </summary>
    public static EnvironmentalDetail FallenLog()
    {
        var loot = new Inventory();
        int stickCount = Random.Shared.Next(2, 5);
        for (int i = 0; i < stickCount; i++)
        {
            loot.Add(Resource.Stick, 0.2 + Random.Shared.NextDouble() * 0.2);
        }

        return new EnvironmentalDetail("fallen_log", "Fallen Log", "A rotting log, partially buried in snow.")
        {
            _mapIcon = "forest",
            Loot = loot,
            InteractionHint = "gather some sticks",
            InteractionMinutes = 3
        };
    }

    /// <summary>
    /// Animal tracks indicating nearby game.
    /// Prey tracks create FreshTrail tension, predator tracks create weak Stalked tension.
    /// </summary>
    public static EnvironmentalDetail AnimalTracks(string animalType = "deer")
    {
        bool isPredator = animalType is "wolf" or "bear";

        return new EnvironmentalDetail("animal_tracks", "Animal Tracks", $"Fresh {animalType} tracks in the snow.")
        {
            _mapIcon = "pets",
            InteractionHint = "examine the tracks",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.TrackExaminations,
            TensionOnInteract = isPredator
                ? () => ActiveTension.Stalked(0.2, animalType)
                : () => ActiveTension.FreshTrail(0.4, animalType)
        };
    }

    /// <summary>
    /// A frozen puddle with a small amount of water.
    /// </summary>
    public static EnvironmentalDetail FrozenPuddle()
    {
        var loot = new Inventory();
        loot.WaterLiters = 0.2 + Random.Shared.NextDouble() * 0.3;

        return new EnvironmentalDetail("frozen_puddle", "Frozen Puddle", "A small frozen puddle. Ice looks thin enough to break.")
        {
            _mapIcon = "water_drop",
            Loot = loot,
            InteractionHint = "break ice for water",
            InteractionMinutes = 5
        };
    }

    /// <summary>
    /// Animal droppings indicating territory.
    /// Prey droppings create FreshTrail tension, predator droppings create weak Stalked tension.
    /// </summary>
    public static EnvironmentalDetail AnimalDroppings(string animalType = "wolf")
    {
        bool isPredator = animalType is "wolf" or "bear";

        return new EnvironmentalDetail("animal_droppings", "Animal Droppings", $"Fresh {animalType} scat.")
        {
            _mapIcon = "scatter_plot",
            InteractionHint = "examine the scat",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.DroppingExaminations,
            TensionOnInteract = isPredator
                ? () => ActiveTension.Stalked(0.15, animalType)
                : () => ActiveTension.FreshTrail(0.3, animalType)
        };
    }

    /// <summary>
    /// Bent or broken branches suggesting something passed through.
    /// 50/50 chance of creating FreshTrail (prey) or weak Stalked (predator) tension.
    /// </summary>
    public static EnvironmentalDetail BentBranches()
    {
        return new EnvironmentalDetail("bent_branches", "Bent Branches", "Low branches bent and broken.")
        {
            _mapIcon = "line_curve",
            InteractionHint = "examine the branches",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.BranchExaminations,
            TensionOnInteract = () => Random.Shared.NextDouble() < 0.5
                ? ActiveTension.FreshTrail(0.3, "unknown")
                : ActiveTension.Stalked(0.1)
        };
    }

    /// <summary>
    /// A small stone pile with potential materials.
    /// </summary>
    public static EnvironmentalDetail StonePile()
    {
        var loot = new Inventory();
        int stoneCount = Random.Shared.Next(1, 4);
        for (int i = 0; i < stoneCount; i++)
        {
            loot.Add(Resource.Stone, 0.3 + Random.Shared.NextDouble() * 0.3);
        }

        return new EnvironmentalDetail("stone_pile", "Stone Pile", "Loose rocks piled at the base of a cliff.")
        {
            _mapIcon = "landscape",
            Loot = loot,
            InteractionHint = "gather stones",
            InteractionMinutes = 5
        };
    }

    /// <summary>
    /// An old campfire ring, long cold.
    /// </summary>
    public static EnvironmentalDetail OldCampfire()
    {
        var loot = new Inventory();
        // Small chance of finding charcoal
        if (Random.Shared.NextDouble() < 0.4)
        {
            loot.Add(Resource.Charcoal, 0.05 + Random.Shared.NextDouble() * 0.1);
        }

        return new EnvironmentalDetail("old_campfire", "Old Campfire", "The remains of a fire ring. Long since cold.")
        {
            _mapIcon = null, // No icon - cold campfires aren't visually prominent
            Loot = loot.IsEmpty ? null : loot,
            InteractionHint = loot.IsEmpty ? null : "sift through ashes",
            InteractionMinutes = 3
        };
    }

    /// <summary>
    /// A hollow tree with potential tinder.
    /// </summary>
    public static EnvironmentalDetail HollowTree()
    {
        var loot = new Inventory();
        int tinderCount = Random.Shared.Next(1, 3);
        for (int i = 0; i < tinderCount; i++)
        {
            loot.Add(Resource.Tinder, 0.02 + Random.Shared.NextDouble() * 0.03);
        }

        return new EnvironmentalDetail("hollow_tree", "Hollow Tree", "A dead tree with a hollow center. Dry material inside.")
        {
            _mapIcon = "nature",
            Loot = loot,
            InteractionHint = "gather tinder",
            InteractionMinutes = 4
        };
    }

    /// <summary>
    /// Scattered bones from an old kill.
    /// </summary>
    public static EnvironmentalDetail ScatteredBones()
    {
        var loot = new Inventory();
        int boneCount = Random.Shared.Next(1, 3);
        for (int i = 0; i < boneCount; i++)
        {
            loot.Add(Resource.Bone, 0.1 + Random.Shared.NextDouble() * 0.2);
        }

        return new EnvironmentalDetail("scattered_bones", "Scattered Bones", "Bones picked clean by scavengers. An old kill.")
        {
            _mapIcon = "skeleton",
            Loot = loot,
            InteractionHint = "collect bones",
            InteractionMinutes = 3
        };
    }
}
