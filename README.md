# Text Survival

A web-based survival game set in an Ice Age world where you've been separated from your tribe during the mountain crossing. Survive until midsummer when the pass clears.

## Vision

An exploration-driven survival experience in a harsh Ice Age world. Cold is the constant enemy — warmth from fire, shelter, and clothing determines how far you can push. Discover locations, hunt megafauna for better gear, and prepare for the mountain crossing when the pass clears.

**Inspirations:** The Long Dark, Don't Starve, RimWorld, A Dark Room

## Design Philosophy

**Cold shapes every decision.** Warmth management is survival. Fire is one tool among many — shelter, clothing, body fat, activity level all contribute. The question isn't "will my fire last" but "can I stay warm long enough."

**Exploration reveals the world.** The grid holds discoveries: locations, animal herds, megafauna territories, and resources. Pushing outward is both necessary (depletion) and rewarding (better hunting, rare finds).

**Gear is progression.** Better equipment enables harder challenges. A mammoth-hide coat means you can reach the glacier. Bear-fur boots mean you can survive the exposed ridgeline. Megafauna hunts are the skill checks that gate progression.

**Intersecting pressures create decisions.** Single problems have solutions. Multiple overlapping problems force tradeoffs. A wolf isn't dangerous because it's a wolf — it's dangerous because you're cold, hungry, and far from shelter.

## Core Systems

**Expeditions** — You have a camp. Everything else is an expedition. Leave camp, travel between locations, work (forage, hunt, explore), and return before the cold catches up.

**Survival Simulation** — Rate-based calculation per minute: energy, hydration, calories, temperature. Thresholds trigger effects. Temperature drops below 95°F, you start shivering. The simulation creates pressure, not busywork.

**Fire** — Physics-based fuel consumption, fire phases (igniting → building → roaring → steady → dying → embers), heat contribution to location temperature. One of several warmth tools alongside shelter and clothing.

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
