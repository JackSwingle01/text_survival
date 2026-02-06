
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

public class ResourceMemory
{
    public Dictionary<Resource, HashSet<Location>> _resourceLocations = new();
    public List<Location> _fireLocations = new();

    public void RememberLocation(Location location)
    {
        // store resources
        foreach (var resource in location.ListResourcesHere())
        {
            if (!_resourceLocations.TryGetValue(resource, out var locations))
            {
                locations = new HashSet<Location>();
                _resourceLocations[resource] = locations;
            }
            locations.Add(location);
        }
        // store fire
        if (location.HasFeature<HeatSourceFeature>())
        {
            _fireLocations.Add(location);
        }
        else
        {
            _fireLocations.Remove(location);
        }
    }

    public void ForgetLocation(Location location)
    {
        foreach (var locs in _resourceLocations.Values)
            locs.Remove(location);
        _fireLocations.Remove(location);
    }

    public void RememberResources(Location location, IEnumerable<Resource> resources)
    {
        foreach (var resource in resources)
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

    public List<Location> WhereIsFirePit() => _fireLocations;
    public Location? GetClosestActiveFire(Location currentLocation,GameMap map) => WhereIsFirePit().Where(f=>f.HasActiveHeatSource()).OrderBy(l=>map.DistanceBetween(currentLocation, l)).FirstOrDefault();
}
