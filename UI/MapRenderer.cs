using text_survival.Core;
using text_survival.Environments;
using text_survival.Environments.Features;

namespace text_survival.UI;

/// <summary>
/// Renders visual compass-style maps using a systematic character grid approach
/// </summary>
public static class MapRenderer
{
    private const int BOX_WIDTH = 54;
    private const int CELL_WIDTH = 10;   // Each location cell is 10 chars wide
    private const int CELL_HEIGHT = 5;   // Each location cell is 5 chars tall

    /// <summary>
    /// Renders a 5x5 grid world map with current zone in center
    /// </summary>
    public static string RenderUnifiedMap(WorldMap worldMap, Zone currentZone, Location currentLocation)
    {
        // Calculate total height: header + world grid + local header + local grid + footer
        int headerHeight = 4; // Top border + 2 header lines + divider
        int gridHeight = 17; // 5 rows × 3 lines (2 content + 1 border) + top/bottom borders
        int localHeaderHeight = 2; // Section divider + header
        int localGridHeight = CalculateLocalGridHeight(currentZone.Locations.Count);
        int footerHeight = 4; // Empty line + legend + prompt + bottom border

        int totalHeight = headerHeight + gridHeight + localHeaderHeight + localGridHeight + footerHeight;

        // Create the master grid
        var masterGrid = new CharacterGrid(BOX_WIDTH, totalHeight);

        int currentRow = 0;
        int contentWidth = BOX_WIDTH - 4;

        // === TOP BORDER ===
        RenderTopBorder(masterGrid, currentRow++);

        // === HEADER ===
        int daysElapsed = (World.GameTime - new DateTime(2025, 1, 1)).Days;

        // Row: "═══ WORLD MAP ═══"
        masterGrid.SetChar(0, currentRow, '│');
        masterGrid.SetChar(BOX_WIDTH - 1, currentRow, '│');
        string header1 = "═══ WORLD MAP ═══";
        int header1X = 2 + (contentWidth - header1.Length) / 2;
        masterGrid.SetText(header1X, currentRow, header1);
        currentRow++;

        // Row: "Known World • Day XX"
        masterGrid.SetChar(0, currentRow, '│');
        masterGrid.SetChar(BOX_WIDTH - 1, currentRow, '│');
        string header2 = $"Known World • Day {daysElapsed:D2}";
        int header2X = 2 + (contentWidth - header2.Length) / 2;
        masterGrid.SetText(header2X, currentRow, header2);
        currentRow++;

        // === DIVIDER ===
        RenderDivider(masterGrid, currentRow++);

        // === 5x5 WORLD GRID ===
        currentRow = Render5x5WorldGrid(masterGrid, currentRow, worldMap, currentZone);

        // === DIVIDER ===
        RenderDivider(masterGrid, currentRow++);

        // === SECTION HEADER ===
        masterGrid.SetChar(0, currentRow, '│');
        masterGrid.SetChar(BOX_WIDTH - 1, currentRow, '│');
        string sectionHeader = $"  Locations in {currentZone.Name}:";
        masterGrid.SetText(2, currentRow, sectionHeader);
        currentRow++;

        // === DIVIDER ===
        RenderDivider(masterGrid, currentRow++);

        // === LOCAL LOCATIONS GRID ===
        currentRow = RenderLocalLocationsGrid(masterGrid, currentRow, currentZone.Locations, currentLocation);

        // === FOOTER ===
        // Empty row
        masterGrid.SetChar(0, currentRow, '│');
        masterGrid.SetChar(BOX_WIDTH - 1, currentRow, '│');
        currentRow++;

        // Legend row
        masterGrid.SetChar(0, currentRow, '│');
        masterGrid.SetChar(BOX_WIDTH - 1, currentRow, '│');
        string legend = "* = Current location";
        masterGrid.SetText(2, currentRow, legend);
        currentRow++;

        // Prompt row
        masterGrid.SetChar(0, currentRow, '│');
        masterGrid.SetChar(BOX_WIDTH - 1, currentRow, '│');
        string prompt = "Enter direction (N/E/S/W) or Q to cancel:";
        masterGrid.SetText(2, currentRow, prompt);
        currentRow++;

        // === BOTTOM BORDER ===
        RenderBottomBorder(masterGrid, currentRow);

        return masterGrid.GetRenderedString();
    }

