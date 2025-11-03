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

    /// <summary>Gets the world map for map display</summary>
    public WorldMap GetWorldMap() => Map;
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
                throw new Exception("Invalid zone!");
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
        else
        {
            Container? container = _currentLocation.Containers.FirstOrDefault(x => x.Items.Contains(item));
            if (container != null)
            {
                container.Remove(item);
                return true;
            }
        }
        return false;
    }

    public void AddItemToLocation(Item item)
    {
        _currentLocation.Items.Add(item);
    }

    public void TravelToAdjacentZone()
    {
        // Display unified map
        string mapDisplay = UI.MapRenderer.RenderUnifiedMap(Map, CurrentZone, CurrentLocation);
        Output.WriteLine(mapDisplay);

        // Get directional input
        string direction = UI.MapController.GetDirectionalInput();

        if (direction == "Q")
        {
            Output.WriteLine("You decide to stay where you are.");
            return;
        }

        // Calculate travel time
        int minutes = UI.MapController.CalculateZoneTravelTime();
        Output.WriteLine($"You travel {direction.ToLower()} for {minutes} minutes...");

        // Move to the zone in the chosen direction
        switch (direction)
        {
            case "N":
                CurrentZone = Map.North;
                break;
            case "E":
                CurrentZone = Map.East;
                break;
            case "S":
                CurrentZone = Map.South;
                break;
            case "W":
                CurrentZone = Map.West;
                break;
        }

        World.Update(minutes);
    }

    /// <summary>
    /// Travel to a location within the current zone using coordinate-based navigation
    /// </summary>
    public void TravelToLocalLocation(string direction)
    {
        var destination = UI.MapController.GetLocationInDirection(CurrentZone, CurrentLocation, direction);

        if (destination == null)
        {
            Output.WriteLine($"There is no location to the {direction.ToLower()} of here.");
            return;
        }

        // Calculate travel time based on distance
        int minutes = UI.MapController.CalculateLocalTravelTime(CurrentLocation, destination);
        Output.WriteLine($"You head {direction.ToLower()} toward {destination.Name}...");
        World.Update(minutes);

        // Set new location
        CurrentLocation = destination;
    }

}