using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival.Actors
{
    public interface IActor
    {
        public string Name { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float Strength { get; set; }
        public float Defense { get; set; }
        public int Speed { get; set; }

        public void Damage(float damage);
        
    }
}