    /// <summary>
    /// Renders the top border into the grid
    /// </summary>
    private static void RenderTopBorder(CharacterGrid grid, int row)
    {
        grid.SetChar(0, row, '┌');
        for (int x = 1; x < BOX_WIDTH - 1; x++)
        {
            grid.SetChar(x, row, '─');
        }
        grid.SetChar(BOX_WIDTH - 1, row, '┐');
    }

    /// <summary>
    /// Renders a divider line into the grid
    /// </summary>
    private static void RenderDivider(CharacterGrid grid, int row)
    {
        grid.SetChar(0, row, '├');
        for (int x = 1; x < BOX_WIDTH - 1; x++)
        {
            grid.SetChar(x, row, '─');
        }
        grid.SetChar(BOX_WIDTH - 1, row, '┤');
    }

    /// <summary>
    /// Renders the bottom border into the grid
    /// </summary>
    private static void RenderBottomBorder(CharacterGrid grid, int row)
    {
        grid.SetChar(0, row, '└');
        for (int x = 1; x < BOX_WIDTH - 1; x++)
        {
            grid.SetChar(x, row, '─');
        }
        grid.SetChar(BOX_WIDTH - 1, row, '┘');
    }

    /// <summary>
    /// Renders a 5x5 grid of world zones with current zone in center
    /// </summary>
    private static int Render5x5WorldGrid(CharacterGrid grid, int startRow, WorldMap worldMap, Zone currentZone)
    {
        const int GRID_SIZE = 5;
        const int CELL_W = 8; // Each cell is 8 characters wide
        const int LEFT_MARGIN = 3; // Space from left border

        int row = startRow;

        // Get current zone coordinates
        var currentCoords = worldMap.GetZoneCoordinates(currentZone) ?? worldMap.GetCurrentCoordinates();
        int centerX = currentCoords.x;
        int centerY = currentCoords.y;

        // Draw outer borders
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');

        // Draw top border of table: ┌────────┬────────┬────────┬────────┬────────┐
        int x = LEFT_MARGIN;
        grid.SetChar(x++, row, '┌');
        for (int col = 0; col < GRID_SIZE; col++)
        {
            for (int i = 0; i < CELL_W; i++)
                grid.SetChar(x++, row, '─');
            if (col < GRID_SIZE - 1)
                grid.SetChar(x++, row, '┬');
        }
        grid.SetChar(x, row, '┐');
        row++;

        // Draw 5 rows
        for (int gridRow = 0; gridRow < GRID_SIZE; gridRow++)
        {
            // Calculate world Y coordinate (Y increases northward, so flip grid row)
            int worldY = centerY + (2 - gridRow); // gridRow 0 = centerY+2 (north), gridRow 4 = centerY-2 (south)

            // === LINE 1: Zone names ===
            grid.SetChar(0, row, '│');
            grid.SetChar(BOX_WIDTH - 1, row, '│');
            x = LEFT_MARGIN;
            grid.SetChar(x++, row, '│');

            for (int gridCol = 0; gridCol < GRID_SIZE; gridCol++)
            {
                int worldX = centerX + (gridCol - 2); // gridCol 0 = centerX-2 (west), gridCol 4 = centerX+2 (east)
                var zone = worldMap.GetZone(worldX, worldY);

                string cellText = GetZoneDisplayName(zone);
                int padding = (CELL_W - cellText.Length) / 2;
                for (int i = 0; i < padding; i++)
                    grid.SetChar(x++, row, ' ');
                for (int i = 0; i < cellText.Length && i < CELL_W; i++)
                    grid.SetChar(x++, row, cellText[i]);
                for (int i = padding + cellText.Length; i < CELL_W; i++)
                    grid.SetChar(x++, row, ' ');

                grid.SetChar(x++, row, '│');
            }
            row++;

            // === LINE 2: Current marker ===
            grid.SetChar(0, row, '│');
            grid.SetChar(BOX_WIDTH - 1, row, '│');
            x = LEFT_MARGIN;
            grid.SetChar(x++, row, '│');

            for (int gridCol = 0; gridCol < GRID_SIZE; gridCol++)
            {
                int worldX = centerX + (gridCol - 2);
                var zone = worldMap.GetZone(worldX, worldY);

                // Show * if this is the current zone
                string marker = (zone == currentZone) ? "*" : "";
                int padding = (CELL_W - marker.Length) / 2;
                for (int i = 0; i < padding; i++)
                    grid.SetChar(x++, row, ' ');
                for (int i = 0; i < marker.Length; i++)
                    grid.SetChar(x++, row, marker[i]);
                for (int i = padding + marker.Length; i < CELL_W; i++)
                    grid.SetChar(x++, row, ' ');

                grid.SetChar(x++, row, '│');
            }
            row++;

            // === HORIZONTAL DIVIDER (or bottom border if last row) ===
            grid.SetChar(0, row, '│');
            grid.SetChar(BOX_WIDTH - 1, row, '│');
            x = LEFT_MARGIN;

            if (gridRow < GRID_SIZE - 1)
            {
                // Middle divider: ├────────┼────────┼────────┼────────┼────────┤
                grid.SetChar(x++, row, '├');
                for (int col = 0; col < GRID_SIZE; col++)
                {
                    for (int i = 0; i < CELL_W; i++)
                        grid.SetChar(x++, row, '─');
                    if (col < GRID_SIZE - 1)
                        grid.SetChar(x++, row, '┼');
                }
                grid.SetChar(x, row, '┤');
            }
            else
            {
                // Bottom border: └────────┴────────┴────────┴────────┴────────┘
                grid.SetChar(x++, row, '└');
                for (int col = 0; col < GRID_SIZE; col++)
                {
                    for (int i = 0; i < CELL_W; i++)
                        grid.SetChar(x++, row, '─');
                    if (col < GRID_SIZE - 1)
                        grid.SetChar(x++, row, '┴');
                }
                grid.SetChar(x, row, '┘');
            }
            row++;
        }

        return row;
    }

