using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// A wooded area with standing trees that can be felled with an axe.
/// Unlike HarvestableFeature, this uses a discrete yield model:
/// work accumulates across sessions until a tree is felled,
/// then yields a large amount of wood and resets for the next tree.
/// </summary>
public class WoodedAreaFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => null; // Forests are common - no need for map icon
    public override int IconPriority => 1;

    /// <summary>
    /// Type of wood in this area (Pine, Birch, Oak).
    /// Null means mixed forest with random wood types.
    /// </summary>
    public Resource? WoodType { get; set; }

    /// <summary>
    /// Minutes of work required to fell one tree.
    /// </summary>
    public double MinutesToFell { get; set; } = 150;

    /// <summary>
    /// Minutes worked on the current tree (persists across sessions).
    /// </summary>
    public double MinutesWorked { get; set; } = 0;

    /// <summary>
    /// Number of trees available. Null means unlimited.
    /// </summary>
    public int? TreesAvailable { get; set; }

    /// <summary>
    /// Hours until a depleted tree respawns. Only used if TreesAvailable is limited.
    /// </summary>
    public double RespawnHoursPerTree { get; set; } = 168; // 1 week

    /// <summary>
    /// Accumulated respawn time (in hours).
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    private double _respawnAccumulator = 0;

    /// <summary>
    /// Progress as a percentage (0-1).
    /// </summary>
    public double ProgressPct => MinutesToFell > 0 ? MinutesWorked / MinutesToFell : 0;

    /// <summary>
    /// Whether the current tree is ready to be felled.
    /// </summary>
    public bool IsTreeReady => MinutesWorked >= MinutesToFell;

    /// <summary>
    /// Whether there are trees available to chop.
    /// </summary>
    public bool HasTrees => TreesAvailable == null || TreesAvailable > 0;

    public WoodedAreaFeature() : base("Wooded Area") { }

    public WoodedAreaFeature(string name, Resource? woodType, double minutesToFell, int? treesAvailable = null)
        : base(name)
    {
        WoodType = woodType;
        MinutesToFell = minutesToFell;
        TreesAvailable = treesAvailable;
    }

    /// <summary>
    /// Provides work option to chop wood if trees are available.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!HasTrees)
            yield break;

        string progressText = MinutesWorked > 0 ? $" ({ProgressPct:P0})" : "";
        string woodName = WoodType?.ToString() ?? "Mixed";

        yield return new WorkOption(
            $"Chop {woodName} wood{progressText}",
            "chop_wood",
            new ChoppingStrategy()
        );
    }

    /// <summary>
    /// Add work progress. Called by ChoppingStrategy.
    /// </summary>
    public void AddProgress(double minutes)
    {
        MinutesWorked += minutes;
    }

    /// <summary>
    /// Fell the tree and get the yield. Resets progress for next tree.
    /// Returns an Inventory containing the harvested resources.
    /// </summary>
    public Inventory FellTree()
    {
        var yield = new Inventory();

        // Determine wood type for this tree
        Resource logType = WoodType ?? GetRandomWoodType();

        // 8-10 logs (random)
        int logCount = Random.Shared.Next(8, 11);
        double logWeight = logType == Resource.Oak ? 4.0 : (logType == Resource.Pine ? 2.5 : 2.0);
        for (int i = 0; i < logCount; i++)
        {
            // Slight weight variation
            double weight = logWeight * (0.9 + Random.Shared.NextDouble() * 0.2);
            yield.Add(logType, weight);
        }

        // 4-6 sticks from branches
        int stickCount = Random.Shared.Next(4, 7);
        for (int i = 0; i < stickCount; i++)
        {
            yield.Add(Resource.Stick, 0.2 + Random.Shared.NextDouble() * 0.3);
        }

        // 2-3 tinder from bark
        int tinderCount = Random.Shared.Next(2, 4);
        for (int i = 0; i < tinderCount; i++)
        {
            yield.Add(Resource.Tinder, 0.03 + Random.Shared.NextDouble() * 0.05);
        }

        // Reset for next tree
        MinutesWorked = 0;

        // Decrement tree count if limited
        if (TreesAvailable != null)
        {
            TreesAvailable--;
        }

        return yield;
    }

    private static Resource GetRandomWoodType()
    {
        return Random.Shared.NextDouble() switch
        {
            < 0.4 => Resource.Pine,
            < 0.75 => Resource.Birch,
            _ => Resource.Oak
        };
    }

    /// <summary>
    /// Update respawn timer for limited tree counts.
    /// </summary>
    public override void Update(int minutes)
    {
        // Only respawn if we have a limited tree count and are depleted
        if (TreesAvailable == null || TreesAvailable > 0)
            return;

        _respawnAccumulator += minutes / 60.0;

        if (_respawnAccumulator >= RespawnHoursPerTree)
        {
            TreesAvailable++;
            _respawnAccumulator -= RespawnHoursPerTree;
        }
    }

    public override FeatureUIInfo? GetUIInfo()
    {
        if (!HasTrees) return null;
        return new FeatureUIInfo(
            "wood",
            WoodType?.ToString() ?? "Mixed",
            ProgressPct > 0 ? $"{(int)(ProgressPct * 100)}% felled" : "trees available",
            null);
    }

    /// <summary>
    /// Get a status description for display.
    /// </summary>
    public string GetStatusDescription()
    {
        if (!HasTrees)
            return "cleared";
        if (MinutesWorked > 0)
            return $"tree {ProgressPct:P0} felled";
        if (TreesAvailable != null)
            return $"{TreesAvailable} trees";
        return "standing timber";
    }

    public override List<Resource> ProvidedResources()
    {
        if (!HasTrees) return [];
        List<Resource> resources = [Resource.Stick, Resource.Tinder];
        if (WoodType != null)
            resources.Add(WoodType.Value);
        else
            resources.AddRange([Resource.Pine, Resource.Birch, Resource.Oak]);
        return resources;
    }
}
