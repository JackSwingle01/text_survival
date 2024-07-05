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
                PrintBattleInfo(player, enemy);
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

        private enum CombatActions
        {
            Attack,
            CastSpell,
            Flee
        }
        public static void PlayerTurn(Player player, ICombatant enemy)
        {
            Output.WriteLine("What do you want to do?");

            List<CombatActions> options = [CombatActions.Attack];
            if (player.Spells.Count > 0)
                options.Add(CombatActions.CastSpell);
            options.Add(CombatActions.Flee);

            int choice = Input.GetSelectionFromList(options);
            switch (options[choice - 1])
            {
                case CombatActions.Attack:
                    player.Attack(enemy);
                    break;
                case CombatActions.CastSpell:
                    player.SelectSpell();
                    break;
                case CombatActions.Flee when Combat.SpeedCheck(player, enemy):
                    Output.WriteLine("You got away!");
                    enemy.IsEngaged = false;
                    player.IsEngaged = false;
                    return; // end combat
                case CombatActions.Flee:
                    Output.WriteLine("You weren't fast enough to get away from ", enemy, "!");
                    break;
                default:
                    break;
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

        public static bool SpeedCheck(Player player, ICombatant? enemy = null)
        {
            if (player.CurrentPlace.IsSafe) return true;

            // if no enemy is passed in, get the fastest enemy
            enemy ??= GetFastestNpc(player.CurrentPlace);

            // compare player to fastest enemy
            double playerCheck = CalcSpeedCheck(player);
            double enemyCheck = CalcSpeedCheck(enemy);

            return !(playerCheck < enemyCheck);
        }


        public static Npc GetFastestNpc(IPlace place)
        {
            double enemyCheck = 0;
            Npc fastestNpc = place.Npcs.First();
            foreach (Npc npc in place.Npcs)
            {
                var currentNpcCheck = CalcSpeedCheck(npc);
                if (!(currentNpcCheck >= enemyCheck)) continue;
                fastestNpc = npc;
                enemyCheck = currentNpcCheck;
            }
            return fastestNpc;
        }

        public static double CalcSpeedCheck(ICombatant actor)
        {
            return actor.Attributes.Speed + actor.Attributes.Agility / 2 + actor.Attributes.Luck / 3;
        }
    }
}