    /// <summary>
    /// Gets display name for a zone (symbol + type name or "???")
    /// </summary>
    private static string GetZoneDisplayName(Zone? zone)
    {
        if (zone == null || !zone.Visited)
            return "???";

        string symbol = zone.GetSymbol();
        string typeName = zone.Type.ToString(); // "Forest", "Tundra", "CaveSystem", etc.
        string truncated = TruncateString(typeName, 5);
        return $"{symbol}{truncated}";
    }

    /// <summary>
    /// DEPRECATED: Old compass rendering - keeping for reference
    /// </summary>
    private static int RenderWorldCompass(CharacterGrid grid, int startRow, WorldMap worldMap, Zone currentZone)
    {
        var currentCoords = worldMap.GetZoneCoordinates(currentZone) ?? worldMap.GetCurrentCoordinates();
        var northZone = worldMap.GetZone(currentCoords.x, currentCoords.y + 1);
        var eastZone = worldMap.GetZone(currentCoords.x + 1, currentCoords.y);
        var westZone = worldMap.GetZone(currentCoords.x - 1, currentCoords.y);
        var southZone = worldMap.GetZone(currentCoords.x, currentCoords.y - 1);

        int row = startRow;
        int contentWidth = BOX_WIDTH - 4; // 50 chars available

        // Row 0: "N" centered
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');
        grid.SetChar(2 + contentWidth / 2, row, 'N');
        row++;

        // Row 1: North zone name centered
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');
        string northName = (northZone != null && northZone.Visited)
            ? $"{northZone.GetSymbol()} {TruncateString(northZone.Name, 20)}"
            : "Unknown";
        int northPadding = (contentWidth - northName.Length) / 2;
        grid.SetText(2 + northPadding, row, northName);
        row++;

        // Row 2: Spacer
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');
        row++;

        // Row 3: Compass line "W [west] ──●── [east] E"
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');

        // W label at position 2
        grid.SetChar(2, row, 'W');

        // West zone name starting at position 5
        string westName = (westZone != null && westZone.Visited)
            ? $"{westZone.GetSymbol()} {TruncateString(westZone.Name, 8)}"
            : "???";
        grid.SetText(5, row, westName);

        // Calculate center position
        int centerX = 2 + contentWidth / 2;

        // Draw line from west name to center (─ characters)
        int lineStartX = 5 + westName.Length + 1;
        for (int x = lineStartX; x < centerX; x++)
        {
            grid.SetChar(x, row, '─');
        }

        // Center marker
        grid.SetChar(centerX, row, '●');

        // East zone name - calculate position working backwards from right
        string eastName = (eastZone != null && eastZone.Visited)
            ? $"{eastZone.GetSymbol()} {TruncateString(eastZone.Name, 8)}"
            : "???";
        int eastNameX = BOX_WIDTH - 4 - eastName.Length;

        // Draw line from center to east name (─ characters)
        for (int x = centerX + 1; x < eastNameX - 1; x++)
        {
            grid.SetChar(x, row, '─');
        }

        // East zone name
        grid.SetText(eastNameX, row, eastName);

        // E label at position BOX_WIDTH - 3
        grid.SetChar(BOX_WIDTH - 3, row, 'E');
        row++;

        // Row 4: Spacer
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');
        row++;

        // Row 5: South zone name centered
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');
        string southName = (southZone != null && southZone.Visited)
            ? $"{southZone.GetSymbol()} {TruncateString(southZone.Name, 20)}"
            : "Unknown";
        int southPadding = (contentWidth - southName.Length) / 2;
        grid.SetText(2 + southPadding, row, southName);
        row++;

        // Row 6: "S" centered
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');
        grid.SetChar(2 + contentWidth / 2, row, 'S');
        row++;

        return row;
    }

