# Text Survival Project Context

## Project Overview

**Text Survival** is a deep, console-based survival game set in an Ice Age world. It emphasizes realism, compound pressures, and system interconnectivity over graphical overhead. The core loop revolves around maintaining a fire ("Fire as Tether") and managing expeditions that must return before the fire dies.

**Key Technologies:**
-   **Language:** C# (.NET 9.0)
-   **UI:** `Spectre.Console` (for rich console output)
-   **Web:** ASP.NET Core (optional web mode via WebSockets)
-   **Mapping:** `Mapster`

**Architecture:**
-   **State:** `GameContext` acts as the central hub holding all game state (Player, Camp, Zones, etc.).
-   **Logic:** Logic is separated into "Runners" (control flow, UI) and "Processors" (stateless domain logic like survival stats, butchering).
-   **Events:** A factory-based event system triggers contextual interrupts based on player state and location.
-   **Systems:** Distinct systems for Fire, Expeditions, Body (hierarchical parts/damage), Survival (stats), Crafting, and Locations interact to create emergent gameplay.

## Build and Run

The project is a standard .NET application.

**Build:**
```bash
dotnet build
```

**Run (Console Mode - Default):**
```bash
dotnet run
```

**Run (Web Mode):**
Starts a local web server (default port 5000) that streams the game via WebSockets to a browser.
```bash
dotnet run -- --web
# OR specify port
dotnet run -- --web --port=5000
```

**Test:**
```bash
dotnet test
```

## Directory Structure

-   `Core/`: Application entry point (`Program.cs`) and configuration.
-   `Actions/`: Game loops and logic runners (`GameRunner`, `ExpeditionRunner`, `WorkRunner`).
-   `Actors/`: Entities like `Player`, `Animals`.
-   `Bodies/`: The detailed body simulation system (parts, organs, damage, capacities).
-   `Survival/`: Stateless survival logic (`SurvivalProcessor`).
-   `Effects/`: Ongoing conditions (bleeding, hypothermia) handled by `EffectRegistry`.
-   `Environments/`: World generation, `Location`, `Zone`, `Weather`.
-   `Items/` & `Crafting/`: Inventory management and need-based crafting system.
-   `IO/` & `UI/`: Input/Output abstraction (`Spectre.Console` wrappers).
-   `Web/`: ASP.NET Core web server and WebSocket handling for the web mode.
-   `documentation/`: Detailed system documentation. **Read these first.**

## Development Conventions

**Documentation First:**
Always consult the following files before making architectural changes:
1.  `principles.md`: The core design philosophy (Experience Goals, Design Principles).
2.  `documentation/overview.md`: High-level system interaction overview.
3.  `documentation/composition-architecture.md`: Details on the composition over inheritance approach.

**Coding Patterns:**
-   **Runners vs. Processors:** Keep stateful control flow in "Runners" and pure domain logic in "Processors".
-   **Factories:** Use factories for complex object creation (e.g., Events, Body Parts).
-   **Composition:** Prefer composition for entity features (e.g., `Feature` classes on `Location`s).
-   **Text-First:** The narrative text *is* the graphics. Ensure output is concise, evocative, and follows the "Narrative Principles" in `principles.md`.

**Web vs. Console:**
The game core is agnostic. `IO/Input.cs` and `IO/Output.cs` abstract the interaction layer. The `Web` folder implements these abstractions over WebSockets, while the default implementation uses the system console. Ensure changes respect this abstraction.
