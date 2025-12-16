
namespace text_survival.Environments
{

    public class Zone
    {
        public string Name { get; }
        public string Description { get; }
        public LocationGraph Graph { get; } = new();
        public ZoneWeather Weather;

        public Zone(string name, string description, double baseTemp = 20)
        {
            Name = name;
            Description = description;
            Weather = new(baseTemp);
        }

        public void Update(int minutes, DateTime time)
        {
            Weather.Update(time);
            foreach (var location in Graph.All)
            {
                location.Update(minutes);
            }
        }
    }
}
