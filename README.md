# Text Survival

A console-based survival RPG set in an Ice Age world with shamanistic elements, inspired by games like RimWorld and The Long Dark. The game emphasizes deep, interconnected systems, leveraging its text-only format to create immersive survival gameplay without the overhead of graphics. Players must navigate procedurally generated environments, manage survival needs, engage in combat, craft tools, and use shamanistic magic to survive harsh conditions.

## Game Vision and Core Concept

**Text Survival** is evolving from a traditional RPG into a focused survival experience where resource management, environmental challenges, and dynamic character interactions are paramount. Set in an Ice Age world where cold, scarcity, and rugged terrain shape gameplay, the player must adapt and overcome through skill development and strategic decision-making.

### Inspirations:
- **The Long Dark**: Emphasis on survival through managing hunger, thirst, and harsh weather conditions
- **RimWorld**: Detailed body part system and emergent storytelling through character interactions

### Design Philosophy:
- **Simplicity with Depth**: Simple systems combine for complex outcomes (e.g., a skilled but injured player vs. an unskilled healthy one)
- **Modularity**: Supports adding features like diseases or prosthetics without major refactoring
- **Realism**: Grounded mechanics (calorie-based fat dynamics, activity-driven muscle growth)

## Technical Architecture Overview

The game is built in C# with a modular architecture, separating concerns into distinct systems:

- **Actors**: Manages entities like Player and NPC (subclasses Humanoid, Animal) with interfaces like ICombatant, IBuffable, and ISpellCaster. Includes BodyPart for tracking health.
- **Items**: Includes Item, Container, FoodItem, Weapon, Armor, and Gear, with interfaces like IEquippable and IEdible. Inventory supports basic item management but lacks stacking.
- **Environments**: Features Zone, Location (e.g., Cave, River), and WorldMap for infinite procedural generation.
- **Survival**: Modules (HungerModule, ThirstModule, ExhaustionModule, TemperatureModule) simulate survival needs, impacting health.
- **Combat**: Combat class handles turn-based fights, integrating actors and items.
- **Level**: Manages player progression with Skills (e.g., Hunting, Firecraft), moving away from traditional attributes.
- **Magic**: Implements Spell and Buff (TimedBuff, TriggeredBuff) for shamanistic effects.
- **Actions**: Actions class dynamically generates player commands (e.g., LookAroundCommand, MoveCommand) based on context.
- **IO**: Handles console input/output, with plans for web and AI-enhanced modes (AI_IO).
- **Event System**: EventHandler decouples systems (e.g., skill leveling triggers experience gain).

## Character Systems

### Body System

#### Structure:
- **Humans**: Detailed hierarchy (e.g., Torso → Heart, Arm → Hand → Fingers)
- **Animals**: Simplified but compatible (e.g., Wolf: Body, Head, Legs, Tail)

#### Capacities:
- Body parts contribute to capacities (e.g., Leg affects Moving with a value of 0.5)
- Stats are calculated from aggregated capacities (e.g., Speed = Moving * (1 - BodyFatPercentage / 100))

#### Body Composition:
- Tracks fat and muscle in kilograms, with percentages dynamically calculated
- Fat reduces speed but aids cold resistance; muscle boosts strength

### Skills and Stats

#### Skills:
- Exclusive to the player, representing mental or knowledge-based abilities:
  - **Hunting**: Improves tracking and animal yields
  - **FireMaking**: Reduces fire-building time and cold damage
  - **Skinning**: Increases hide/bone yields
  - **ToolCrafting**: Unlocks advanced recipes
  - **Shamanism**: Enhances magic rituals
- Improve with use via an XP system and do not decrease, reflecting permanent learning

#### Body-Derived Stats:
- Physical attributes derived from the body part system, applicable to both players and NPCs
- Influenced by body condition (injuries, fat, muscle), not skill progression

### NPCs
- Rely solely on body-derived stats, no skills
- Differentiated by body traits (e.g., Wolf: high Speed; Mammoth: high Vitality)

### Player Progression
- **Mental Growth**: Skills enhance with practice, improving combat or crafting outcomes
- **Physical Dynamics**: Stats fluctuate with body condition, not skill loss

## Core Mechanics

### Survival Mechanics
- **SurvivalManager**: Monitors hunger, thirst, fatigue, and body fat/muscle
- Realistic conversion: 7700 kcal = 1 kg of fat
- Cold resistance tied to Vitality and fat percentage

### Combat
- Driven by body-derived stats (Strength, Speed) for all characters
- Player-specific boost from skills (e.g., Hunting increases accuracy and critical hit chance)
- **Ranged Hunting**: A distinct mechanic for non-hostile wildlife, leveraging Hunting skill

### Injury and Healing
- Damage to body parts impacts capacities (e.g., injured Leg lowers Moving)
- Features include pain, scarring, and specific effects (e.g., Spine damage causing paralysis)

### Shamanistic Magic
- Requires specific items (herbs, bones) for spells like SpiritSummon, FireRitual, and WeatherCall
- Tied to Shamanism skill for modifiers and effectiveness

### Weather System
- Dynamic weather (blizzards, fog) per Zone, affecting visibility, temperature, and action success
- WeatherCall spell allows limited influence over weather conditions

## Development Roadmap

### Immediate (1-2 Weeks)
- Refactor Player using composition (SurvivalManager, InventoryManager, CombatController, etc.)
- Fix inventory stacking with ItemStack implementation
- Simplify event system using C#'s event and delegate via GameEventManager
- Implement TimeManager and enhanced actions system (time elapsed, calorie burn, skill modifiers)

### Short-Term (2-4 Weeks)
- Redesign skills for survival focus, removing traditional RPG attributes
- Adopt JSON data-driven design for items, NPCs, locations, and recipes
- Develop crafting system for tools, weapons, and gear
- Implement weather system affecting gameplay

### Mid-Term (4-8 Weeks)
- Complete injury/disease system with detailed body part tracking
- Implement shamanistic magic system requiring gathered materials
- Add barter-based trading system integrated with DialogueSystem
- Develop Body Composition System tracking weight, fat, and muscle

### Long-Term (8+ Weeks)
- Polish IO with rich, Ice Age-specific descriptions
- Integrate AI dialogue for non-hostile NPC interactions
- Add win condition (GreatSpirit boss, SpiritTotem)
- Test and balance gameplay systems

## Planned Enhancements

### Systems Revamps
- **Refactor Player Class**: Split into focused components using composition
- **Fix Inventory Stacking**: Implement ItemStack for better inventory management
- **Simplify Event System**: Use C#'s native event and delegate system
- **Data-Driven Design**: JSON templates for content to improve maintainability and support modding

### Content Additions
- **More Items**: Flint tools, furs, bone weapons
- **New NPCs**: Sabertooth, mammoth, tribal humans
- **New Locations**: Glacial cliffs, tundra, frozen lakes

### Mechanical Improvements
- **Armor Revamp**: Materials-based system with randomized stats
- **Crafting System**: JSON-driven with skill-based quality tiers
- **Thematic Overhaul**: Replace RPG elements with Ice Age equivalents
- **Enhanced Locations**: Biome-specific mechanics and vivid descriptions

## Conclusion

"Text Survival" aims to deliver a rich survival experience through its Ice Age setting, detailed character systems, and modular mechanics. This vision blends realism and emergent complexity, offering a foundation for future growth and a unique gameplay identity. By leveraging the text-based format, the game can focus on deep systems and immersive storytelling without graphical limitations.