    /// <summary>
    /// Renders the local map section, returns next row number
    /// </summary>
    private static int RenderLocalMap(CharacterGrid grid, int startRow, GridLayout layout, List<Location> locations, Location currentLocation)
    {
        // Create a temporary grid just for the local map
        var localGrid = new CharacterGrid(layout.GridWidth, layout.GridHeight);

        // STEP 1: Calculate connections between nearby locations
        var connections = CalculateConnections(locations);

        // STEP 2: Draw connection lines FIRST (so they appear behind cells)
        foreach (var (from, to) in connections)
        {
            var pos1 = layout.Positions[from];
            var pos2 = layout.Positions[to];

            // Calculate edge connection points instead of centers
            // Determine which edge of each box to connect from
            int centerX1 = pos1.x + CELL_WIDTH / 2;
            int centerY1 = pos1.y + CELL_HEIGHT / 2;
            int centerX2 = pos2.x + CELL_WIDTH / 2;
            int centerY2 = pos2.y + CELL_HEIGHT / 2;

            // Determine which edges to connect
            int startX, startY, endX, endY;

            // From box 1: choose edge based on direction to box 2
            if (centerX2 > centerX1)
                startX = pos1.x + CELL_WIDTH; // Right edge
            else if (centerX2 < centerX1)
                startX = pos1.x - 1; // Left edge
            else
                startX = centerX1; // Same X, use center

            if (centerY2 > centerY1)
                startY = pos1.y + CELL_HEIGHT; // Bottom edge
            else if (centerY2 < centerY1)
                startY = pos1.y - 1; // Top edge
            else
                startY = centerY1; // Same Y, use center

            // To box 2: choose edge based on direction from box 1
            if (centerX1 > centerX2)
                endX = pos2.x + CELL_WIDTH; // Right edge
            else if (centerX1 < centerX2)
                endX = pos2.x - 1; // Left edge
            else
                endX = centerX2; // Same X, use center

            if (centerY1 > centerY2)
                endY = pos2.y + CELL_HEIGHT; // Bottom edge
            else if (centerY1 < centerY2)
                endY = pos2.y - 1; // Top edge
            else
                endY = centerY2; // Same Y, use center

            DrawConnectionLine(localGrid, startX, startY, endX, endY);
        }

        // STEP 3: Render each location cell into the local grid (will overwrite line endpoints)
        foreach (var location in locations)
        {
            var pos = layout.Positions[location];
            RenderLocationCell(localGrid, pos.x, pos.y, location, location == currentLocation);
        }

        // STEP 4: Copy the local grid into the master grid, centered and with borders
        int row = startRow;
        for (int localY = 0; localY < layout.GridHeight; localY++)
        {
            grid.SetChar(0, row, '│');
            grid.SetChar(BOX_WIDTH - 1, row, '│');

            // Center the local map content
            int contentWidth = BOX_WIDTH - 4;
            int leftPadding = (contentWidth - layout.GridWidth) / 2;
            int startX = 2 + leftPadding;

            // Copy this row from local grid to master grid
            for (int localX = 0; localX < layout.GridWidth; localX++)
            {
                char c = localGrid._grid[localY, localX];
                grid.SetChar(startX + localX, row, c);
            }

            row++;
        }

        return row;
    }

