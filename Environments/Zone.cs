
namespace text_survival.Environments
{
    public enum ZoneType
    {
        Forest,
        CaveSystem,
        Tundra,
        RiverValley,
        Plains,
        Unknown
    }

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
        public ZoneType Type { get; set; } = ZoneType.Unknown;


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
                // Generate 3 locations with coordinate assignments
                for (int i = 0; i < 3; i++)
                {
                    var location = LocationTable.GenerateRandom(this);

                    // Assign coordinates for map display
                    if (i == 0)
                    {
                        // First location is at zone entry point (0, 0)
                        location.CoordinateX = 0;
                        location.CoordinateY = 0;
                    }
                    else
                    {
                        // Subsequent locations get random positions
                        // ensuring minimum distance from existing locations
                        bool validPosition = false;
                        int attempts = 0;
                        while (!validPosition && attempts < 20)
                        {
                            location.CoordinateX = Utils.RandInt(-300, 300);
                            location.CoordinateY = Utils.RandInt(-300, 300);

                            // Check distance from all existing locations
                            validPosition = true;
                            foreach (var existingLoc in Locations)
                            {
                                double distance = Math.Sqrt(
                                    Math.Pow(location.CoordinateX - existingLoc.CoordinateX, 2) +
                                    Math.Pow(location.CoordinateY - existingLoc.CoordinateY, 2)
                                );

                                if (distance < 150) // Minimum separation
                                {
                                    validPosition = false;
                                    break;
                                }
                            }
                            attempts++;
                        }

                        // If we couldn't find a valid position after 20 attempts, use the last random position
                    }

                    Locations.Add(location);
                }
            }

        }

        public void Update()
        {
            Locations.ForEach(x => x.Update());
        }

        /// <summary>Returns emoji symbol for this zone type for map display</summary>
        public string GetSymbol()
        {
            return Type switch
            {
                ZoneType.Forest => "🌲",
                ZoneType.CaveSystem => "⛰",
                ZoneType.Tundra => "❄",
                ZoneType.RiverValley => "〰",
                ZoneType.Plains => "🌾",
                _ => "?"
            };
        }

        public override string ToString() => Name;

    }
}
