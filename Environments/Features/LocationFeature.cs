namespace text_survival.Environments.Features;

public abstract class LocationFeature
{
    public string Name { get; private set; }
    public LocationFeature(string name)
    {
        Name = name;
    }
    public virtual void Update(int minutes) {}
    // public virtual void Initialize() { }
}



