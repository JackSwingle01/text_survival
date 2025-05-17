
using text_survival.Survival;

namespace text_survival.Environments;

public abstract class LocationFeature
{
    public string Name { get; private set; }
    protected Location ParentLocation { get; private set; }
    public LocationFeature(string name, Location location)
    {
        Name = name;
        ParentLocation = location;
    }
    // public virtual void Initialize() { }
    // public virtual void Update() { }
}



