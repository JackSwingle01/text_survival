﻿using text_survival.Actors.text_survival.Actors;
using text_survival.Environments;
using text_survival.Interfaces;
using text_survival.IO;
using text_survival.Items;
using text_survival.Level;
using text_survival.Magic;

namespace text_survival.Actors
{
    public class Npc : ICombatant, IInteractable, IClonable<Npc>
    {
        public string Name { get; set; }
        public string Description { get; set; }
        //public double Health => Body.Health;
        //public double MaxHealth => Body.MaxHealth;
        public BodyPart Body { get; }
        public double UnarmedDamage { get; protected set; }
        public double ArmorRating { get; private set; }
        public bool IsHostile { get; private set; }
        private Container Loot { get; }
        public bool IsAlive => Body.Health > 0;
        public bool IsFound { get; set; }
        public bool IsEngaged { get; set; }
        public Attributes Attributes { get; }
        public List<Buff> Buffs { get; }
        public IClonable<Npc>.CloneDelegate Clone { get; set; }


        public Npc(string name, Attributes? attributes = null)
        {
            Name = name;
            Attributes = attributes ?? new Attributes();
            int health = (int)(((Attributes.Strength + Attributes.Endurance) / 10) * 2);
            if (this is Humanoid)
            {
                Body = BodyPartFactory.CreateHumanBody(name, health);
            }
            else if (this is Animal)
            {
                Body = BodyPartFactory.CreateAnimalBody(name, health);
            }
            else
            {
                Body = BodyPartFactory.CreateGenericBody(name, health);
            }
            //Health = MaxHealth;
            Loot = new Container(name, 10);
            IsHostile = true;
            Description = "";
            UnarmedDamage = 2;
            Buffs = [];
            Clone = () => new Npc(name, attributes);
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

        public double DetermineHitChance(ICombatant attacker) => 1;

        public double DetermineDodgeChance(ICombatant attacker)
        {
            return Combat.CalculateDodgeChance(
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
            double block = (weaponBlock * 100 + (Attributes.Luck + Attributes.Speed + Attributes.Strength) / 6) / 2;
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

            target.Damage(damage);

            // gain experience
            if (target is Player player)
            {
                player.HandleArmorXpGain();
            }
            Thread.Sleep(1000);
        }

        public override string ToString() => Name;

        public void Damage(double damage)
        {
            Body.Damage(damage);
        }

        public void Heal(double heal)
        {
            Body.Heal(heal);
        }

        public void DropInventory(Location location)
        {
            while (!Loot.IsEmpty)
            {
                Item item = Loot.GetItem(0);
                Output.WriteLine(this, " dropped ", item, "!");
                DropItem(item, location);
            }
        }
        private void DropItem(Item item, Location location)
        {
            item.IsFound = true;
            Loot.Remove(item);
            location.PutThing(item);
        }

        public void AddLoot(Item item) => Loot.Add(item);
        public void AddLoot(List<Item> items)
        {
            foreach (Item item in items)
            {
                Loot.Add(item);
            }
        }

    }
}
