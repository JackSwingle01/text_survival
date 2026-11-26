# A body is read in tiers: what an injury stops

Spec for issue #55, part of the map #49. Written 2026-11-24 from the grilling with Jack. A dated record
of what was planned; lasting terms are in `GLOSSARY.md` (capacity, faculty, vitality, consciousness,
unconscious, crippled, lame, deprivation), and the lasting decision is the record "A body is read in
tiers, and the world reads only its faculties". It builds on "Conditions are derived, never stored",
"No special cases", "The NPC AI uses the Sim's public interface" and "The UI shows what the game knows".
It settles the parity-audit item BOD-17.

## Problem Statement

The old game made you wait out a crippling injury: at a tenth of your moving capacity every action was
blocked. The rebuild refuses only walking, and nobody decided that.

Looking at why showed the body is read inconsistently. Walking is refused below a tenth of moving, but
with both arms destroyed a player still butchers, a quarter slower. One destroyed arm costs nothing at
all, because work slows in a single step at one half. An unconscious player can chop wood. Travel and
making feel a blow to the head, and foraging and butchering do not, because some verbs read the body's
faculties and others reach past them to a capacity.

Underneath, one number leaks everywhere. Vitality is the least of consciousness, blood pumping and
breathing, and all four faculties multiply by it. So a player who has been awake sixteen hours, a normal
day, loses more than half of their strength, speed and perception.

## Solution

An injury stops what that part of the body does, and nothing else. With no working leg you cannot walk,
forage, fell or build, but you can still butcher, make things and tend a fire. With no working arm you
cannot do hand work, including feeding yourself. Any working limb, however weak, lets you try, slowly.
Unconscious, you can do nothing and time passes.

Everything the game asks of a body, it asks through four faculties: strength, speed, dexterity and
perception. A verb is refused when the faculty it needs is at zero, and takes longer the further that
faculty is below a fresh body's. The slowing is gradual from the first wound, so the player feels an
injury worsen and mend. A refusal says why.

A body is read in tiers, each reading only the one below, so nothing leaks: a foggy head no
longer drains your strength through vitality, and losing blood still weakens you.

## User Stories

### What an injury stops

1. As a player, I want a leg injury to stop only what legs do, so that I am not locked out of knapping
   flint by a broken leg.
2. As a player, I want hand work refused when no arm works at all, so that losing both arms costs more
   than a quarter of my time.
3. As a player, I want any working limb, however weak, to let me try, so that I am never refused by a
   number I cannot see.
4. As a player, I want walking refused only when no leg works at all, so that one bad leg means slow
   travel and not being stranded.
5. As a player, I want one destroyed arm to slow my hand work, so that the injury costs something.
6. As a player, I want work to slow gradually as a limb worsens and speed up as it mends, so that I can
   feel the injury change.
7. As a player, I want the worst a failing limb can do to hand work to be doubling its time, so that a
   wound is a heavy cost and not a wall.
8. As a player with a bad leg and a bad arm, I want a job that needs both to go at the pace of the worse
   one, so that two wounds do not multiply into an impossible time.
9. As a player, I want a refusal to say what stops me (no working leg, no working arm, lost blood, too
   heavy a load, unconscious), so that I know what to mend.
10. As a player, I want foraging and salvage to need a working hand as well as legs, so that the same
    rule holds for every job done with hands.
11. As a player, I want searching and tracking refused when I can perceive nothing, so that I never
    spend hours on a search that cannot find anything.
12. As a player, I want eating and drinking to need a working hand but never to take longer for a weak
    one, so that an injury can stop me feeding myself but a sore arm never starves me slowly.
13. As a player, I want waiting, resting and sleeping never refused, so that I can always let time pass.

### Unconsciousness

14. As a player, I want every action that needs a faculty refused while I am unconscious, so that I
    cannot chop wood while out cold.
15. As a player, I want time to pass while I am unconscious until I come round or die, so that being
    knocked out is a danger and not a pause.
16. As a player, I want unconsciousness to come from a blow to the head, lost blood, cold, shock or
    pain, so that it follows from what happened to me.

### What reaches what

17. As a player, I want a foggy head to stop draining my strength and speed by a second route, so that
    a blow to the head or a short night costs me once and not twice.
18. As a player, I want lost blood and failing breath to weaken my limbs and my head, so that bleeding
    out makes me weak.
