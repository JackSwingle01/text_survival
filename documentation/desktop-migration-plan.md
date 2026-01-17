# Desktop Migration Plan: Raylib + ImGui.NET

## Overview

Migrate from WebSocket-based web UI to native desktop using Raylib (rendering/input) + ImGui.NET (overlay UI). The game logic stays unchanged; only the I/O layer is replaced.

**Current state:** ~11,500 lines of web infrastructure (WebIO, DTOs, frontend JS)
**Target state:** ~1,500-2,000 lines of desktop rendering code

**Key Decisions:**
- **Single-threaded** — Game logic and rendering on main thread (no background threads)
- **Clean break** — Delete all web infrastructure in Phase 1, not after migration
- **No dual mode** — Desktop only, git rollback if needed

---

## Architecture Comparison

### Current (Web)
```
Main Thread: ASP.NET Core HTTP/WebSocket handling
Game Thread: Task.Run(() => RunGame()) - blocking game logic
Receive Thread: WebSocket receive loop

GameRunner/Handlers
    ↓
Input.cs / GameDisplay.cs (thin abstractions)
    ↓
WebIO.cs (1,787 lines - blocking I/O over WebSocket)
    ↓
DTOs (serialize full game state)
    ↓
WebSocket
    ↓
JavaScript frontend (4,700 lines)
```

### Target (Desktop)
```
Single Thread: Raylib game loop (input → update → render)

GameRunner/Handlers
    ↓
Input.cs / GameDisplay.cs (same abstractions, new implementation)
    ↓
DesktopIO.cs (~300 lines - direct ImGui calls)
    ↓
Game loop renders directly from GameContext
    ↓
Raylib (window, input, world rendering)
ImGui.NET (overlay UI)
```

The single-threaded model works because:
- Game is turn-based (no real-time simulation while waiting)
- Raylib provides the game loop via `while (!WindowShouldClose())`
- "Blocking" I/O becomes ImGui waiting for button clicks within the render loop

---

## What Stays (Unchanged)

All game logic:
- `GameContext`, `GameRunner`, all Runners
- `Body`, `SurvivalProcessor`, `EffectRegistry`
- `Inventory`, `Crafting`, all Handlers
- `Events`, `Tensions`, `Combat`
- `Locations`, `Features`, `Grid/GameMap`
- `Herds`, `NPCs`
- `SaveManager` (JSON serialization stays)

---

## What Goes (Delete in Phase 1)

Delete immediately at start of migration (no dual-mode period):

- `Web/` directory entirely
  - `WebIO.cs` (1,787 lines)
  - `WebServer.cs`
  - `WebGameSession.cs`
  - `SessionRegistry.cs`
- `Web/Dto/` directory entirely (~5,300 lines)
- `wwwroot/` directory entirely (~4,700 lines JS)
- ASP.NET Core dependencies from `.csproj`

**Rationale:** Clean break avoids confusion. Git provides rollback if needed. No point maintaining two UIs.

---

## What's New (Create)

```
Desktop/
├── Program.cs              # Entry point, Raylib game loop
├── DesktopIO.cs            # Replaces WebIO, implements Input abstraction
├── GameLoop.cs             # Main loop: input → update → render
├── Rendering/
│   ├── WorldRenderer.cs    # Terrain, features, entities
│   ├── Camera.cs           # Viewport, pan, zoom
│   └── SpriteAtlas.cs      # Texture management
├── UI/
│   ├── ImGuiLayer.cs       # ImGui setup and frame management
│   ├── Overlays/
│   │   ├── InventoryOverlay.cs
│   │   ├── CraftingOverlay.cs
│   │   ├── EventOverlay.cs
│   │   ├── FireOverlay.cs
│   │   ├── EatingOverlay.cs
│   │   ├── CombatOverlay.cs
│   │   └── ... (one per WebIO.Run*UI method)
│   └── Panels/
│       ├── StatsPanel.cs       # Survival stats display
│       ├── LogPanel.cs         # Narrative log
│       └── LocationPanel.cs    # Current location info
└── Input/
    └── InputHandler.cs     # WASD, mouse, keyboard mapping
```

---

## Migration Phases

### Phase 1: Project Setup & Web Removal

**Goal:** Raylib window running, web infrastructure deleted, project compiles

#### 1a. Delete Web Infrastructure

Remove all web-related code immediately:

```bash
# Delete directories
rm -rf Web/
rm -rf wwwroot/

# These will cause compile errors - that's expected
```

Update `.csproj` to remove ASP.NET:
```xml
<!-- REMOVE these lines -->
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>

<!-- ADD these lines -->
<PackageReference Include="Raylib-cs" Version="6.0.0" />
<PackageReference Include="ImGui.NET" Version="1.90.1.1" />
<PackageReference Include="rlImGui-cs" Version="2.0.3" />
```

#### 1b. Stub Out Input.cs

The deletion will break `Input.cs` (calls WebIO). Create temporary stubs:

```csharp
// IO/Input.cs - temporary stubs
public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices, ...)
{
    throw new NotImplementedException("Desktop IO not yet implemented");
}
// ... stub all methods
```

This lets the project compile while we build the desktop layer.

#### 1c. Create Desktop Entry Point

Replace `Core/Program.cs`:

