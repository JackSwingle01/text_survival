﻿using static text_survival.Environments.Zone;

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
        }

        public Zone CurrentZone => GetZone(0, 0);
        public Zone North => GetZone(X, Y + 1);
        public Zone South => GetZone(X, Y - 1);
        public Zone East => GetZone(X + 1, Y);
        public Zone West => GetZone(X - 1, Y);


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

        private Zone GetZone(int x, int y)
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
            EnvironmentType type = Utils.GetRandomEnum<EnvironmentType>();
            Zone zone = AreaFactory.GenerateArea(type);
            //Zone zone = switch type
            //{
            //    EnvironmentType.Forest => new Forest(),
            //    _ => throw new Exception("Invalid zone type.")
            //};
            return zone;
        }

    }
}
