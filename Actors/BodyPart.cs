using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_survival.IO;
using text_survival.Magic;

namespace text_survival.Actors
{
    public class BodyPart : IBuffable
    {
       public string Name { get; private set; }
        public double Health { get; private set; }
        public double MaxHealth { get; set; }
        public bool IsVital { get; set; }
        public bool IsDamaged => Health < MaxHealth;
        public bool IsDestroyed => Health <= 0;
        public List<BodyPart> Parts { get; private set; }
        public BodyPart? Parent { get; private set; }
        public List<Buff> Buffs { get; set; }

        public BodyPart(string name, double maxHealth, bool isVital)
        {
            Name = name;
            MaxHealth = maxHealth;
            Health = maxHealth;
            IsVital = isVital;
            Parts = new List<BodyPart>();
            Buffs = new List<Buff>();
        }
        public void Damage(double damage)
        {
            if (Parts.Count > 0 && Utils.FlipCoin())
            {
                BodyPart p = Parts[Utils.RandInt(0, Parts.Count-1)];
                p.Damage(damage);
            } 
            else
            {
                Health -= damage;
                OutputDamageMessage(damage);
            }

            if (IsDestroyed)
            {
                Destroy();
            }
        }

        public void Destroy()
        {
            Health = 0;
            OutputDestructionMessage();
            if (IsVital)
            {
                Parent?.Destroy();
            }
            Parent?.Parts.Remove(this);
        }
        
        public void Heal(double healing)
        {
            if (Parts.Count > 0 && Utils.FlipCoin())
            {
                BodyPart p = Parts[Utils.RandInt(0, Parts.Count-1)];
                p.Heal(healing);
            } 
            else
            {
                Health += healing;
                if (Health > MaxHealth)
                {
                    Health = MaxHealth;
                }
                OutputHealingMessage(healing);

            }
        }

        public void AddPart(BodyPart part)
        {
            part.Parent = this;
            Parts.Add(part);
        }

        


        #region IO
        private void OutputDestructionMessage()
        {
            StringBuilder sb = new StringBuilder();
            BodyPart? parent = Parent;
            while (parent != null)
            {
                sb.Insert(0, $"{parent.Name}'s ");
                parent = parent.Parent;
            }
            sb.Append($"{Name} has been destroyed!");
            Output.WriteLine(sb.ToString());
        }
        private void OutputHealingMessage(double healing)
        {
            StringBuilder sb = new StringBuilder();
            BodyPart? parent = Parent;
            while (parent != null)
            {
                sb.Insert(0, $"{parent.Name}'s ");
                parent = parent.Parent;
            }
            sb.Append($"{Name} has been healed for ");
            Output.WriteLine(sb.ToString(), healing, "!");
        }

        private void OutputDamageMessage(double damage)
        {
            StringBuilder sb = new StringBuilder();
            BodyPart? parent = Parent;
            while (parent != null)
            {
                sb.Insert(0, $"{parent.Name}'s ");
                parent = parent.Parent;
            }
            
            sb.Append($"{Name} has been damaged for ");
            Output.WriteLine(sb.ToString(), damage, "!");
        }
        #endregion
    }
}
