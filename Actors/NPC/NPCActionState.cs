using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Actors;

/// <summary>Durable action data. Runtime recipes are resolved by identity, never serialized delegates.</summary>
public sealed class NPCActionState
{
    public string Kind { get; set; } = "";
    public int Duration { get; set; }
    public int Progress { get; set; }
    public bool Settled { get; set; }
    public bool IsFollowingPursuit { get; set; }
    public Location? Destination { get; set; }
    public Actor? Target { get; set; }
    public Resource Resource { get; set; }
    public ResourceCategory Category { get; set; }
    public ToolType Tool { get; set; }
    public double Amount { get; set; }
    public List<Resource>? Wanted { get; set; }
    public string? RecipeName { get; set; }
    public ShelterImprovementType Improvement { get; set; }
    public int Quantity { get; set; }

    public static NPCActionState Capture(NPCAction action)
    {
        var state = new NPCActionState { Kind = action.GetType().Name, Duration = action.DurationMinutes, Progress = action.MinutesSpent, Settled = action.Settled, IsFollowingPursuit = action.IsFollowingPursuit };
        switch (action)
        {
            case NPCEat eat: state.Resource = eat.Food; state.Amount = eat.Amount; break;
            case NPCMove move: state.Destination = move.Destination; break;
            case NPCHarvest harvest: state.Wanted = harvest.Wanted?.ToList(); break;
            case NPCStash stash: state.Category = stash.Category; break;
            case NPCTakeToolFromCache take: state.Tool = take.Tool; break;
            case NPCTakeResourceFromCache take: state.Category = take.Category; state.Amount = take.TargetWeight; break;
            case NPCCraft craft: state.RecipeName = craft.Recipe.Name; break;
            case NPCFight fight: state.Target = fight.Threat; break;
            case NPCFlee flee: state.Target = flee.Threat; break;
            case NPCImproveShelter shelter: state.Improvement = shelter.Improvement; state.Resource = shelter.Material; state.Quantity = shelter.Quantity; break;
        }
        return state;
    }

    public NPCAction Restore(NPC npc)
    {
        NPCAction action = Kind switch
        {
            nameof(NPCEat) => new NPCEat(Resource, Amount),
            nameof(NPCMove) => new NPCMove(Destination!, npc),
            nameof(NPCForage) => new NPCForage(Duration),
            nameof(NPCHarvest) => new NPCHarvest(Duration, Wanted),
            nameof(NPCChopWood) => new NPCChopWood(Duration),
            nameof(NPCStartFire) => new NPCStartFire(),
            nameof(NPCTendFire) => new NPCTendFire(),
            nameof(NPCRest) => new NPCRest(Duration),
            nameof(NPCSleep) => new NPCSleep(Duration),
            nameof(NPCStash) => new NPCStash(Category),
            nameof(NPCStashWater) => new NPCStashWater(),
            nameof(NPCTakeToolFromCache) => new NPCTakeToolFromCache(Tool),
            nameof(NPCTakeResourceFromCache) => new NPCTakeResourceFromCache(Category, Amount),
            nameof(NPCCraft) => new NPCCraft(new NeedCraftingSystem().AllOptions.First(r =>
                r.Name == RecipeName)),
            nameof(NPCFight) => new NPCFight(Target!),
            nameof(NPCFlee) => new NPCFlee(Target!),
            nameof(NPCCookMeat) => new NPCCookMeat(),
            nameof(NPCMeltSnow) => new NPCMeltSnow(),
            nameof(NPCDrinkWater) => new NPCDrinkWater(),
            nameof(NPCImproveShelter) => new NPCImproveShelter(Improvement, Resource, Quantity),
            _ => throw new InvalidOperationException($"Unknown saved NPC action: {Kind}")
        };
        action.DurationMinutes = Duration;
        action.MinutesSpent = Progress;
        action.Settled = Settled;
        action.IsFollowingPursuit = IsFollowingPursuit;
        return action;
    }
}
