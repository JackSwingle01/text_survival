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
                player.Update(1);
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
            item.Write();
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
        public static void PrintBattleInfo(ICombatant combatant1, ICombatant combatant2)
        {
            WriteCombatStats(combatant1);
            Utils.WriteLine("VS");
            WriteCombatStats(combatant2);
        }

        public static bool DetermineDodge(ICombatant attacker, ICombatant defender)
        {
            const int baseDodge = 10;
            double speedDiff = defender.Attributes.Speed - attacker.Attributes.Speed;
            double chance = baseDodge + speedDiff;

            int roll = Utils.RandInt(0, 100);
            return roll <= chance;
        }
        public static double CalcDamage(ICombatant attacker, ICombatant defender)
        {
            // base damage - defense percentage
            double damage = attacker.Attributes.Strength - ((defender.ArmorRating / 100) * attacker.Attributes.Strength);
            damage *= Utils.RandFloat(.5F, 1.5F);
            if (damage < 0)
            {
                damage = 0;
            }
            return damage;
        }
        public static void WriteCombatStats(ICombatant c)
        {
            Utils.Write(c, " => ",
                "HP: ", Math.Round(c.Health, 2), "/", c.MaxHealth,
                "\nStr: ", c.Attributes.Strength,
                ", Def: ", c.ArmorRating,
                ", Spd: ", c.Attributes.Speed, "\n");
        }
    }
}
