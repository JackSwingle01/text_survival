using text_survival.Items;
using text_survival.Level;
using text_survival.Magic;

namespace text_survival.Actors
{
    public class Npc : ICombatant
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double Health { get; private set; }
        public double MaxHealth { get; private set; }
        public double UnarmedDamage { get; protected set; }
        public double ArmorRating { get; private set; }
        public bool IsHostile { get; private set; }
        private List<Item> Loot { get; }
        public bool IsAlive => Health > 0;
        public Attributes Attributes { get; }
        private List<Buff> Buffs { get; }

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

        public double DetermineBlockChance(ICombatant attacker)
        {
            double weaponBlock = 0;
            if (this is Humanoid humanoid)
            {
                weaponBlock = humanoid.Weapon.BlockChance;
            }
            double block = (weaponBlock * 100 + (Attributes.Luck + Attributes.Agility + Attributes.Strength) / 6) / 2;
            return block / 100;
        }

        public void Attack(ICombatant target)
        {
            // do calculations
            double damage = DetermineDamage(target);

            // check for dodge and miss
            if (Combat.DetermineDodge(this, target))
            {
                EventHandler.Publish(new GainExperienceEvent(1, SkillType.Dodge));
                return;
            }
            if (!Combat.DetermineHit(this, target)) return;
            if (Combat.DetermineBlock(this, target))
            {
                EventHandler.Publish(new GainExperienceEvent(1, SkillType.Block));
                return;
            }
            // apply damage
            Utils.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");
            target.Damage(damage);

            // gain experience
            if (target is Player player)
            {
                player.HandleArmorXpGain();
            }
            Thread.Sleep(1000);
        }



        public override string ToString()
        {
            return Name;
        }

        public void Damage(double damage)
        {
            Health -= damage;
            if (Health < 0)
            {
                EventHandler.Publish(new EnemyDefeatedEvent(this));
            }
        }

        public void Heal(double heal)
        {
            Health += heal;
            if (Health > MaxHealth)
            {
                Health = MaxHealth;
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

        public Item? DropItem()
        {
            if (Loot.Count == 0)
                return null;
            Item item = Loot[Utils.RandInt(0, Loot.Count - 1)];
            Loot.Remove(item);
            return item;
        }

        public void AddLoot(Item item)
        {
            Loot.Add(item);
        }
        public void AddLoot(List<Item> items)
        {
            Loot.AddRange(items);
        }

    }
}
