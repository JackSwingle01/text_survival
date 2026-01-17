using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actions.Tensions;
using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
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

    public string Id { get; init; }

    public string DisplayName { get; init; } = "";

    public string Description { get; init; } = "";

    public ExaminationVariant[]? ExaminationPool { get; init; }

    public string? _mapIcon;
    public override string? MapIcon => !Interacted ? _mapIcon : null;
    public override int IconPriority => 0; // Low priority - other features show first

    public bool _discovered = true; // Start visible for now
    public bool Discovered => _discovered;

    public bool _interacted = false;
    public bool Interacted => _interacted;

    public Inventory? Loot { get; init; }

    public string? InteractionHint { get; init; }

    public int InteractionMinutes { get; init; } = 5;

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

    public bool CanInteract => Discovered && !Interacted && (Loot != null || InteractionHint != null || ExaminationPool != null || TensionOnInteract != null);

    public string GetStatusDescription()
    {
        if (Interacted) return "examined";
        if (InteractionHint != null) return InteractionHint;
        return Description;
    }

    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!CanInteract) yield break;

        string label = InteractionHint != null
            ? $"{DisplayName} ({InteractionHint})"
            : DisplayName;

        yield return new WorkOption(label, $"examine_{Id}", new ExamineStrategy(Id));
    }

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

    public static EnvironmentalDetail AnimalTracks(AnimalType animalType = AnimalType.Caribou)
    {
        bool isPredator = animalType.IsPredator();
        var displayName = animalType.DisplayName().ToLower();

        return new EnvironmentalDetail("animal_tracks", "Animal Tracks", $"Fresh {displayName} tracks in the snow.")
        {
            _mapIcon = "tracks",
            InteractionHint = "examine the tracks",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.TrackExaminations,
            TensionOnInteract = isPredator
                ? () => ActiveTension.Stalked(0.2, animalType)
                : () => ActiveTension.FreshTrail(0.4, animalType)
        };
    }

    public static EnvironmentalDetail FrozenPuddle()
    {
        var loot = new Inventory();
        loot.WaterLiters = 0.2 + Random.Shared.NextDouble() * 0.3;

        return new EnvironmentalDetail("frozen_puddle", "Frozen Puddle", "A small frozen puddle. Ice looks thin enough to break.")
        {
            _mapIcon = "water",
            Loot = loot,
            InteractionHint = "break ice for water",
            InteractionMinutes = 5
        };
    }

    public static EnvironmentalDetail ForestPuddle()
    {
        var loot = new Inventory();
        loot.WaterLiters = 0.3 + Random.Shared.NextDouble() * 0.4;

        return new EnvironmentalDetail("forest_puddle", "Forest Puddle", "A shallow puddle fed by melting snow, sheltered by the trees.")
        {
            _mapIcon = "water",
            Loot = loot,
            InteractionHint = "collect water",
            InteractionMinutes = 3
        };
    }

    public static EnvironmentalDetail AnimalDroppings(AnimalType animalType = AnimalType.Wolf)
    {
        bool isPredator = animalType.IsPredator();
        var displayName = animalType.DisplayName().ToLower();

        return new EnvironmentalDetail("animal_droppings", "Animal Droppings", $"Fresh {displayName} scat.")
        {
            _mapIcon = "rocks",
            InteractionHint = "examine the scat",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.DroppingExaminations,
            TensionOnInteract = isPredator
                ? () => ActiveTension.Stalked(0.15, animalType)
                : () => ActiveTension.FreshTrail(0.3, animalType)
        };
    }

    public static EnvironmentalDetail BentBranches()
    {
        return new EnvironmentalDetail("bent_branches", "Bent Branches", "Low branches bent and broken.")
        {
            _mapIcon = "trail",
            InteractionHint = "examine the branches",
            InteractionMinutes = 1,
            ExaminationPool = ExaminationVariants.BranchExaminations,
            TensionOnInteract = () => Random.Shared.NextDouble() < 0.5
                ? ActiveTension.FreshTrail(0.3, null)
                : ActiveTension.Stalked(0.1)
        };
    }

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
            _mapIcon = "overlook",
            Loot = loot,
            InteractionHint = "gather stones",
            InteractionMinutes = 5
        };
    }

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
            _mapIcon = "thicket",
            Loot = loot,
            InteractionHint = "gather tinder",
            InteractionMinutes = 4
        };
    }

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
            _mapIcon = "bones",
            Loot = loot,
            InteractionHint = "collect bones",
            InteractionMinutes = 3
        };
    }

    public static EnvironmentalDetail DryGrassTussock()
    {
        var loot = new Inventory();
        int fiberCount = Random.Shared.Next(1, 3);
        for (int i = 0; i < fiberCount; i++)
        {
            loot.Add(Resource.PlantFiber, 0.03 + Random.Shared.NextDouble() * 0.04);
        }

        return new EnvironmentalDetail("dry_grass", "Dry Grass Tussock", "A clump of dead grass, dry and brittle.")
        {
            _mapIcon = "grass",
            Loot = loot,
            InteractionHint = "gather fiber",
            InteractionMinutes = 2
        };
    }

    public static EnvironmentalDetail AnimalBurrow()
    {
        return new EnvironmentalDetail("animal_burrow", "Animal Burrow", "A small hole in the ground. Something lives here.")
        {
            _mapIcon = "burrow",
            InteractionHint = "examine the burrow",
            InteractionMinutes = 1,
            ExaminationPool =
            [
                new ExaminationVariant("Fresh digging at the entrance. The occupant is nearby."),
                new ExaminationVariant("Tracks around the opening. Rabbit, most likely."),
                new ExaminationVariant("The burrow goes deep. You can hear movement below."),
                new ExaminationVariant("Fur caught on roots at the entrance. Recent activity.")
            ],
            TensionOnInteract = () => ActiveTension.FreshTrail(0.2, AnimalType.Rabbit)
        };
    }

    public static EnvironmentalDetail WindsweptSnow()
    {
        return new EnvironmentalDetail("windswept_snow", "Windswept Depression", "A shallow hollow where wind has scoured the snow thin.")
        {
            _mapIcon = "wind_shelter",
            InteractionHint = "examine the area",
            InteractionMinutes = 1,
            ExaminationPool =
            [
                new ExaminationVariant("Hard-packed snow. The wind keeps it clear."),
                new ExaminationVariant("Bare ground visible in patches. Nothing of use."),
                new ExaminationVariant("Ice crystals glint in the exposed surface.")
            ]
        };
    }

    public static EnvironmentalDetail OldNest()
    {
        var loot = new Inventory();
        // Mix of tinder and plant fiber
        if (Random.Shared.NextDouble() < 0.6)
        {
            loot.Add(Resource.Tinder, 0.01 + Random.Shared.NextDouble() * 0.02);
        }
        loot.Add(Resource.PlantFiber, 0.02 + Random.Shared.NextDouble() * 0.03);

        return new EnvironmentalDetail("old_nest", "Old Nest", "An abandoned bird nest, woven from grass and twigs.")
        {
            _mapIcon = "nest",
            Loot = loot,
            InteractionHint = "gather nesting material",
            InteractionMinutes = 2
        };
    }

    public static EnvironmentalDetail LichenRocks()
    {
        return new EnvironmentalDetail("lichen_rocks", "Lichen-Covered Rocks", "Pale green and orange lichens crust the stone.")
        {
            _mapIcon = "webs",
            InteractionHint = "examine the lichens",
            InteractionMinutes = 1,
            ExaminationPool =
            [
                new ExaminationVariant("The lichens are old. This rock hasn't moved in centuries."),
                new ExaminationVariant("Crusty growth patterns. The north-facing side is thickest."),
                new ExaminationVariant("Brittle and dry. Not edible, but the rock beneath is solid.")
            ]
        };
    }

    public static EnvironmentalDetail DeadReeds()
    {
        var loot = new Inventory();
        int reedCount = Random.Shared.Next(2, 4);
        for (int i = 0; i < reedCount; i++)
        {
            loot.Add(Resource.PlantFiber, 0.02 + Random.Shared.NextDouble() * 0.03);
        }

        return new EnvironmentalDetail("dead_reeds", "Dead Reeds", "Brown stalks of last season's growth, dry and hollow.")
        {
            _mapIcon = "grass",
            Loot = loot,
            InteractionHint = "gather reeds",
            InteractionMinutes = 3
        };
    }

    public static EnvironmentalDetail IceFormation()
    {
        return new EnvironmentalDetail("ice_formation", "Ice Formation", "Strange shapes in the frozen surface.")
        {
            _mapIcon = "ice",
            InteractionHint = "examine the ice",
            InteractionMinutes = 1,
            ExaminationPool =
            [
                new ExaminationVariant("Bubbles frozen mid-rise. The ice formed quickly here."),
                new ExaminationVariant("Cracks radiating from a pressure point. The ice groans."),
                new ExaminationVariant("Clear as glass in places. Dark water visible below."),
                new ExaminationVariant("Frost flowers cluster on the surface. Delicate and beautiful.")
            ]
        };
    }

    public override List<Resource> ProvidedResources() => [];
}
