using text_survival.Environments;
namespace text_survival.Interfaces;

public interface ILocatable
{
    public Location CurrentLocation {get;}
    public Zone CurrentZone {get;}
}