19. As a player, I want a blow to the head to make my hands clumsy and my senses dull, so that being
    dazed touches every job the same way.
20. As a player, I want strength to depend on my limbs and my build, so that a strong body stays strong
    when it is only sleepy.
21. As a player, I want darkness to dim what I perceive and how well I work with my hands, without
    counting as an injury, so that night does not make me look wounded.

### Animals and other people

22. As a player, I want an animal with no working leg to be unable to walk, the same as me, so that a
    crippled animal stays where it fell.
23. As a player, I want a predator to pick the weakest animal by one overall read of each body, so that
    the wounded, the wasted and the unconscious all look like easy prey.
24. As a player, I want an NPC to be refused and slowed by exactly the rules I am, so that a wounded
    companion behaves like a wounded person.
25. As a player, I want an animal to eat and drink with its mouth, so that the rule about hands does not
    starve every animal in the valley.

### Reading a body on screen

26. As a player, I want the health panel to keep showing what my body can do, part by part, so that I
    can see what a wound has taken.
27. As a player, I want one overall read of how capable my body is, so that I can tell at a glance how
    bad things are.
28. As a player, I want that overall read not to drop at night or under a heavy pack, so that it tells
    me about my body and not my surroundings.

### Developers and tuning

29. As a developer, I want everything outside a body to read only its four faculties, so that a change
    inside the body cannot change a verb by a route nobody designed.
30. As a developer, I want each tier of a body to be its own type, so that the structure is visible in
    the code.
31. As a developer, I want the build to fail when a tier reads the wrong tier, so that the next leak is
    caught the day it is written.
32. As a developer, I want the places that reach past the faculties today on a list that only shrinks,
    so that the migration can land in pieces.
33. As a developer, I want tests held to the same rule as the sim, so that a test does not break when
    the inside of a body changes and no faculty does.
34. As a developer, I want no time ever computed for a body that cannot do the thing, so that a zero
    never reaches a division.
35. As a developer, I want every faculty to be a real number at or above zero for any body at all, so
    that no formula downstream has to defend itself.
36. As a developer, I want "is this body alive" to be its own read, so that a paralysed, blind body is
    not taken for dead.
37. As a developer, I want a before-and-after report of the faculties for a set of reference bodies, so
    that the balance change is seen before it is merged.

## Implementation Decisions

### The tiers

- A body is read in five tiers, each reading only the one below: state and effects; blood and breath;
  capacities; faculties; vitality.
- **State** is what a body stores: tissue and organ condition, blood volume, core temperature, fat and
  muscle, effects. Nothing here changes.
- **Blood and breath** stop being capacities. They are read from the heart, the lungs, blood volume and
  the effects that name them, and they feed the capacities. The two rules that exist today move here
  unchanged: lost blood scales consciousness, moving and manipulation, and breath below three tenths
  lowers consciousness.
- **Capacities** are moving, manipulation, consciousness, sight and hearing.
- **Faculties** are strength, speed, dexterity and perception.
- **Vitality** is read from the four faculties.
- **Alive** is a separate read from heart, lungs and brain tissue, with no effects. It exists today
  under another name and keeps its rule.
- Digestion feeds healing only, as today. It stays inside the body and is not one of the five
  capacities.

### Zero means the part gives nothing

- Moving comes from the legs and manipulation from the arms. The rest of the body scales what the limbs
  give; it no longer adds a share of its own. So no working leg is moving at zero, and any working leg
  is above zero. The same holds for arms. A four-legged body is built the same way from its legs.
- A body is unconscious when consciousness is at zero, not below a tenth as today. An unconscious
  body's limbs give nothing: moving and manipulation are at zero.
- The rule that consciousness below a tenth cuts moving and manipulation to a tenth is removed.
- Effects keep subtracting from the capacities they name, as today. Deprivation keeps its current sizes
  and its stacking until the deprivation ticket.

### The faculties

- **Strength** reads moving, manipulation and build. It no longer reads vitality. It stays above zero
  on an unconscious body, on purpose.
- **Speed** reads moving, build and load, and strength for how heavy the load feels. It no longer reads
  vitality. It is held at zero and never goes below it.
- **Dexterity** reads manipulation and consciousness, with darkness and wet hands as today. The
  starting weight of consciousness is a half: dexterity is manipulation times (a half plus half of
  consciousness). It no longer reads vitality. The separate "dazed" checks in making and fire starting
  are removed, because dexterity now carries them.
