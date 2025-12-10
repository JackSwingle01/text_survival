# Skill Check System

## Overview

The skill check system provides probabilistic success/failure mechanics for crafting recipes, with skill progression affecting success rates. This system balances realistic uncertainty (fire-making, hunting) with player agency, ensuring that success is never guaranteed but also never impossible.

---

## Core Concepts

### What is a Skill Check?

A **skill check** is a probabilistic test that determines whether a crafting action succeeds. Not all crafting recipes use skill checks - most simple crafting (like making a shelter) has 100% success rate. Skill checks are used for:

- **Fire-making** (Hand Drill, Bow Drill, Flint & Steel)
- **Hunting** (tracking and killing prey)
- **Advanced crafting** (complex items that could realistically fail)

### Design Philosophy

**When to use skill checks:**
- Actions with realistic uncertainty (starting fires friction)
- High-skill activities (tracking elusive prey)
- Dramatic moments (critical survival tasks)

**When NOT to use skill checks:**
- Simple crafting (combining items)
- Infrastructure building (shelters, features)
- Material processing (skinning, butchering)

**Core principles:**
- **Player agency**: 5%-95% clamp prevents pure RNG gates
- **Learning from failure**: 1 XP awarded on failure encourages experimentation
- **Skill matters**: +10%/level provides clear progression benefit
- **Base rates matter**: 30-50% base rates feel challenging but fair

---

## Formula

### Success Chance Calculation

```csharp
double successChance = BaseSuccessChance + (SkillLevel - DC) * 0.1;
successChance = Math.Clamp(successChance, 0.05, 0.95);
bool success = Utils.DetermineSuccess(successChance);
```

Where:
- **BaseSuccessChance**: Starting probability (typically 0.30 to 0.50)
- **SkillLevel**: Player's current skill level (starts at 0)
- **DC** (Difficulty Class): Target skill level (typically 0)
- **Modifier per level**: 0.1 (+10% per skill level)
- **Clamp range**: 0.05 to 0.95 (5% to 95%)

### Example Calculations

#### Hand Drill (Base 30%, DC 0)

| Skill Level | Calculation | Success Chance |
|-------------|-------------|----------------|
| 0 | 0.30 + (0 - 0) × 0.1 = 0.30 | 30% |
| 1 | 0.30 + (1 - 0) × 0.1 = 0.40 | 40% |
| 2 | 0.30 + (2 - 0) × 0.1 = 0.50 | 50% |
| 3 | 0.30 + (3 - 0) × 0.1 = 0.60 | 60% |
| 5 | 0.30 + (5 - 0) × 0.1 = 0.80 | 80% |
| 7 | 0.30 + (7 - 0) × 0.1 = 1.00 → 0.95 | 95% (clamped) |

#### Bow Drill (Base 50%, DC 1)

| Skill Level | Calculation | Success Chance |
|-------------|-------------|----------------|
| 0 | 0.50 + (0 - 1) × 0.1 = 0.40 | 40% |
| 1 | 0.50 + (1 - 1) × 0.1 = 0.50 | 50% |
| 2 | 0.50 + (2 - 1) × 0.1 = 0.60 | 60% |
| 3 | 0.50 + (3 - 1) × 0.1 = 0.70 | 70% |
| 5 | 0.50 + (5 - 1) × 0.1 = 0.90 | 90% |
| 6 | 0.50 + (6 - 1) × 0.1 = 1.00 → 0.95 | 95% (clamped) |

#### Flint & Steel (Base 90%, DC 2)

| Skill Level | Calculation | Success Chance |
|-------------|-------------|----------------|
| 0 | 0.90 + (0 - 2) × 0.1 = 0.70 | 70% |
| 1 | 0.90 + (1 - 2) × 0.1 = 0.80 | 80% |
| 2 | 0.90 + (2 - 2) × 0.1 = 0.90 | 90% |
| 3 | 0.90 + (3 - 2) × 0.1 = 1.00 → 0.95 | 95% (clamped) |

---

## CraftingRecipe Properties

### Skill Check Properties

```csharp
public double? BaseSuccessChance { get; set; } = null;  // null = always succeeds (100%)
public int? SkillCheckDC { get; set; } = null;          // null = no skill check modifier
```

**Default behavior** (both null): Recipe always succeeds (100%)

**BaseSuccessChance only**: Fixed probability with no skill modifier
```csharp
BaseSuccessChance = 0.50  // Always 50%, regardless of skill level
SkillCheckDC = null
```

