namespace text_survival.Environments;
public class LocationGraph
{
    private List<Location> _locations = [];
    
    public IReadOnlyList<Location> All => _locations.AsReadOnly();
    
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
        double exposure = 0.5)
    {
        var path = new Location(name, parent)
        {
            BaseTraversalMinutes = traversalMinutes,
            WindCoverFactor = exposure,
        };
        
        a.AddBidirectionalConnection(path);
        path.AddBidirectionalConnection(b);
        
        _locations.Add(path);
    }

    public void ConnectWithPath(Location path, Location a, Location b)
    {
        a.AddBidirectionalConnection(path);
        path.AddBidirectionalConnection(b);
        _locations.Add(path);
    }
}