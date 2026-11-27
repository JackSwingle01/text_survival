# Deprivation wears through the body: hunger, thirst and tiredness

Spec for issue #206, part of the map #49. Written 2026-11-27 from the grilling with Jack. A dated record
of what was planned; lasting terms are in `GLOSSARY.md` (deprivation, sleep debt, consciousness,
faculty), and the lasting decision is the record "Deprivation wears through the body". It builds on "A
body is read in tiers, and the world reads only its faculties", "Conditions are derived, never stored",
"No special cases" and "The NPC AI uses the Sim's public interface". It takes over BOD-18 (the healing
gate) from #58, and the parts of the tiers spec (#55) that it left out of scope.

## Problem Statement

Going without food, water or sleep wears a body down in ways that make no sense, and they show up in
every normal day of play. These numbers come from a probe of the real sim, a fresh human body at rest
with no cold:

- **A normal day ends exhausted.** The energy store drains one unit a minute awake and is empty at hour
  16. The tired effect starts at hour 11 and is full at hour 16, which leaves speed at 55%, strength at
  72% and perception at 45%. Every player and every NPC goes to bed crippled, every night.
- **Deprivation takes away limbs.** The three effects subtract from moving and manipulation directly,
  so one destroyed leg plus full thirst and tiredness takes moving to zero. The walk is then refused
  with the reason "no working limb", although the leg is not the cause: the body cannot walk to the
  water it needs.
- **Starving is close to free for a month.** A body burns all its fat before any muscle. The hungry
  effect holds at 0.14 for the whole fat phase, about 5% off moving, and a game-start body keeps more
  than 90% of its speed for about 60 days. Then muscle falls in one slide to a floor measured against
  the body's current weight, a floor that keeps shrinking as the body does.
- **Sleep repays a flat two minutes a minute**, so a deep debt clears no faster than a shallow one.
- **One 30% threshold drives all three**, and the NPCs' rest and food wants keep a second set of
  thresholds of their own.

## Solution

Deprivation wears a body down through the physiology it already models, and never stops a verb on its
own.

- **Tiredness** dulls the mind. You get clumsy and slow to notice, but your legs and arms keep their
  strength.
- **Thirst** dulls the mind and thins the blood, so a badly dehydrated body is weak as well as slow to
  think.
- **Hunger** is a pang when the stomach runs empty. The long cost of starving is wasting: from the first
  week, a starving body loses muscle along with fat, and its strength and speed follow the muscle it
  has left.

A normal day costs nothing. An all-nighter is felt, and two days awake leave a body clumsy and half
blind, but still able to walk. Each deprivation ends in an explicit event: tiredness in collapsing
asleep, and thirst and hunger in death. No body is ever too thirsty to walk to water. It can still
collapse and die of thirst on the way.

## User Stories

### Tiredness

1. As a player, I want a normal waking day to cost me nothing, so that bedtime is a choice and not a
   cliff I go over every evening.
2. As a player, I want staying up past about 18 hours to start making me clumsy and dull, so that an
   all-nighter is a real cost.
3. As a player, I want two days awake to be the worst tiredness gets, so that the scale has an end I can
   plan around.
4. As a player, I want tiredness to leave my strength and speed whole, so that a short night makes me
   clumsy, not weak.
5. As a player, I want tiredness to lower my dexterity and perception, so that a tired body botches fine
   work and misses signs.
6. As a player, I want a deep sleep debt to clear in one long night, so that I recover from an
   all-nighter without losing two days to it.
7. As a player, I want a shallow debt to repay at today's rate, so that a normal night's sleep is still
   eight hours.
8. As a player, I want to risk collapsing asleep where I stand only after about a day and a half awake,
   so that the collapse means real exhaustion.
9. As a player, I want the chance of collapse to rise as I stay awake longer, so that pushing on is a
   gamble that gets worse.

### Thirst

10. As a player, I want thirst to start being felt at half my water store, so that I have warning before
    it bites.
