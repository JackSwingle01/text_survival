using text_survival.Environments.Factories;

namespace text_survival.Environments
{
    public class WorldMap
    {
        private Dictionary<(int, int), Zone> map = new();

        private int X;
        private int Y;

        public WorldMap(Zone startingZone)
        {
            X = 0;
            Y = 0;
            map.Add((X, Y), startingZone);
            startingZone.Visited = true;
        }

        public Zone CurrentZone => GetZone(X, Y);
        public Zone North => GetZone(X, Y + 1);
        public Zone South => GetZone(X, Y - 1);
        public Zone East => GetZone(X + 1, Y);
        public Zone West => GetZone(X - 1, Y);

        /// <summary>Returns the coordinates of the current zone</summary>
        public (int x, int y) GetCurrentCoordinates() => (X, Y);

        /// <summary>Returns the coordinates of a specific zone, or null if not found</summary>
        public (int x, int y)? GetZoneCoordinates(Zone zone)
        {
            foreach (var kvp in map)
            {
                if (kvp.Value == zone)
                {
                    return kvp.Key;
                }
            }
            return null;
        }


        public void MoveNorth() => MoveTo(X, Y + 1);
        public void MoveSouth() => MoveTo(X, Y - 1);
        public void MoveEast() => MoveTo(X + 1, Y);
        public void MoveWest() => MoveTo(X - 1, Y);


        private void MoveTo(int x, int y)
        {
            Zone zone = GetZone(x, y) ?? throw new Exception("Invalid zone.");
            X = x;
            Y = y;
            zone.Visited = true;
        }

        public Zone GetZone(int x, int y)
        {
            Zone? zone = map.GetValueOrDefault((x, y));
            if (zone == null)
            {
                zone = GenerateRandomZone();
                map.Add((x, y), zone);
            }
            return zone;
        }

        private Zone GenerateRandomZone()
        {
            // todo
            return Utils.GetRandomFromList([ZoneFactory.MakeForestZone(), ZoneFactory.MakeCaveSystemZone()]);
        }

    }
}
