﻿using text_survival.Actors;
using text_survival.Interfaces;
using text_survival.Items;
using static text_survival.Environments.Location;

namespace text_survival.Environments
{
    public class Zone : IPlace, IHasLocations
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double BaseTemperature { get; protected set; }
        public bool Visited { get; set; } = false;
        public List<Location> Locations { get; protected set; } = [];
        public virtual List<Item> ItemList { get; } = [];
        public virtual List<Npc> NpcList { get; } = [];

        public enum EnvironmentType
        {
            Forest,
        }
        public Zone(string name, string description, double baseTemp = 70, int subLocations = 1)
        {
            Name = name;
            Description = description;
            BaseTemperature = baseTemp;
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
        public double GetTemperature()
        {
            double effect = GetTemperatureModifer();
            return effect + BaseTemperature;
        }

        public void GenerateRandomSubLocation(int maxItems = 0, int maxNpcs = 0)
        {

            LocationType type = LocationType.None;
            while (type == LocationType.None)
            {
                type = Utils.GetRandomEnum<LocationType>();
            }
            int items = Utils.RandInt(0, maxItems);
            int npcs = Utils.RandInt(0, maxNpcs);
            Location.GenerateSubLocation(this, type, items, npcs);
        }

        public virtual void Update()
        {
            List<IUpdateable> updateables = Locations.OfType<IUpdateable>().ToList();
            foreach (var updateable in updateables)
            {
                updateable.Update();
            }
        }

        public void PutLocation(Location location) => Locations.Add(location);
        protected static readonly List<string> genericAdjectives = ["", "Open", "Dark", "Ominous", "Shady", "Lonely", "Ancient",];
        public override string ToString() => Name;

    }
}
