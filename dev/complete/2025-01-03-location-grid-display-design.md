# Location Grid Display Design

**Date:** 2025-01-03
**Status:** Approved, Ready for Implementation

## Overview

Replace the complex graph-based location display with a simple, scannable 4-column grid showing all locations within the current zone. Focus on visual appeal and quick information scanning without requiring coordinate positioning.

## Design Goals

1. **Visual Appeal** - Make the game feel more immersive and polished
2. **Quick Scanning** - See location status (fire, shelter, resources, danger) at a glance
3. **Consistent Style** - Match the 5x5 world grid table format

## Layout Structure

### Complete Unified Map

```
┌────────────────────────────────────────────────────┐
│                 ═══ WORLD MAP ═══                  │
│                Known World • Day 00                │
├────────────────────────────────────────────────────┤
│   ┌────────┬────────┬────────┬────────┬────────┐  │
│   │  ???   │  ???   │  ???   │  ???   │  ???   │  │
│   │        │        │        │        │        │  │
│   ├────────┼────────┼────────┼────────┼────────┤  │
│   │  ???   │  ???   │  ???   │  ???   │  ???   │  │
│   │        │        │        │        │        │  │
│   ├────────┼────────┼────────┼────────┼────────┤  │
│   │  ???   │  ???   │🌲Forest│  ???   │  ???   │  │
│   │        │        │   *    │        │        │  │
│   ├────────┼────────┼────────┼────────┼────────┤  │
│   │  ???   │  ???   │  ???   │  ???   │  ???   │  │
│   │        │        │        │        │        │  │
│   ├────────┼────────┼────────┼────────┼────────┤  │
│   │  ???   │  ???   │  ???   │  ???   │  ???   │  │
│   │        │        │        │        │        │  │
│   └────────┴────────┴────────┴────────┴────────┘  │
├────────────────────────────────────────────────────┤
│  Locations in Ancient Pine Forest:                │
├────────────────────────────────────────────────────┤
│     ┌─────────┬─────────┬─────────┬─────────┐     │
│     │Clearing │ Grove   │Woodland │ Thicket │     │
│     │   *     │         │         │         │     │
│     │🔥 Fire  │💧 Water │🌿Forage │⚠Danger │     │
│     │📦 5     │         │         │         │     │
│     ├─────────┼─────────┼─────────┼─────────┤     │
│     │ Stand   │         │         │         │     │
│     │         │         │         │         │     │
│     │🏠Shelter│         │         │         │     │
│     │         │         │         │         │     │
│     └─────────┴─────────┴─────────┴─────────┘     │
│                                                    │
│  * = Current location                              │
│  Enter direction (N/E/S/W) or Q to cancel:         │
└────────────────────────────────────────────────────┘
```

## Grid Specifications

### Dimensions
- **Columns:** 4 maximum per row
- **Rows:** Dynamic - wrap after 4 locations
- **Cell Width:** 9 characters (content area)
- **Cell Height:** 4 lines (content area)
- **Total Grid Width:** ~42 characters (fits within 54-char box)

### Grid Behavior
- Left-to-right, top-to-bottom fill
- Wrap to new row after 4 locations
- Partial rows: Leave trailing cells empty but maintain borders
- Entire grid centered within outer box borders

## Cell Content

### Line-by-Line Layout

**Line 1:** Location Name
- Extract last word from full name (e.g., "Shadowy Forest" → "Forest")
- Truncate to 8 characters if needed
- Centered in 9-character cell

**Line 2:** Current Location Marker
- Display `*` if player's current location
- Centered, otherwise blank

**Line 3-4:** Feature/Status Display
- Show top 2 features from priority list
- Left-aligned with icon + text format

### Feature Priority (Top 2 Shown)

1. **🔥 Fire** - Active fire or embers (e.g., `🔥 Fire`, `🔥Embers`)
2. **⚠ Danger** - Hostile NPCs present (e.g., `⚠Danger`, `⚠ Wolf`)
3. **🏠 Shelter** - Shelter feature exists
4. **💧 Water** - Water source feature exists
5. **🌿 Forage** - Forage feature exists (harvestable resources)
6. **📦 Items** - Items on ground (e.g., `📦 Items`, `📦 5`)

### Display Rules
- Show highest 2 priorities that exist for the location
- If only 1 feature, display on line 3, leave line 4 blank
- If no features, leave lines 3-4 blank
- Icon + space + short text (6-7 chars max)

### Examples

```
┌─────────┬─────────┬─────────┬─────────┐
│Clearing │ Grove   │Woodland │ Thicket │
│   *     │         │         │         │
│🔥 Fire  │💧 Water │🌿Forage │⚠Danger │
│📦 5     │         │         │         │
└─────────┴─────────┴─────────┴─────────┘
```

## Name Extraction Logic

### Zone Names (World Grid)
**Current Issue:** Shows "Shadowy" instead of "Forest" for "Shadowy Forest"

**Solution:** Extract zone type instead of first word
- Use `Zone.Type` enum (Forest, Tundra, CaveSystem, RiverValley, Plains)
- Display: Symbol + Type name (e.g., `🌲Forest`, `❄Tundra`)
- Truncate type name to 5 chars if needed