```csharp
using Raylib_cs;
using rlImGuiCs;
using ImGuiNET;
using text_survival.Persistence;

namespace text_survival;

public static class Program
{
    public static void Main()
    {
        Raylib.InitWindow(1280, 720, "Text Survival");
        Raylib.SetTargetFPS(60);
        rlImGui.Setup(true);

        // Load game (reuses existing save system)
        var ctx = GameInitializer.LoadOrCreateNew();

        while (!Raylib.WindowShouldClose())
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.DarkGray);

            // Test world rendering
            Raylib.DrawRectangle(100, 100, 50, 50, Color.Green);

            // Test ImGui
            rlImGui.Begin();
            ImGui.Begin("Game State");
            ImGui.Text($"Location: {ctx.CurrentLocation?.Name ?? "Unknown"}");
            ImGui.Text($"Energy: {ctx.player.Body.Energy:F0}");
            ImGui.Text($"Game Time: {ctx.GameTime}");
            if (ImGui.Button("Save & Quit"))
            {
                SaveManager.Save(ctx);
                break;
            }
            ImGui.End();
            rlImGui.End();

            Raylib.EndDrawing();
        }

        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }
}
```

#### 1d. Verify

- Project compiles (with stub exceptions in Input.cs)
- Window opens with test rectangle
- ImGui panel shows real game state from GameContext
- Save/load works

**Deliverable:** Desktop window displaying live game state, web code gone

---

### Phase 2: World Rendering (Days 2-4)

**Goal:** Port the canvas renderer from JavaScript to C#/Raylib

The current JS canvas renderer is ~2,200 lines across two files:
- `CanvasGridRenderer.js` (1,564 lines) - Grid, camera, tiles, icons, effects
- `TerrainRenderer.js` (719 lines) - Procedural terrain textures

#### 2a. Core Structure

Create `Desktop/Rendering/` with:

```
Rendering/
├── WorldRenderer.cs      # Main renderer, coordinates all drawing
├── Camera.cs             # Viewport tracking, world↔screen coords
├── TerrainRenderer.cs    # Procedural terrain textures
├── TileRenderer.cs       # Individual tile rendering
├── EdgeRenderer.cs       # Rivers, cliffs, trails between tiles
├── EffectsRenderer.cs    # Snow particles, vignette, night overlay
└── RenderUtils.cs        # Seeded random, color helpers, drawing primitives
```

#### 2b. API Mapping (JS Canvas → Raylib)

| JS Canvas | Raylib C# | Notes |
|-----------|-----------|-------|
| `fillRect(x,y,w,h)` | `DrawRectangle(x,y,w,h,color)` | Direct equivalent |
| `arc(x,y,r,start,end)` | `DrawCircleSector()` | For partial arcs |
| `beginPath()+moveTo()+lineTo()` | `DrawTriangle()` or `DrawLine()` | Build shapes |
| `quadraticCurveTo()` | `DrawLineBezierQuad()` | Bezier curves |
| `bezierCurveTo()` | `DrawLineBezierCubic()` | Cubic curves |
| `ellipse()` | `DrawEllipse()` | Direct equivalent |
| `createRadialGradient()` | Custom shader or layered circles | No direct equivalent |
| `fillText()` | `DrawText()` or `DrawTextEx()` | Font handling differs |
| `globalAlpha` | Color with alpha: `new Color(r,g,b,a)` | Per-call alpha |
| `strokeStyle + stroke()` | `DrawRectangleLines()` / `DrawCircleLines()` | Outline variants |
| `setTransform()/scale()` | `BeginMode2D(camera)` | Camera transforms |

#### 2c. Terrain Textures to Port

Each terrain type has procedural rendering (from `TerrainRenderer.js`):

| Terrain | Elements | Complexity |
|---------|----------|------------|
| **Forest** | 5-7 triangle trees, snow highlights | Medium |
| **Water/Ice** | Concentric pressure rings, cracks, shimmer patches | High |
| **Plain** | Lichen clusters, snow drifts, tussock grass, shrubs | High |
| **Clearing** | Dirt patches, grass tufts, stumps, fallen branches, edge trees | High |
| **Hills** | 3 stacked mounds with snow caps, rock patches, contours | Medium |
| **Rock** | 4 angular boulders with shadows, cracks, gravel | Medium |
| **Mountain** | Peak silhouettes, snow caps, ridge lines | Low |
| **Marsh** | Cattail clusters, dead reeds, ice cracks | Medium |

All use seeded random for consistent per-tile patterns:
```csharp
// Port directly - same algorithm
public static float SeededRandom(int worldX, int worldY, int seed)
{
    int h = (worldX * 73856093) ^ (worldY * 19349663) ^ (seed * 83492791);
    return MathF.Abs(MathF.Sin(h)) % 1.0f;
}
```

#### 2d. Grid Renderer Features to Port

From `CanvasGridRenderer.js`:

**Core rendering:**
- 7x7 tile viewport centered on player
- Tile size: 120px default, scales with window
- Terrain base color + texture overlay
- Fog of war (explored but not visible = dimmed)
- Tile highlights (player tile glow, hover highlight, adjacent indicators)

**Camera system:**
- `worldToView()` / `viewToWorld()` coordinate conversion
- Animated camera transitions (300ms ease-out-cubic)
- `startAnimatedPan()` for synchronized travel animation

**Feature icons:**
- Material icon font rendering with glow effects
- 4 icon positions per tile (corners)
- Special styling for fire (orange glow), water, traps

**Animal icons:**
- Emoji rendering at cardinal positions
- Shadow beneath each icon

**Player rendering:**
- Material icon with shadow
- Animated position during travel

**Edge rendering (between tiles):**
- Rivers: wavy icy-blue lines
- Cliffs: rocky texture with descent arrows
- Climbs: hazard stripes
- Trails: worn dirt paths (GameTrail, TrailMarker, CutTrail)

