using text_survival.Environments;
using text_survival.IO;
using text_survival.Items;

namespace text_survival.PlayerComponents;

class LocationManager
{
    public LocationManager(Location startingLocation)
    {
        Map = new WorldMap(startingLocation.Parent);
        _currentLocation = startingLocation;
    }
    private WorldMap Map { get; }
    public Location CurrentLocation
    {
        get
        {
            return _currentLocation;
        }
        set
        {
            Output.WriteLine("You go to the ", value);
            int minutes = Utils.RandInt(1, 10);
            World.Update(minutes);
            Output.WriteLine("You arrive at the ", value, " after walking ", minutes, " minutes.");
            _currentLocation = value;
            _currentLocation.Visited = true;
            Output.WriteLine("You should probably look around.");
        }
    }
    private Location _currentLocation;

    public Zone CurrentZone
    {
        get
        {
            return Map.CurrentZone;
        }
        set
        {
            if (CurrentZone == value)
            {
                Output.WriteLine("There's nowhere to leave. Travel instead.");
                return;
            }
            if (Map.North == value)
            {
                Output.WriteLine("You go north.");
                Map.MoveNorth();
            }
            else if (Map.East == value)
            {
                Output.WriteLine("You go east.");
                Map.MoveEast();
            }
            else if (Map.South == value)
            {
                Output.WriteLine("You go south.");
                Map.MoveSouth();
            }
            else if (Map.West == value)
            {
                Output.WriteLine("You go west.");
                Map.MoveWest();
            }
            else
                throw new Exception("Invalid zone.");
            Location? newLocation = Utils.GetRandomFromList(value.Locations);

            CurrentLocation = newLocation ?? throw new Exception("No Locations In Zone");
            Output.WriteLine("You enter ", value);
            Output.WriteLine(value.Description);
        }
    }

    public bool RemoveItemFromLocation(Item item)
    {
        if (_currentLocation.Items.Contains(item))
        {
            _currentLocation.Items.Remove(item);
            return true;
        }
        return false;
    }

    public void AddItemToLocation(Item item)
    {
        _currentLocation.Items.Add(item);
    }

    public void TravelToAdjacentZone()
    {
        Output.WriteLine("Where would you like to go?");

        Output.WriteLine(1, ". North: ", (Map.North.Visited ? Map.North.Name : " Unknown"));
        Output.WriteLine(2, ". East: ", (Map.East.Visited ? Map.East.Name : " Unknown"));
        Output.WriteLine(3, ". South: ", (Map.South.Visited ? Map.South.Name : " Unknown"));
        Output.WriteLine(4, ". West: ", (Map.West.Visited ? Map.West.Name : " Unknown"));

        Output.WriteLine("0. Cancel");
        int input = Input.ReadInt(0, 4);

        if (input == 0) return;

        int minutes = Utils.RandInt(30, 60);
        Output.WriteLine("You travel for ", minutes, " minutes...");

        switch (input)
        {
            case 1:
                CurrentZone = Map.North;
                break;
            case 2:
                CurrentZone = Map.East;
                break;
            case 3:
                CurrentZone = Map.South;
                break;
            case 4:
                CurrentZone = Map.West;
                break;
        }

        World.Update(minutes);
    }

}