11. As a player, I want thirst at its worst to dull my mind and weaken my body through my blood, so that
    dehydration feels like dehydration.
12. As a player, I want thirst alone never to stop me walking, so that I can always try to reach water.
13. As a player, I want an empty water store to go on killing me as it does today, so that thirst stays
    the fastest deprivation to death.
14. As a player, I want thirst to lower my blood only in how my body reads it, not in what a wound has
    taken, so that drinking restores it at once and thirst never counts as bleeding.

### Hunger

15. As a player, I want hunger to be felt when my stomach is running empty, so that I know to eat.
16. As a player, I want the hunger pang to cost a little focus and no strength, so that a missed meal is
    a nuisance and not an injury.
17. As a player, I want starving to cost me muscle from the first week, so that I feel it long before
    the end.
18. As a player, I want a lean body to waste faster than a well-fed one, so that what I had in reserve
    matters.
19. As a player, I want my strength and speed to fall with the muscle I lose, so that a starved body is a
    weak body.
20. As a player, I want starvation to kill me after about two months with water and rest, so that the
    timeline matches real starvation.
21. As a player, I want death from starvation to come when my muscle is half gone, so that the end has a
    cause I can see coming.

### Never stopped by deprivation alone

22. As a player, I want deprivation alone never to take a faculty to zero, so that I am never refused by
    hunger, thirst or tiredness without being told what happened.
23. As a player with one working leg, I want to keep walking however hungry, thirsty and tired I am, so
    that one wound plus a bad day does not strand me.
24. As a player, I want a refusal to name its real cause, so that "no working limb" never appears when my
    limbs work.
25. As a player, I want each deprivation's ending to be its own event (collapsing asleep, or death), so
    that the worst outcomes are things that happen to me, not a number that silently reaches zero.

### Healing

26. As a player, I want healing to slow when I am hungry or thirsty instead of stopping, so that a meal I
    missed does not freeze my wounds.
27. As a player, I want healing to stop only when my water store is empty or my core is cold, so that
    the hard stops are the real ones.
28. As a player, I want to heal while awake without having to keep my energy above half, so that being
    a little tired does not stop my wounds closing.

### Starting a game

29. As a player, I want to start a game already short of food and water and an hour into my day, so that
    I have a reason to act from the first hour.

### Animals and other people

30. As a player, I want NPCs to wear down by exactly the rules I do, so that a tired companion acts tired
    the same way I do.
31. As a player, I want an NPC to want sleep at a normal bedtime and need it once tiredness begins, so
    that NPCs keep human hours.
32. As a player, I want an NPC to count as hungry when the hunger pang begins, so that it eats when I
    would.
33. As a player, I want animals to tire, thirst, starve and waste by the same rules, with their own
    numbers, so that a starving wolf is thin and weak and not just hungry.

### Developers and tuning

34. As a developer, I want hunger, thirst and tiredness read from the body's stores each minute, not
    stored as fading effects, so that a deprivation lifts the moment its need is met and adds nothing to
    a save.
35. As a developer, I want no deprivation to name a limb's capacity, so that the tiers stay clean: limbs
    are lost only to wounds, blood and wasting.
36. As a developer, I want each deprivation's size and onset as named, tunable values beside its rule,
    so that a balance pass changes one number.
37. As a developer, I want the onset and full hours of tiredness to be species values, so that a species
    with a different day can be tuned without code.
38. As a developer, I want one threshold source for both the body and the NPC's wants, so that the two
    never drift apart.
39. As a developer, I want the balance report to print a deprivation timeline for reference bodies, so
    that a change in the curves is seen before it is merged.
40. As a developer, I want property tests that deprivation alone never zeroes a faculty and that any
    working leg walks, so that the promise is held for any body.

## Implementation Decisions

### The three routes

- A deprivation acts only through consciousness, blood, or wasting. No deprivation names moving,
  manipulation, sight or hearing. Sight and hearing lose nothing directly, and perception falls through
  consciousness.