**Effects:**
- Snow particles (25 particles, drift + fall)
- Night overlay (time-based darkness)
- Vignette (radial darkening at edges)
- Time-of-day color adjustment (HSL lightness scaling)

#### 2e. Implementation Order

1. **RenderUtils.cs** - Seeded random, color helpers
2. **Camera.cs** - Viewport, coordinate conversion
3. **TerrainRenderer.cs** - All 8 terrain textures
4. **TileRenderer.cs** - Single tile with terrain, fog, highlights
5. **WorldRenderer.cs** - Grid loop, render all visible tiles
6. **EdgeRenderer.cs** - Rivers, cliffs, trails
7. **EffectsRenderer.cs** - Snow, vignette, night
8. **Icons** - Feature icons, animal icons, player

#### 2f. Wire Up to Game State

```csharp
var ctx = SaveManager.Load(sessionId) ?? GameSetup.NewGame();
var camera = new Camera(ctx.Map.CurrentPosition);
var worldRenderer = new WorldRenderer();

while (!Raylib.WindowShouldClose())
{
    camera.Update(ctx.Map.CurrentPosition);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(worldRenderer.GetBackgroundColor());

    worldRenderer.Draw(ctx, camera);

    Raylib.EndDrawing();
}
```

**Deliverable:** Full game world renders from GameContext with all terrain, edges, and effects

---

### Phase 3: Basic Input & Game Loop (Days 3-4)

**Goal:** WASD moves player, time advances, survival simulation runs

1. Create `InputHandler.cs`:
   - WASD → movement direction
   - Map clicks → travel target (like current web behavior)
   - Escape → pause/menu

2. Create `GameLoop.cs`:
   ```csharp
   public void Update()
   {
       // Handle input
       if (InputHandler.GetMovement() is Direction dir)
       {
           TravelHandler.Move(ctx, dir);
           // Movement costs time
           ctx.Update(travelTimeMinutes, ActivityType.Traveling);
       }

       // Handle pending events/encounters
       if (ctx.HasPendingEncounter)
           ctx.HandlePendingEncounter();
   }
   ```

3. Modify `Input.cs` to route to desktop:
   ```csharp
   public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices)
   {
       // Instead of WebIO, signal the UI layer
       return DesktopIO.Select(ctx, prompt, choices);
   }
   ```

**Deliverable:** Can walk around the map, time passes, stats drain

---

### Phase 4: Stats & Log Panels (Day 4)

**Goal:** ImGui panels showing survival stats, narrative log, location info

1. Create `StatsPanel.cs`:
   - Energy, Hydration, Calories bars
   - Temperature display
   - Active effects list
   - Port logic from `GameStateDto.FromContext()`

2. Create `LogPanel.cs`:
   - Scrolling narrative log
   - Color-coded by LogLevel
   - Read from `ctx.Log`

3. Create `LocationPanel.cs`:
   - Current location name and description
   - Available features
   - Weather conditions

**Deliverable:** HUD showing game state alongside world view

---

### Phase 5: Overlay System (Days 5-10)

**Goal:** Port all 16 overlay types from JavaScript to ImGui

The web frontend has **16 distinct overlay types** that can stack (multiple active simultaneously). Each needs an ImGui equivalent.

#### 5a. Complete Overlay Inventory

| Priority | Overlay | Lines | Complexity | Key Features |
|----------|---------|-------|------------|--------------|
| P0 | **EventOverlay** | 188 | High | Choices, outcomes, stat deltas, effect summaries |
| P0 | **FireOverlay** | 313 | High | Dual-mode (starting/tending), tool selection, success %, fuel list |
| P0 | **EatingOverlay** | 192 | Medium | Food/drink lists, calories/hydration bars, progress |
| P0 | **CraftingOverlay** | 246 | High | Tabbed categories, recipes, requirements badges |
| P1 | **InventoryOverlay** | 175 | Medium | Gear slots, tools, resources by category, weight |
| P1 | **TransferOverlay** | 124 | Medium | Side-by-side inventory, click-to-move |
| P1 | **HuntOverlay** | 181 | High | Distance animation, tracking states, outcome phases |
| P1 | **CookingOverlay** | 180 | Medium | Options list, result display, time costs |
| P2 | **ForageOverlay** | 143 | Medium | Focus selection (fuel/food/medicine), time options |
| P2 | **ButcherOverlay** | 106 | Low | Mode selection, decay status, time estimates |
| P2 | **HazardOverlay** | 54 | Low | Quick vs careful choice, injury risk display |
| P2 | **DiscoveryOverlay** | 34 | Low | Location name popup |
| P2 | **WeatherChangeOverlay** | 34 | Low | Weather transition notification |
| P2 | **DiscoveryLogOverlay** | 79 | Medium | Categories, discovered vs remaining |
| P2 | **ConfirmOverlay** | 42 | Low | Yes/no with custom message |
| P2 | **DeathOverlay** | 62 | Low | Death cause, survival stats, restart |

**Total: 2,153 lines of overlay JS to port**

#### 5b. Combat Mode (Special Case)

Combat is NOT an overlay - it **replaces the entire rendering mode**. The grid renderer switches from world map to combat grid.

**CombatGridRenderer.js features:**
- 25x25 meter grid (vs 7x7 tile world grid)
- Distance zone visualization (concentric rings)
- Unit rendering with health colors
- Boldness indicators (aggressive/bold/wary/cautious)
- Selection highlighting
- Unit position animation

