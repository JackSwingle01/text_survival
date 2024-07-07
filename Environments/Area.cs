using text_survival.Actors;
using text_survival.Interfaces;
using text_survival.IO;
using text_survival.Items;
using static text_survival.Environments.Location;

namespace text_survival.Environments
{
    public class Area : Location
    {
        //public string Name { get; set; }
        public string Description { get; set; }
        public double BaseTemperature { get; private set; }
        //public bool Visited { get; set; }
        //public List<IInteractable> Things { get;   set; }
        public List<Area> NearbyAreas { get; private set; }
        
        public bool IsSafe => !Npcs.Any(npc => npc.IsHostile);
        //public List<IUpdateable> GetUpdateables => Things.OfType<IUpdateable>().ToList();
        public List<Npc> Npcs => Things.OfType<Npc>().Where(npc => npc.IsAlive).ToList();
        //private List<Location> ChildLocations => Things.OfType<Location>().ToList();
        public List<Item> Items => Things.OfType<Item>().ToList(); 
        protected override IPlace Parent => throw new Exception("Areas have no parent.");

        public enum EnvironmentType
        {
            Forest,
        }
        public Area(string name, string description, double baseTemp = 70, int subLocations = 1)
        {
            Name = name;
            Description = description;
            BaseTemperature = baseTemp;
            Things = [];
            NearbyAreas = [];
            for (int i = 0; i < subLocations; i++)
            {
                GenerateRandomSubLocation(1, 1);
            }
        }

        public double GetTemperatureModifer()
        {
            double modifier = 0;
            if (World.GetTimeOfDay() == World.TimeOfDay.Morning)
            {
                modifier -= 5;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Afternoon)
            {
                modifier += 10;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Evening)
            {
                modifier += 5;
            }
            else if (World.GetTimeOfDay() == World.TimeOfDay.Night)
            {
                modifier -= 10;
            }
            modifier += Utils.RandInt(-3, 3);
            return modifier;
        }
        public override double GetTemperature()
        {
            double effect = GetTemperatureModifer();
            return effect + BaseTemperature;
        }

        public override string ToString() => Name;

        public void GenerateNearbyAreas(int count = 3)
        {
            Array types = Enum.GetValuesAsUnderlyingType(typeof(EnvironmentType));

            for (int i = 0; i < count; i++)
            {
                EnvironmentType type = (EnvironmentType)Utils.RandInt(0, (types.Length - 1));
                NearbyAreas.Add(AreaFactory.GenerateArea(type));
            }
        }
        public bool ContainsThing(IInteractable thing)
        {
            if (Things.Contains(thing)) return true;
            foreach (var t in Things)
            {
                if (t is IPlace place)
                {
                    if (place.ContainsThing(thing)) return true;
                }
            }
            return false;
        }

        public override void Enter(Player player)
        {
            Output.WriteLine("You enter ", this);
            Output.WriteLine(Description);
            player.MoveTo(this);
            if (!Visited) GenerateNearbyAreas();
            Visited = true;
            Output.WriteLine("You should probably look around.");
        }

        public override void Leave(Player player)
        {
            Output.WriteLine("You can't leave ", this, " travel instead.");
        }

        public void GenerateRandomSubLocation(int maxItems = 0, int maxNpcs = 0)
        {
            LocationType type = Utils.GetRandomEnum<LocationType>();
            int items = Utils.RandInt(0, maxItems);
            int npcs = Utils.RandInt(0, maxNpcs);
            GenerateSubLocation(type, items, npcs);
        }

        
    }
}
