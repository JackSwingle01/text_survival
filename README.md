# Text Survival

A web-based survival game set in an Ice Age world where you've been separated from your tribe during the mountain crossing. Survive until midsummer when the pass clears.

## Vision

A camp-centric survival experience where fire is the anchor. Every expedition is a commitment — leave camp, do the work, return before your fire dies. Player knowledge is the only progression; the game doesn't get easier, you get better.

**Inspirations:** The Long Dark, Don't Starve, RimWorld, A Dark Room

## Design Philosophy

**Fire as tether.** Every decision runs through one question: can I do this and get back before my fire dies? The fire burns whether you're there or not. This single constraint makes everything matter — temperature, calories, time, distance.

**Intersecting pressures create decisions.** A wolf isn't dangerous because it's a wolf. It's dangerous because you're bleeding, limping, low on fuel, and far from camp. Single systems create problems. Intersecting systems create meaningful choices.

**Knowledge is progression.** No meta-progression, no unlocks, no character stats that grow. The player gets better, not the character. An experienced player survives because they understand the systems — when 10 minutes of fire is actually fine, when "abundant" doesn't mean safe, when to let a fire die on purpose.

**The game decides low-stakes, the player decides high-stakes.** The interface reads game state and surfaces what matters. "Tend fire" appears prominently when fuel is low. The player chooses from 2-4 meaningful options, not exhaustive menus.

## Core Systems

**Expeditions** — You have a camp. Everything else is an expedition. Leave camp, travel between locations, work (forage, hunt, explore), and return before your fire dies.

**Survival Simulation** — Rate-based calculation per minute: energy, hydration, calories, temperature. Thresholds trigger effects. Temperature drops below 95°F, you start shivering. The simulation creates pressure, not busywork.

**Fire** — Physics-based fuel consumption, fire phases (igniting → building → roaring → steady → dying → embers), heat contribution to location temperature. Fire is infrastructure that extends your expedition radius.

**Body** — Regional body parts contributing to capacities (moving, manipulation, breathing, consciousness). Injuries attach to parts with severity. Body composition (fat, muscle) affects temperature resistance and speed.

**Hunting & Herds** — Persistent animal populations that migrate and behave. Stalking, ranged attacks, and multi-session megafauna hunts. Carcasses decay over time, creating pressure to butcher before spoilage.

**Events & Tensions** — Contextual events based on player state, weather, location, and active tensions. Tensions are unresolved narrative threads that escalate until confrontation or resolution.

**Crafting** — Need-based system. Express what you need, see what's craftable from available materials. Tools, equipment, treatments, and camp improvements.

**Depletion** — Areas run out. The nearby grove is plentiful on day one, sparse by day three, picked over by day five. The game pushes you outward — range further or move camp.

## Build and Run

```bash
dotnet build
dotnet run                  # Starts web server on port 5000
dotnet run --port=8080      # Custom port
```

Then open http://localhost:5000 in a browser. Requires .NET 9.0.

## Project Structure

```
text_survival/
├── Actions/        # Runners, handlers, events, expeditions, tensions
├── Actors/         # Player, Camp, Animals, Herds
├── Bodies/         # Body parts, damage, capacities
├── Combat/         # Combat manager and narration
├── Config/         # Activity configuration
├── Crafting/       # Need-based crafting system
├── Effects/        # Effect registry, conditions
├── Environments/   # Grid, locations, features, weather
├── Items/          # Gear (tools, equipment), inventory, rewards
├── Persistence/    # Save/load, game initialization
├── Survival/       # Stateless survival calculations
├── Web/            # WebSocket server, DTOs, web UI backend
└── wwwroot/        # Browser frontend (HTML, JS, CSS)
```

## Status

Core systems functional: fire-tethered expeditions, grid-based world with location discovery, hunting with stalking and megafauna hunts, herd system with persistent animal populations, foraging with depletion and environmental clues, carcass butchering with decay timing, body/damage with regional injuries, contextual events with tensions, need-based crafting, equipment wear and waterproofing. Current focus: balance tuning and content expansion.
