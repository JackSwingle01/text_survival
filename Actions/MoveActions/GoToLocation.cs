using text_survival.Environments;

namespace text_survival.Actions;

public class GoToLocation(Location location) : GameActionBase($"Go to {location.Name}{(location.Visited ? " (Visited)" : "")}")
{
    public override bool IsAvailable(GameContext ctx) => location.IsFound;

    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }
    protected override void OnExecute(GameContext ctx)
    {
        location.Interact(ctx.player);
    }
    private readonly Location location = location;
}
