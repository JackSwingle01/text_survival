using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Items;

namespace text_survival.Environments.Features;

/// <summary>
/// Items dropped on the ground at a location (overflow from full inventory).
/// No capacity limit, no protection. Player can pick up or drop items freely.
/// Auto-removed when empty.
/// </summary>
public class GroundItemsFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => HasItems ? "ground_items" : null;
    public override int IconPriority => 1;

    public Inventory Storage { get; } = new() { MaxWeightKg = 10000 };

    public bool HasItems => !Storage.IsEmpty;

    public GroundItemsFeature() : base("Dropped Items") { }

    public void Add(Inventory items)
    {
        Storage.Combine(items);
    }

    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!HasItems) yield break;

        yield return new WorkOption(
            $"Pick up items ({Storage.CurrentWeightKg:F1} kg on ground)",
            "ground_stash",
            new GroundStashStrategy()
        );
    }

    public override List<Resource> ProvidedResources() =>
        Storage.GetResourceTypes();
}
