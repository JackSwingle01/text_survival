using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Actions;

public class FleeCombat(Npc enemy) : GameActionBase("Flee")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new EnemyCombatTurn(enemy), new EndCombatAction(enemy)];

    protected override void OnExecute(GameContext ctx)
    {
        if (CombatUtils.SpeedCheck(ctx.player, enemy))
        {
            Output.WriteLine("You got away!");
            enemy.IsEngaged = false;
            ctx.player.IsEngaged = false;
            ctx.player.Skills.Reflexes.GainExperience(2);
        }
        else
        {
            Output.WriteLine("You weren't fast enough to get away from ", enemy, "!");
            ctx.player.Skills.Reflexes.GainExperience(1);
        }
    }
}
