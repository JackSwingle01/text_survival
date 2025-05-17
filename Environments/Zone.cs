
namespace text_survival.Environments
{
    public class Zone
    {
        public string Name { get; }
        public string Description { get; }
        public bool Visited = false;
        private double BaseTemperature { get; }
        public int Elevation { get; }
        public virtual List<Location> Locations { get; } = [];
        private LocationTable LocationTable;
        public ZoneWeather Weather;


        public Zone(string name, string description, LocationTable locationTable, double baseTemp = 20, int elevation = 0)
        {
            Name = name;
            Description = description;
            BaseTemperature = baseTemp;
            Elevation = elevation;
            LocationTable = locationTable;
            Weather = new(this);
            if (!LocationTable.IsEmpty())
            {
                for (int i = 0; i < 3; i++)
                {
                    Locations.Add(LocationTable.GenerateRandom(this));
                }
            }

        }

        // private double GetTemperatureModifier()
        // {
        //     double modifier = 0;
        //     if (World.GetTimeOfDay() == World.TimeOfDay.Morning)
        //     {
        //         modifier = -.10;
        //     }
        //     else if (World.GetTimeOfDay() == World.TimeOfDay.Afternoon)
        //     {
        //         modifier = .20;
        //     }
        //     else if (World.GetTimeOfDay() == World.TimeOfDay.Evening)
        //     {
        //         modifier = .15;
        //     }
        //     else if (World.GetTimeOfDay() == World.TimeOfDay.Night)
        //     {
        //         modifier = -.30;
        //     }
        //     modifier += Utils.RandDouble(-.1, .1);
        //     return modifier;
        // }
        // public double GetTemperature()
        // {
        //     double effect = GetTemperatureModifier();
        //     return effect * BaseTemperature;
        // }

        public void Update()
        {
            Locations.ForEach(x => x.Update());
        }

        // protected static readonly List<string> genericAdjectives = ["", "Open", "Dark", "Ominous", "Shady", "Lonely", "Ancient",];
        public override string ToString() => Name;

    }
}
