using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival.Actors
{
    internal class Animal : Npc
    {
        public Animal(string name, int health, int maxHealth, int attack, int defense) : 
            base(name, health, maxHealth, attack, defense)
        {

        }
    }
}
