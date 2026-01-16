# Desktop Migration Plan: Raylib + ImGui.NET

## Overview

Migrate from WebSocket-based web UI to native desktop using Raylib (rendering/input) + ImGui.NET (overlay UI). The game logic stays unchanged; only the I/O layer is replaced.

**Current state:** ~11,500 lines of web infrastructure (WebIO, DTOs, frontend JS)
**Target state:** ~1,500-2,000 lines of desktop rendering code

---

## Architecture Comparison

### Current (Web)
```
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

## What Goes (Delete)

After migration complete:
- `Web/` directory entirely
  - `WebIO.cs` (1,787 lines)
  - `WebServer.cs`
  - `WebGameSession.cs`
  - `SessionRegistry.cs`
- `Web/Dto/` directory entirely (~5,300 lines)
- `wwwroot/` directory entirely (~4,700 lines JS)
- WebSocket dependencies

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

### Phase 1: Project Setup (Day 1)

**Goal:** Raylib window showing a colored rectangle, ImGui showing "Hello World"

1. Create new project or add to existing solution:
   ```bash
   dotnet add package Raylib-cs
   dotnet add package ImGui.NET
   dotnet add package rlImGui-cs  # Raylib-ImGui bridge
   ```

2. Create minimal `Program.cs`:
   ```csharp
   using Raylib_cs;
   using rlImGuiCs;
   using ImGuiNET;

   Raylib.InitWindow(1280, 720, "Text Survival");
   Raylib.SetTargetFPS(60);
   rlImGui.Setup(true);

   while (!Raylib.WindowShouldClose())
   {
       Raylib.BeginDrawing();
       Raylib.ClearBackground(Color.DarkGray);

       // Test world rendering
       Raylib.DrawRectangle(100, 100, 50, 50, Color.Green);

       // Test ImGui
       rlImGui.Begin();
       ImGui.Begin("Test Panel");
       ImGui.Text("Hello from ImGui!");
       if (ImGui.Button("Click me"))
           Console.WriteLine("Clicked!");
       ImGui.End();
       rlImGui.End();

       Raylib.EndDrawing();
   }

   rlImGui.Shutdown();
   Raylib.CloseWindow();
   ```

3. Verify builds and runs on target platforms

**Deliverable:** Window with rectangle + ImGui panel

---

### Phase 2: World Rendering (Days 2-3)

**Goal:** Render the game grid with terrain, player position, basic camera

1. Create `Camera.cs`:
   - Track viewport center (player position)
   - Convert world coords ↔ screen coords
   - Handle zoom level

2. Create `WorldRenderer.cs`:
   - Port terrain rendering from `TerrainRenderer.js`
   - Draw grid tiles based on `GameMap`
   - Draw player marker
   - Draw visible features (fires, caches, etc.)

3. Create `SpriteAtlas.cs`:
   - Load terrain textures
   - Load feature icons
   - Simple sprite management

4. Wire up to game state:
   ```csharp
   var ctx = SaveManager.Load(sessionId) ?? GameSetup.NewGame();

   while (!Raylib.WindowShouldClose())
   {
       Raylib.BeginDrawing();
       WorldRenderer.Draw(ctx, camera);
       Raylib.EndDrawing();
   }
   ```

**Deliverable:** Game world renders from actual GameContext

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

### Phase 5: Core Overlays (Days 5-8)

**Goal:** Port the main interactive overlays from WebIO

Priority order (most used first):

#### 5a. Event Overlay
- Modal dialog with event text
- Choice buttons
- Maps to `WebIO.ShowEvent()` / current `EventOverlay.js`
- Critical: this is how events interrupt gameplay

#### 5b. Inventory Overlay
- List of items with weights
- Transfer to/from storage
- Maps to `WebIO.RunTransferUI()`

#### 5c. Fire Management Overlay
- Fire status display
- Add fuel, tend fire, start fire
- Tool/tinder selection
- Maps to `WebIO.RunFireUI()`

#### 5d. Eating Overlay
- Food/drink lists
- Consumption buttons
- Calorie/hydration feedback
- Maps to `WebIO.RunEatingUI()`

#### 5e. Crafting Overlay
- Need categories
- Recipe list with requirements
- Craft button
- Maps to `CraftingRunner` + current overlay

**Pattern for each overlay:**
```csharp
public class InventoryOverlay
{
    private bool _isOpen;
    private int _selectedIndex;

    public void Open() => _isOpen = true;

    public void Draw(GameContext ctx)
    {
        if (!_isOpen) return;

        ImGui.Begin("Inventory", ref _isOpen);

        foreach (var item in ctx.Inventory.Items)
        {
            if (ImGui.Selectable(item.Name, _selectedIndex == i))
                _selectedIndex = i;
        }

        if (ImGui.Button("Use"))
            OnUseItem?.Invoke(ctx.Inventory.Items[_selectedIndex]);

        ImGui.End();
    }