- Each deprivation multiplies what it acts on by a factor that runs from one down to its size at full.
  Factors multiply with each other and with everything else. No factor reaches zero, so no
  combination of deprivations alone takes consciousness, moving or manipulation to zero.
- **Tired** multiplies consciousness by 0.5 at full.
- **Thirsty** multiplies consciousness by 0.7 at full. It also lowers the blood the body reads, as if 15%
  of it were lost, through the blood-and-breath tier. That lowers the blood capacity and the lost-blood
  scale, which then scales consciousness, moving and manipulation as the tiers spec says. Stored blood
  volume does not change. Alive reads stored blood with no effects, so thirst never reaches alive this
  way.
- **Hungry** multiplies consciousness by 0.9 at full.
- **Wasting** is the muscle a body has lost. It reaches strength and speed through build, as it already
  does. It adds no new term.
- The worst case, all three at full on a whole body: consciousness about 0.22 and moving 0.7. With one
  destroyed leg moving is about 0.35, so that body walks.

### Derived, not stored

- Hungry, thirsty and tired are read each minute from the body's stores: sleep debt, the water store and
  the calorie store. They stop being stored effects that fade at one an hour. They leave the effects
  file's capacity deltas, and the content checks hold that no effect names them.
- The discomfort saturation (the stacking with diminishing returns, capped at 0.8) covered only these
  three. It goes with them.
- The screen, narration and anything else that shows a deprivation reads its severity through the
  body's read-out, as it shows effects today.
- "Well fed" keeps waiting on minutes lived without the hunger pang.
- If a saved type changes (the energy store becoming the sleep debt, and stored deprivation effects
  disappearing from saves), the sample save fixture is regenerated per the save-format guide. There is
  no migration, per "No save migrations until the rebuild is finished".

### Tiredness is a sleep debt

- The energy store becomes a **sleep debt**: minutes awake not yet repaid by sleep. Awake, it rises one
  minute a minute. Asleep, it falls at the repay rate, down to zero.
- Tired severity is zero up to the onset (18 hours for a human) and rises in a straight line to one at
  the full hour (48 hours).
- **The repay rate** is the species' base rate (its waking hours over its sleeping hours, two for a human
  sleeping eight hours), scaled up for a deep debt. The scale is one at a debt of one waking day or
  less (16 hours for a human), rising in a straight line to two at the full hour. A human then repays two
  minutes a minute after a normal day, and four a minute from 48 hours, so a full debt clears in about
  12 hours of sleep.
- The onset and full hours are species values in the species table. Every species starts on the human
  values until #103 and #116 tune them.
- Sleep healing quality keeps reading how deep the debt is, as it does today.

### Thirst

- Thirsty severity is zero above half the water store, and rises in a straight line to one at an empty
  store. From full, a resting human starts to feel thirst at about hour 19, and it is full at about hour
  38.
- Past empty, the brain damage that already kills stays as it is: 0.03 an hour, unconscious about nine
  hours after empty, dead about 33 hours after.

### Hunger and wasting

- Hungry severity is the pang alone: zero above 30% of the calorie store, rising in a straight line to
  one at an empty store. The ramp from the fat and muscle reserves (twenty days of resting burn) is
  deleted.
- **Forbes' rule splits each day's deficit.** Once the calorie store is empty, each kilogram the body
  loses comes partly from muscle and partly from fat. The muscle share of that mass is
  `F₀ / (F₀ + fat kg)`, with Forbes' constant F₀ = 10.4 kg, as a named value. A body with 12 kg of fat
  draws about 46% of each kilogram from muscle (about 12% of the energy). With 2 kg of fat, the muscle
  share rises to about 84%. Energy per kilogram stays as today: 7,700 kcal for fat, 1,320 for muscle.
- The fat floor is removed: Forbes' share makes fat approach zero without reaching it. The muscle floor
  measured against current weight is removed.
