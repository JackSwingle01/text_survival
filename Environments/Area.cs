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
        public bool Visited { get; set; }
        public List<Area> NearbyAreas { get; set; }

        public enum EnvironmentType
        {
            Forest,
            Cave,
            AbandonedBuilding,
            // Road,
            River
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
            NearbyAreas = new List<Area>();
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
            string str = Name;
            return str;
        }
        public void WriteInfo()
        {
            Utils.Write(Name);
            Utils.Write(Description);
            Utils.Write("Temperature: ", GetTemperature());

        }
        public void Enter(Player player)
        {
            Utils.WriteLine("You enter ", this);
            Utils.WriteLine(Description);
            this.NearbyAreas.Add(player.CurrentArea);
            player.CurrentArea = this;
            if (!Visited)
                GenerateNearbyAreas();
            Visited = true;
            Utils.WriteLine("You should probably look around.");
        }

        private void GenerateNearbyAreas(int count = 3)
        {
            Array types = Enum.GetValuesAsUnderlyingType(typeof(EnvironmentType));

            for (int i = 0; i < count; i++)
            {
                EnvironmentType type = (EnvironmentType)Utils.Rand(0, (types.Length - 1));
                NearbyAreas.Add(AreaFactory.GenerateArea(type));
            }
        }

        private void OnEnemyDefeated(EnemyDefeatedEvent e)
        {
            if (!Npcs.Contains(e.DefeatedEnemy)) return;
            this.Npcs.Remove(e.DefeatedEnemy);
        }

        private void OnItemTaken(ItemTakenEvent e)
        {
            if (!Items.Contains(e.TakenItem)) return;
            this.Items.Remove(e.TakenItem);
        }

    }
}
