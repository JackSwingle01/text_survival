using text_survival.Actors;

namespace text_survival.Actions;

public class FightNpc(Npc npc) : GameActionBase($"Fight {npc.Name}")
{
    public override bool IsAvailable(GameContext ctx) => npc.IsAlive && npc.IsFound;

    protected override void OnExecute(GameContext ctx)
    {
        Combat.CombatLoop(ctx.player, npc);
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }
    private readonly Npc npc = npc;
}
