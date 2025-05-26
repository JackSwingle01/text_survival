namespace text_survival.Actions;

public class DefaultAction() : GameActionBase("Default")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        var actions = new List<IGameAction>();
        actions.AddRange([
                new LookAround(ctx.currentLocation),
                new Forage(),
                new OpenInventory(),
                new CheckStats(),
                new Sleep(),
                new Move(),
            ]);
        return actions;
    }
    protected override void OnExecute(GameContext ctx)
    {
        // this is mainly just a router to select the next action 
        ctx.player.DescribeSurvivalStats();
    }
}
