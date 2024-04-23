using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Magic
{
    public static class CommonBuffs
    {
        public static Buff Warmth(double degrees)
        {
            Buff buff = new Buff("Warmth", BuffType.Warmth)
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

        public static TimedBuff Bleeding(int hpPerMin, int minutes)
        {
            return new TimedBuff("Bleeding", minutes, BuffType.Bleed)
            {
                ApplyEffect = (target => Output.WriteLine(target, " has been cut!")),
                TickEffect = ((target) =>
                {
                    if (target is not IActor actor) return;

                    actor.Damage(hpPerMin);
                    if (actor is Player player)
                        Output.WriteDanger("You are bleeding!");
                    else
                        Output.WriteLine(actor, " is bleeding");
                }),
                RemoveEffect = (target => Output.WriteLine(target, " has stopped bleeding."))

            };
        }

        public static TimedBuff Poison(int hpPerMin, int minutes)
        {
            TimedBuff poison = new TimedBuff("Poison", minutes, BuffType.Poison);

            poison.ApplyEffect = target =>
            {
                if (target is not IActor actor)
                {
                    poison.RemoveEffect = x => Output.WriteLine("That had no effect");
                    poison.Remove();
                    return;
                } 
                Output.WriteLine(actor, " has been poisoned!");
            };
            poison.TickEffect = (target) =>
            {
                if (target is not IActor actor) return;
                actor.Damage(hpPerMin);
                if (actor is Player player)
                    Output.WriteDanger("You are poisoned!");
                else
                    Output.WriteLine(actor, " is poisoned");
            };
            poison.RemoveEffect = target => Output.WriteLine(target, " has stopped being poisoned.");


            return poison;
        }

        public static InstantEffectBuff Heal(int hp)
        {
            return new InstantEffectBuff("Heal", BuffType.Heal)
            {
                ApplyEffect = (target =>
                {
                    if (target is not IActor actor)
                    {
                        Output.WriteLine("That had no effect");
                        return;
                    }
                    actor.Heal(hp);
                    Output.WriteLine(target, " has been healed!");
                })
            };
        }

        public static TriggeredBuff Venomous(int hpPerMin, int minutes, double chance)
        {
            TriggeredBuff buff = new TriggeredBuff("Venomous", -1, BuffType.Generic);
            buff.TriggerOn = EventType.OnHit;
            buff.TriggerEffect = (e =>
            {
                if (e is CombatEvent combatEvent)
                {
                    if (combatEvent.Weapon != buff.Target) return;
                    if (Utils.RandDouble(0, 1) > chance) return;
                    Buff poison = Poison(hpPerMin, minutes);
                    poison.ApplyTo(combatEvent.Defender);
                }
            });
            return buff;
        }

        public static TriggeredBuff PoisionedWeapon(int hpPerMin, int minutes, double chance = 1)
        {
            TriggeredBuff buff = new TriggeredBuff("Poisoned Weapon", 1, BuffType.Generic)
            {
                TriggerOn = EventType.OnHit,
            };
            buff.TriggerEffect = (e =>
            {
                if (e is CombatEvent combatEvent)
                {
                    if (combatEvent.Attacker != buff.Target) return;
                    if (Utils.RandDouble(0, 1) > chance) return;
                    Buff poison = CommonBuffs.Poison(hpPerMin, minutes);
                    poison.ApplyTo(combatEvent.Defender);
                }
            });
            return buff;
        }

    }
}
