using text_survival.IO;

namespace text_survival.Actions;

public class CheckStats() : GameActionBase("Check Stats")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [];

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.Body.Describe();
        ctx.player._skillRegistry.Describe();
        Output.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }
}