    /// <summary>
    /// Calculates grid positions for all locations
    /// </summary>
    private static GridLayout CalculateGridLayout(List<Location> locations)
    {
        if (locations.Count == 0)
        {
            return new GridLayout
            {
                Positions = new Dictionary<Location, (int x, int y)>(),
                GridWidth = 0,
                GridHeight = 0
            };
        }

        // Find min/max coordinates
        int minX = locations.Min(l => l.CoordinateX);
        int maxX = locations.Max(l => l.CoordinateX);
        int minY = locations.Min(l => l.CoordinateY);
        int maxY = locations.Max(l => l.CoordinateY);

        // Map location coordinates to grid positions
        var positions = new Dictionary<Location, (int x, int y)>();
        int gridMaxX = 0;
        int gridMaxY = 0;

        foreach (var loc in locations)
        {
            // Normalize to 0-based grid coordinates
            int normalizedX = loc.CoordinateX - minX;
            int normalizedY = loc.CoordinateY - minY;

            // Convert to grid position (simple: each location gets a column)
            // Arrange locations left to right based on X coord, top to bottom based on Y coord
            int rangeX = maxX - minX;
            int rangeY = maxY - minY;

            if (rangeX == 0) rangeX = 1;
            if (rangeY == 0) rangeY = 1;

            // Simple grid layout: divide space into cells with spacing
            // Add extra spacing between cells for cleaner line routing
            int gridX = (normalizedX * (CELL_WIDTH + 8)) / rangeX;
            int gridY = (normalizedY * (CELL_HEIGHT + 4)) / rangeY;

            positions[loc] = (gridX, gridY);

            if (gridX + CELL_WIDTH > gridMaxX) gridMaxX = gridX + CELL_WIDTH;
            if (gridY + CELL_HEIGHT > gridMaxY) gridMaxY = gridY + CELL_HEIGHT;
        }

        return new GridLayout
        {
            Positions = positions,
            GridWidth = gridMaxX,
            GridHeight = gridMaxY
        };
    }

    /// <summary>
    /// Renders a single location cell (10w × 5h) into the grid
    /// </summary>
    private static void RenderLocationCell(CharacterGrid grid, int x, int y, Location location, bool isCurrent)
    {
        // Draw the box
        grid.DrawBox(x, y, CELL_WIDTH, CELL_HEIGHT);

        // Prepare text lines
        var lines = PrepareLocationLines(location, isCurrent);

        // Write lines inside the box
        for (int i = 0; i < lines.Length && i < 3; i++)
        {
            grid.SetText(x + 1, y + 1 + i, lines[i]);
        }
    }

    /// <summary>
    /// Prepares up to 3 lines of text for a location cell (8 chars max each)
    /// </summary>
    private static string[] PrepareLocationLines(Location location, bool isCurrent)
    {
        var lines = new string[3];

        // Line 1: Name + current marker
        string name = GetLocationShortName(location.Name);
        if (isCurrent)
        {
            name = name.Length > 5 ? name.Substring(0, 5) : name;
            lines[0] = $"{name} *";
        }
        else
        {
            name = name.Length > 8 ? name.Substring(0, 8) : name;
            lines[0] = name;
        }
        lines[0] = lines[0].PadRight(8);

        // Line 2: Status
        string line2 = "";
        var fire = location.GetActiveFireStatus();
        var shelter = location.GetShelterStatus();
        var threat = location.GetNearbyThreats();

        if (fire != null)
        {
            if (fire.Contains("Burning"))
                line2 = "[FIRE]";
            else if (fire.Contains("Dying"))
                line2 = "[Dying]";
            else if (fire.Contains("Embers"))
                line2 = "[Ember]";
        }
        else if (shelter != null)
        {
            line2 = "[Sheltr]";
        }
        else if (threat != null)
        {
            line2 = "!DANGER!";
        }
        lines[1] = line2.PadRight(8);

        // Line 3: Items/wildlife
        string line3 = "";
        if (location.Items.Count > 0)
        {
            line3 = $"{location.Items.Count} items";
        }
        lines[2] = TruncateString(line3, 8).PadRight(8);

        return lines;
    }

