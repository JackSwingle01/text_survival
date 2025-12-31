# Text Survival — Design Principles

*A web-based Ice Age survival game where you've been separated from your tribe during the mountain crossing. Survive until midsummer when the pass clears — or gear up and attempt the crossing early.*

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

No carry-over between runs. The player gets better, not the character. You learn to read situations, learn when risks are worth taking, learn the systems. The game teaches through consequences—but only if the player can read cause and effect

*Example: An experienced player knows when 10 minutes of fire is actually fine, when "abundant" foraging is worth staying for, when to let a fire die on purpose to preserve embers.*

**Contextual dynamics**

The same event, resource, or location plays differently depending on your current state. This prevents "solving" the game and creates replayability.

*Example: A wolf sighting when healthy and close to camp is opportunity — you might hunt it. The same wolf when injured and far from camp is crisis.*

**Emergence from systems**

Stories come from mechanics intersecting, not authored sequences. Events are written, but they respond to context — what you're carrying, your physical state, where you are, what tensions are active.

The test for any new system: Is it deep/elegant, or is it shallow/inelegant? Elegance = emergent complexity /mechanical complexity. A shallow module has a complex interface relative to what it does. An inelegant mechanic has complex rules relative to the decisions it creates.

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

## Events

Events are the translation layer between simulation and felt experience. The survival math runs in the background; events make it tangible. Players don't experience mechanics—they experience the aesthetics those mechanics produce. Events bridge that gap.

**Communicate dynamics, not just outcomes.** "A wolf attacks" is an outcome. "You hear movement behind you—something has been following your blood trail" is a dynamic made visible. Events should surface *why* something is happening, not just *what*. This lets players update their mental models and make better decisions next time—closing the skill atom loop of attempt → feedback → learning → refined attempt.

**Elegance ratio applies here.** A good event system generates many meaningfully different situations from few rules. Events should connect to existing systems rather than creating parallel mechanics.

Events should:
- Trigger based on context (location, player state, weather, active tensions, etc.)
- Create decisions with tradeoffs and no clear right answer
- Build the world (discover locations, reveal information) not just tax the player

**Skill and preparation should matter.** Good events reward players who've built accurate mental models:
- What should you have brought? (preparation payoff)
- What can you spend to improve odds? (resource tradeoffs)  
- What details indicate the right choice? (reading the situation)

The feedback must be legible. When a player survives or dies, they should understand *why*—that's how learning happens.

**Tensions** create narrative arcs and shape **session-level pacing**. A tension is an unresolved narrative thread persists and escalates, producing rising interest curves that climax in resolution.

*You spot tracks (creates "Stalked" tension) → Later, something watches from the treeline (tension rises) → You glimpse movement following you (tension peaks) → Confrontation or escape (resolution, release).*

Emergent stories from player choices hit harder than scripted ones. Tensions are scaffolding for emergence.

The code should serve the event experience - don't limit event design to what currently exists. Effects, injuries, loot pools exist to support events.

## Pacing

**Interest curves operate at three scales:**

*Moment-to-moment* (seconds to minutes) — Each expedition choice, each event decision, each risk calculation. The fire-tether and intersecting pressures handle this.

*Session-level* (30-90 minutes) — The rhythm of a play session. Tension systems create rising arcs that climax in confrontation or escape. Safe recovery at camp punctuates dangerous expedition pushes.

*Campaign-level* (across the full run) — The emotional arc of the whole experience. Day 10 should feel different from day 5, not just harder. Pull goals provide this structure.

### Pull: Goals Beyond Survival

Survival is the context, not the goal. Pressure without aspiration creates stress, not engagement. The game needs *pull*—things the player wants to achieve, not just threats to avoid.

**The mountain crossing** — The pass is visible. Reaching it requires preparation: warmth, supplies, condition. You could attempt it underprepared. You'll probably die, but it's your choice. The crossing is the final exam for everything the game taught you.

**Megafauna hunts** — The mammoth coat, the bear-hide boots. Serious insulation comes from serious animals. Serious animals require serious preparation. These are skill checks that prove mastery.

**Exploration** — Distant locations that are genuinely hard to reach. The glacier, the cave system, the sheltered valley. Rewards for expanding your range and reading risk accurately.

**Camp investment** — Base building that persists within a run. Better shelter, storage, fire structures. Makes this camp worth defending—and leaving behind feel costly.

Pull goals work because they're visible, desirable, and just out of reach.

## Design Tests

The game works if:
- An experienced player reaches a meaningful decision in under 5 minutes
- Two players would reasonably choose differently in the same situation
- Resource management creates genuine tension without tedium
- Stories emerge from systems interacting, not authored sequences
- The player feels pulled toward goals, not just pushed by survival pressure