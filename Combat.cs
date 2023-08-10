using text_survival.Actors;
using text_survival.Items;

namespace text_survival
{
    public static class Combat
    {
        public static void CombatLoop(Player player, Npc enemy)
        {
            Utils.WriteLine("You encounter: ", enemy, "!");

            if (enemy.Attributes.Speed > player.Attributes.Speed)
            {
                enemy.Attack(player);
            }
            while (player.Health > 0 && enemy.Health > 0)
            {
                PrintBattleInfo(player, enemy);
                Utils.WriteLine("What do you want to do?");
                Utils.WriteLine(1, ". Attack");
                Utils.WriteLine(2, ". Run");
                int choice = Utils.ReadInt(1, 2);
                if (choice == 1)
                {
                    player.Attack(enemy);
                    if (enemy.Health > 0)
                    {
                        enemy.Attack(player);
                    }
                }
                else if (choice == 2)
                {
                    Utils.Write("You ran away!\n");
                    break;
                }
                World.Update(1);
            }
            if (player.Health <= 0)
            {
                Utils.Write("You died!\n");
            }
            else if (enemy.Health <= 0)
            {
                Utils.Write("You killed ", enemy, "!\n");
                if (enemy.Loot.Count > 0)
                {
                    GetLoot(player, enemy);
                }

            }
        }
        public static void GetLoot(Player player, Npc npc)
        {
            if (npc.Loot.Count == 0)
            {
                Utils.WriteLine(npc.Name + " has no loot.");
                return;
            }
            Utils.Write(npc.Name + " dropped: ");
            Item item = npc.Loot[Utils.RandInt(0, npc.Loot.Count - 1)] as Item;
            Examine.ExamineItem(item);
            Utils.WriteLine("\nDo you want to pick it up?\n", 1, ". Yes\n", 2, ". No");

            int choice = Utils.ReadInt(1, 2);
            if (choice == 1)
            {
                player.Inventory.Add(item);
            }
            else
            {
                player.CurrentArea.Items.Add(item);
                Utils.Write("You left the " + item + " on the ground.\n");
            }
        }
        public static void PrintBattleInfo(IActor combatant1, IActor combatant2)
        {
            Examine.ExamineCombatant(combatant1);
            Utils.WriteLine("VS");
            Examine.ExamineCombatant(combatant2);
        }

    }
}
