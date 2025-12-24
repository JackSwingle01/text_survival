using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
namespace text_survival.Actors.Player;


public class Camp(Location startingLocation)
{
    public Location Location { get; private set; } = startingLocation;
    public HeatSourceFeature? Fire => Location.GetFeature<HeatSourceFeature>();
    public ShelterFeature? Shelter => Location.GetFeature<ShelterFeature>();
    public CuringRackFeature? CuringRack => Location.GetFeature<CuringRackFeature>();

    // Camp storage backed by CacheFeature on location
    public CacheFeature? Cache => Location.GetFeature<CacheFeature>();
    public Inventory Storage => Cache?.Storage ?? throw new InvalidOperationException("Camp has no cache");

    public double FireMinutesRemaining =>
        HasActiveFire ? Fire!.HoursRemaining * 60 : 0;

    public bool HasActiveFire => Fire?.IsActive == true;

    public void MoveTo(Location newLocation)
    {
        Location = newLocation;
        newLocation.Explore();
    }

    /// <summary>
    /// Set camp location directly without triggering exploration (for save/load).
    /// </summary>
    internal void SetLocation(Location location)
    {
        Location = location;
    }
}