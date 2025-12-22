# Text Survival — Technical Principles

*These principles exist to serve the experience goals in the Design Principles doc. Code is a means to an end.*

---

## Core Discipline

**Code serves experience**

Work backward from player experience. What decision does this enable? What moment does this create? If the player never sees a distinction, don't model it. The design doc defines what matters — this doc is about how to build it without losing the thread.

**Simple until forced otherwise**

Write direct, obvious code. If/else chains are fine. Avoid patterns requiring multi-file tracing. Concrete code is easier to refactor than wrong abstractions. Wait for 3 examples before abstracting.

**Extend before creating**

Can existing systems handle this, or does it genuinely need something new? The answer is usually "existing systems can handle it." Question every new abstraction.

---

## Architecture Principles

**Deep modules with simple interfaces**

Create abstractions that hide complexity behind clear, stable interfaces. Once the body system is built right, you just call `Body.Damage()` — you don't need to understand the organ hierarchy.

**Data objects shouldn't reference parents**

Features don't need to know their parent Location. If processing logic needs context, the processor receives both objects. Keep the dependency graph clean.

**Derived state over tracked state**

If state can be calculated from other state, calculate it. Don't track a separate property you have to keep in sync. Expedition phase should derive from path index, not be a separate enum.

**Simpler params beat complex return types**

Return an int when that's all you need, not a result object. Take two parameters instead of four when the processor can get what it needs from them.

**Features are data bags, processors have logic**

Features hold state. Processors calculate outcomes. Don't spread logic across both — pick one home for each responsibility.

---

## Development Practices

**Question artificial distinctions**

"Why are forage and harvest separate expedition types?" "Do sites and paths really need different classes?" Often the answer is no. Merge concepts until you have a concrete reason to separate them.

**Gut old code that predates current vision**

Don't preserve code out of inertia. If it was built for a different game direction, remove it. Dead code creates confusion.

**Think through scenarios before building**

Create concrete scenarios. Verify the architecture supports them. "Let's really make sure we've thought this through before we start."

**State machines should be obvious**

Draw them out. Derive don't track. If the state transitions aren't clear, the code will be buggy.

---

## Code Quality

**Single source of truth**

Don't repeat yourself. When you need to change how something works, there should be exactly one place to make that change.

**Predictability**

Function signatures and names signal behavior. No surprises, no magic side effects, no hidden retries. Code does what it looks like it does.

**Self-documenting**

Type hints + clear naming + explicit contracts > comments explaining confusing code. If it needs extensive comments, consider refactoring.

## Style Guidelines:

To prevent bugs, use explicit naming conventions for numeric values including the units, e.g. `timeMinutes`.
For abstract values and multipliers use the following convention:

| Suffix | Range | Baseline | Op | Example |
|---|---|---|---|---|
|**Delta**|-inf...inf|0|+|`CaloriesDelta`|
|**Factor**|0...inf|1|*|`WindFactor`|
|**Pct**|0...1|1|*|`CapacityPct`|
|**Level**|0...1|0|1-x|`InsulationLevel`|

---

## The Test

When evaluating a feature or fix:
1. Does this create a meaningful decision? (design doc)
2. Can existing systems handle it? (extend before creating)
3. Is this the simplest implementation that works? (simple until forced)

When in doubt: simple over complex, extend before creating.

---