    /// <summary>
    /// Extracts a short name from a location name (last word)
    /// </summary>
    private static string GetLocationShortName(string fullName)
    {
        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string lastWord = words[^1]; // Get last word
        return TruncateString(lastWord, 8); // Ensure fits in cell
    }

    /// <summary>
    /// Truncates a string to the specified length
    /// </summary>
    private static string TruncateString(string str, int maxLength)
    {
        if (str.Length <= maxLength) return str;
        return str.Substring(0, maxLength - 2) + "..";
    }

    /// <summary>
    /// Calculates which locations should be connected based on proximity
    /// </summary>
    private static List<(Location from, Location to)> CalculateConnections(List<Location> locations)
    {
        const double CONNECTION_DISTANCE_THRESHOLD = 400; // World units

        var connections = new List<(Location, Location)>();
        var usedPairs = new HashSet<(Location, Location)>();

        foreach (var loc in locations)
        {
            // Find all locations within distance threshold
            var neighbors = locations
                .Where(other => other != loc)
                .Select(other => new {
                    Location = other,
                    Distance = Math.Sqrt(
                        Math.Pow(loc.CoordinateX - other.CoordinateX, 2) +
                        Math.Pow(loc.CoordinateY - other.CoordinateY, 2)
                    )
                })
                .Where(d => d.Distance <= CONNECTION_DISTANCE_THRESHOLD)
                .OrderBy(d => d.Distance);

            foreach (var neighbor in neighbors)
            {
                // Ensure bidirectional uniqueness (A->B same as B->A)
                var pair1 = (loc, neighbor.Location);
                var pair2 = (neighbor.Location, loc);

                if (!usedPairs.Contains(pair1) && !usedPairs.Contains(pair2))
                {
                    connections.Add(pair1);
                    usedPairs.Add(pair1);
                    usedPairs.Add(pair2);
                }
            }
        }

        return connections;
    }

    /// <summary>
    /// Draws a connection line between two points using orthogonal (horizontal/vertical) segments
    /// </summary>
    private static void DrawConnectionLine(CharacterGrid grid, int x1, int y1, int x2, int y2)
    {
        // Use L-shaped path: horizontal first, then vertical
        // This creates clean, continuous lines instead of scattered characters

        // Determine the elbow/corner position
        // We'll go horizontal from (x1,y1) to (x2,y1), then vertical to (x2,y2)

        // Draw horizontal segment
        DrawHorizontalLine(grid, x1, y1, x2);

        // Draw vertical segment
        DrawVerticalLine(grid, x2, y1, y2);

        // Place corner character at the junction point (x2, y1)
        // But only if it's not the start or end point
        if (y1 != y2 && x1 != x2)
        {
            char corner = GetCornerChar(x1, y1, x2, y2);
            if (!IsCellBorder(grid, x2, y1))
            {
                grid.SetChar(x2, y1, corner);
            }
        }
    }

    /// <summary>
    /// Draws a horizontal line segment from (x1, y) to (x2, y)
    /// </summary>
    private static void DrawHorizontalLine(CharacterGrid grid, int x1, int y, int x2)
    {
        int startX = Math.Min(x1, x2);
        int endX = Math.Max(x1, x2);

        for (int x = startX; x <= endX; x++)
        {
            if (!IsCellBorder(grid, x, y))
            {
                grid.SetChar(x, y, '─');
            }
        }
    }

    /// <summary>
    /// Draws a vertical line segment from (x, y1) to (x, y2)
    /// </summary>
    private static void DrawVerticalLine(CharacterGrid grid, int x, int y1, int y2)
    {
        int startY = Math.Min(y1, y2);
        int endY = Math.Max(y1, y2);

        for (int y = startY; y <= endY; y++)
        {
            if (!IsCellBorder(grid, x, y))
            {
                grid.SetChar(x, y, '│');
            }
        }
    }

