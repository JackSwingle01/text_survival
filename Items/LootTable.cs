using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_survival.Items
{
    public class LootTable
    {
        private List<Delegate> factoryMethods;
        public LootTable()
        {
            factoryMethods = new List<Delegate>();
        }
        public LootTable(List<Delegate> factoryMethods)
        {
            this.factoryMethods = factoryMethods;
        }
        public void AddLoot(Delegate loot)
        {
            factoryMethods.Add(loot);
        }
        public Item GenerateRandomItem()
        {
            var loot = Utils.GetRandomFromList(factoryMethods).DynamicInvoke();
            if (loot is Item i)
            {
                return i;
            }
            else throw new Exception("Trying to generate from empty loot table!");
        }
    }
}
