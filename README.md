text_survival
A console-based survival RPG set in an Ice Age world with magic elements, inspired by games like RimWorld and The Long Dark. The game emphasizes deep, interconnected systems, leveraging its text-only format to create immersive survival gameplay without the overhead of graphics. Players must navigate procedurally generated environments, manage survival needs (hunger, thirst, exhaustion, temperature), engage in combat, craft tools, and use shamanistic magic to survive harsh conditions.
Overview
The game is built in C# with a modular architecture, separating concerns into distinct systems:

Actors: Manages entities like Player and NPC (subclasses Humanoid, Animal) with interfaces like ICombatant, IBuffable, and ISpellCaster. Includes BodyPart for tracking health (incomplete).
Items: Includes Item, Container, FoodItem, Weapon, Armor, and Gear, with interfaces like IEquippable and IEdible. Inventory supports basic item management but lacks stacking.
Environments: Features Zone, Location (e.g., Cave, River), and WorldMap for infinite procedural generation.
Survival: Modules (HungerModule, ThirstModule, ExhaustionModule, TemperatureModule) simulate survival needs, impacting health.
Combat: Combat class handles turn-based fights, integrating actors and items.
Level: Manages player progression with Attributes (e.g., Strength, Dexterity) and Skills (e.g., Archery, Destruction).
Magic: Implements Spell and Buff (TimedBuff, TriggeredBuff) for magical effects, currently underutilized.
Actions: Actions class dynamically generates player commands (e.g., LookAroundCommand, MoveCommand) based on context.
IO: Handles console input/output, with plans for web and AI-enhanced modes (AI_IO).
Event System: EventHandler decouples systems (e.g., skill leveling triggers experience gain).

The game is transitioning from a traditional RPG to an Ice Age survival focus, requiring updates to remove RPG-centric elements (e.g., swords, attributes) and enhance survival mechanics.
To Do
The following tasks outline planned improvements and new features to align the game with its Ice Age survival theme and deepen its systems:
Existing Tasks

More Content: Add new items (e.g., flint tools, furs), NPCs (e.g., sabertooth, mammoth), and locations (e.g., glacial cliffs, tundra).
Armor Revamp: Introduce ArmorMaterial and randomize stats (e.g., warmth, durability) in ItemFactory. Update ArmorRating to reflect materials and skills.
Shops/Crafting/Alchemy: Implement crafting for tools/weapons and alchemy for potions. Replace shops with a barter-based trading system.
Body Part Damage: Fully integrate BodyPart into combat, replacing flat HP with part-specific health and effects (e.g., injured leg slows movement).
AI Dialogue: Integrate AI_IO with a DialogueSystem for non-hostile NPC interactions (e.g., trading, storytelling).
Win Condition: Add a final boss (e.g., GreatSpirit) with unique loot (e.g., SpiritTotem) to trigger a game-ending event.

New Tasks (Planned Enhancements)

Refactor Player Class: Use composition to split Player responsibilities into SurvivalManager, InventoryManager, CombatController, SkillManager, and BodyComposition to reduce complexity.
Fix Inventory Stacking: Implement ItemStack (e.g., { Item, Quantity }) to support stacking and improve inventory management.
Simplify Event System: Replace EventHandler with C#’s event and delegate via a GameEventManager for simpler, scalable event handling.
Data-Driven Design: Use JSON templates for items, NPCs, locations, and recipes to improve maintainability and support modding.
Redesign Skill System: Replace RPG skills (e.g., OneHanded, Destruction) with survival-focused ones:
Hunting: Improves tracking and animal yields.
FireMaking: Reduces fire-building time and cold damage.
Skinning: Increases hide/bone yields.
ToolCrafting: Unlocks advanced recipes.
Shamanism: Enhances magic rituals.
Remove attributes (e.g., Strength, Dexterity) and add perks at skill milestones.


Enhance Actions System: Add time elapsed, calorie burn, and skill-based modifiers to actions (e.g., BuildFire, Hunt). Introduce TimeManager to track in-game hours and update survival modules.
Body Composition System: Track weight, fat, and muscle, affecting calorie needs, cold resistance, and action efficiency. Calorie intake/expenditure adjusts fat/muscle dynamically.
Shamanistic Magic: Expand magic to require items (e.g., herbs, bones) for spells like SpiritSummon, FireRitual, and WeatherCall. Tie to Shamanism skill for modifiers.
Crafting System: Implement a JSON-driven crafting system for tools, weapons, and gear (e.g., FlintSpear). Use ToolCrafting skill for recipe unlocks and quality tiers.
Weather System: Add dynamic weather (e.g., blizzards, fog) per Zone, affecting visibility, temperature, and action success. Allow WeatherCall spell to influence weather.
Trading System: Implement a barter-based trading system where players exchange items with NPCs based on value and NPC needs, integrated with DialogueSystem.
Injury and Disease System: Expand BodyPart to track injuries (e.g., cuts, frostbite) and diseases (e.g., infection, pneumonia). Injuries apply penalties (e.g., broken arm slows crafting); diseases progress if untreated.
Enhance Locations: Add biome-specific mechanics (e.g., IceCave requires light, Tundra has blizzards) and vivid descriptions to distinguish locations and reinforce the Ice Age theme.
Thematic Overhaul: Replace RPG items (e.g., swords) with Ice Age equivalents (e.g., FlintSpear, FurCoat). Shift combat to be less frequent but more impactful, integrating survival (e.g., cold reduces stamina).
Polish IO: Enhance console output with rich, Ice Age-specific descriptions (e.g., “The tundra crunches underfoot as a mammoth roars in the distance”).

Development Roadmap

Immediate (1-2 Weeks): Refactor Player, fix inventory stacking, simplify event system, implement TimeManager and enhanced actions.
Short-Term (2-4 Weeks): Redesign skills, adopt JSON data, develop crafting and weather systems.
Mid-Term (4-8 Weeks): Complete injury/disease system, implement shamanistic magic, add trading system.
Long-Term (8+ Weeks): Polish IO, integrate AI dialogue, add win condition, test and balance.

