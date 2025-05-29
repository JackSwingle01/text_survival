using text_survival.Actors;

namespace text_survival.Actions;

public class AttackEnemy(Npc enemy) : GameActionBase($"Attack {enemy.Name}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new EnemyCombatTurn(enemy), new EndCombatAction(enemy)];

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.Attack(enemy);
    }
}
