using text_survival.Actors;
using text_survival.Environments;
using text_survival.Interfaces;
using text_survival.Items;
using text_survival.Level;
using text_survival.Magic;

namespace text_survival.Actors
{
    public class Npc : ICombatant, IInteractable
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double Health { get; private set; }
        public double MaxHealth { get; private set; }
        public double UnarmedDamage { get; protected set; }
        public double ArmorRating { get; private set; }
        public bool IsHostile { get; private set; }
        private Container Loot { get; }
        public bool IsAlive => Health > 0;
        public bool IsFound { get; set; }
        public bool IsEngaged { get; set; }
        public Attributes Attributes { get; }
        public List<Buff> Buffs { get; }

        public Npc(string name, Attributes? attributes = null)
        {
            Name = name;
            Attributes = attributes ?? new Attributes();
            MaxHealth = (int)(((Attributes.Strength + Attributes.Endurance) / 10) * 2);
            Health = MaxHealth;
            Loot = new Container(name, 10);
            IsHostile = true;
            Description = "";
            UnarmedDamage = 2;
            Buffs = new List<Buff>();
        }

        // Interact //

        public void Interact(Player player)
        {
            if (IsAlive)
            {
                Combat.CombatLoop(player, this);
            }
            else // loot
            {
                if (Loot.IsEmpty)
                {
                    Output.WriteLine("There is nothing to loot.");
                    return;
                }
                Loot.Open(player);
                //this.DropInventory(player.CurrentArea);
            }
        }
        public Command<Player> InteractCommand
        {
            get
            {
                string name;
                if (IsAlive)
                    name = "Fight " + Name;
                else
                    name = "Loot " + Name;
                return new Command<Player>(name, Interact);
            }
        }

        // Update //
        public void Update()
        {
            if (!IsAlive) return;
            List<Buff> buffs = new List<Buff>(Buffs);
            foreach (Buff buff in buffs)
            {
                if (buff is TimedBuff timedBuff)
                    timedBuff.Tick();
            }
            buffs.Clear();
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
            // attack event
            var e = new CombatEvent(EventType.OnAttack, this, target);
            if (this is Humanoid humanoid)
            {
                e.Weapon = humanoid.Weapon;
            }
            EventHandler.Publish(e);

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

            Output.WriteLine(this, " attacked ", target, " for ", Math.Round(damage, 1), " damage!");

            // trigger hit event
            e = new CombatEvent(EventType.OnHit, this, target)
            {
                Damage = damage
            };
            EventHandler.Publish(e);

            // apply damage
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
        }

        public void Heal(double heal)
        {
            Health += heal;
            if (Health > MaxHealth)
            {
                Health = MaxHealth;
            }
        }

        public void AddToBuffList(Buff buff)
        {
            Buffs.Add(buff);
        }

        public void RemoveFromBuffList(Buff buff)
        {
            Buffs.Remove(buff);
        }

        public void DropInventory(Area area)
        {
            while (!Loot.IsEmpty)
            {
                Item item = Loot.GetItem(0);
                Output.WriteLine(this, " dropped ", item, "!");
                DropItem(item, area);
            }
        }
        private void DropItem(Item item, Area area)
        {
            item.IsFound = true;
            Loot.Remove(item);
            area.PutThing(item);
        }

        public void AddLoot(Item item)
        {
            Loot.Add(item);
        }
        public void AddLoot(List<Item> items)
        {
            foreach (Item item in items)
            {
                Loot.Add(item);
            }
        }

    }
}
