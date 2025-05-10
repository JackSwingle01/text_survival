using text_survival.Actors;
using text_survival.Environments;
using text_survival.IO;

namespace text_survival
{
    public static class Combat
    {
        public static void CombatLoop(Player player, ICombatant enemy)
        {
            Output.WriteLine("You encounter: ", enemy, "!");
            player.IsEngaged = true;
            enemy.IsEngaged = true;
            if (enemy.Attributes.Speed > player.Attributes.Speed)
            {
                enemy.Attack(player);
            }
            while (player.IsAlive && enemy.IsAlive)
            {
                if (!player.IsEngaged || !player.IsAlive) break;
                PlayerTurn(player, enemy);

                if (!enemy.IsEngaged || !enemy.IsAlive) break;
                enemy.Attack(player);

                World.Update(1);
            }
            player.IsEngaged = false;
            enemy.IsEngaged = false;

            if (!player.IsAlive)
                Output.WriteDanger("You died!");
            else if (!enemy.IsAlive)
            {
                Output.WriteLine("You killed ", enemy, "!");
            }
        }



        public static void PlayerTurn(Player player, ICombatant enemy)
        {
            Output.WriteLine("What do you want to do?");

            List<string> options = ["Attack", "Cast Spell", "Flee"];

            string? choice = Input.GetSelectionFromList(options);

            switch (choice)
            {
                case "Attack":
                    player.Attack(enemy);
                    break;
                case "Cast Spell":
                    player.SelectSpell();
                    break;
                case "Flee":
                    if (SpeedCheck(player, enemy))
                    {
                        Output.WriteLine("You got away!");
                        enemy.IsEngaged = false;
                        player.IsEngaged = false;
                    }
                    else
                    {
                        Output.WriteLine("You weren't fast enough to get away from ", enemy, "!");
                        player._skillRegistry.AddExperience("Athletics", 1); // XP for flee attempt
                    }
                    break;
                default:
                    throw new InvalidOperationException("Invalid Selection");
            }
        }

        public static bool SpeedCheck(Player player, ICombatant? enemy = null)
        {
            if (player.CurrentLocation.IsSafe) return true;

            enemy ??= GetFastestNpc(player.CurrentLocation);

            double playerCheck = CalcSpeedCheck(player);
            double enemyCheck = CalcSpeedCheck(enemy);

            return playerCheck >= enemyCheck;
        }

        public static Npc GetFastestNpc(Location location)
        {
            double enemyCheck = 0;
            Npc fastestNpc = location.Npcs.First();
            foreach (Npc npc in location.Npcs)
            {
                if (npc == fastestNpc) continue;
                if (!npc.IsAlive) continue;
                var currentNpcCheck = CalcSpeedCheck(npc);
                if (currentNpcCheck < enemyCheck) continue;
                fastestNpc = npc;
                enemyCheck = currentNpcCheck;
            }
            return fastestNpc;
        }

        public static double CalcSpeedCheck(ICombatant actor)
        {
            double athleticsBonus = actor._skillRegistry.GetLevel("Athletics");
            return actor.Attributes.Speed + actor.Attributes.Luck / 2 + athleticsBonus;
        }
    }
}