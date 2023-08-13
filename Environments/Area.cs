using text_survival.Actors;
using text_survival.Items;

namespace text_survival.Environments
{
    public class Area
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<Item> Items { get; private set; }
        public List<Npc> Npcs { get; private set; }
        public double BaseTemperature { get; private set; }
        public bool IsShelter { get; set; }
        public bool Visited { get; set; }
        public List<Area> NearbyAreas { get; private set; }
        private List<Location> Locations { get; }

        public enum EnvironmentType
        {
            Forest,
            Cave,
            AbandonedBuilding,
            Road,
            River
        }
        public Area(string name, string description, double baseTemp = 70)
        {
            Name = name;
            Description = description;
            BaseTemperature = baseTemp;
            Items = new List<Item>();
            Npcs = new List<Npc>();
            EventHandler.Subscribe<ItemTakenEvent>(OnItemTaken);
            EventHandler.Subscribe<EnemyDefeatedEvent>(OnEnemyDefeated);
            NearbyAreas = new List<Area>();
            Locations = new List<Location>();
        }
        public double GetTemperature()
        {
            double effect = 0;
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

        public void GenerateNearbyAreas(int count = 3)
        {
            Array types = Enum.GetValuesAsUnderlyingType(typeof(EnvironmentType));

            for (int i = 0; i < count; i++)
            {
                EnvironmentType type = (EnvironmentType)Utils.RandInt(0, (types.Length - 1));
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
