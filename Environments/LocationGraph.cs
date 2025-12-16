namespace text_survival.Environments;
public class LocationGraph
{
    private List<Location> _locations = [];
    
    public IReadOnlyList<Location> All => _locations.AsReadOnly();
    public IEnumerable<Location> Sites => _locations.Where(l => l.IsSite);
    public IEnumerable<Location> Paths => _locations.Where(l => l.IsPath);
    
    public void Add(Location location)
    {
        _locations.Add(location);
    }
    
    public void CreatePath(
        string name,
        Zone parent,
        Location a,
        Location b,
        int traversalMinutes,
        TerrainType terrain = TerrainType.Clear,
        double exposure = 0.5)
    {
        var path = new Location(name, parent)
        {
            BaseTraversalMinutes = traversalMinutes,
            Terrain = terrain,
            Exposure = exposure,
        };
        
        a.AddBidirectionalConnection(path);
        path.AddBidirectionalConnection(b);
        
        _locations.Add(path);
    }
}