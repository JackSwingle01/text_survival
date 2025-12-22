# Text Survival — Design Principles

*A console-based Ice Age survival game where you've been separated from your tribe during the mountain crossing. Survive until midsummer when the pass clears — or gear up and attempt the crossing early.*

---

## Experience Goals

What we're optimizing for, in priority order:

1. **Immersion** — The world feels real and present. Text carries the weight that graphics would.
2. **Tension** — The player feels pressure. Decisions have stakes.
3. **Memorable moments** — Peaks of triumph and sorrow that stick with you. The barely-made-it-back. The wolf-got-me-50-meters-from-safety.
4. **Agency** — Your choices caused this outcome, not dice rolls.
5. **A living world** — Not formulaic or repetitive. The world has memory. Things develop over time.

Also: risk, struggle, attainment. The satisfaction of earning survival through skill and good decisions.

**Work backward from player experience.** Start with what decision this enables, what moment this creates, what tension this produces. Then build the systems to support it. If the player never sees a distinction, don't model it.

---

## Design Principles

**Compound pressure creates choices**

Single problems have solutions. Multiple overlapping problems force tradeoffs. A wolf isn't dangerous because it's a wolf — it's dangerous because you're bleeding, low on fuel, and far from camp.

*Example: You find good foraging, but you're already tired, it's getting dark, and your fire is burning low. Stay and risk the return trip, or leave with less than you need?*

**Time is the universal currency**

Everything costs time. Time depletes everything simultaneously — calories, hydration, warmth, fuel. This is how pressures compound. Standing still deciding costs you.

*Example: Fire is the most visible implementation. It burns whether you're there or not. But food works the same way — the deer carcass is rotting while you decide whether to butcher it or carry it whole.*

**Tradeoffs, not right answers**

There shouldn't be clean good or bad choices. Players weigh every decision. Time or resources? Safety or reward? Carry more or move faster?

*Example: You can butcher the deer here (time cost, but lighter carry) or drag it whole back to camp (faster now, but exhausting and leaves a scent trail).*

**Player knowledge is progression**

No carry-over between runs. The player gets better, not the character. You learn to read situations, learn when risks are worth taking, learn the systems. The game teaches through consequences, not menus.

*Example: An experienced player knows when 10 minutes of fire margin is actually fine, when "abundant" foraging is worth staying for, when to let a fire die on purpose to preserve embers.*

**Contextual dynamics**

The same event, resource, or location plays differently depending on your current state. This prevents "solving" the game and creates replayability.

*Example: A wolf sighting when healthy and close to camp is opportunity — you might hunt it. The same wolf when injured and far from camp is crisis.*

**Emergence from systems**

Stories come from mechanics intersecting, not authored sequences. Events are written, but they respond to context — what you're carrying, your physical state, where you are, what tensions are active.

*Example: You don't get a random wolf attack. You get a wolf encounter because you're in wolf territory, carrying meat, and the "Stalked" tension has been building for an hour.*

**Realism as intuition aid**

Physics-based modeling where possible. Players can reason about the world because it works like reality. Fire needs fuel and oxygen. Wet clothes conduct heat away. Heavy loads slow you down.

*Example: You don't need to memorize that birch bark is good tinder — it makes sense because birch bark is papery and oily. The game matches expectations.*

---

## Narrative Principles

The text IS the game. No graphics to carry weight.

**Brevity** — Laconic, punchy. Short sentences over flowery prose.

**Simplicity** — Germanic over Romance words. "Your hunger makes your hands shake" not "You're developing hypoglycemia."

**Second person, present tense** — "You find a good log." Immediate and embodied.

---

## Events

Events are the translation layer between simulation and felt experience. The survival math runs in the background; events make it tangible.

Events should:
- Trigger based on context (location, player state, weather, active tensions)
- Create player choices, not just outcomes
- Connect to existing systems rather than creating parallel mechanics
- Build the world (discover locations, reveal information) not just tax the player

Events should reward preparation and skill:
- What should you have brought? (preparation pays off)
- What can you spend to improve odds? (resource trades)
- What details indicate the right choice? (reading the situation)

**Tensions** create narrative arcs. A tension is an unresolved thread that persists and escalates:

*Example: You spot tracks (creates "Stalked" tension) → Later, something is watching you (tension increases) → You glimpse movement in the trees (tension peaks) → Confrontation or escape (tension resolves). The world remembered. The threat developed.*

The code should serve the event experience — don't limit event design to what currently exists. Effects, injuries, loot pools exist to support events.

---

## Pull: Goals Beyond Survival

Survival is the context, not the goal. The game needs aspiration, not just pressure.

**The mountain crossing** — The pass is visible. Reaching it requires preparation: warmth, supplies, condition. You could attempt it underprepared. You'll probably die, but it's a choice.

**Megafauna hunts** — The mammoth coat, the bear-hide boots. Serious insulation comes from serious animals. Serious animals require serious preparation.

**Exploration** — Distant locations that are genuinely hard to reach. The glacier, the cave system, the sheltered valley. Rewards for expanding your range.

**Camp investment** — Base building that persists within a run. Better shelter, storage, fire structures. Makes this camp worth defending.

Look for opportunities to create pull — things the player wants to achieve, not just threats to avoid.

---

## Design Tests

The game works if:
- An experienced player reaches a meaningful decision in under 5 minutes
- Two players would reasonably choose differently in the same situation
- Resource management creates genuine tension without tedium
- Stories emerge from systems interacting, not authored sequences
- The player feels pulled toward goals, not just pushed by survival pressure