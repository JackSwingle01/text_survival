using text_survival.Actors;
using text_survival.Interfaces;
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
            TimedBuff bleeding = new TimedBuff("Bleeding", minutes, BuffType.Bleed);
            
            bleeding.ApplyEffect = target =>
            {
                if (target is not IDamageable d)
                {
                    bleeding.RemoveEffect = x => Output.WriteLine("That had no effect");
                    bleeding.Remove();
                    return;
                }
                Output.WriteLine(target, " has been cut!");
            };
            bleeding.TickEffect = (target) =>
            {
                if (target is not IDamageable d) return;

                d.Damage(hpPerMin);
                if (d is Player player)
                    Output.WriteDanger("You are bleeding!");
                else
                    Output.WriteLine(d, " is bleeding");
            };
            bleeding.RemoveEffect = target => Output.WriteLine(target, " has stopped bleeding.");
            return bleeding;
            
        }

        public static TimedBuff Poison(int hpPerMin, int minutes)
        {
            TimedBuff poison = new TimedBuff("Poison", minutes, BuffType.Poison);

            poison.ApplyEffect = target =>
            {
                if (target is not IDamageable d)
                {
                    poison.RemoveEffect = x => Output.WriteLine("That had no effect");
                    poison.Remove();
                    return;
                } 
                Output.WriteLine(d, " has been poisoned!");
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
            InstantEffectBuff heal = new InstantEffectBuff("Heal", BuffType.Heal);

            heal.ApplyEffect = target =>
            {
                if (target is not IDamageable d)
                {
                    Output.WriteLine("That had no effect");
                    return;
                }
                d.Heal(hp);
                Output.WriteLine(target, " has been healed!");
            };
            return heal;
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
                    if (!Utils.DetermineSucess(chance)) return;
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
