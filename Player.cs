using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace text_survival
{
    public class Player
    {
        private float HUNGER_RATE = (2000 / (24 * 60)); // calories per minute
        private float THIRST_RATE = (4000 / (24 * 60)); // mL per minute

        private const float MAX_HEALTH = 100.0F; // percent
        private const float MAX_HUNGER = 2500.0F; // calories
        private const float MAX_THIRST = 3000.0F; // mL

        private List<Item> _inventory = new List<Item>();

        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Health { get; private set; } 

        public Player()
        {
            Hunger = MAX_HUNGER;
            Thirst = MAX_THIRST;
            Health = MAX_HEALTH;
        }

        public List<Item> Inventory { get => _inventory; private set => _inventory = value; }


        public string GetStats()
        {
            string stats = "";
            stats += "Health: " + (int) Health + "%";
            stats += "\n";
            stats += "Hunger: " + (int) ((Hunger / MAX_HUNGER) * 100) + "%";
            stats += "\n";
            stats += "Thirst: " + (int) ((Thirst / MAX_THIRST) * 100) + "%";
            stats += "\n";
            return stats;
        }

        public void Eat(FoodItem food)
        {
            if (Hunger + food.Calories > MAX_HUNGER)
            {
                Utils.Write("You are too full to finish it.");
                food.Calories -= (int) (MAX_HUNGER - Hunger);
                Hunger = MAX_HUNGER;
                return;
            }
            Hunger += food.Calories;
            Thirst += food.WaterContent;
            Inventory.Remove(food);
            Update(1);
        }

        private void UpdateOneMinute()
        {
            Hunger -= HUNGER_RATE;
            Thirst -= THIRST_RATE;
            if (Hunger <= 0)
            {
                Hunger = 0;
                Health -= 1;
            }
            if (Thirst <= 0)
            {
                Thirst = 0;
                Health -= 1;
            }
            if (Health <= 0)
            {
                Health = 0;
            }
        }

        public void Update(int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                UpdateOneMinute();
                if (Health <= 0)
                {
                    Utils.Write("You died");
                    break;
                }
            }
        }

        public Item? GetItemFromInventory(int index)
        {
            if (index < 0 || index >= Inventory.Count)
            {
                return null;
            }
            return Inventory[index];
        }

        public string GetInventoryToString()
        {
            if (Inventory.Count == 0)
            {
                return "Inventory is empty!";
            }
            string str = "";
            int count = 1;
            foreach (Item item in Inventory)
            {
                str += count + ". ";
                str += item.ToString();
                str += "\n";
                count++;
            }
            return str;
        }
        public Item? OpenInventory()
        {
            Utils.Write("Inventory:");
            Utils.Write(GetInventoryToString());
            if (Inventory.Count == 0)
            {
                return null;
            }
            Utils.Write("Enter the number of the item you want to use or type 'exit' to exit");
            string? input = Console.ReadLine();
            if (input == "exit" || input == null)
            {
                return null;
            }
            int index = int.Parse(input) - 1;
            return GetItemFromInventory(index);

        }

        public void Damage(float damage)
        {
            Health -= damage;
            Utils.Write("You took " + damage + " damage!");
            if (Health <= 0)
            {
                Utils.Write("You died!");
                Health = 0;
            }
        }
        
        public void Heal(float heal)
        {
            Health += heal;
            if (Health > MAX_HEALTH)
            {
                Health = MAX_HEALTH;
            }
        }

        
    }
}
