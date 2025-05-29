using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Actions;

public class StartCombatAction(Npc enemy) : GameActionBase($"Fight {enemy.Name}")
{
    public override bool IsAvailable(GameContext ctx) => enemy.IsAlive && enemy.IsFound;

    protected override void OnExecute(GameContext ctx)
    {
        Output.WriteLine("!");
        Thread.Sleep(500);
        Output.WriteLine(CombatNarrator.DescribeCombatStart(ctx.player, enemy));

        ctx.player.IsEngaged = true;
        enemy.IsEngaged = true;

        // First strike determination
        bool enemyFirstStrike = enemy.Body.CalculateSpeed() > ctx.player.Body.CalculateSpeed();

        if (enemyFirstStrike)
        {
            Output.WriteLine($"The {enemy.Name} moves with surprising speed!");
            Thread.Sleep(500);
            NextActionOverride = new EnemyCombatTurn(enemy);
            return;
        }
        else
        {
            Output.WriteLine("You're quick to react, giving you the initiative!");
            Thread.Sleep(500);
        }
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new PlayerCombatTurnAction(enemy)];
    private readonly Npc enemy = enemy;
}
