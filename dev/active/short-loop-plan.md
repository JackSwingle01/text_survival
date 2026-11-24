# Short-Loop Plan: Make Days 1–3 Great

*Status: planned. Implement after the clean-room rebuild (`documentation/clean-room-architecture.md`)
reaches phase 5. Built on `rebuild/Sim`, not the old code. A few cheap asks of the rebuild itself are
listed under [Asks of the Rebuild](#asks-of-the-rebuild); those should land during the rebuild.*

*How these features are presented on screen (the information hierarchy, layout, components and
visual language) is in [ui-design.md](ui-design.md), with a working prototype in
`dev/prototypes/ui/`.*

---

## Why

Players get bored in the first one to three days. The long horizon (the pass, the journey east
through the valley's basins, leading a band) doesn't matter if nobody stays long enough to reach it.
The short loop has to be satisfying on its own first.

What a blind playtest of the old game showed (seed 7, 11 in-game hours):

- **Nothing happened.** No events at all. `EventsPerHour = .15`, damped to 30% for the first 2 hours
  and 60% until hour 6: about 0.45 expected events in the first six hours.
- **The best moment was invisible.** The fire was due to die while I slept; Anu fed it. I only learned
  that by inspecting the tile.
- **Camp actions are deterministic conversions.** Cook, eat, improve shelter, add fuel: input in,
  known output out. No stake, no reveal, no risk, no visible payoff. They feel like menu traversal.
- **Camp investment never shows its return.** Nothing tells you what your shelter or bedding did.
- **Progression is invisible.** Skill level-ups are a commented-out `// todo`; only Firecraft and
  Hunting ever earn XP.

What works, and is the model for everything else: **foraging.** It is a bet (tile, focus, duration),
the bar reveals the result a little at a time, a luck streak sets the mood early, rare finds read
differently from the tally, dry runs are real, and the result is legible.

---

## Working Rules

1. **A bet shows its odds before you commit; the result just resolves.** Show the breakdown (base,
   plus or minus each factor, total) up front. No explanatory flavour text after: the outcome is a
   combination of factors the player already saw.
2. **Repeated information is an icon, colour, bar or map marker, never a sentence.** Text is for the
   first time and for the rare. A line heard 100 times is noise.
3. **No decision means one click or automatic.** Eating a known ration, moving items and melting snow
   are not bets.
4. **Anything automatic gives a receipt you can't miss:** one batched summary, never a stream of popups,
   and never silent.
5. **Background state shows on the map where it lives,** not as HUD clutter.
6. **Pace target: about 15 real minutes per waking day, including menus and decisions.** That's roughly
   one game hour per real minute overall. Bars stay fast (18 s per game hour now); the budget goes on
   decisions, so every click on a routine path spends it.
7. **Something resolves every couple of game hours.**

---

## Vocabulary

Not a framework to build. Words to design with.

**What makes an action feel good.** Pick two or three, not all five:

| Ingredient | Meaning |
|---|---|
| Stake | Something small, usually plentiful, spent up front: tinder, bait, fuel, time |
| The bar tells the story | The outcome comes out over time, not all at once at the end |
| Edge | A rare but real downside: running out, illness, smell drawing something in |
| Legible odds | The bet's factors are visible before committing (rule 1) |
| Growth | A skill tick, and a milestone you can see |

**Four shapes of bar:**

| Shape | Feel | Use |
|---|---|---|
| Trickle | Many small rolls add up; streaks; the occasional jackpot | Foraging, butchering, cooking |
| Gauntlet | One outcome that can fail at any point; near misses | Fire-starting, crafting, treatment |
| Vigil | A long wait, things happen along the way, a reveal at the end | The night, drying, traps |
| Tug | A meter that rises and falls while you push your luck | The stalk before a hunt |

---

## Features

Ordered by build priority. Each lists what it is, its hook into the rebuild, and when it's done.

### 1. Odds Sheet

Every bet shows its odds as factors before you commit: fire-starting first, then crafting, cooking
and treatment.

- Chance functions return their factors, not one number: e.g. `Odds(List<(string Factor, double Pct)>)`,
  with the total derived from the terms (clamped).
- Factors include the player's condition and the environment (dexterity: cold hands, wetness,
  darkness; impaired consciousness), not just tool and tinder.
- Shown as a segmented bar (green adds, red subtracts), with the numbers beside it.
- **Rebuild hook:** `Fire.StartChancePct` (`rebuild/Sim/Fire.cs:337`).
- **Done when:** the fire screen shows every term that moves the total; a test checks the terms add
  up to the total for some extreme cases.

### 2. Fire-Starting as a Gauntlet

The bar can stop partway on a failure, as in The Long Dark. Failing twice at 50% and then sweating at
90% is the point.

- The outcome is still rolled up front, so odds stay honest. On a failure, pick the stop point
  across the run, with some weight toward late stops so near misses happen.
- The bar stops where it failed, marked red. No staged failure text: a stop at 90% reads as a near
  miss without a word.
- A failure costs tinder plus the elapsed time only. Kindling is kept.
- **Try again** keeps the same tool and tinder, so retrying is one click.
- **Rebuild hook:** `StartFire` resolves on its first minute (`Activity.cs`, "Fire activities act on
  the first minute"). Have `Step` return `Interrupted` at the stop minute.
- **Done when:** over many seeded attempts, failure stop points spread across the bar, and success
  frequency matches the odds sheet.

### 3. Skills You Can See

- Skills live on `Agent`, so NPCs learn by the same rules ("one agent model").
- XP from: fire-starting, foraging, cooking, butchering, crafting. These all already exist as skills
  in the old registry; only Firecraft and Hunting earn XP there.
- A longer curve with smaller steps, so skill keeps growing over a whole run. The old 10% per level
  maxes out within a few levels.
- **Fix while porting:** the old `LevelUpThreshold => Level * 10` is 0 at level 0, so the first
  attempt grants a free level (clear bug: fix it and add a regression test).
- Feedback: a small skill icon with a pip bar that fills at the end of the action, and a short line
  on level-up. The skill's term appears on the odds sheet.
- Bonuses: foraging gives discovery speed or yield; cooking gives less fuel and fewer bad results;
  butchering gives yield; crafting gives fewer failures.
- **Done when:** each listed action awards XP in a test; level-up emits a `WorldEvent`; the odds sheet
  shows the skill's term.

### 4. Place Memory and Receipts

One feature merges three ideas: the morning report, the return-to-camp receipt, and field signs.
**"What happened here since you last saw it."** Sleeping counts as being away.

This builds out clean-room decision 7 ("One knowledge model per agent").

**Place memory (per agent: player, NPCs, herds).** Per tile the agent has seen:
- the last-seen minute
- state as seen, summarised and notable only: fire phase, snare states, carcass present, cache
  contents
- resources (what NPC `ResourceMemory` holds today)
- sightings of animals and people, with minutes (folds in old `PredatorObservations`)
- danger: where it got hurt, thin ice, a den

Store only tiles actually seen, and only notable state. Never copies of feature objects.

**Tile journal.** A bounded per-tile list of `WorldEvent`s, kept only for tiles someone has a stake
in: camp, and tiles with your snares, caches or carcasses. Written from the event stream.

**Receipt = belief versus truth on observation.** When an agent observes a tile (arrive, wake),
lazy catch-up runs, the journal plus a comparison against memory give the differences, and then memory
updates.

- **At camp, full information:** "Meat ready." "Water 1.2L." "Urk added 5kg of fuel." "Anu fed the
  fire at 3am." "Fire burned out at 2pm." "Urk ate 1kg of your dried meat."
- **In the field, evidence only,** derived from tracks and feature state: "Paw prints, heading north,
  a few hours old." "Your snare is sprung; fur, nothing in it." Old enough or snowed-over evidence
  tells you nothing.
- **Allies report everything they did** (decided for now; selective reporting can come later).

**Presentation:**
- Camp: one receipt card on arrival or waking, icon rows in the forage-result style, attributed by
  name. Batched, impossible to miss, logged to the journal.
- Field: a card only when *your* things changed. Otherwise signs show as icons on the map and in the
  tile popup.

**Belief sharing (last step).** When allies meet, memories merge: "Anu saw caribou east of the
creek." It gives NPCs meaningful things to say with no dialogue system, and makes a companion
valuable as a scout. Later, NPCs read the journal too: the commented-out `WitnessedTheft` and
`StoleFrom` memories come back.

- **Rebuild hooks:** `WorldEvent` stream (`rebuild/Sim/WorldEvent.cs`), lazy catch-up (decision 3),
  `StepResult.Events` (`rebuild/Sim/Sim.cs`).
- **Done when:**
  - After 4 h away, the camp receipt lists an NPC's `FuelAdded` and the `FireDied` at the minutes they
    happened.
  - A field tile visited before and after a herd passed shows the herd's sign; after enough erosion
    it shows nothing.
  - An NPC's memory lists a sighting the player also saw, after they meet.

### 5. The Map Draws What You Know

- The map renders the player's beliefs, not the truth (clean-room decision 7).
- **The fire on the camp tile:** the flame is sized and coloured by phase (roaring, steady, dying,
  embers, out). Away from camp it shows **as last seen**, fading with age. You know it was roaring
  three hours ago; the rest is your bet. Hover or tile popup gives time left as of last seen.
- No HUD fire bar while away. The HUD keeps the "+°F" when you're beside a fire.
- The same applies to carcasses, snares and sightings.
- **Done when:** a test leaves camp with a roaring fire, lets it die, and the player's map still shows
  it roaring until the player sees camp again.

### 6. The Fire as a Workbench

As with Don't Starve's crockpot: things produce at camp while you're away, so coming back pays off.

- Start with two jobs: **melt water** and **cook or dry meat**. Set up once from the fire screen.
- **Not punishing:** finished jobs wait for a long time; nothing overcooks while you're on a 2 h forage.
- **Collection is automatic** on returning to camp, and always shows on the receipt (feature 4).
- **Reminder when done:** a toast if you're at camp, plus an icon on the camp tile (progress ring while
  working, a mark when done).
- **Slots scale with fire size,** which ties it to the fuel bet.
- Light edge (later, optional): unattended meat smell can draw a scavenger.
- **Rebuild hooks:** jobs are fire or tile state processed by `Fire` during catch-up, emitting a
  `JobDone` event.
- **Done when:** a job set before a 2 h trip shows on the receipt and lands in inventory, at the right
  minute, with no menu step.
- *Amended 2026-11-24 (grilling for #54):*
  - A "job" here is a **transformation**, worked by a **station** (`GLOSSARY.md`).
  - Nothing is collected automatically. A finished thing stays at the fire until taken; the receipt,
    the toast and the tile icon say it is ready. So "lands in inventory ... with no menu step" is
    dropped from the done-when.
  - Room does not scale with fire size. It is fixed by pit type (open 2, mound 3, stone 4), and the
    fire's temperature sets how fast everything on it goes.
  - Cooking and melting by hand are removed; the fire does them.
  - The meat-smell edge is not optional: food at a fire or on a rack is exposed like a pile.

### 7. The Night as a Vigil

The camp's big daily bet. Every camp system feeds it: fire, shelter, bedding, food, companions.
It is the foraging structure, rolled hourly.

- **Before sleep:** how to lay the fire (bank to embers, one big log, or pile high to keep predators
  off), how much to eat, and whether an ally keeps watch. Show the odds that matter.
- **During:** the bar runs and short lines trickle in only when something happens ("2am: wind
  rising"). The night's mood is set early by weather.
- **Jackpots** (rare, discovery tone): a full snare, a fox drawn to scraps, a straggler drawn to your
  light, an ally back with a hare, a still night that barely burned fuel.
- **Busts:** fire dead at 4am, cold wakes you, something took the meat.
- **Morning:** the camp receipt (feature 4). This is where shelter and bedding visibly pay off: "the
  windbreak held."
- **Done when:** sleeping with a fire that can't last produces a real cost (cold, waking early) unless
  something intervenes, and the receipt says what intervened.

### 8. Cooking Quality and Crafting Luck

Light tiers, kept simple.

- **Cooking** scales the result's calories (and warmth): a crit gives more, charring gives less.
  Fat drips can add a little tallow. The cooking skill shifts the odds. A trickle bar.
- **Crafting** never varies item stats. A lucky craft refunds one material; an unlucky one fails,
  losing one material and the time. A gauntlet bar. Knapping is the obvious first case.
- Both show odds first (feature 1).
- **Done when:** the odds sheet reflects skill, and seeded runs produce all outcomes.

### 9. Cut Friction

- **Fire tending:** one **Build it up** action feeds kindling and then logs, replacing click-by-click
  feeding around the temperature thresholds. The fuel choice that matters (wet or green wood might
  smoulder, a big log might not catch) stays as a bet.
- **Click audit:** count clicks on each routine path (start fire, tend, eat, drink, store, cook, sleep)
  and push each toward one. Fold forage's trailing popups ("Discovery!", result screens) into the
  progress summary.
- **Text diet:** remove repeated lines. Candidates from the playtest: forage luck lines after the
  first, "You've learned to read the land", the "Discovery! New resources" popup, the 5pm "fire won't
  last" warnings (replaced by the map flame turning amber).
- **Done when:** each routine path is at or near one click in a scripted CLI run.

### 10. Event Rate

When events port (rebuild phase 4), revisit `EventsPerHour = .15` and the early-game damping. The
first hours are when the player most needs hooks. Start around 0.6 events per hour with no damping
and tune from playtests.

---

## Later Candidates

Sketched, not designed. Use the vocabulary above.

- **Butchering:** a trickle of yields (hide, meat, sinew, marrow), crits for a clean hide; scent
  rises the longer you work.
- **Trapping:** a vigil, then the snare line as a row of reveals (empty, catch, stolen, sprung).
  It's a reason to head out each morning.
- **Hunting:** a tug (stalk awareness), then one gauntlet throw. A near-camp hare as a day-1 hunt.
- **Treatment:** a gauntlet (a stitch can fail), with an infection timer alongside.
- **Allies:** small social bets (ask, gift), visible changes in opinion.
- **First threat:** wolf sign at your snares on day 2–3, building a tension.

## Day 1–3 Cadence (Target)

Not a script. Starting conditions and defaults make each beat *available*.

- **Day 1:** first fire (gauntlet), first knapped tool, snares set, first proper night (vigil plus
  receipt).
- **Day 2:** check snares (reveals), first small hunt, first cooked meal, meet an ally properly.
- **Day 3:** first real threat; first camp improvement visibly paying off.

Test: a next thing is always visible, and something resolves every couple of game hours.

---

## Asks of the Rebuild

Cheap now, expensive later. These should land during the rebuild, not after.

| Ask | Where | Why |
|---|---|---|
| Every `WorldEvent` carries a `TileId` | `rebuild/Sim/WorldEvent.cs` (`FuelAdded` has a fire but no tile) | So events can be filed in tile journals and knowledge |
| Catch-up stamps events with the minute they happened | Lazy catch-up (decision 3) | "Fire burned out at 2pm," not at the moment you arrived |
| Chance functions return factors | `Fire.StartChancePct` (`Fire.cs:337`) | Odds sheet (feature 1) |
| Pass skill and dexterity into the fire chance | `Fire.cs:389` calls `StartChancePct(a.Tool, a.Tinder)` with neither | The port currently ignores the player's condition (clear bug) |
| Skills on `Agent` | Phase 3 | Feature 3; NPCs learn too |
| Knowledge model scoped as in feature 4 | Phase 3 (the NPC AI needs it when `ResourceMemory` ports) | Receipts and the map from beliefs |

**Landed (rebuild phase 4).** Events carry `Tile`; catch-up stamps the minute things happened (fires,
melting snow shelters); `Odds` breaks out fire, throw, injury and forage-yield chances; the fire roll reads
Firecraft and dexterity. Skills live on `PersonMind`, not `Agent` as the row above says: only a person learns,
and kind-specific state lives in the mind (clean-room decision 2). Place memory (`Beliefs`), tile journals and
`Knowledge.Receipt` are in `rebuild/Sim/Knowledge.cs`, and the NPC's old memory now uses them. What is left out
is in `dev/active/rebuild-deferred.md` under "Knowledge, receipts and skills".

---

## Build Order (After the Rebuild)

| Step | Scope | Gate |
|---|---|---|
| 1 | Odds sheet, fire gauntlet with Try again, visible skills | Fire-start stop points spread across the bar; odds terms add up; XP from all listed actions |
| 2 | Place memory, tile journal, camp receipt | The 4 h-away receipt test passes |
| 3 | Map from beliefs, fire flame on the camp tile | The fire-as-last-seen test passes |
| 4 | Fire workbench (water, meat) | The 2 h-trip job test passes with no menu step |
| 5 | The night as a vigil | A fire that can't last costs you, and the receipt says what saved you |
| 6 | Cooking quality, crafting luck | All outcomes occur in seeded runs |
| 7 | Friction cuts and event rate | Routine paths near one click; a blind playtest of days 1–3 |
| 8 | Belief sharing between allies | The merged-sighting test passes |

Step 7's blind playtest is the real gate. Run it after each step if possible.

---

## Deferred: The Long Horizon

Recorded so it isn't lost. Build it once the short loop holds players.

- **Lead a band over the mountains:** stragglers scattered through the valley; winning is about
  who crosses with you.
- **Basins as chapters:** the valley layout's stages from west to east (`ValleyLayout`) set content,
  danger and identity, not distance from camp. Moving camp east becomes the main rhythm.
- **Connections between basins as chokepoints;** lane choice as a bet on risk; caves as shortcuts.
- **The tribe's trail** along the layout's routes: one sign per connection.
- **Seasons and herd migration** as the clock.
- **A predator that remembers you** (uses the knowledge model).
- **A chronicle** of the run at the end.