- **Lethal hunger:** heart damage at its present rate (a twenty-fourth an hour) begins when the calorie
  store is empty and muscle has fallen below half of a fresh body's of the same species and sex. The
  target is death at around day 60 for a body at the species average fat, living a normal day
  of 16 hours awake and 8 asleep, with water.

### Side channels

- **The shared 30% threshold goes.** Each deprivation has its own onset, above.
- **The healing gate becomes scaling.** Healing is multiplied by (1 − 0.5 × hungry) × (1 − 0.5 ×
  thirsty). It stops entirely only at an empty water store or a core below 95 °F. The conditions on the
  calorie store, the water store and the energy store go, and so does the scaling by how full the calorie
  store is. The hungry term replaces it, since the pang is read from the same store. The scaling by
  digestion and the healing factor stays, and so does sleep's sixfold bonus.
- **Collapsing from exhaustion:** the chance per minute awake is zero up to 36 hours of sleep debt (for
  a human; the species value is three quarters of the way from onset to full), rising in a straight line
  to 1% at the full hour, and held at 1% beyond it. A collapse lasts 15 to 90 minutes and is stored as
  the Asleep effect, which the sim lives as sleep and which refuses other verbs, as today. The
  "Exhausted" event fires when the debt reaches the full hour.
- **Lethal thirst** is unchanged.

### Starting a game

- A new player body starts at three quarters of its calorie and water stores, as today, and one hour
  into its waking day (a sleep debt of 60 minutes), in place of 900 of 960 energy.

### NPCs and animals

- An NPC's rest priority is a Want at a sleep debt of one waking day (16 hours for a human) and a Need
  once tiredness begins (18 hours). Its food priority is a Need when the hunger pang begins. Both read
  the body's own onsets through its public interface. The NPC keeps no thresholds of its own.
- Animals live by the same rules through their needs step: the same sleep debt, routes, Forbes split
  and lethal channels, with their species' sleep hours and onset values.
- An animal's sleep-depth factor (how deep its sleep is where it lies) multiplies its repay rate, as it
  does today. See Further Notes.
- A predator's hunger, for boldness and desperation, reads the calorie store as it does today, and is
  unchanged.

### The balance report

The balance report gains a deprivation timeline. It reports and never gates.

- **No food, water or sleep, from full:** each faculty, vitality and the three severities, each hour for
  72 hours.
- **A normal day:** 16 hours awake and 8 asleep, fed and watered, each hour.
- **Starving with water and a normal day,** on the species-average body and on the game-start body:
  fat, muscle, the hunger pang, faculties and vitality each day until death, with the day and cause of
  death.
- **One destroyed leg with all three deprivations at full:** the faculties, and whether the body walks.

## Testing Decisions

- A good test steps a body or a world the way the game does and reads what a caller can read: the four
  faculties, vitality, alive, the stores, a deprivation's severity through the read-out, a verb's
  refusal and its reason. No test reads a capacity, blood or breath. Tests are held to the tiers'
  dependency rule.
- **A body stepped through time** is the main seam: step a body minute by minute with a context (awake
  or asleep, activity, warmth) and read through its interface. It covers:
  - the sleep debt's rise awake, the deep-debt repay rate, and the onset and full hours;
  - tiredness leaving strength and speed whole while lowering dexterity and perception;
  - thirst's onset, its consciousness and blood factors, and that stored blood and alive are untouched;
  - the hunger pang's onset and size;
  - Forbes' split: a leaner body losing a larger share from muscle, and strength and speed falling with
    muscle;
  - death from starvation when muscle is below half of fresh, and death from thirst unchanged;
  - collapse odds against the debt;
  - healing scaled by the deprivations and stopped only by an empty water store or a cold core.

  Prior art: the survival tests, the body survival property tests and the faculties tests.
