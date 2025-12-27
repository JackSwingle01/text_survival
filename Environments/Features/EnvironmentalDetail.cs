using text_survival.Actions;
using text_survival.Actions.Variants;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Lightweight one-time interactable details for terrain tiles.
/// Adds world flavor and minor discoveries without the complexity of full features.
/// Examples: fallen logs, animal tracks, frozen puddles, old campsites.
/// </summary>
public class EnvironmentalDetail : LocationFeature
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
    private string? _mapIcon;
    public override string? MapIcon => !Interacted ? _mapIcon : null;
    public override int IconPriority => 0; // Low priority - other features show first

    /// <summary>
    /// Whether this detail has been discovered by the player.
    /// </summary>
    public bool Discovered { get; private set; } = true; // Start visible for now

    /// <summary>
    /// Whether this detail has been interacted with (one-time only).
    /// </summary>
    public bool Interacted { get; private set; } = false;

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
    /// Returns loot (if any) and examination text (for info-only details).
    /// Returns (null, null) if already interacted.
    /// </summary>
    public (Inventory? loot, string? examinationText) Interact()
    {
        if (Interacted) return (null, null);

        Interacted = true;

        // Loot-based detail
        if (Loot != null)
            return (Loot, null);

        // Info-only detail with examination pool
        if (ExaminationPool?.Length > 0)
        {
            var variant = ExaminationVariants.SelectRandom(ExaminationPool);
            return (new Inventory(), variant.Description);
        }

        // Fallback to base description
        return (new Inventory(), Description);
    }

    /// <summary>
    /// Check if this detail can be interacted with.
    /// </summary>
    public bool CanInteract => Discovered && !Interacted && (Loot != null || InteractionHint != null || ExaminationPool != null);

    /// <summary>
    /// Get status description for display.
    /// </summary>
    public string GetStatusDescription()
    {
        if (Interacted) return "examined";
        if (InteractionHint != null) return InteractionHint;
        return Description;
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
    /// </summary>
    public static EnvironmentalDetail AnimalTracks(string animalType = "deer")
    {
        return new EnvironmentalDetail("animal_tracks", "Animal Tracks", $"Fresh {animalType} tracks in the snow.")
        {
            _mapIcon = "footprint",
            InteractionHint = "examine the tracks",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.TrackExaminations
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
    /// </summary>
    public static EnvironmentalDetail AnimalDroppings(string animalType = "wolf")
    {
        return new EnvironmentalDetail("animal_droppings", "Animal Droppings", $"Fresh {animalType} scat.")
        {
            _mapIcon = "scatter_plot",
            InteractionHint = "examine the scat",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.DroppingExaminations
        };
    }

    /// <summary>
    /// Bent or broken branches suggesting something passed through.
    /// </summary>
    public static EnvironmentalDetail BentBranches()
    {
        return new EnvironmentalDetail("bent_branches", "Bent Branches", "Low branches bent and broken.")
        {
            _mapIcon = "line_curve",
            InteractionHint = "examine the branches",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.BranchExaminations
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
            _mapIcon = "fireplace",
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