**Combat DTO structure:**
- `CombatModeDto` - Full combat state
- `CombatGridDto` - 25x25 grid cells, units, terrain
- `CombatUnitDto` - ID, name, position, vitality, threat, icon

**Combat WebIO methods:**
- `RenderCombat()`, `WaitForCombatChoice()`
- `WaitForTargetChoice()` - body part targeting
- `WaitForCombatContinue()` - outcome display

**Implementation:** Create `CombatRenderer.cs` separate from `WorldRenderer.cs`. Switch between them based on game mode.

#### 5c. Tile Popup System

The web version has a **tile popup** that appears when hovering/clicking tiles:

**TilePopupRenderer.js features:**
- Dynamic positioning (right of tile, vertically centered)
- Quick glance badges (color-coded by type)
- Feature detail cards
- NPC stat bars (health, food, water, energy, temp)
- Travel time display
- "Go" button for travel

**Implementation:** ImGui popup window positioned relative to hovered tile. Use `ImGui.SetNextWindowPos()` with screen coordinates from tile position.

#### 5d. Overlay Pattern for ImGui

```csharp
public abstract class OverlayBase
{
    protected bool _isOpen;
    public bool IsOpen => _isOpen;

    public void Open() => _isOpen = true;
    public void Close() => _isOpen = false;

    public abstract void Draw(GameContext ctx);
}

public class FireOverlay : OverlayBase
{
    public enum Mode { Starting, Tending }
    private Mode _mode = Mode.Starting;
    private int _selectedTool;
    private int _selectedTinder;
    private int _selectedFuel;

    public override void Draw(GameContext ctx)
    {
        if (!_isOpen) return;

        ImGui.SetNextWindowSize(new Vector2(400, 500), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Fire Management", ref _isOpen))
        {
            ImGui.End();
            return;
        }

        // Mode tabs
        if (ImGui.BeginTabBar("FireTabs"))
        {
            if (ImGui.BeginTabItem("Start Fire"))
            {
                _mode = Mode.Starting;
                DrawStartingMode(ctx);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Tend Fire"))
            {
                _mode = Mode.Tending;
                DrawTendingMode(ctx);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void DrawStartingMode(GameContext ctx) { /* ... */ }
    private void DrawTendingMode(GameContext ctx) { /* ... */ }

    public event Action<FireAction>? OnAction;
}
```

#### 5e. Animation System

The web version has an `Animator.js` with:
- `tweenValue()` - cubic ease-out numeric animation
- `progressBar()` - 0-100% progress
- `distance()` - hunt distance meter
- `distanceMask()` - hunt distance bar animation

**For desktop:** Use Raylib's frame timing:
```csharp
public class Animator
{
    public static float EaseOutCubic(float t) => 1 - MathF.Pow(1 - t, 3);

    public static float Tween(float from, float to, float progress)
        => from + (to - from) * EaseOutCubic(progress);
}
```

Progress bars can use elapsed time vs duration for smooth animation.

**Deliverable:** All 16 overlays work, combat mode renders, tile popup shows info

---

### Phase 6: Blocking I/O Pattern (Day 10-11)

**Goal:** Solve the "multi-step blocking flow" problem cleanly

The current architecture has `WebIO.RunEatingUI()` blocking in a while loop. With single-threaded desktop, blocking calls become nested render loops.

**Chosen Approach: Message Pump (Nested Render Loop)**

```csharp
public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices)
{
    T? result = default;
    bool done = false;

    while (!done && !Raylib.WindowShouldClose())
    {
        // Keep rendering while waiting
        Raylib.BeginDrawing();
        WorldRenderer.Draw(ctx, camera);

        rlImGui.Begin();
        ImGui.Begin("Select");
        foreach (var choice in choices)
            if (ImGui.Button(choice.ToString()))
            {
                result = choice;
                done = true;
            }
        ImGui.End();
        rlImGui.End();

        Raylib.EndDrawing();
    }

    return result!;
}
```

This preserves the current game logic structure — handlers call `Input.Select()` and block until user chooses. The "blocking" is just a nested render loop that keeps the UI responsive.

**Why not state machines or coroutines?**
- Requires refactoring all handlers to be async/event-driven
- Current blocking pattern works fine for turn-based game
- Can refactor later if needed (e.g., for real-time elements)

**Alternative approaches (for future consideration):**

*Option A: Coroutine-style* — Game logic yields control, requires rewriting handlers as IEnumerator

*Option B: State machine* — Overlays emit events, game logic becomes reactive. Better architecture but significant refactor.

**Deliverable:** Blocking flows work without freezing the UI

---

### Phase 7: Polish & Cleanup (Days 11-14)

1. **Input refinement**
   - Keyboard shortcuts (I for inventory, F for fire, etc.)
   - Mouse hover tooltips
   - Scroll wheel zoom

2. **Visual polish**
   - ImGui theme/styling to match game aesthetic
   - Smooth camera transitions
   - Particle effects (snow, fire sparks)

3. **Audio** (optional)
   - Raylib has built-in audio
   - Ambient sounds, UI feedback

4. **Save/Load UI**
   - New game / continue
   - Manual save slots

5. **Final cleanup**
   - Remove any remaining WebIO stubs
   - Delete unused DTOs if any were preserved for reference
   - Clean up any TODO comments from migration

**Deliverable:** Complete, polished desktop game

---

## Font & Icon Dependencies

### Current Web Fonts
The web version uses Google Fonts:
- **Oswald** (400, 700) - Headings, location names
- **JetBrains Mono** (400, 500, 600) - Stats, numbers, monospace
- **Material Symbols Outlined** - All icons (variable weight 100-700)

