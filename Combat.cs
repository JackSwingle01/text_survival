using text_survival.Actors;
using text_survival.Items;

namespace text_survival
{
    public static class Combat
    {
        public static void CombatLoop(Player player, Npc enemy)
        {
            Utils.Write("You encounter a ", enemy, "!\n");

            if (enemy.Speed > player.Speed)
            {
                Attack(enemy, player);
            }
            while (player.Health > 0 && enemy.Health > 0)
            {
                PrintBattleInfo(player, enemy);
                Utils.Write("What do you want to do?\n");
                Utils.Write("1. Attack\n");
                Utils.Write("2. Run\n");
                int choice = Utils.ReadInt(1, 2);
                if (choice == 1)
                {
                    Attack(player, enemy);
                    if (enemy.Health > 0)
                    {
                        Attack(enemy, player);
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
                if (enemy.Loot.Count() > 0)
                {
                    GetLoot(player, enemy);
                }
            }
        }
        public static void GetLoot(Player player, Npc npc)
        {
            if (npc == null)
            {
                return;
            }
            if (npc.Loot.Count() == 0)
            {
                Utils.Write(npc.Name + " has no loot.\n");
                return;
            }
            Utils.Write(npc.Name + " dropped: ");
            Item item = npc.Loot[Utils.Rand(0, npc.Loot.Count() - 1)];
            item.Write();
            Utils.Write("\nDo you want to pick it up?\n");
            Utils.Write("1. Yes\n");
            Utils.Write("2. No\n");
            int choice = Utils.ReadInt(1, 2);
            if (choice == 1)
            {
                player.Inventory.Add(item);
            }
            else
            {
                Utils.Write("You left the " + item + " on the ground.\n");
            }
        }
        public static void PrintBattleInfo(IActor combatant1, IActor combatant2)
        {
            WriteCombatStats(combatant1);
            Utils.Write("VS\n");
            WriteCombatStats(combatant2);
        }
        public static void Attack(IActor attacker, IActor defender)
        {
            float damage = CalcDamage(attacker, defender);
            if (DetermineDodge(attacker, defender))
            {
                Utils.Write(defender, " dodged the attack!\n");
                return;
            }
            defender.Damage(damage);
            Utils.Write(attacker, " attacked ", defender, " for ", Math.Round(damage, 1), " damage!\n");
        }
        public static bool DetermineDodge(IActor attacker, IActor defender)
        {
            int baseDodge = 10;
            int speedDiff = defender.Speed - attacker.Speed;
            int chance = baseDodge + speedDiff;
            Random rand = new Random();
            int roll = rand.Next(0, 100);
            if (roll <= chance)
            {
                return true;
            }
            return false;
        }
        public static float CalcDamage(IActor attacker, IActor defender)
        {
            // base damage - defense percentage
            float damage = attacker.Strength - ((defender.Defense / 100) * attacker.Strength);
            if (damage < 0)
            {
                damage = 0;
            }
            return damage;
        }
        public static void WriteCombatStats(IActor c)
        {
            Utils.Write(c.Name, " => ",
                "HP: ", Math.Round(c.Health, 2), "/", c.MaxHealth,
                ", Str: ", c.Strength,
                ", Def: ", c.Defense,
                ", Spd: ", c.Speed, "\n");
        }
    }
}