**Both properties**: Skill-modified probability
```csharp
BaseSuccessChance = 0.30  // 30% base
SkillCheckDC = 0          // Modified by (SkillLevel - 0) × 0.1
```

### Related Properties

```csharp
public string RequiredSkill { get; set; } = "Crafting";  // Skill name to check
public int RequiredSkillLevel { get; set; } = 0;         // Minimum level to attempt
```

**RequiredSkillLevel**: Gates recipe availability (can't attempt if too low)
**RequiredSkill**: Determines which skill to check and award XP to

---

## RecipeBuilder API

### WithSuccessChance(double baseChance)

Sets the base success rate for the recipe.

**Parameters:**
- `baseChance`: Base probability (0.0 to 1.0, typically 0.30 to 0.90)

**Example:**
```csharp
new RecipeBuilder()
    .Named("Hand Drill")
    .WithSuccessChance(0.30)  // 30% base rate
    .Build();
```

### WithSkillCheck(string skillName, int difficultyClass)

Adds skill-based modifier to success rate.

**Parameters:**
- `skillName`: Name of skill to check (e.g., "Firecraft", "Hunting", "Crafting")
- `difficultyClass`: Target skill level (DC), typically 0-3

**Example:**
```csharp
new RecipeBuilder()
    .Named("Bow Drill")
    .WithSuccessChance(0.50)       // 50% base
    .WithSkillCheck("Firecraft", 1) // +10% per level above DC 1
    .Build();
```

**Note**: `.WithSkillCheck()` automatically sets `RequiredSkill`, so you don't need both `.WithSkillCheck()` and `.UtilizingSkill()`.

### Complete Example

```csharp
new RecipeBuilder()
    .Named("Hand Drill Fire")
    .WithDescription("Spin a wooden drill against a board to create friction and embers")
    .WithPropertyRequirement(ItemProperty.Wood, 0.5)     // 0.5kg wood
    .WithPropertyRequirement(ItemProperty.Tinder, 0.05)  // 0.05kg tinder
    .WithSuccessChance(0.30)        // 30% base success
    .WithSkillCheck("Firecraft", 0)  // +10% per Firecraft level
    .TakingMinutes(20)              // 20 minutes to attempt
    .ResultingInLocationFeature(
        new LocationFeatureResult("Campfire",
            loc => new HeatSourceFeature(loc, heatOutput: 15.0)))
    .Build();
```

---

## CraftingSystem Integration

### Skill Check Execution

Location: `Crafting/CraftingSystem.cs` (lines ~42-60)

```csharp
// After consuming ingredients
if (recipe.BaseSuccessChance.HasValue)
{
    // Calculate success chance
    double successChance = recipe.BaseSuccessChance.Value;

    if (recipe.SkillCheckDC.HasValue)
    {
        var skill = _player.Skills.GetSkill(recipe.RequiredSkill);
        double skillModifier = (skill.Level - recipe.SkillCheckDC.Value) * 0.1;
        successChance += skillModifier;
    }

    // Clamp to 5-95% range (never guaranteed, never impossible)
    successChance = Math.Clamp(successChance, 0.05, 0.95);

    // Perform the check
    bool success = Utils.DetermineSuccess(successChance);

    if (!success)
    {
        // Award failure XP (1 XP)
        _player.Skills.AddExperience(recipe.RequiredSkill, 1);
        return (false, $"Failed {recipe.Name} (earned 1 XP)");
    }
}

// Success - award normal XP and create result
_player.Skills.AddExperience(recipe.RequiredSkill, /* XP based on recipe difficulty */);
```

### Key Behaviors

1. **Materials consumed before check**: Failure costs materials (realistic)
2. **Clamp at 5%-95%**: Always some chance of success/failure
3. **Failure XP**: 1 XP awarded on failure to encourage learning
4. **Success XP**: Higher XP awarded on success (typically 2-5 XP)

---

## Utils.DetermineSuccess()

Location: `Utils/Utils.cs` (line 17)

```csharp
public static bool DetermineSuccess(double chance)
{
    return (random.NextDouble() < chance);
}
```

**Parameters:**
- `chance`: Probability of success (0.0 to 1.0)

**Returns:**
- `true` if random roll succeeds
- `false` if random roll fails

**Example:**
```csharp
bool success = Utils.DetermineSuccess(0.70);  // 70% chance of true
```

---

## Skill System Integration

### Checking Skill Level

```csharp
var skill = player.Skills.GetSkill("Firecraft");
int level = skill.Level;       // Current skill level
double experience = skill.Experience;  // Current XP
```

### Awarding XP

```csharp
// On success
player.Skills.AddExperience("Firecraft", 3);  // 3 XP

// On failure
player.Skills.AddExperience("Firecraft", 1);  // 1 XP
```

### Skill Leveling

XP requirements for leveling (standard progression):
- Level 0 → 1: 10 XP
- Level 1 → 2: 25 XP
- Level 2 → 3: 50 XP
- Level 3 → 4: 100 XP
- etc.

**Example progression** (Hand Drill at 30% base, 3 XP success, 1 XP failure):
- Start: 0 XP, Level 0, 30% success rate
- After 10 XP: Level 1, 40% success rate
- After 35 XP: Level 2, 50% success rate
- After 85 XP: Level 3, 60% success rate

---

## Action System Integration

### Displaying Success Rates

```csharp
// Calculate and display success chance
var skill = player.Skills.GetSkill(recipe.RequiredSkill);
double successChance = recipe.BaseSuccessChance.Value;

if (recipe.SkillCheckDC.HasValue)
{
    successChance += (skill.Level - recipe.SkillCheckDC.Value) * 0.1;
}

successChance = Math.Clamp(successChance, 0.05, 0.95);
int displayPercent = (int)(successChance * 100);

Output.WriteLine($"{recipe.Name} ({displayPercent}% success chance)");
```

### Example Output

```
Choose a fire-making method:
1. Hand Drill (30% base, +10% per Firecraft level) - 20 minutes
   Requires: 0.5kg Wood, 0.05kg Tinder

2. Bow Drill (50% base, +10% per Firecraft level) - 45 minutes
   Requires: 1.0kg Wood, 0.1kg Binding, 0.05kg Tinder

3. Flint & Steel (90%) - 5 minutes
   Requires: 0.2kg Firestarter, 0.3kg Stone, 0.05kg Tinder
```

---

## Design Patterns

### When to Use Each Approach

#### No Skill Check (Default)
```csharp
new RecipeBuilder()
    .Named("Sharp Rock")
    // No WithSuccessChance() = always succeeds
    .Build();
```

**Use for:**
- Simple crafting (combining items)
- Infrastructure (shelters, features)
- Material processing

#### Fixed Probability
```csharp
new RecipeBuilder()
    .Named("Random Event")
    .WithSuccessChance(0.50)  // Always 50%
    // No WithSkillCheck() = no skill modifier
    .Build();
```

**Use for:**
- Random events
- Luck-based outcomes
- Non-skill activities

#### Skill-Modified Probability (Recommended)
```csharp
new RecipeBuilder()
    .Named("Hand Drill")
    .WithSuccessChance(0.30)         // 30% base
    .WithSkillCheck("Firecraft", 0)  // +10%/level
    .Build();
```

**Use for:**
- Fire-making
- Hunting
- Advanced crafting
- Any skill-based activity

---

## Balance Guidelines

### Choosing Base Success Rates

**30% (Difficult)**
- Very challenging for beginners
- Requires ~5 skill levels to feel reliable (80%)
- Example: Hand Drill (primitive fire-making)

**50% (Moderate)**
- Challenging but fair for beginners
- Requires ~3 skill levels to feel reliable (80%)
- Example: Bow Drill (improved fire-making)

**70% (Easy)**
- Accessible for beginners
- Quickly becomes reliable (1-2 levels)
- Example: Advanced tools with good materials

**90% (Very Easy)**
- Nearly guaranteed even for beginners
- Minimal skill requirement
- Example: Flint & Steel (advanced fire-making tech)

### Choosing Difficulty Classes

**DC 0 (No Penalty)**
- Everyone starts with base success rate
- Skill only provides bonuses
- Most common for essential survival tasks

**DC 1 (Minor Penalty)**
- Beginners start 10% below base rate
- Reaching DC cancels penalty
- Use for slightly advanced techniques

**DC 2-3 (Major Penalty)**
- Beginners start 20-30% below base rate
- Significant skill investment required
- Use for advanced/expert techniques

### XP Award Guidelines

**Failure**: Always 1 XP
- Encourages experimentation
- Learning from mistakes

**Success (Simple)**: 2-3 XP
- Quick, low-time-investment actions
- Example: Hand Drill (20 min)

**Success (Moderate)**: 3-5 XP
- Medium time/material investment
- Example: Bow Drill (45 min)

**Success (Complex)**: 5-10 XP
- High time/material investment
- Multi-step processes

---

## Common Recipes

### Fire-Making Recipes

```csharp
// Hand Drill (primitive, difficult)
new RecipeBuilder()
    .Named("Hand Drill Fire")
    .WithSuccessChance(0.30)
    .WithSkillCheck("Firecraft", 0)
    .TakingMinutes(20)
    .ResultingInLocationFeature(/* campfire */)
    .Build();

// Bow Drill (improved, moderate)
new RecipeBuilder()
    .Named("Bow Drill Fire")
    .WithSuccessChance(0.50)
    .WithSkillCheck("Firecraft", 1)
    .TakingMinutes(45)
    .ResultingInLocationFeature(/* campfire */)
    .Build();

// Flint & Steel (advanced, easy)
new RecipeBuilder()
    .Named("Flint & Steel Fire")
    .WithSuccessChance(0.90)
    .WithSkillCheck("Firecraft", 2)
    .TakingMinutes(5)
    .ResultingInLocationFeature(/* campfire */)
    .Build();
```

### Hunting Check (Future)

```csharp
new RecipeBuilder()
    .Named("Track Deer")
    .WithSuccessChance(0.40)
    .WithSkillCheck("Hunting", 0)
    .TakingMinutes(60)
    .ResultingInItem(() => /* deer encounter */)
    .Build();
```

---

## Testing Checklist

When adding skill checks:

- [ ] Base success rate feels appropriate (test at level 0)
- [ ] Progression feels rewarding (test at levels 1-3)
- [ ] 95% cap reached at reasonable level (not too early/late)
- [ ] Failure XP is awarded (1 XP)
- [ ] Success XP is awarded (2-5 XP)
- [ ] Materials consumed on failure (not refunded)
- [ ] Recipe only available when skill requirement met
- [ ] Display shows correct success percentage
- [ ] Clamping works (no >95% or <5% displayed)

---

## Design Rationale

### Why 5%-95% Clamp?

**100% success rejected:**
- Removes all tension
- Makes skill progression meaningless at high levels
- Unrealistic (even experts fail sometimes)

**0% failure rejected:**
- Pure RNG gates are frustrating
- Removes player agency
- Can block essential survival tasks indefinitely

**5%-95% selected:**
- Always some hope (never truly impossible)
- Always some risk (never complacent)
- Matches reality (experts rarely fail, but it happens)

### Why +10% Per Level?

**Smaller bonuses (+5%) rejected:**
- Too slow progression
- 10 levels to go from 30% → 80%
- Feels grindy and unrewarding

**Larger bonuses (+15-20%) rejected:**
- Too fast progression
- Reaches 95% cap too quickly
- Skill investment feels less meaningful

**+10% per level selected:**
- ~5 levels to go from 30% → 80% (challenging → reliable)
- Clear, noticeable improvement per level
- Reaches 95% cap at level 6-7 (reasonable for endgame)

### Why 1 XP on Failure?

**No XP on failure rejected:**
- Punishes experimentation
- Feels unfair when materials consumed
- Discourages trying difficult recipes

**Full XP on failure rejected:**
- Removes penalty for failure
- Can exploit for easy leveling
- Doesn't reflect realistic learning (failure teaches less)

**1 XP on failure selected:**
- Acknowledges learning from mistakes
- Still provides progression after bad luck
- Small enough to not be exploitable
- Feels fair given material cost

---

## Future Enhancements

Potential improvements not currently implemented:

### Quality Modifiers
- Material quality affects success rate
- Good tinder (+5%), Poor tinder (-5%)
- Tool quality affects crafting

### Environmental Modifiers
- Weather affects fire-making (-10% rain, -20% snow)
- Temperature affects manual dexterity
- Wind affects success rates

### Critical Success/Failure
- 5% chance: Critical success (bonus reward, extra XP)
- 5% chance: Critical failure (extra material loss, potential injury)

### Team Bonuses
- NPC assistance provides success bonus
- Teaching mechanic (mentor provides +10%)
- Collaborative crafting

### Diminishing Returns
- XP from same recipe decreases with overuse
- Encourages trying new recipes
- Prevents grinding single recipe

---

## See Also

- [crafting-system.md](crafting-system.md) - Complete crafting system documentation
- [fire-management-system.md](fire-management-system.md) - Fire-making recipe examples
- [action-system.md](action-system.md) - Displaying skill check results to player
- [complete-examples.md](complete-examples.md) - Full fire-making implementation examples
