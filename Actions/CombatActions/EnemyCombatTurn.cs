using text_survival.Actors;

namespace text_survival.Actions;

public class EnemyCombatTurn(Npc enemy) : GameActionBase("Enemy Turn")
{
    public override bool IsAvailable(GameContext ctx)
    {
        return ctx.player.IsAlive && enemy.IsAlive && enemy.IsEngaged;
    }
    protected override void OnExecute(GameContext ctx)
    {
        Thread.Sleep(500); // Pause before enemy attack
        enemy.Attack(ctx.player);
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new PlayerCombatTurnAction(enemy), new EndCombatAction(enemy)];
    private readonly Npc enemy = enemy;
}
