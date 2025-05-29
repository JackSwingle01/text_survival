using text_survival.Actors;

namespace text_survival.Actions;

public class CastSpell(Npc enemy) : GameActionBase("Cast Spell")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new EnemyCombatTurn(enemy), new EndCombatAction(enemy)];

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.SelectSpell();
    }
    private readonly Npc enemy = enemy;
}
