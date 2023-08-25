using text_survival_rpg_web.Actors;
using text_survival_rpg_web.Interfaces;

namespace text_survival_rpg_web.Environments
{
    public class Area
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<IInteractable> Things { get; private set; }

        /// <summary>
        /// Gets live NPCs in the area.
        /// </summary>
        public List<Npc> GetNpcs => Things.OfType<Npc>().Where(npc => npc.IsAlive).ToList();

        /// <summary>
        /// Returns true if there are no hostile NPCs in the area.
        /// </summary>
        public bool IsSafe => !GetNpcs.Any(npc => npc.IsHostile);
        public List<IUpdateable> GetUpdateables => Things.OfType<IUpdateable>().ToList();
        public double BaseTemperature { get; private set; }
        public bool IsShelter { get; set; }
        public bool Visited { get; set; }
        public List<Area> NearbyAreas { get; private set; }
        //private List<Location> Locations { get; }

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
            Things = new List<IInteractable>();
            NearbyAreas = new List<Area>();
           // Locations = new List<Location>();
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

        public void PutThing(IInteractable thing) => Things.Add(thing);
        public void RemoveThing(IInteractable thing) => Things.Remove(thing);
        public bool ContainsThing(IInteractable thing) => Things.Contains(thing);

    }
}
