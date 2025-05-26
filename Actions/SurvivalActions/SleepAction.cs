using text_survival.IO;

namespace text_survival.Actions;
public class Sleep() : GameActionBase("Sleep")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        Output.WriteLine("How many hours would you like to sleep?");
        ctx.player.Sleep(Input.ReadInt() * 60);
    }
    public override bool IsAvailable(GameContext ctx)
    {
        // return ctx.player.Body. // todo only let you sleep when tired
        return base.IsAvailable(ctx);
    }
}