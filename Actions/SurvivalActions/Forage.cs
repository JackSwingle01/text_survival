using text_survival.Environments;
using text_survival.IO;

namespace text_survival.Actions;

public class Forage(string name = "Forage") : GameActionBase(name)
{
    public override bool IsAvailable(GameContext ctx)
    {
        var forageFeature = ctx.player.CurrentLocation.GetFeature<ForageFeature>();
        return forageFeature != null;
    }

    protected override void OnExecute(GameContext ctx)
    {
        var forageFeature = ctx.currentLocation.GetFeature<ForageFeature>();
        if (forageFeature == null)
        {
            Output.WriteLine("You can't forage here");
            return;
        }

        Output.WriteLine("You forage for 1 hour");
        forageFeature.Forage(1);
    }

    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [
            new Forage("Keep Foraging"),
            new LookAround(ctx.currentLocation),
            new ReturnAction("Finish foraging...")
        ];
    }
}
