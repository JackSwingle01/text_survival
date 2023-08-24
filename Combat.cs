using text_survival_rpg_web.Actors;
using text_survival_rpg_web.Environments;
using text_survival_rpg_web.Items;

namespace text_survival_rpg_web
{
    public static class Combat
    {
        public static void CombatLoop(Player player, ICombatant enemy)
        {
            Output.WriteLine("You encounter: ", enemy, "!");

            if (enemy.Attributes.Speed > player.Attributes.Speed)
            {
                enemy.Attack(player);
            }
            while (player.Health > 0 && enemy.Health > 0)
            {
                PrintBattleInfo(player, enemy);
                PlayerTurn(player, enemy);
                if (enemy.Health > 0)
                {
                    enemy.Attack(player);
                }
                World.Update(1);
            }

            if (player.Health <= 0)
                Output.WriteDanger("You died!");

            else if (enemy.Health <= 0)
            {
                Output.WriteLine("You killed ", enemy, "!");
                if (enemy is Npc npc)
                    GetLoot(player, npc);
            }
        }

        public static void PlayerTurn(Player player, ICombatant enemy)
        {
            Output.WriteLine("What do you want to do?");
            List<string> options = new()
            {
                "Attack",
                "Cast Spell",
                "Run away"
            };
            int choice = Input.GetSelectionFromList(options);
            switch (choice)
            {
                case 1:
                    player.Attack(enemy);
                    break;
                case 2:
                    player.SelectSpell();
                    break;
                case 3 when Combat.SpeedCheck(player, enemy):
                    Output.WriteLine("You got away!");
                    return;
                case 3:
                    Output.WriteLine("You weren't fast enough to get away from ", enemy, "!");
                    break;
            }
        }
        public static void GetLoot(Player player, Npc npc)
        {
            Item? loot = npc.DropItem();
            if (loot is null)
            {
                Output.WriteLine(npc.Name, " has no loot.");
                return;
            }
            Output.Write(npc.Name, " dropped: ");
            Describe.DescribeItem(loot);
            Output.WriteLine("\nDo you want to pick it up?\n", 1, ". Yes\n", 2, ". No");

            int choice = Input.ReadInt(1, 2);
            if (choice == 1)
            {
                player.TakeItem(loot);
            }
            else
            {
                player.CurrentArea.Things.Add(loot);
                Output.Write("You left the ", loot, " on the ground.\n");
            }
        }
        public static void PrintBattleInfo(ICombatant combatant1, ICombatant combatant2)
        {
            Describe.DescribeCombatant(combatant1);
            Output.WriteLine("VS");
            Describe.DescribeCombatant(combatant2);
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

        /// <summary>
        /// Determines if the defender dodges the attack.
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        /// <returns>True if the defender dodges, false if not.</returns>
        public static bool DetermineDodge(ICombatant attacker, ICombatant defender)
        {
            double dodgeChance = defender.DetermineDodgeChance(attacker);
            double dodgeRoll = Utils.RandDouble(0, 100);
            if (dodgeRoll <= dodgeChance)
            {
                Output.WriteLine(defender.Name + " dodged the attack!");
                return true;
            }
            return false;
        }

        public static bool DetermineHit(ICombatant attacker, ICombatant defender)
        {
            double hitChance = attacker.DetermineHitChance(defender); // 0-1
            double roll = Utils.RandDouble(0, 1);
            if (roll > hitChance)
            {
                Output.WriteLine(attacker, " missed ", defender, "!");
                return false;
            }
            return true;
        }

        public static bool DetermineBlock(ICombatant attacker, ICombatant defender)
        {
            double blockChance = defender.DetermineBlockChance(attacker);
            double roll = Utils.RandDouble(0, 1);

            if (!(roll < blockChance)) return false;

            Output.WriteLine(defender, " blocked ", attacker, "'s attack!");
            return true;
        }

        public static bool SpeedCheck(Player player, IActor? enemy = null)
        {
            if (player.CurrentArea.IsSafe) return true;

            // if no enemy is passed in, get the fastest enemy
            enemy ??= GetFastestNpc(player.CurrentArea);

            // compare player to fastest enemy
            double playerCheck = CalcSpeedCheck(player);
            double enemyCheck = CalcSpeedCheck(enemy);

            return !(playerCheck < enemyCheck);
        }


        public static Npc GetFastestNpc(Area area)
        {
            double enemyCheck = 0;
            Npc fastestNpc = area.GetNpcs.First();
            foreach (Npc npc in area.GetNpcs)
            {
                var currentNpcCheck = CalcSpeedCheck(npc);
                if (!(currentNpcCheck >= enemyCheck)) continue;
                fastestNpc = npc;
                enemyCheck = currentNpcCheck;
            }
            return fastestNpc;
        }

        public static double CalcSpeedCheck(IActor actor)
        {
            return actor.Attributes.Speed + actor.Attributes.Agility / 2 + actor.Attributes.Luck / 3;
        }
    }
}