- **Properties over any body**, at the same seam: for any wounds, blood, build and any mix of
  deprivations, no faculty is at zero unless a wound, lost blood, unconsciousness from another cause,
  the load or blindness puts it there. A body's strength and speed change with tiredness alone by
  nothing. They run on one fixed seed in PR checks and on fresh seeds nightly, like the existing body
  property tests.
- **Activities stepped on a world, read through verbs and refusals**, the tiers spec's seam: a body
  with any working leg is not refused walking, whatever its deprivations, and the reason is never "no
  working limb" while a leg works. A body collapsed from exhaustion is refused every verb but waiting
  until it wakes. Prior art: the crippled-verbs tests.
- **Priorities**, the NPC's wants: rest is a Want at one waking day of debt and a Need at tiredness's
  onset; food is a Need at the hunger pang's onset. Prior art: the priorities tests and the NPC need
  tests.
- The balance report has a test that it runs and prints every timeline; its numbers are not asserted.
- The NPC bench (20 seeds, 60 days) and the herd numbers are reported in each PR that changes the
  curves, beside the `baselines` branch. They are reports, not criteria.
- Tests run in Release with the class filter during the build, and the fast suite before a push. Tests
  pinned to the old curves are re-pinned to the new ones and named in the work log.

## Out of Scope

- How a hit damages a body: the body damage rethink (#153).
- Treatment and medicine (#56). The healing scaling here is what treatment builds on.
- Cold, heat and their effects. The healing gate keeps its cold-core stop.
- Per-species tuning of the onset hours, store sizes and sleep (#103, #116).
- How the player sees a deprivation on screen (#52). The read-out keeps showing what it shows.
- Water containers (#193).
- Whether a deprivation's severity should affect boldness or the planner beyond the NPC's rest and food
  wants.

## Further Notes

- The grilling settled every number above with Jack, 2026-11-26 and 2026-11-27. The sizes started
  from these references and are open to the first balance pass after the build:
  - **Sleep deprivation:** impairment begins at about 17 to 19 hours awake and is severe by 36 to 48
    hours, with microsleeps from about a day and a half. Recovery after 40 hours awake takes one long
    night.
  - **Dehydration:** thinking suffers from about 2% body-weight water loss. At 5 to 6%, plasma volume
    is down 10 to 15%. Death comes after about three days in mild conditions.
  - **Starvation:** the lean share of weight lost follows Forbes' rule. People on hunger strike die after
    about 45 to 75 days with water.
- These details were not put to Jack and are my picks, to be checked in review of this spec:
  - **Thirst's blood factor is large.** With the tiers' circulation rule (circulation fails at half the
    blood), reading 15% of blood as lost gives a lost-blood scale of 0.7. At full thirst that is moving
    0.7, and consciousness about 0.49 with its own 0.7 factor. That matches severe dehydration, but it is
    the largest single cost here.
  - **The repay scale reaching two at the full hour** generalises Q8's "two a minute to four a minute"
    to species with other sleep hours. So does the collapse onset at three quarters of the way from
    onset to full (36 hours for a human).
  - **The scaling by how full the calorie store is goes from healing,** because the hungry term reads
    the same store. Keeping both would count hunger twice.
  - **The animals' sleep-depth factor stays as a multiplier on the repay rate.** In the grilling it was to
    "fold into" the faster repayment, but the code says it is how deep an animal sleeps where it lies, a
    fact about the place and not the debt. Folding it in would drop that. If the review disagrees,
    removing it is one line.
  - **Forbes' rule here applies to the sim's muscle,** not to fat-free mass as in the literature, which
    also counts organs and water. The energy per kilogram of muscle (1,320 kcal) already allows for the
    water, so the split stays reasonable. If the reference body dies far outside days 45 to 75, the
    build tunes the muscle death line (half of fresh), not the constant, and reports it.
- A game starts short (three quarters of food and water), so in play every clock runs earlier than the
  timelines from full.
- The probe that produced the problem numbers stepped `Body.Update` minute by minute on a fresh human
  body at rest, with no cold, fed, watered and rested at the start. The balance report's timelines
  replace it.
