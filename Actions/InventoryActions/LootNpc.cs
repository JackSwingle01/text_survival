using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Actions;

public class LootNpc(Npc npc) : GameActionBase($"Loot {npc.Name}")
{
    public override bool IsAvailable(GameContext ctx) => npc.IsFound && !npc.IsAlive && !npc.Loot.IsEmpty;
    protected override void OnExecute(GameContext ctx)
    {
        if (npc.Loot.IsEmpty)
        {
            Output.WriteLine("There is nothing to loot.");
            return;
        }
        // just validate and pass to the OpenContainer action
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [new OpenContainer(npc.Loot)];
    }
    private readonly Npc npc = npc;
}
