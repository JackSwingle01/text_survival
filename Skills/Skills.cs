using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival.Skills
{
    public class Skills
    {
        public List<Skill> All { get; set; }
        public Skill Strength { get; set; }
        public Skill Defense { get; set; }
        public Skill Speed { get; set; }

        public Skills()
        {
            All = new List<Skill>();
            Strength = new Skill("Strength");
            Defense = new Skill("Defense");
            Speed = new Skill("Speed");

        }


    }
}
