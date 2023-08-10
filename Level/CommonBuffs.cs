﻿namespace text_survival.Level
{
    public static class CommonBuffs
    {
        public static Buff Warmth(int degrees, int minutes = -1)
        {
            Buff buff = new Buff("Warmth", minutes)
            {
                ApplyEffect = target =>
                {
                    if (target is not Player player) return;
                    player.WarmthBonus += degrees;
                    Utils.WriteLine("You feel warmer.");
                },
                RemoveEffect = target =>
                {
                    if (target is not Player player) return;
                    player.WarmthBonus -= degrees;
                    Utils.WriteWarning("You're no longer being warmed up.");
                }
            };
            return buff;
        }

        public static Buff Bleeding(int hpPerMin, int minutes)
        {
            return new Buff("Bleeding", minutes)
            {
                ApplyEffect = (target => Utils.WriteLine(target, " has been cut!")),
                TickEffect = (target) =>
                {
                    target.Health -= hpPerMin;
                    if (target is Player player)
                        Utils.WriteDanger("You are bleeding!");
                    else
                        Utils.WriteLine(target, " is bleeding");
                },
                RemoveEffect = (player) => { Console.WriteLine("You have stopped bleeding."); }
            };
        }

    }
}
