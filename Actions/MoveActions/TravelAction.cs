namespace text_survival.Actions;
public class Travel() : GameActionBase("Travel to a different area")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.Travel();
    }
    public override bool IsAvailable(GameContext ctx)
    {
        if (ctx.player.CurrentLocation.GetFeature<ShelterFeature>() != null)
        {
            return false;
        }
        return base.IsAvailable(ctx);
    }
}