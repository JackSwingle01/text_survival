using text_survival.Actors;
using text_survival.Actors.Player;

namespace text_survival.Combat;

public static class CombatUtils
{
    public static bool SpeedCheck(Player player, Actor enemy)
    {
        double playerCheck = CalcSpeedCheck(player);
        double enemyCheck = CalcSpeedCheck(enemy);

        return playerCheck >= enemyCheck;
    }

    public static double CalcSpeedCheck(Actor actor)
    {
        double athleticsBonus = 0;
        if (actor is Player player)
        {
            athleticsBonus = player.Skills.Reflexes.Level;
        }
        return actor.Speed + athleticsBonus;
    }
}
