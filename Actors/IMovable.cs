
using text_survival.Environments;
using text_survival.Environments.Grid;

namespace text_survival.Actors;

public interface IMovable
{
    public Location CurrentLocation { get; set; }
    public double GetMovementFactor();
    public GameMap Map {get; set;}
}