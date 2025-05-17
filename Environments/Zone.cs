
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

        public void Update()
        {
            Locations.ForEach(x => x.Update());
        }

        public override string ToString() => Name;

    }
}
