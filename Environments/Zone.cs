namespace text_survival.Environments
{
    public class Zone
    {
        public string Name { get; }
        public string Description { get; }
        public LocationGraph Graph { get; } = new();
        public ZoneWeather Weather;

        // Pool of locations not yet connected to the graph
        private List<Location> _unrevealedLocations = [];

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

        /// <summary>
        /// Add a location to the unrevealed pool (not yet connected to graph)
        /// </summary>
        public void AddUnrevealedLocation(Location location)
        {
            _unrevealedLocations.Add(location);
        }

        /// <summary>
        /// Check if there are any unrevealed locations remaining
        /// </summary>
        public bool HasUnrevealedLocations()
        {
            return _unrevealedLocations.Count > 0;
        }

        /// <summary>
        /// Reveal a random location from the pool and connect it to the specified location
        /// </summary>
        public Location? RevealRandomLocation(Location connectFrom)
        {
            if (_unrevealedLocations.Count == 0)
                return null;

            // Pick a random unrevealed location
            var random = new Random();
            int index = random.Next(_unrevealedLocations.Count);
            var newLocation = _unrevealedLocations[index];
            _unrevealedLocations.RemoveAt(index);

            // Connect it to the graph
            connectFrom.AddBidirectionalConnection(newLocation);
            Graph.Add(newLocation);
            newLocation.Explore();

            return newLocation;
        }

        /// <summary>
        /// Get count of unrevealed locations (for UI hints)
        /// </summary>
        public int UnrevealedCount => _unrevealedLocations.Count;
    }
}