### Icon Usage Map
| Category | Icons Used |
|----------|------------|
| Fire | `local_fire_department` (orange glow), `fireplace` (embers) |
| Water | `water_drop` (cyan) |
| Tools | `construction`, `handyman` |
| Weapons | `shield`, `trip_origin` |
| Food | `restaurant` |
| Energy | `bolt` |
| Health | `monitor_heart`, `favorite` |
| Temperature | `thermostat`, `ac_unit` |
| Wind | `air`, `wind_power` |
| Visibility | `visibility`, `visibility_off` |
| Storage | `backpack`, `inventory_2` |
| Traps | `check_circle` (catch ready, yellow glow) |
| Megafauna | `warning` (red glow) |

**100+ icons used total from Material Icons library**

### Desktop Font Strategy

**Option A: Bundle TTF fonts**
```csharp
// Load fonts at startup
var oswaldFont = Raylib.LoadFontEx("fonts/Oswald-Regular.ttf", 32, null, 0);
var monoFont = Raylib.LoadFontEx("fonts/JetBrainsMono-Regular.ttf", 16, null, 0);

// Use with DrawTextEx
Raylib.DrawTextEx(oswaldFont, "FROZEN CREEK", pos, 24, 1, Color.White);
```

**Option B: System fonts with fallback**
ImGui can use system fonts, but consistency across platforms is harder.

**Recommendation:** Bundle TTF files. Download from Google Fonts, include in project.

### Icon Strategy

**Option A: Font icons (like web)**
- Bundle Material Symbols TTF
- Render icons as text characters
- Requires Unicode codepoint mapping

**Option B: Sprite atlas**
- Pre-render needed icons to PNG
- Load as texture atlas
- Simpler, faster, no font complexity

**Option C: ImGui icon font**
- FontAwesome or similar
- Limited icon set but well-supported

**Recommendation:** Start with Option B (sprite atlas) for simplicity. Only ~30 unique icons actually used. Can switch to font icons later if needed.

---

## Data Flow Architecture

### Current Web: DTO Serialization

```
GameContext (C#)
    ↓
GameStateDto.FromContext() - Pre-computes everything
    ↓
JSON serialization
    ↓
WebSocket
    ↓
JavaScript parses JSON
    ↓
Renders to DOM/Canvas
```

**Problem:** Every frame serializes ~50 fields of game state, even if only time changed.

### Desktop: Direct Access

```
GameContext (C#)
    ↓
Renderer reads properties directly
    ↓
Raylib draws to screen
```

**Benefit:** No serialization overhead. Read exactly what you need.

### What GameStateDto Currently Computes

The backend pre-computes display values the frontend just renders:

| Field | Type | Example |
|-------|------|---------|
| `StatSeverity` | string | "good", "caution", "critical" |
| `TemperatureTrend` | string | "↑ warming", "↓ cooling", "→ stable" |
| `FireUrgency` | string | "safe", "caution", "warning", "critical" |
| `EffectBadges` | list | Pre-formatted effect display strings |
| `CapacityWarnings` | list | "Movement impaired", "Hands numb" |

**For desktop:** Move this display logic to the renderer or keep it in a `DisplayCalculator` utility class. Don't recompute every frame - cache and invalidate on state change.

---

## UI Mode System

The web version has **4 mutually exclusive rendering modes**:

| Mode | Grid Visible | What Renders |
|------|--------------|--------------|
| `TravelMode` | Yes | World map, stats panel, action buttons |
| `ProgressMode` | No | Activity text, progress bar, timer |
| `TravelProgressMode` | Yes (animated) | World map with camera pan + progress bar |
| `CombatMode` | Combat grid | 25x25m combat grid, unit positions |

**Desktop equivalent:**
```csharp
public enum RenderMode
{
    Travel,      // Normal exploration
    Progress,    // Activity in progress
    Combat       // Combat encounter
}

// In game loop:
switch (currentMode)
{
    case RenderMode.Travel:
        worldRenderer.Draw(ctx, camera);
        statsPanel.Draw(ctx);
        break;
    case RenderMode.Progress:
        progressDisplay.Draw(activityText, progress);
        break;
    case RenderMode.Combat:
        combatRenderer.Draw(combatState);
        break;
}

// Overlays draw on top of any mode
overlayManager.DrawAll(ctx);
```

---

## Risk Mitigation

### Risk: ImGui doesn't feel right for the game aesthetic
**Mitigation:** ImGui is highly skinnable. Custom fonts, colors, window styles. Can also mix: use ImGui for menus, custom Raylib rendering for in-world UI.

**Fallback:** If ImGui feels too "debug tool", use Raylib's `DrawRectangle`/`DrawText` for custom UI components. More work but full control.

### Risk: Blocking I/O pattern causes issues
**Mitigation:** Option C (message pump) preserves current architecture. Can refactor to state machine later if needed.

**Note:** The current WebIO already blocks with `while(true)` loops. Desktop just replaces the render calls inside those loops.

### Risk: Performance issues with large maps
**Mitigation:** Raylib is very fast for 2D. Only render visible tiles. Culling is trivial with camera bounds.

### Risk: Cross-platform issues
**Mitigation:** Both Raylib and ImGui are well-tested cross-platform. Test on Mac/Linux early in Phase 1.

### Risk: Material Icons dependency
**Mitigation:** Only ~30 unique icons used. Pre-render to sprite atlas. No font loading complexity.

### Risk: Combat renderer complexity
**Mitigation:** Combat grid is simpler than world grid (no terrain textures, just zones and units). Port after world renderer is stable.

