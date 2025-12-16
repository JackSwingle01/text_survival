using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
namespace text_survival.Actors.Player;


public class Camp(Location startingLocation)
{
    public Location Location { get; private set; } = startingLocation;
    public HeatSourceFeature? Fire => Location.GetFeature<HeatSourceFeature>();
    public ShelterFeature? Shelter => Location.GetFeature<ShelterFeature>();
    public List<Item> Stash => Location.Items;

    public double FireMinutesRemaining =>
        HasActiveFire ? Fire!.HoursRemaining * 60 : 0;

    public bool HasActiveFire => Fire?.IsActive == true;

    public void MoveTo(Location newLocation)
    {
        Location = newLocation;
        newLocation.Explore();
    }
}