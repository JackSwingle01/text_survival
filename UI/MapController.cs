using text_survival.Environments;
using text_survival.IO;

namespace text_survival.UI;

/// <summary>
/// Handles input and navigation for the map UI
/// </summary>
public static class MapController
{
    /// <summary>
    /// Gets directional input from user (N/E/S/W/Q)
    /// </summary>
    /// <returns>Direction string or "Q" for quit</returns>
    public static string GetDirectionalInput()
    {
        while (true)
        {
            string? input = Input.Read()?.Trim().ToUpper();

            if (string.IsNullOrEmpty(input))
            {
                Output.WriteLine("Please enter a direction (N/E/S/W) or Q to cancel.");
                continue;
            }

            // Accept single letter or full word
            if (input == "N" || input == "NORTH") return "N";
            if (input == "E" || input == "EAST") return "E";
            if (input == "S" || input == "SOUTH") return "S";
            if (input == "W" || input == "WEST") return "W";
            if (input == "Q" || input == "QUIT" || input == "CANCEL") return "Q";

            Output.WriteLine("Invalid input. Enter N, E, S, W, or Q.");
        }
    }

    /// <summary>
    /// Finds the location in the specified direction from the current location
    /// </summary>
    public static Location? GetLocationInDirection(Zone zone, Location current, string direction)
    {
        var otherLocations = zone.Locations.Where(l => l != current).ToList();
        if (!otherLocations.Any()) return null;

        Location? best = null;
        double bestScore = double.MinValue;

        foreach (var loc in otherLocations)
        {
            int dx = loc.CoordinateX - current.CoordinateX;
            int dy = loc.CoordinateY - current.CoordinateY;

            double score = direction switch
            {
                "N" => dy,  // North = positive Y
                "S" => -dy, // South = negative Y
                "E" => dx,  // East = positive X
                "W" => -dx, // West = negative X
                _ => 0
            };

            // Only consider locations actually in that general direction
            bool isInDirection = direction switch
            {
                "N" => dy > 0,
                "S" => dy < 0,
                "E" => dx > 0,
                "W" => dx < 0,
                _ => false
            };

            if (isInDirection && score > bestScore)
            {
                bestScore = score;
                best = loc;
            }
        }

        return best;
    }

    /// <summary>
    /// Calculates travel time between two locations based on coordinate distance
    /// </summary>
    public static int CalculateLocalTravelTime(Location from, Location to)
    {
        double distance = Math.Sqrt(
            Math.Pow(to.CoordinateX - from.CoordinateX, 2) +
            Math.Pow(to.CoordinateY - from.CoordinateY, 2)
        );

        // 3-15 minute range based on distance
        // Assuming 50 units ≈ 1 minute of travel
        int time = Math.Max(3, Math.Min(15, (int)(distance / 50)));
        return time;
    }

    /// <summary>
    /// Calculates zone travel time (random 30-60 minutes as per current implementation)
    /// </summary>
    public static int CalculateZoneTravelTime()
    {
        return Utils.RandInt(30, 60);
    }
}
