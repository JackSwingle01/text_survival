using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments
{
    public class Area
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<Item> Items { get; set; }
        public List<Npc> Npcs { get; set; }
        public float BaseTemperature { get; set; }
        public bool IsShelter { get; set; }

        public enum EnvironmentType
        {
            Forest,
            Cave,
            AbandonedBuilding,
            Road,
        }
        public Area(string name, string description)
        {
            Name = name;
            Description = description;
            BaseTemperature = 70;
            Items = new List<Item>();
            Npcs = new List<Npc>();
            EventAggregator.Subscribe<ItemTakenEvent>(OnItemTaken);
            EventAggregator.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
        }
        public float GetTemperature()
        {
            float effect = 0;
            if (World.GetTimeOfDay() == World.TimeOfDay.Morning)
            {
                effect -= 5;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Afternoon)
            {
                effect += 10;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Evening)
            {
                effect += 5;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Night)
            {
                effect -= 10;
            }
            effect += new Random().Next(-3, 3);
            if (IsShelter)
            {
                effect /= 2;
            }

            return effect + BaseTemperature;
        }

        public override string ToString()
        {
            return Name;
        }
        public void WriteInfo()
        {
            Utils.Write(Name);
            Utils.Write(Description);
            Utils.Write("Temperature: ", GetTemperature());

        }
        public void Enter(Player player)
        {
            player.CurrentArea = this;
            Utils.WriteLine("You enter ", this);
            Utils.WriteLine(Description);
            if (Items.Count > 0)
            {
                Utils.WriteLine("You see:");
                foreach (var item in Items)
                {
                    Utils.WriteLine(item);
                }
            }
            if (Npcs.Count > 0)
            {
                Utils.WriteLine("You see:");
                foreach (var npc in Npcs)
                {
                    Utils.WriteLine(npc);
                }
            }
           
        }

        private void OnEnemyDefeated(EnemyDefeatedEvent e)
        {
            if (!Npcs.Contains(e.DefeatedEnemy)) return;
            this.Npcs.Remove(e.DefeatedEnemy);
            //Utils.WriteWarning("enemy defeated");
        }

        private void OnItemTaken(ItemTakenEvent e)
        {
            if (!Items.Contains(e.TakenItem)) return;
            this.Items.Remove(e.TakenItem);
            //Utils.WriteWarning("item taken");
        }
        
    }
}
