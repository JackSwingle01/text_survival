
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

public class ResourceMemory
{
    public Dictionary<Resource, HashSet<Location>> _resourceLocations = new();
    public List<Location> _fireLocations = new();

    // Tracks when each location was last visited, as a monotonic counter rather than game
    // time (nothing here needs wall-clock precision, just relative ordering). This is what
    // lets a random walk prefer unexplored or long-stale ground over the tile it just left,
    // instead of re-rolling a uniformly random neighbor every hop with no memory of where
    // it's already been.
    private readonly Dictionary<Location, int> _lastVisitedTick = new();
    private int _visitCounter;

    public void RememberLocation(Location location)
    {
        _lastVisitedTick[location] = ++_visitCounter;

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

    public IEnumerable<Location> WhereIs(Resource r) =>
        _resourceLocations.TryGetValue(r, out var locs) ? locs : [];

    public List<Location> WhereIsFirePit() => _fireLocations;
    public Location? GetClosestActiveFire(Location currentLocation,GameMap map) => WhereIsFirePit().Where(f=>f.HasActiveHeatSource()).OrderBy(l=>map.DistanceBetween(currentLocation, l)).FirstOrDefault();

    /// <summary>
    /// Pick the candidate that's gone longest without a visit (never-visited beats any
    /// visited tile). Used to make an exploring NPC actually spread outward instead of
    /// bouncing between the same couple of tiles on a uniform random pick.
    /// </summary>
    public Location? LeastRecentlyVisited(IReadOnlyList<Location> candidates)
    {
        if (candidates.Count == 0) return null;
        return candidates.MinBy(l => _lastVisitedTick.TryGetValue(l, out var tick) ? tick : -1);
    }
}
