# Skill System

*Created: 2024-11*
*Last Updated: 2025-12-20*

How skills affect gameplay and the probability formula for skill checks.

---

## Status: Vestigial

**Skills are currently deprioritized.** The skill system exists and is used in a few places (fire-starting, combat), but is not a core design focus. The design philosophy emphasizes player knowledge as progression rather than character stats that improve over time.

Skills may be revisited later, but current development focuses on other systems.

---

## What Exists

Skills are tracked per player via `SkillRegistry`:

```csharp
public class SkillRegistry
{
    public Skill Fighting { get; }
    public Skill Endurance { get; }
    public Skill Reflexes { get; }
    public Skill Defense { get; }
    public Skill Hunting { get; }
    public Skill Crafting { get; }
    public Skill Foraging { get; }
    public Skill Firecraft { get; }
    public Skill Mending { get; }
    public Skill Healing { get; }
    public Skill Magic { get; }  // Unused
}
```

Each skill has:
- **Level** — Current skill level (starts at 0)
- **XP** — Experience points toward next level
- **LevelUpThreshold** — XP needed for next level (Level * 10)

**Key Files:**
- `Skills/Skill.cs` — Skill class
- `Skills/SkillRegistry.cs` — Player's skill collection

---

## Where Skills Are Used

### Fire-Starting

Location: `Actions/GameRunner.cs:330-400`

Fire tools have base success chances modified by Firecraft skill:

```csharp
double baseChance = GetFireToolBaseChance(tool);  // 30%, 50%, or 90%
var skill = ctx.player.Skills.GetSkill("Firecraft");
double successChance = baseChance + (skill.Level * 0.1);
successChance = Math.Clamp(successChance, 0.05, 0.95);
```

| Tool | Base | Level 0 | Level 3 | Level 7 |
|------|------|---------|---------|---------|
| Hand Drill | 30% | 30% | 60% | 95% (capped) |
| Bow Drill | 50% | 50% | 80% | 95% (capped) |
| Fire Striker | 90% | 90% | 95% (capped) | 95% (capped) |

XP awarded: 3 on success, 1 on failure.

### Combat

Location: `Combat/CombatManager.cs`

Skills affect combat outcomes:
- **Fighting** — Attack skill bonus
- **Reflexes** — Dodge chance
- **Defense** — Block effectiveness

### Hunting/Stealth

Location: `Actors/Player/StealthManager.cs`

Hunting skill affects stealth calculations during stalking.

---

## Skill Check Formula

The standard formula used throughout:

```csharp
double successChance = BaseChance + (SkillLevel * 0.1);
successChance = Math.Clamp(successChance, 0.05, 0.95);
bool success = Utils.DetermineSuccess(successChance);
```

**Key properties:**
- **+10% per level** — Each skill level adds 10% success chance
- **5%-95% clamp** — Never guaranteed, never impossible
- **Base rates vary** — 30% (hard) to 90% (easy)

### Example: Hand Drill Fire

| Skill Level | Calculation | Success Chance |
|-------------|-------------|----------------|
| 0 | 0.30 + (0 × 0.1) = 0.30 | 30% |
| 1 | 0.30 + (1 × 0.1) = 0.40 | 40% |
| 3 | 0.30 + (3 × 0.1) = 0.60 | 60% |
| 7 | 0.30 + (7 × 0.1) = 1.00 → 0.95 | 95% (capped) |

---

## XP and Leveling

### Gaining XP

```csharp
player.Skills.GetSkill("Firecraft").GainExperience(3);
```

### Level Thresholds

XP needed per level: `Level * 10`

| Level | XP Needed | Total XP |
|-------|-----------|----------|
| 0 → 1 | 0 | 0 |
| 1 → 2 | 10 | 10 |
| 2 → 3 | 20 | 30 |
| 3 → 4 | 30 | 60 |
| 4 → 5 | 40 | 100 |

---

## Design Notes

### Why 5%-95% Clamp?

- **95% cap** — Even experts fail sometimes. Maintains tension.
- **5% floor** — Never truly impossible. Maintains hope.

### Why +10% Per Level?

- Noticeable improvement per level
- ~5 levels to go from "challenging" (30%) to "reliable" (80%)
- Reaches cap at level 6-7 (reasonable for long-term play)

### Why 1 XP on Failure?

- Learning from mistakes
- Softens frustration of material loss
- Small enough to not be exploitable

---

## Future Considerations

If skills are revived as a focus:

- **More skill checks** — Hunting, foraging, crafting could use skill modifiers
- **Quality modifiers** — Material quality affects success
- **Environmental modifiers** — Weather, temperature affect checks
- **Diminishing returns** — XP decreases with repetition

---

**Related Files:**
- [overview.md](overview.md) — System overview (mentions vestigial status)
- [action-system.md](action-system.md) — Runner architecture
