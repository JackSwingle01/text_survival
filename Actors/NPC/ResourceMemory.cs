
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

public class ResourceMemory
{
    // Ordered by when each location was first learned about, not hashed. Location has no
    // GetHashCode of its own, so a HashSet here iterated in allocation order - which differs
    // between two runs of the same seed, and leaked into every "closest known source" tie,
    // making seeded runs unreproducible.
    public Dictionary<Resource, List<Location>> _resourceLocations = new();
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

        // Refresh, not append: a tile foraged to depletion has to drop out of memory, or
        // GetClosestKnownResource keeps sending the NPC back to it - and two stale tiles
        // next to each other are each the other's nearest "known" source, which is a loop.
        var here = location.ListResourcesHere();
        foreach (var (resource, locations) in _resourceLocations)
        {
            if (here.Contains(resource))
            {
                if (!locations.Contains(location)) locations.Add(location);
            }
            else locations.Remove(location);
        }
        foreach (var resource in here)
        {
            if (!_resourceLocations.ContainsKey(resource))
                _resourceLocations[resource] = [location];
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
    public Location? GetClosestActiveFire(Location currentLocation, GameMap map) => WhereIsFirePit().Where(f => f.HasActiveHeatSource()).OrderBy(l => map.DistanceBetween(currentLocation, l)).FirstOrDefault();

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