**Implementation:**
```csharp
private static string GetZoneDisplayName(Zone? zone)
{
    if (zone == null || !zone.Visited)
        return "???";

    string symbol = zone.GetSymbol();
    string typeName = zone.Type.ToString(); // "Forest", "Tundra", etc.
    string truncated = TruncateString(typeName, 5);
    return $"{symbol}{truncated}";
}
```

### Location Names (Local Grid)
**Rule:** Extract last word from location name

**Examples:**
- "Shadowy Forest" → "Forest"
- "Ancient Pine Grove" → "Grove"
- "Clearing" → "Clearing" (single word, unchanged)
- "Ice-coated Woodland" → "Woodland"

**Implementation:**
```csharp
private static string GetLocationShortName(string fullName)
{
    var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    string lastWord = words[^1]; // Get last word
    return TruncateString(lastWord, 8); // Ensure fits in cell
}
```

## Edge Cases

### Single Location
```
┌─────────┬─────────┬─────────┬─────────┐
│Clearing │         │         │         │
│   *     │         │         │         │
│🔥 Fire  │         │         │         │
│📦 3     │         │         │         │
└─────────┴─────────┴─────────┴─────────┘
```

### Many Locations (e.g., 9)
- Row 1: 4 locations
- Row 2: 4 locations
- Row 3: 1 location + 3 empty cells

### Location with No Features
```
│  Forest │
│         │
│         │  (blank lines)
│         │
```

### Long Names After Extraction
- "Woodland" (8 chars) → "Woodland" (exact fit)
- "Settlements" (11 chars) → "Settleme" (truncated to 8)

## Implementation Plan

### New Methods in `UI/MapRenderer.cs`

1. **`RenderLocalLocationsGrid()`**
   - Main grid rendering logic
   - Calculates rows needed
   - Calls helper methods for each row

2. **`DrawHorizontalBorder()`**
   - Draws `┌─┬─┐` (top) or `├─┼─┤` (middle) or `└─┴─┘` (bottom)
   - Parameters: row index, isTop flag, columns, cell width

3. **`DrawLocationRow()`**
   - Draws one content line across all cells in a row
   - Parameters: grid, row index, locations, current location, row number, line number

4. **`GetLocationFeatures()`**
   - Returns prioritized list of features for a location
   - Queries location for fire, shelter, water, forage, items, danger

5. **`FormatFeature()`**
   - Formats feature as icon + text string
   - Examples: `🔥 Fire`, `💧 Water`, `📦 5`

6. **`GetLocationShortName()`** (UPDATE EXISTING)
   - Change from "get first word" to "get last word"
   - Extract last word from space-separated name

7. **`GetZoneDisplayName()`** (UPDATE EXISTING)
   - Change from using zone name to using zone type
   - Format: symbol + Type.ToString()

### Integration Changes

**Update `RenderUnifiedMap()`:**
```csharp
// After world grid
RenderDivider(masterGrid, currentRow++);

// Section header
masterGrid.SetChar(0, currentRow, '│');
masterGrid.SetChar(BOX_WIDTH - 1, currentRow, '│');
string header = $"Locations in {currentZone.Name}:";
masterGrid.SetText(2, currentRow, header);
currentRow++;

RenderDivider(masterGrid, currentRow++);

// Local locations grid
currentRow = RenderLocalLocationsGrid(
    masterGrid,
    currentRow,
    currentZone.Locations,
    currentLocation
);
```

### Height Calculation Update

Update total height calculation in `RenderUnifiedMap()`:
```csharp
int headerHeight = 4; // Unchanged
int worldGridHeight = 17; // Unchanged (5x5 grid)
int localHeaderHeight = 2; // Section divider + header
int localGridHeight = CalculateLocalGridHeight(locations.Count);
int footerHeight = 4; // Legend + prompt + bottom border

int totalHeight = headerHeight + worldGridHeight + localHeaderHeight
                  + localGridHeight + footerHeight;
```

Where `CalculateLocalGridHeight()`:
```csharp
private static int CalculateLocalGridHeight(int locationCount)
{
    int rows = (locationCount + 3) / 4; // Ceiling division
    return 1 + (rows * 5); // Top border + (rows × 5 lines each)
    // Each row: 4 content lines + 1 border line
}
```

## Technical Notes

- Uses existing `CharacterGrid` class for systematic rendering
- Cell borders use box-drawing characters: `┌─┬┐├┼┤└┴┘│`
- Left margin: 6 characters (centers ~42-char grid in 54-char box)
- Feature detection uses existing `Location` methods:
  - `GetActiveFireStatus()` - Fire state
  - `GetShelterStatus()` - Shelter presence
  - `GetFeature<T>()` - Water, Forage features
  - `GetNearbyThreats()` - Hostile NPCs
  - `Items.Count` - Item count

## Testing Scenarios

1. Zone with 1 location (minimal)
2. Zone with 4 locations (single row, full)
3. Zone with 5 locations (two rows, partial second)
4. Zone with 8+ locations (multiple full rows)
5. Location with all feature types
6. Location with no features
7. Location names of varying lengths
8. Zone names with different types

---

**Next Steps:**
1. Implement helper methods
2. Update name extraction logic
3. Integrate into `RenderUnifiedMap()`
4. Test with various zone configurations