### Risk: Animation timing differences
**Mitigation:** Use Raylib's `GetFrameTime()` for delta-time animations. Match web easing curves exactly.

### Risk: Overlay stacking complexity
**Mitigation:** ImGui handles window stacking natively. Multiple windows can be open simultaneously. Z-order managed automatically.

---

## Dependencies

```xml
<PackageReference Include="Raylib-cs" Version="6.0.0" />
<PackageReference Include="ImGui.NET" Version="1.90.1.1" />
<PackageReference Include="rlImGui-cs" Version="2.0.3" />
```

**Font files to bundle:**
- `Oswald-Regular.ttf`, `Oswald-Bold.ttf`
- `JetBrainsMono-Regular.ttf`, `JetBrainsMono-Medium.ttf`

**Optional:**
- `MaterialSymbols-Outlined.ttf` (if using font icons)

---

## File-by-File Migration Map

### Renderers (wwwroot/modules/grid/)

| JS File | C# File | Lines | Notes |
|---------|---------|-------|-------|
| `CanvasGridRenderer.js` | `WorldRenderer.cs` | 1,564 | Main world grid |
| `TerrainRenderer.js` | `TerrainRenderer.cs` | 719 | Procedural textures |
| `CombatGridRenderer.js` | `CombatRenderer.cs` | ~400 | Combat mode grid |
| `TilePopupRenderer.js` | `TilePopup.cs` | ~200 | Hover info popup |

### Overlays (wwwroot/overlays/)

| JS File | C# File | Lines | Priority |
|---------|---------|-------|----------|
| `EventOverlay.js` | `EventOverlay.cs` | 188 | P0 |
| `FireOverlay.js` | `FireOverlay.cs` | 313 | P0 |
| `EatingOverlay.js` | `EatingOverlay.cs` | 192 | P0 |
| `CraftingOverlay.js` | `CraftingOverlay.cs` | 246 | P0 |
| `InventoryOverlay.js` | `InventoryOverlay.cs` | 175 | P1 |
| `TransferOverlay.js` | `TransferOverlay.cs` | 124 | P1 |
| `HuntOverlay.js` | `HuntOverlay.cs` | 181 | P1 |
| `CookingOverlay.js` | `CookingOverlay.cs` | 180 | P1 |
| `ForageOverlay.js` | `ForageOverlay.cs` | 143 | P2 |
| `ButcherOverlay.js` | `ButcherOverlay.cs` | 106 | P2 |
| `HazardOverlay.js` | `HazardOverlay.cs` | 54 | P2 |
| `DiscoveryOverlay.js` | `DiscoveryOverlay.cs` | 34 | P2 |
| `WeatherChangeOverlay.js` | `WeatherOverlay.cs` | 34 | P2 |
| `DiscoveryLogOverlay.js` | `DiscoveryLogOverlay.cs` | 79 | P2 |
| `ConfirmOverlay.js` | `ConfirmOverlay.cs` | 42 | P2 |
| `DeathOverlay.js` | `DeathOverlay.cs` | 62 | P2 |

### Core Modules (wwwroot/modules/)

| JS File | C# Equivalent | Notes |
|---------|---------------|-------|
| `frameQueue.js` | (deleted) | Not needed - direct rendering |
| `connection.js` | (deleted) | Not needed - local |
| `progress.js` | `ProgressDisplay.cs` | Activity bar animation |
| `survival.js` | Inline in `StatsPanel.cs` | Stat formatting |
| `temperature.js` | Inline in `StatsPanel.cs` | Temp trend display |
| `fire.js` | Inline in `StatsPanel.cs` | Fire mini-display |
| `effects.js` | `EffectsPanel.cs` | Active effects list |
| `icons.js` | `IconAtlas.cs` | Icon sprite mapping |
| `location.js` | Inline in `StatsPanel.cs` | Location display |
| `notifications.js` | `NotificationManager.cs` | Toast messages |

### Components (wwwroot/lib/components/)

| JS File | C# Equivalent | Notes |
|---------|---------------|-------|
| `Icon.js` | `IconAtlas.Draw()` | Sprite-based |
| `Badge.js` | ImGui colored text | Use `ImGui.TextColored()` |
| `Bar.js` | `ImGui.ProgressBar()` | Built-in |
| `Gauge.js` | Custom render | Raylib circles |
| `ActionButton.js` | `ImGui.Button()` | Built-in |
| `RadioGroup.js` | `ImGui.RadioButton()` | Built-in |
| `StatRow.js` | Custom ImGui layout | Table rows |
| `helpers.js` | (not needed) | DOM manipulation |
| `OverlayBase.js` | `OverlayBase.cs` | Abstract base class |

### Backend (Web/) — Deleted in Phase 1

| C# File | Action | Notes |
|---------|--------|-------|
| `WebIO.cs` | Delete, replace with `DesktopIO.cs` | Similar method signatures |
| `WebServer.cs` | Delete | Session management not needed |
| `WebGameSession.cs` | Delete | Session management not needed |
| `SessionRegistry.cs` | Delete | Session management not needed |
| `Web/Dto/*.cs` | Delete (~5,300 lines) | No serialization needed |

All deleted at start of migration. `Input.cs` stubbed until `DesktopIO` is built.

### Data Access Changes

| Current | New | Notes |
|---------|-----|-------|
| `GameStateDto.FromContext(ctx)` | `ctx.player.Body.Energy` | Direct property access |
| `GridStateDto` | `ctx.Map.GetVisibleTiles()` | Direct method call |
| JSON serialization | None | No network boundary |

