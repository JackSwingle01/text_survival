using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace text_survival
{
    public static class Combat
    {
        public static void CombatLoop(Player player, NPC enemy)
        {
            if (enemy.Speed > player.Speed)
            {
                Attack(enemy, player);
            }
            while (player.Health > 0 && enemy.Health > 0)
            {
                Utils.Write(CombatStatsToString(player));
                Utils.Write("VS");
                Utils.Write(CombatStatsToString(enemy));
                Utils.Write("What do you want to do?");
                Utils.Write("1. Attack");
                Utils.Write("2. Run");
                int choice = Utils.ReadInt(1,2);
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
                    Utils.Write("You ran away!");
                    break;
                }
                player.Update(1);
            }
            if (player.Health <= 0)
            {
                Utils.Write("You died!");
            }
            else if (enemy.Health <= 0)
            {
                Utils.Write("You killed " + enemy.Name + "!");
            }
            
        }
        public static void Attack(IActor attacker, IActor defender)
        {
            float damage = CalcDamage(attacker, defender);
            if (DetermineDodge(attacker, defender))
            {
                Utils.Write(defender.Name + " dodged the attack!");
                return;
            }
            defender.Damage(damage);
            Utils.Write(attacker.Name + " attacked " + defender.Name + " for " + damage.ToString("0.0") + " damage!");
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
            float damage = attacker.Strength - ((defender.Defense /100) * attacker.Strength);
            // dodge chance based on speed 
            if (damage < 0)
            {
                damage = 0;
            }
            return damage;
        }
        public static string CombatStatsToString(IActor c)
        {
            string stats = "";
            stats += c.Name + ":\n";
            stats += "Health: " + c.Health + "/" + c.MaxHealth + "\n";
            stats += "Strength: " + c.Strength + "\n";
            stats += "Defense: " + c.Defense + "\n";
            stats += "Speed: " + c.Speed + "\n";
            return stats;
        }
    }
}