- **Perception** reads sight, hearing and consciousness, with darkness as today. Consciousness counts
  once, not twice.
- A faculty is "whole" when it equals a fresh body of the same species and sex. Verbs measure a faculty
  as that ratio, held at one for a body better than fresh.
- A faculty at zero comes with its reasons, as a list a refusal can carry: no working limb, lost blood,
  the load, unconscious, blind and deaf. This extends the list dexterity already gives for its costs.

### Vitality

- Vitality is the mean of the body's faculties, each as its ratio to a fresh body of its kind, read with
  no surroundings: no darkness, no wet, no load.
- A faculty that a fresh body of the species has at zero is left out. A wolf has no hands, so its
  vitality is read from strength, speed and perception.
- Nothing inside a body reads vitality. Its readers are a predator choosing the weakest of a herd, and
  the screen.

### Refusing and slowing

- A verb is refused when a faculty it needs is at zero. The refusal is data, like every other: the
  reason, and what is missing.
- A work verb's time is its whole-body time divided by (a half plus half of the faculty's ratio). That
  runs from one to two times the whole-body time. It replaces the two steps at one half.
- Where a verb is slowed by two faculties, the lower ratio sets the time.
- Walking keeps its present rule: crossing time is proportional to speed. A body at zero is refused before
  any time is computed. A body above zero walks at no less than a tenth of a fresh body's speed of its
  species and sex, its load counted (`SpeciesTable.MinSpeedPct`, the floor running uses too). Without it, a
  body just above zero would owe a crossing of days, and an animal whose leg mends within a minute would go on
  owing it (#288).
- A crossing goes at the walker's speed each minute: each minute walked covers one over the minutes the whole
  crossing would take at that minute's speed, load, pace and weather, so a wound, a load dropped or a leg
  mended part way across counts from that minute. A forecast agrees with the walk for a body and world that do
  not change on the way.
- Anything that turns a faculty into a time asks whether the body can do it first. This holds for
  forecasts and for route planning, which compute times without stepping a verb.
- A chase keeps its floor on running speed for a body that can run. Running speed and burst cost are
  never computed for a body whose speed is at zero.

| Verb | Needs above zero | Slowed by |
|---|---|---|
| Walk | Speed | Speed, proportionally |
| Forage | Speed, dexterity | Speed |
| Fell and gather wood | Speed, dexterity | Speed, dexterity |
| Salvage | Speed, dexterity | Speed |
| Butcher | Dexterity | Dexterity |
| Make something carried | Dexterity | Dexterity |
| Build something placed | Speed, dexterity | Speed, dexterity |
| Start a fire | Dexterity | Dexterity, through its odds |
| Search and track | Perception | Perception, through what is found |
| Eat, drink | Dexterity | Nothing |
| Wait, rest, sleep | Nothing | Nothing |

- Foraging loses its slowdown from breathing, since breath now reaches it through the capacities.
- Eating and drinking need dexterity only for a species with the hands ability. A body without hands
  eats and drinks with its mouth and needs speed above zero, so an unconscious animal still cannot.
- No crawl verb. No new rule for predators around a helpless body.

### The interface and how it is held

- The body's tiers become separate types. The faculties, vitality and alive are the body's interface
  to the rest of the sim.
- One named read-out gives the screen a body's capacities, blood and breath for display. Nothing else
  may call it.
- A dependency-rule library is added to the test project only. Which one is a research step at the
  start of the build. It holds these rules: nothing outside the body uses the capacities type;
  capacities do not depend on faculties; faculties do not depend on vitality; there are no cycles
  among the tiers.
- The places that read a capacity from outside the body today (about a dozen in the sim and about
  fifty in the tests) go on one exception list that only shrinks. It is a ratchet, with its section in
  the ratchets tracker. When the list is empty, the capacities type is hidden from the rest of the sim.
- The content checks hold that an effect names only capacities, blood or breath.
- No saved type is expected to change, because capacities and faculties are derived and never stored.
  If one does, the save-format guide applies.

### The balance report

- A small report prints the four faculties and vitality for a fixed set of reference bodies, before
  and after: whole; one arm destroyed; one leg destroyed; both arms; both legs; half blood lost; a
  blow to the head; sixteen and forty hours awake; a day and a half without water; starved to the
  floors. It reports and never gates, like the NPC bench report.

## Testing Decisions

- A good test drives the game the way a player does and reads what a caller can read: a verb's
  refusal, the minutes it took, a faculty. No test reads a capacity, blood or breath. Tests are held
  to the same dependency rule as the sim.
- **Activities stepped on a world, read through verbs and refusals**, is the main seam. It covers the
  verb table (each verb refused at zero and slowed as the table says), eating and drinking gated and
  never slowed, an unconscious body refused everything that needs a faculty, the lower faculty setting
  the time, a refusal carrying its reason, searching refused at zero perception, and the planner and
  forecasts asking "can it" before "how long". Prior art: the verbs test that every listed refusal
  matches what stepping does, and the travel test that a body which cannot walk is refused.
- **The body's faculties** is the second seam: build a body (wounds, blood, effects, build, load) and
  read the four faculties, vitality and alive. It covers no working leg as speed at zero and any
  working limb above zero, blood and breath feeding the capacities, lowered consciousness alone
  leaving strength and speed unchanged, strength above zero when unconscious, vitality ignoring surroundings and leaving out a faculty
  the species lacks, and alive holding for a body whose faculties are all at zero. Prior art: the body
  tests, which move up one tier from reading capacities.
- **Properties over any body** hold the bounds, at those two seams. For a body with any wounds, blood,
  effects, build and load:
  - every faculty is a real number at or above zero;
  - a fresh body's faculties are above zero wherever its species has them, so the ratio to fresh never
    divides by zero;
  - any time computed from a faculty is either refused or a positive whole number of minutes;
  - a work verb's time is between one and two times its whole-body time;
  - a chase's running speed and burst cost are real numbers for any body that can run;
  - a making odds table sums to one for any dexterity above zero.

  Prior art: the body survival and effects property tests. They run on one fixed seed in the PR checks
  and on fresh seeds at ten times the iterations nightly.
- **Dependency rules** sit with the architecture tests, beside the rule that the NPC uses only the
  public interface. They hold the tier order and the shrinking list.
- **Content load checks** hold that every effect names only capacities, blood or breath.
- The balance report has a test that it runs and prints every reference body; its numbers are not
  asserted.
- Tests run in Release with the class filter during the build, and the fast suite before a push.

## Out of Scope

- How hunger, thirst and tiredness reach a body and how large they are; the tiredness bank and how
  sleep repays a debt; fat burning before muscle; the healing gate (BOD-18); exhaustion sleep; lethal
  thirst and hunger; the shared 30% threshold. All go to a new grilling ticket, "Deprivation rethink",
  which starts with a sim probe.
- What a body with zeroed faculties does in a fight, and how a hit damages a body: the body damage
  rethink (#153).
- Treatment and medicine (#56), and whether destroyed organs heal (BOD-16, in #58).
- A crawl.
- Redesigning the health panel. It keeps what it shows.
- Retuning any effect's sizes.

## Further Notes

- The grilling settled the structure. These details were not put to Jack and are my picks, to be
  checked in review of this spec:
  - vitality as the mean of the faculties, not the least of them;
  - a faculty's ratio held at one for a body better than fresh, so nobody works faster than whole;
  - the floor on speed in the crossing-time division removed, so a very weak leg can make a crossing
    take many hours;
  - digestion kept inside the body, outside the five capacities;
  - eating and drinking gated by speed for a body without hands. Without some such rule every animal
    is refused food, because a four-legged body's manipulation is zero today.
- Until the deprivation ticket, deprivation still subtracts from moving and manipulation. So a body
  with one working leg can still be made lame by thirst and tiredness together, as it can today. For
  the same reason a tired body still loses about a quarter of its strength through its limbs; a short
  night leaving strength whole is that ticket's promise, not this one's. The
  grilling leaned toward deprivation reaching the limbs only through consciousness; that is the next
  ticket's to settle.
- The balance moves even with no effect retuned, because vitality leaves the four formulas. A model of
  two days with no food, water or sleep put strength at 16% today. The model was a copy of the sim's
  formulas, not a run of the sim; the balance report replaces it with real numbers.
- The dependency-rule library is a new kind of check for this repo. The existing architecture tests
  read source text; a library that reads the compiled code also catches a value passed through a
  helper.
- A chart of the tiers and the modelled curves from the grilling are not in the repo.