    /// <summary>
    /// Determines the appropriate corner character for an L-shaped path
    /// </summary>
    private static char GetCornerChar(int x1, int y1, int x2, int y2)
    {
        // Determine which direction we're going
        bool goingRight = x2 > x1;
        bool goingDown = y2 > y1;

        // Corner character depends on the turn direction
        if (goingRight && goingDown)
            return '┐'; // Going right then down
        else if (goingRight && !goingDown)
            return '┘'; // Going right then up
        else if (!goingRight && goingDown)
            return '┌'; // Going left then down
        else
            return '└'; // Going left then up
    }

    /// <summary>
    /// Calculates the height needed for the local locations grid
    /// </summary>
    private static int CalculateLocalGridHeight(int locationCount)
    {
        if (locationCount == 0) return 2; // Just top and bottom borders

        int rows = (locationCount + 3) / 4; // Ceiling division for 4 columns
        return 1 + (rows * 5); // Top border + (rows × 5 lines each: 4 content + 1 border)
    }

    /// <summary>
    /// Renders a 4-column grid of locations within the current zone
    /// </summary>
    private static int RenderLocalLocationsGrid(CharacterGrid grid, int startRow, List<Location> locations, Location currentLocation)
    {
        const int COLS = 4;
        const int CELL_W = 9;
        const int CELL_H = 4;
        const int LEFT_MARGIN = 6; // Center in 54-char box

        int rows = (locations.Count + COLS - 1) / COLS; // Ceiling division
        int row = startRow;

        // For each row of locations
        for (int r = 0; r < rows; r++)
        {
            // Draw top border or divider
            DrawHorizontalLocationBorder(grid, row++, r == 0, r == rows - 1, COLS, CELL_W, LEFT_MARGIN);

            // Draw 4 content lines
            for (int line = 0; line < CELL_H; line++)
            {
                DrawLocationContentRow(grid, row++, locations, currentLocation, r, COLS, line, CELL_W, LEFT_MARGIN);
            }
        }

        // Draw bottom border
        DrawHorizontalLocationBorder(grid, row++, false, true, COLS, CELL_W, LEFT_MARGIN);

        return row;
    }

    /// <summary>
    /// Draws horizontal border for location grid (top, middle, or bottom)
    /// </summary>
    private static void DrawHorizontalLocationBorder(CharacterGrid grid, int row, bool isTop, bool isBottom, int cols, int cellWidth, int leftMargin)
    {
        // Draw outer box borders
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');

        int x = leftMargin;

        // Left corner
        if (isTop)
            grid.SetChar(x++, row, '┌');
        else if (isBottom)
            grid.SetChar(x++, row, '└');
        else
            grid.SetChar(x++, row, '├');

        // Draw cells with dividers
        for (int col = 0; col < cols; col++)
        {
            // Horizontal line
            for (int i = 0; i < cellWidth; i++)
                grid.SetChar(x++, row, '─');

            // Divider or right corner
            if (col < cols - 1)
            {
                // Middle divider
                if (isTop)
                    grid.SetChar(x++, row, '┬');
                else if (isBottom)
                    grid.SetChar(x++, row, '┴');
                else
                    grid.SetChar(x++, row, '┼');
            }
            else
            {
                // Right corner
                if (isTop)
                    grid.SetChar(x++, row, '┐');
                else if (isBottom)
                    grid.SetChar(x++, row, '┘');
                else
                    grid.SetChar(x++, row, '┤');
            }
        }
    }

