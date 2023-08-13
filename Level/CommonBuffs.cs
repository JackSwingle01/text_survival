using text_survival.Magic;

namespace text_survival.Level
{
    public static class CommonBuffs
    {
        public static Buff Warmth(double degrees, int minutes = -1)
        {
            Buff buff = new Buff("Warmth", minutes)
            {
                ApplyEffect = target =>
                {
                    if (target is not Player player) return;
                    player.AddWarmthBonus(degrees);
                    Utils.WriteLine("You feel warmer.");
                },
                RemoveEffect = target =>
                {
                    if (target is not Player player) return;
                    player.RemoveWarmthBonus(degrees);
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
                TickEffect = ((target) =>
                {
                    target.Damage(hpPerMin);
                    if (target is Player player)
                        Utils.WriteDanger("You are bleeding!");
                    else
                        Utils.WriteLine(target, " is bleeding");
                }),
                RemoveEffect = (target => Utils.WriteLine(target, " has stopped bleeding."))

            };
        }

        public static Buff Poison(int hpPerMin, int minutes)
        {
            return new Buff("Poison", minutes)
            {
                ApplyEffect = (target => Utils.WriteLine(target, " has been poisoned!")),
                TickEffect = ((target) =>
                {
                    target.Damage(hpPerMin);
                    if (target is Player player)
                        Utils.WriteDanger("You are poisoned!");
                    else
                        Utils.WriteLine(target, " is poisoned");
                }),
                RemoveEffect = (target => Utils.WriteLine(target, " has stopped being poisoned."))

            };
        }

        public static Buff Heal(int hp)
        {
            return new Buff("Heal", 0)
            {
                ApplyEffect = (target =>
                {
                    target.Heal(hp);
                    Utils.WriteLine(target, " has been healed!");
                })
            };
        }

    }
}
