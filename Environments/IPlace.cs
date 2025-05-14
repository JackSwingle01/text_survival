namespace text_survival.Environments;

public abstract class Place
{
    public string Name { get; set; } = "";
    public bool Visited { get; set; }
    public virtual List<Location> Locations { get; } = [];

    public abstract double GetTemperature();
    public virtual void Update() { Locations.ForEach(x => x.Update()); }
    public void PutLocation(Location location) => Locations.Add(location);
}