---

## Detailed Scope Estimate

| Category | JS Lines | Est. C# Lines | Notes |
|----------|----------|---------------|-------|
| Renderers | 2,883 | ~1,500 | Raylib API is more concise |
| Overlays | 2,153 | ~1,800 | ImGui requires less boilerplate |
| Modules | ~800 | ~400 | Much simpler without DOM |
| Components | ~600 | ~200 | ImGui built-ins replace most |
| **Total New** | - | **~4,000** | Desktop rendering layer |
| **Web Deleted** | 15,000+ | - | WebIO, DTOs, JS, CSS |
| **Net Change** | - | **-11,000** | Significant reduction |

---

## Potential Gotchas & Surprises

### 1. WebIO Method Signatures Are Complex

Many WebIO methods have multiple overloads and optional parameters:

```csharp
// Example: Select has 4+ overloads
Select<T>(ctx, prompt, choices)
Select<T>(ctx, prompt, choices, formatter)
Select<T>(ctx, prompt, choices, formatter, isDisabled)
Select<T>(ctx, prompt, choices, formatter, isDisabled, defaultChoice)
```

**Action:** Audit all WebIO public methods before starting. Document each signature.

### 2. Overlay State Dictionaries

WebIO has **17 static dictionaries** tracking overlay state:
```csharp
private static readonly Dictionary<string, InventoryDto> _currentInventory = new();
private static readonly Dictionary<string, TransferDto> _currentTransfer = new();
// ... 15 more
```

**Action:** Desktop doesn't need these (no session IDs). But understand what state each overlay needs before porting.

### 3. Fire Overlay Has Two Modes

`FireOverlay.js` (313 lines) handles BOTH:
- Starting a new fire (tool + tinder selection, success chance)
- Tending existing fire (add fuel, collect charcoal, light carriers)

**Action:** Treat as one overlay with mode enum, not two separate overlays.

### 4. Hunt/Encounter/Combat Are Three Different Systems

- **Hunt** - Tracking animals, approach decisions
- **Encounter** - Predator confrontations, distance management
- **Combat** - Turn-based fighting with body targeting

Each has its own WebIO methods, DTOs, and overlays. Don't conflate them.

### 5. Events Can Have Delayed Outcomes

`EventOutcomeDto` includes:
- Immediate stat changes
- Delayed effects (wounds, tensions)
- Inventory changes
- Spawned encounters

The outcome display is animated and may trigger follow-up events.

### 6. Progress Animations Sync With Game Time

When `ctx.Update(N, activity)` runs, the web version shows a progress bar that advances over real-time seconds while game time advances N minutes.

**Current flow:**
1. Backend calls `RenderWithDuration(seconds)`
2. Frontend animates progress bar
3. Backend blocks waiting for completion signal
4. Frontend signals done
5. Backend continues

**Desktop:** Need to replicate this timing. Can't just skip the animation.

### 7. Grid State Includes Edge Data

`GridStateDto` contains not just tiles but **edges** between tiles:
- Rivers (wavy line)
- Cliffs (descent only)
- Climbs (hazardous)
- Trails (travel bonus)

Edges are rendered AFTER tiles but BEFORE player icon. Order matters.

### 8. NPC Stat Bars in Tile Popup

The tile popup shows NPC health bars:
- Health (vitality)
- Food (calories)
- Water (hydration)
- Energy
- Temperature

This data comes from `FeatureDetailDto` which includes NPC body state. Make sure NPC data is accessible from tile info.

### 9. Crafting Categories Are Dynamic

`CraftingDto` categories come from `NeedCategory` enum but the recipes within each are computed based on:
- Available materials
- Known recipes
- Tool availability

The crafting UI shows "can craft" vs "missing requirements" per recipe.

### 10. Input.cs Already Abstracts I/O

The good news: game logic calls `Input.Select()`, not `WebIO.Select()` directly. The abstraction layer exists:

```csharp
// IO/Input.cs
public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices)
{
    return WebIO.Select(ctx, prompt, choices, c => c.ToString()!);
}
```

**Action:** Replace the body of Input methods to call DesktopIO instead of WebIO. Game logic unchanged.

### 11. Confirmation Dialogs Have Custom Buttons

`Confirm()` vs `PromptConfirm()`:
- `Confirm()` - Standard Yes/No
- `PromptConfirm()` - Custom button labels

Both need to block and return bool.

### 12. Color Scheme From CSS Variables

The web version uses CSS variables for theming:
```css
--bg-midnight: hsl(215, 30%, 5%);
--bg-surface: hsl(215, 25%, 12%);
--text-primary: rgba(255, 255, 255, 0.9);
--accent-fire: #e08830;
--accent-tech: #60a0b0;
--danger: #a05050;
```

**Action:** Create a `Theme.cs` with equivalent Color constants for consistent styling.

### 13. Time-of-Day Affects Everything

The `timeFactor` (0=midnight, 1=noon) affects:
- Background color lightness
- Terrain color brightness
- Vignette intensity
- Night overlay alpha
- Icon visibility

All colors need to be adjustable, not hardcoded.

---

## Success Criteria

1. All current gameplay works in desktop version
2. No web dependencies remain
3. Codebase reduced by ~10,000 lines
4. Input feels responsive (no network latency)
5. Multi-step flows (eating, fire management) feel natural
6. Runs on Windows, Mac, Linux
7. Visual parity with web version (terrain, icons, effects)
8. All 16 overlay types functional
9. Combat mode works with targeting
10. Animations feel smooth (60fps target)

---

