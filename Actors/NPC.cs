﻿using text_survival.Items;
using text_survival.Level;

namespace text_survival.Actors
{
    public class Npc : ICombatant
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double Health { get; set; }
        public double MaxHealth { get; set; }
        public double UnarmedDamage { get; set; }
        public double ArmorRating { get; set; }
        public bool IsHostile { get; set; }
        public List<Item> Loot { get; set; }
        public bool IsAlive => Health > 0;
        public Attributes Attributes { get; set; }
        public List<Buff> Buffs { get; set; }

        public Npc(string name, Attributes? attributes = null)
        {
            Name = name;
            Attributes = attributes ?? new Attributes();
            MaxHealth = (int)(((Attributes.Strength + Attributes.Endurance) / 10) * 2);
            Health = MaxHealth;
            Loot = new List<Item>();
            IsHostile = true;
            Description = "";
            UnarmedDamage = 2;
            Buffs = new List<Buff>();
        }

        // Update //
        public void Update()
        {
            foreach (Buff buff in Buffs)
            {
                buff.Tick(this);
            }
        }

        // COMBAT //

        public virtual double DetermineDamage(ICombatant defender)
        {
            double baseDamage = UnarmedDamage;
            if (this is Humanoid humanoid)
            {
                baseDamage = humanoid.Weapon.Damage;
            };

            double damage = Combat.CalculateAttackDamage(baseDamage, Attributes.Strength, defender.ArmorRating);

            return damage;
        }

        public double DetermineHitChance(ICombatant attacker)
        {
            return 1;
        }

        public double DetermineDodgeChance(ICombatant attacker)
        {
            return Combat.CalculateDodgeChance(
                Attributes.Agility,
                Attributes.Speed,
                attacker.Attributes.Speed,
                Attributes.Luck);
        }

        public void Attack(ICombatant target)
        {
            // do calculations
            double damage = DetermineDamage(target);
            double hitChance = DetermineHitChance(target);
            double dodgeChance = target.DetermineDodgeChance(this);

            // roll and determine miss or dodge
            int roll = Utils.RandInt(0, 100);
            if (roll > hitChance * 100)
            {
                Utils.WriteLine(this, " missed ", target, "!");
                return;
            }
            if (roll > (1 - dodgeChance) * 100)
            {
                Utils.Write(target, " dodged the attack!\n");
                // gain experience
                if (target is Player)
                    EventAggregator.Publish(new GainExperienceEvent(1, SkillType.Dodge));
                return;
            }

            // apply damage
            Utils.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");
            target.Damage(damage);

            // gain experience
            if (target is Player player)
            {
                if (player.Armor.Any(a => a.Type == ArmorClass.Light))
                    EventAggregator.Publish(new GainExperienceEvent(1, SkillType.LightArmor));
                if (player.Armor.Any(a => a.Type == ArmorClass.Heavy))
                    EventAggregator.Publish(new GainExperienceEvent(1, SkillType.HeavyArmor));
            }
            Thread.Sleep(1000);
        }


        public override string ToString()
        {
            return Name;
        }
        public void Write()
        {
            Utils.Write(this);
        }

        public void Damage(double damage)
        {
            Health -= damage;
            if (Health < 0)
            {
                EventAggregator.Publish(new EnemyDefeatedEvent(this));
            }
        }

        public void ApplyBuff(Buff buff)
        {
            buff.ApplyEffect?.Invoke(this);
            Buffs.Add(buff);
        }

        public void RemoveBuff(Buff buff)
        {
            buff.RemoveEffect?.Invoke(this);
            Buffs.Remove(buff);
        }

    }
}
