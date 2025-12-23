using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
namespace text_survival.Actors.Player;


public class Camp(Location startingLocation)
{
    public Location Location { get; private set; } = startingLocation;
    public HeatSourceFeature? Fire => Location.GetFeature<HeatSourceFeature>();
    public ShelterFeature? Shelter => Location.GetFeature<ShelterFeature>();

    // Camp storage (aggregate-based, unlimited capacity)
    public Inventory Storage { get; } = Inventory.CreateCampStorage();

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