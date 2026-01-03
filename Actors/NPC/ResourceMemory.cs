
using text_survival.Environments;

public class ResourceMemory
{
    public Dictionary<Resource, HashSet<Location>> _resourceLocations = new();

    public void RememberLocation(Location location)
    {
        foreach (var resource in location.ListResourcesHere())
        {
            if (!_resourceLocations.TryGetValue(resource, out var locations))
            {
                locations = new HashSet<Location>();
                _resourceLocations[resource] = locations;
            }
            locations.Add(location);
        }
    }

    public IEnumerable<Location> WhereIs(Resource r) =>
        _resourceLocations.TryGetValue(r, out var locs) ? locs : [];
}
