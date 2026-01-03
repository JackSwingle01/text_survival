
using text_survival.Environments;

public class ResourceMemory
{
    public Dictionary<Location, HashSet<Resource>> _locationResources = new();
    public void RememberLocation(Location location)
    {
        _locationResources[location] = location.ListResourcesHere().ToHashSet();
    }

    public IEnumerable<Location> WhereIs(Resource r) =>
       _locationResources.Where(kv => kv.Value.Contains(r)).Select(kv => kv.Key);

    public HashSet<Resource>? WhatIsAt(Location loc) =>
        _locationResources.GetValueOrDefault(loc);
}