    public event Action<Item>? OnUseItem;
}
```

**Deliverable:** Can manage fire, eat, craft, handle events - core gameplay loop works

---

### Phase 6: Secondary Overlays (Days 8-10)

Port remaining overlays:

- **Cooking Overlay** - cook meat, melt snow
- **Hunt Overlay** - track selection, approach options
- **Combat Overlay** - distance zones, action buttons, animal state
- **Butcher Overlay** - carcass processing
- **Forage Overlay** - time selection, area display
- **Discovery Overlay** - new location/feature reveals
- **Weather Change Overlay** - weather transition display
- **Death Overlay** - game over screen

**Deliverable:** All gameplay interactions work in desktop

---

### Phase 7: Blocking I/O Pattern (Day 10-11)

**Goal:** Solve the "multi-step blocking flow" problem cleanly

The current architecture has `WebIO.RunEatingUI()` blocking in a while loop. Desktop can do better.

**Option A: Coroutine-style**
```csharp
// Game loop yields control to overlay
public IEnumerator<WaitFor> EatingFlow(GameContext ctx)
{
    while (true)
    {
        var choice = yield return new WaitForOverlay<FoodItem>(eatingOverlay);
        if (choice == null) break; // Closed

        ConsumptionHandler.Consume(ctx, choice);
        ctx.Update(5, ActivityType.Eating);
    }
}
```

**Option B: State machine**
```csharp
enum GameState { Exploring, InMenu, InOverlay }

// Overlay signals completion via callback
eatingOverlay.OnItemSelected += item => {
    ConsumptionHandler.Consume(ctx, item);
    ctx.Update(5, ActivityType.Eating);
};
eatingOverlay.OnClosed += () => gameState = GameState.Exploring;
```

**Option C: Keep blocking with message pump**
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

Option C is closest to current architecture and requires least refactoring. Start there, optimize later if needed.

**Deliverable:** Blocking flows work without freezing the UI

---

### Phase 8: Polish & Cleanup (Days 11-14)

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

5. **Delete web infrastructure**
   - Remove `Web/` directory
   - Remove `wwwroot/`
   - Remove WebSocket dependencies
   - Update `.csproj`

**Deliverable:** Complete, polished desktop game

---

## Risk Mitigation

### Risk: ImGui doesn't feel right for the game aesthetic
**Mitigation:** ImGui is highly skinnable. Custom fonts, colors, window styles. Can also mix: use ImGui for menus, custom Raylib rendering for in-world UI.

### Risk: Blocking I/O pattern causes issues
**Mitigation:** Option C (message pump) preserves current architecture. Can refactor to state machine later if needed.

### Risk: Performance issues with large maps
**Mitigation:** Raylib is very fast for 2D. Only render visible tiles. Culling is trivial with camera bounds.

### Risk: Cross-platform issues
**Mitigation:** Both Raylib and ImGui are well-tested cross-platform. Test on Mac/Linux early in Phase 1.

---

## Dependencies

```xml
<PackageReference Include="Raylib-cs" Version="6.0.0" />
<PackageReference Include="ImGui.NET" Version="1.90.1.1" />
<PackageReference Include="rlImGui-cs" Version="2.0.3" />
```

---

## File-by-File Migration Map

| Current (Web) | New (Desktop) | Notes |
|--------------|---------------|-------|
| `WebIO.Select()` | `DesktopIO.Select()` | Blocking with message pump |
| `WebIO.RunFireUI()` | `FireOverlay.cs` | ImGui panel |
| `WebIO.RunEatingUI()` | `EatingOverlay.cs` | ImGui panel |
| `WebIO.RunTransferUI()` | `InventoryOverlay.cs` | ImGui panel |
| `WebIO.RunCookingUI()` | `CookingOverlay.cs` | ImGui panel |
| `WebIO.ShowEvent()` | `EventOverlay.cs` | Modal dialog |
| `GameStateDto` | Direct reads | No serialization needed |
| `CanvasGridRenderer.js` | `WorldRenderer.cs` | Port terrain logic |
| `TerrainRenderer.js` | `WorldRenderer.cs` | Port texture rendering |
| `frameQueue.js` | (deleted) | Not needed - direct rendering |
| `connection.js` | (deleted) | Not needed - local |

---

## Success Criteria

1. All current gameplay works in desktop version
2. No web dependencies remain
3. Codebase reduced by ~10,000 lines
4. Input feels responsive (no network latency)
5. Multi-step flows (eating, fire management) feel natural
6. Runs on Windows, Mac, Linux

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

