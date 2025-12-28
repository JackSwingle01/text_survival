# Text Survival

A text-based survival game set in an Ice Age world. The text-only format enables deep, interconnected survival systems without graphical overhead.

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

**Body** — Hierarchical body parts contributing to capacities (moving, manipulation, breathing, consciousness). Injuries attach to parts with severity. Body composition (fat, muscle) affects temperature resistance and speed.

**Depletion** — Areas run out. The nearby grove is plentiful on day one, sparse by day three, picked over by day five. The game pushes you outward — range further or move camp.

## Build and Run

```bash
dotnet build
dotnet run
```

Requires .NET 9.0.

## Project Structure

```
text_survival/
├── Core/           # Program.cs, Config.cs
├── Actions/        # GameRunner, ExpeditionRunner, WorkRunner, events
├── Actors/         # Player, Camp, Animals
├── Bodies/         # Body parts, damage, capacities
├── Survival/       # SurvivalProcessor (stateless)
├── Effects/        # EffectRegistry, conditions
├── Environments/   # Zones, locations, features, weather
├── Items/          # Tools, fuel, food, inventory
├── Crafting/       # Need-based crafting
├── IO/             # Output.cs, Input.cs
└── UI/             # GameDisplay rendering
```

## Status

Core systems functional: camp-centric loop, fire management, flexible expeditions, hunting with stalking/ranged attacks, foraging with depletion, survival stats, body/damage, contextual events, need-based crafting. Current focus: balance tuning and content expansion.