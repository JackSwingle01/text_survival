using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Environments;

namespace text_survival;

public static class CombatUtils
{
    public static bool SpeedCheck(Player player, Actor enemy)
    {
        double playerCheck = CalcSpeedCheck(player);
        double enemyCheck = CalcSpeedCheck(enemy);

        return playerCheck >= enemyCheck;
    }

    public static Npc? GetFastestHostileNpc(Location location)
    {
        double fastestCheck = 0;
        Npc? fastestNpc = null;
        foreach (Npc npc in location.Npcs)
        {
            if (npc == fastestNpc) continue;
            if (!npc.IsAlive) continue;
            if (!npc.IsHostile) continue;

            fastestNpc ??= npc;

            var currentNpcCheck = CalcSpeedCheck(npc);
            if (currentNpcCheck > fastestCheck)
            {
                fastestNpc = npc;
                fastestCheck = currentNpcCheck;
            }
        }
        return fastestNpc;
    }

    public static double CalcSpeedCheck(Actor actor)
    {
        double athleticsBonus = 0;
        if (actor is Player player)
        {
            athleticsBonus = player.Skills.Reflexes.Level;
        }
        return AbilityCalculator.CalculateSpeed(actor.Body) + athleticsBonus;
    }
}
