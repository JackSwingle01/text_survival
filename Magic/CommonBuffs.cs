using text_survival_rpg_web.Actors;
using text_survival_rpg_web.Items;

namespace text_survival_rpg_web.Magic
{
    public static class CommonBuffs
    {
        public static Buff Warmth(double degrees, int minutes = -1)
        {
            Buff buff = new Buff("Warmth", minutes, BuffType.Warmth)
            {
                ApplyEffect = target =>
                {
                    if (target is not Player player) return;
                    player.AddWarmthBonus(degrees);
                    Output.WriteLine("You feel warmer.");
                },
                RemoveEffect = target =>
                {
                    if (target is not Player player) return;
                    player.RemoveWarmthBonus(degrees);
                    Output.WriteWarning("You're no longer being warmed up.");
                }
            };
            return buff;
        }

        public static Buff Bleeding(int hpPerMin, int minutes)
        {
            return new Buff("Bleeding", minutes, BuffType.Bleed)
            {
                ApplyEffect = (target => Output.WriteLine(target, " has been cut!")),
                TickEffect = ((target) =>
                {
                    target.Damage(hpPerMin);
                    if (target is Player player)
                        Output.WriteDanger("You are bleeding!");
                    else
                        Output.WriteLine(target, " is bleeding");
                }),
                RemoveEffect = (target => Output.WriteLine(target, " has stopped bleeding."))

            };
        }

        public static Buff Poison(int hpPerMin, int minutes)
        {
            return new Buff("Poison", minutes, BuffType.Poison)
            {
                ApplyEffect = (target => Output.WriteLine(target, " has been poisoned!")),
                TickEffect = ((target) =>
                {
                    target.Damage(hpPerMin);
                    if (target is Player player)
                        Output.WriteDanger("You are poisoned!");
                    else
                        Output.WriteLine(target, " is poisoned");
                }),
                RemoveEffect = (target => Output.WriteLine(target, " has stopped being poisoned."))

            };
        }

        public static Buff Heal(int hp)
        {
            return new Buff("Heal", 0, BuffType.Heal)
            {
                ApplyEffect = (target =>
                {
                    target.Heal(hp);
                    Output.WriteLine(target, " has been healed!");
                })
            };
        }

        public static Buff ApplyPoisonOnHit(int hpPerMin, int minutes)
        {
            Buff buff = new Buff("Poisoned Weapon", -1, BuffType.Generic)
            {
                TriggerOn = EventType.OnHit,
                
            };
            buff.TriggerEffect = (e =>
            {
                if (e is CombatEvent combatEvent)
                {
                    if (combatEvent.Attacker != buff.Target) return;
                    Buff poison = Poison(hpPerMin, minutes);
                    poison.ApplyTo(combatEvent.Defender);
                }
                buff.Remove();
            });
            return buff;
        }

    }
}