## Future Considerations (Post-Migration)

After the desktop migration is stable:

1. **Free movement within locations**
   - WASD moves player position continuously
   - Features have world coordinates
   - Collision detection for terrain

2. **Sprite-based rendering**
   - Replace colored rectangles with actual sprites
   - Animated player, animals, fire

3. **Steam integration**
   - Achievements
   - Cloud saves
   - Workshop support (mods?)

4. **Sound design**
   - Ambient audio per location/weather
   - UI feedback sounds
   - Music

---

## Current Progress & Remaining Work

*Updated: Phase 7 Complete*

### Completed

**Phase 1-4: Core Infrastructure** ✅
- Raylib window, ImGui integration, world rendering
- Action panel with contextual buttons
- Inventory and Crafting overlays
- Event overlay with choices

**Phase 5: Activity Overlays** ✅
- `Desktop/UI/FireOverlay.cs` (526 lines) - Starting/tending modes
- `Desktop/UI/EatingOverlay.cs` (225 lines) - Food/drink consumption
- `Desktop/UI/CookingOverlay.cs` (167 lines) - Cook meat, melt snow
- `Desktop/UI/TransferOverlay.cs` (284 lines) - Inventory transfer
- `Desktop/UI/OverlayManager.cs` updated with all new overlays

**Phase 6: Blocking I/O Pattern** ✅
- `Desktop/DesktopRuntime.cs` (379 lines) - Shared state + BlockingDialog helper
- Core DesktopIO methods: Select, Confirm, PromptConfirm, ReadInt
- Activity prompts: SelectForageOptions, SelectButcherOptions, PromptHazardChoice
- Display methods: ShowDiscovery, ShowWeatherChange, ShowDiscoveryLogAndWait

**Phase 7a: Blocking UI Loop Wrappers** ✅
- `RunTransferUI`, `RunFireUI`, `RunEatingUI`, `RunCookingUI`
- Nested render loops with overlay result processing

**Phase 7b: Hunt Overlay** ✅
- `Desktop/UI/HuntOverlay.cs` (~280 lines)
- Animal info, distance tracking, approach options
- Hunt outcome display with kill/escape results
- DesktopIO methods: `RenderHunt`, `WaitForHuntChoice`, `WaitForHuntContinue`

**Phase 7c: Encounter Overlay** ✅
- `Desktop/UI/EncounterOverlay.cs` (~300 lines)
- Predator info with boldness indicator
- Threat factors, distance zones, choice buttons
- DesktopIO methods: `RenderEncounter`, `WaitForEncounterChoice`, `WaitForEncounterContinue`

**Phase 7d: Combat Overlay** ✅
- `Desktop/UI/CombatOverlay.cs` (~380 lines)
- Phase-based rendering (Intro, PlayerChoice, PlayerAction, AnimalAction, BehaviorChange, Outcome)
- Distance zone visualization, player status, combat actions
- Auto-advance timer for AI turns
- DesktopIO methods: `RenderCombat`, `WaitForCombatChoice`, `WaitForTargetChoice`, `WaitForCombatContinue`

**Phase 7e: Travel Progress Animation** ✅
- Animated camera pan from origin to destination
- Progress bar at bottom of screen
- Visual trail line feedback
- Eased animation (ease-out-cubic)

**Phase 7f: Grid/Map Interaction** ✅
- `RenderGridAndWaitForInput` blocking method
- WASD/Arrow keyboard movement
- Click-to-travel for adjacent tiles
- Action keyboard shortcuts (I=inventory, C=crafting, Space=wait)

**Phase 7g: Inventory/Crafting Display** ✅
- `RenderInventory`, `ShowInventoryAndWait`
- `RenderCrafting`

---

### Remaining Work

The core desktop migration is complete. All DesktopIO methods are implemented. Remaining work is polish and integration:

**Integration Testing:**
- Test full game loops with new overlays
- Verify hunt → encounter → combat flow
- Test travel with events and hazards
- Verify all blocking dialogs work correctly

**Polish (Optional):**
- ImGui theme/styling improvements
- Audio integration (Raylib has built-in audio)
- Save/Load UI improvements
- Additional keyboard shortcuts

**Files Created/Modified:**
- `Desktop/UI/HuntOverlay.cs` (~280 lines)
- `Desktop/UI/EncounterOverlay.cs` (~300 lines)
- `Desktop/UI/CombatOverlay.cs` (~380 lines)
- `Desktop/DesktopIO.cs` (~900 lines total)

**DTOs Created:**
- `HuntDto`, `HuntChoiceDto`, `HuntOutcomeDto`
- `EncounterDto`, `EncounterChoiceDto`, `EncounterOutcomeDto`, `ThreatFactorDto`
- `CombatDto`, `CombatActionDto`, `CombatOutcomeDto`, `CombatPhase` enum

---

### Architecture Notes

**Blocking Pattern:**
All blocking methods use nested render loops that:
1. Call `DesktopRuntime.RenderFrameWithDialog()` to render game + dialog
2. Process ImGui input within the dialog callback
3. Set a result variable when user makes choice
4. Exit loop when result is set or window closes

**Overlay Pattern:**
Non-blocking overlays that integrate with main game loop:
1. Open via `DesktopRuntime.Overlays.Open*()`
2. Render during `Overlays.Render()` in main loop
3. Return results via `OverlayResults`
4. Process results in `Program.ProcessOverlayResults()`

**When to use which:**
- **Blocking:** Simple prompts, confirmations, single-screen interactions
- **Overlay:** Complex multi-step interactions, need game to keep rendering