    /// <summary>
    /// Draws one content line across all cells in a location grid row
    /// </summary>
    private static void DrawLocationContentRow(CharacterGrid grid, int row, List<Location> locations, Location currentLocation, int gridRow, int cols, int contentLine, int cellWidth, int leftMargin)
    {
        // Draw outer box borders
        grid.SetChar(0, row, '│');
        grid.SetChar(BOX_WIDTH - 1, row, '│');

        int x = leftMargin;
        grid.SetChar(x++, row, '│'); // Left border of table

        for (int col = 0; col < cols; col++)
        {
            int locationIndex = gridRow * cols + col;
            string cellText = "";

            if (locationIndex < locations.Count)
            {
                var location = locations[locationIndex];

                if (contentLine == 0)
                {
                    // Line 1: Location name
                    cellText = GetLocationShortName(location.Name);
                }
                else if (contentLine == 1)
                {
                    // Line 2: Current marker
                    if (location == currentLocation)
                        cellText = "*";
                }
                else
                {
                    // Lines 3-4: Features
                    var features = GetLocationFeatures(location);
                    int featureIndex = contentLine - 2; // 0 or 1
                    if (featureIndex < features.Count)
                    {
                        cellText = FormatFeature(features[featureIndex]);
                    }
                }
            }

            // Center or left-align text in cell
            bool centerText = contentLine <= 1; // Center name and marker
            int padding = centerText ? (cellWidth - cellText.Length) / 2 : 1;

            for (int i = 0; i < padding; i++)
                grid.SetChar(x++, row, ' ');
            for (int i = 0; i < cellText.Length && i < cellWidth; i++)
                grid.SetChar(x++, row, cellText[i]);
            for (int i = padding + cellText.Length; i < cellWidth; i++)
                grid.SetChar(x++, row, ' ');

            grid.SetChar(x++, row, '│'); // Cell right border
        }
    }

    /// <summary>
    /// Checks if a grid position contains a cell border character
    /// </summary>
    private static bool IsCellBorder(CharacterGrid grid, int x, int y)
    {
        if (x < 0 || x >= grid.Width || y < 0 || y >= grid.Height)
            return true;

        char c = grid._grid[y, x];

        // Check if character is a box border
        return c == '┌' || c == '┐' || c == '└' || c == '┘' ||
               c == '─' || c == '│' || c == '├' || c == '┤' ||
               c == '┬' || c == '┴' || c == '┼';
    }

    /// <summary>
    /// Gets prioritized list of features to display for a location (max 2)
    /// Priority: Fire > Danger > Shelter > Water > Forage > Items
    /// </summary>
    private static List<string> GetLocationFeatures(Location location)
    {
        var features = new List<string>();

        // 1. Fire status
        var fireStatus = location.GetActiveFireStatus();
        if (fireStatus != null)
        {
            if (fireStatus.Contains("Burning"))
                features.Add("fire_burning");
            else if (fireStatus.Contains("Dying"))
                features.Add("fire_dying");
            else if (fireStatus.Contains("Embers"))
                features.Add("fire_embers");
        }

        // 2. Danger (hostile NPCs)
        var threat = location.GetNearbyThreats();
        if (threat != null)
        {
            features.Add("danger");
        }

        // 3. Shelter
        var shelter = location.GetShelterStatus();
        if (shelter != null)
        {
            features.Add("shelter");
        }

        // 4. Water source (not implemented yet - placeholder for future)
        // TODO: Add water source feature detection when WaterSourceFeature is implemented

        // 5. Forage
        var forageFeature = location.GetFeature<ForageFeature>();
        if (forageFeature != null)
        {
            features.Add("forage");
        }

        // 6. Items on ground
        if (location.Items.Count > 0)
        {
            features.Add($"items_{location.Items.Count}");
        }

        // Return top 2
        return features.Take(2).ToList();
    }

    /// <summary>
    /// Formats a feature code as icon + text for display
    /// </summary>
    private static string FormatFeature(string featureCode)
    {
        if (featureCode.StartsWith("fire_"))
        {
            return featureCode switch
            {
                "fire_burning" => "🔥 Fire",
                "fire_dying" => "🔥Dying",
                "fire_embers" => "🔥Embers",
                _ => "🔥 Fire"
            };
        }
        else if (featureCode == "danger")
        {
            return "⚠Danger";
        }
        else if (featureCode == "shelter")
        {
            return "🏠Shelter";
        }
        else if (featureCode == "water")
        {
            return "💧 Water";
        }
        else if (featureCode == "forage")
        {
            return "🌿Forage";
        }
        else if (featureCode.StartsWith("items_"))
        {
            string count = featureCode.Substring(6);
            return $"📦 {count}";
        }

        return "";
    }

    private class GridLayout
    {
        public Dictionary<Location, (int x, int y)> Positions { get; set; } = new();
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }
    }
}
