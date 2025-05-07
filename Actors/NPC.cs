using text_survival.Actors.text_survival.Actors;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Interfaces;
using text_survival.IO;
using text_survival.Items;
using text_survival.Level;
using text_survival.Magic;
using text_survival.PlayerComponents;

namespace text_survival.Actors
{
    public class Npc : ICombatant, IInteractable, IClonable<Npc>
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public double UnarmedDamage { get; protected set; }
        public double ArmorRating { get; private set; }
        public bool IsHostile { get; private set; }
        private Container Loot { get; }
        public bool IsAlive => SurvivalManager.IsAlive;
        public bool IsFound { get; set; }
        public bool IsEngaged { get; set; }
        public Attributes Attributes { get; }
        public IClonable<Npc>.CloneDelegate Clone { get; set; }
        private SurvivalManager SurvivalManager { get; }

        public Npc(string name, Attributes? attributes = null)
        {
            Name = name;
            Attributes = attributes ?? new Attributes();
            int health = (int)(((Attributes.Strength + Attributes.Endurance) / 10) * 2);
            BodyPart body;
            if (this is Humanoid)
            {
                body = BodyPartFactory.CreateHumanBody(name, health);
            }
            else if (this is Animal)
            {
                body = BodyPartFactory.CreateAnimalBody(name, health);
            }
            else
            {
                body = BodyPartFactory.CreateGenericBody(name, health);
            }
            SurvivalManager = new SurvivalManager(this, false, body);

            Loot = new Container(name, 10);
            IsHostile = true;
            Description = "";
            UnarmedDamage = 2;
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

        public Location CurrentLocation => throw new NotImplementedException();

        public Zone CurrentZone => throw new NotImplementedException();

        // Update //
        public void Update()
        {
            SurvivalManager.Update();
        }

        // COMBAT //

        public virtual double DetermineDamage()
        {
            double baseDamage = UnarmedDamage;
            if (this is Humanoid humanoid)
            {
                baseDamage = humanoid.Weapon.Damage;
            }

            double damage = Combat.CalculateAttackDamage(baseDamage, Attributes.Strength);

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

            double damage = DetermineDamage();

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

        public void Damage(double damage) => SurvivalManager.Damage(damage);


        public void Heal(double heal) => SurvivalManager.Heal(heal);


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

        public void ApplyEffect(IEffect effect) => SurvivalManager.AddEffect(effect);

        public void RemoveEffect(string effectType) => SurvivalManager.RemoveEffect(effectType);

    }
}
