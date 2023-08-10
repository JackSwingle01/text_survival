﻿using text_survival.Actors;
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
                player.AddToInventory(item);
            }
            else
            {
                player.CurrentArea.Items.Add(item);
                Utils.Write("You left the " + item + " on the ground.\n");
            }
        }
        public static void PrintBattleInfo(ICombatant combatant1, ICombatant combatant2)
        {
            Examine.ExamineCombatant(combatant1);
            Utils.WriteLine("VS");
            Examine.ExamineCombatant(combatant2);
        }

        /// <summary>
        /// Calculates the damage of an attack.
        /// </summary>
        /// <param name="baseDamage">The attacker's base damage (weapon or unarmed)</param>
        /// <param name="strength">The attacker's strength attribute</param>
        /// <param name="skillBonus">The damage bonus from attacker's skills</param>
        /// <param name="defenderArmorRating">1/percentage of damage blocked</param>
        /// <param name="otherModifiers">Multiplier so 1 does nothing.</param>
        /// <returns>Damage dealt in hp</returns>
        public static double CalculateAttackDamage(
            double baseDamage,
            double strength,
            double defenderArmorRating = 0,
            double skillBonus = 0,
            double otherModifiers = 1)
        {
            double strengthModifier = (strength + 50) / 100;
            double damage = baseDamage + skillBonus;
            double defenderDefense = defenderArmorRating;
            damage *= strengthModifier * (1 - defenderDefense) * otherModifiers;

            damage *= Utils.RandDouble(.5, 2); // randomize damage

            if (damage < 0)
                damage = 0;

            return damage;
        }

        /// <summary>
        /// Calculates dodge chance, 1 is 100% chance to dodge, 0 is 0% chance to dodge.
        /// </summary>
        /// <param name="agility"></param>
        /// <param name="speed"></param>
        /// <param name="attackerSpeed"></param>
        /// <param name="luck"></param>
        /// <param name="dodgeLevel"></param>
        /// <returns>dodge chance from 0-1</returns>
        public static double CalculateDodgeChance(double agility, double speed, double attackerSpeed, double luck,
            double dodgeLevel = 0)
        {
            double baseDodge = (dodgeLevel + agility / 2 + luck / 10) / 200;
            double speedDiff = speed - attackerSpeed;
            double chance = baseDodge + speedDiff;
            return chance;
        }
    }
}
