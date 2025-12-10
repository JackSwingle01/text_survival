using text_survival.Environments;
using text_survival.Environments.Features;
namespace text_survival.Actors.Player;


public class CampManager (Location startingLocation)
{
    public Location CampLocation {get; private set;} = startingLocation;
    public HeatSourceFeature? Fire => CampLocation.GetFeature<HeatSourceFeature>();
    public ShelterFeature? Shelter => CampLocation.GetFeature<ShelterFeature>();

    public double GetFireMinutesRemaining()
    {
        if (Fire is null || !Fire.IsActive)
        {
            return 0;
        }
        return Fire.HoursRemaining * 60;
    }
    
    public void SetCamp(Location locaiton)
    {
        CampLocation = locaiton;
    }
}