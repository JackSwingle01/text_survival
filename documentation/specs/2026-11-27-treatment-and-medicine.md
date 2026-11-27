# Treatment and medicine: injuries, illness and remedies

Spec for issue #56, part of the map #49. Written 2026-11-27 from the grilling with Jack. A dated record
of what was planned; lasting terms are in `GLOSSARY.md` (injury, open, remedy), and the lasting decision
is the record "Injuries are the record; tissue condition is read from them". It builds on "No effect
needs a treatment the game doesn't have", "Damage is measured in tissue condition", "A body is read in
tiers, and the world reads only its faculties", "The UI shows what the game knows" and "The NPC AI uses
the Sim's public interface". It takes the remedy recipes as given from
`2026-11-24-recipes-that-make-sense.md`, and the healing scaling from `2026-11-27-deprivation-rethink.md`.
It answers parity audit items BOD-9 (frostbite's floor), BOD-10 (infection and fever), BOD-13 (teas)
and BOD-15 (treatment), and the loose end from #221 on where health ranks among an NPC's needs.

## Problem Statement

A wound in the rebuild has nothing to do about it, and nothing comes after it.

- **Bleeding has no answer.** A deep cut bleeds until it clots on its own. A destroyed limb bleeds at
  3000 ml an hour and kills in under two hours, and the only thing a person can do is sleep by a fire.
- **Ten medicines are foraged and nothing uses them.** Sphagnum, birch polypore, pine resin, usnea,
  amadou, chaga, willow bark, juniper, pine needles and rose hip are found, carried and weighed, and the
  Medicine forage focus brings more of them, for nothing. None of the remedy recipes the recipes spec
  settled is built.
- **A wound has no long tail.** There is no infection and no fever, so a wound is over once it stops
  bleeding, and there is no reason to keep a cut clean or covered.
- **Frostbite never heals.** It is a capacity penalty from a core near death, with a 5% floor that no
  action clears, against the decision record. It damages nothing, and mittens and boots do not
  prevent it.
- **A body cannot say which wound is which.** Tissue keeps one condition number and nothing about the
  blow, and bleeding is one number for the whole body. A bandage could only stop every bleed at once,
  an infection could not belong to the cut that festers, and a bruise and a cut look the same.
- **Healing does not fit its wound.** A shattered leg mends in 0.7 hours of good sleep, a scratch at
  the same rate as a fracture.
- **NPCs have no health need.** Health has no rank on their ladder; bleeding badly by a fire with nothing
  else critical, an NPC sleeps, because it has nothing else to do.

## Solution

Wounds become things in the world, illness comes from the world, and remedies answer both.

- **Injuries are the record.** Each hit leaves an injury where it landed (a region, and a site within
  it such as a hand), with its kind (cut, puncture, bruise, burn, frostbite, fracture), how deep it
  reaches, and whether it is open, bleeding, dressed or infected. Tissue condition is read from the
  injuries on it. Each injury heals at its kind's pace: a cut in days, a fracture in weeks.
- **Bleeding belongs to wounds.** Each injury bleeds and clots on its own, and the body bleeds their
  sum. A cut bleeds outside; a broken bone or torn organ under unbroken skin bleeds inside. A dressing
  goes on one open wound.
- **Wounds fester.** An open injury left undressed becomes infected, faster when the body is dirty or
  bloody. Past a point the infection stops fading and grows until it is cleaned, and a bad infection
  brings a fever that heats the body and dries it out.
- **Frostbite is an injury.** Cold air on poorly covered hands and feet damages their skin, and deeper
  when it goes on. It heals like any wound and can become infected. Mittens and boots prevent it.
- **Illness from the world.** A long, deep chill brings a cough that spoils sleep. Meat from a
  decaying carcass, or raw meat, can bring nausea.
- **Remedies are items, used like food.** The made remedies and the raw medicines are listed with what
  each would do to you now, and using one takes a minute to drink or five to dress a wound. Dressings
  stop bleeding and keep a wound clean while they are on; cleansers clear an infection; draughts ease a
  symptom for hours. Painkillers mask, dressings and cleansers cure, so pressing on and fixing it
  properly are different choices.
- **No effect keeps a floor that nothing clears.** Infection gains a floor together with the cleansers
  that clear it; Frostbite's floor goes with the Frostbite effect.
- **NPCs look after their wounds.** Health joins the ladder, first in rank, and an NPC dresses a bleed,
  cleans an infection and keeps dressings in stock.

## User Stories

### Injuries

1. As a player, I want each wound I take to be its own injury with a place and a kind, so that I know
   what happened to me and where.
2. As a player, I want the body screen to name my injuries ("a deep cut on the left forearm, dressed"),
   so that I can see what needs looking after.
3. As a player, I want a club to bruise or break bone without opening the skin, and a blade or a fang
   to open it, so that the kind of blow decides what the wound needs.
4. As a player, I want a blow that breaks bone to leave a fracture, so that a broken limb is its own
   kind of trouble.
5. As a player, I want two wounds on one arm to stay two wounds, so that each can be dressed, bleed
   and heal on its own.
6. As a player, I want my limbs and faculties to suffer from injuries exactly as they suffer from
   damage today, so that the change of record does not change how a fight goes.
7. As a player, I want a wound's site to be finer than its region (hand, forearm, upper arm; foot,
   shin, thigh), so that frostbite can take fingers and toes without the body growing new parts.
8. As a player, I want harm from cold, starvation and poison to count against my organs and tissue as
   it does today, without being a wound I could dress, so that only real wounds take remedies.

### Healing

9. As a player, I want a bruise to fade in three or four days, a shallow cut or puncture to close in
   two or three, a deep cut or burn in about six, frostbite in about ten and a fracture in two to
   three weeks, so that a wound's weight in the game matches what it is.
10. As a player, I want those times to hold for a body living a normal day, sleeping its nights,
    well fed and warm, so that I can plan around them.
11. As a player, I want sleep to heal faster than waking and a good bed faster than bare ground, so
    that resting up after an injury pays.
12. As a player, I want hunger, thirst and cold to slow healing as the deprivation rethink sets, so
    that a wound heals best when I am looked after.
13. As a player, I want each of my injuries to heal at the same time as the others, so that one bad
    wound does not hold every scratch open.
14. As a player, I want a broken limb to stay weak until the bone mends, so that a fracture shapes the
    weeks after it.
15. As a player, I want a destroyed organ to stay destroyed, as it does today, so that the change of
    record changes nothing there.

### Bleeding

16. As a player, I want each injury to bleed by what it destroyed and how deep it went, as wounds
    bleed today (a club's broken bone bleeding inside, under unbroken skin), so that a deep wound is an emergency and a scratch is not.
17. As a player, I want each injury's bleeding to slow as it clots on its own, so that small wounds
    stop without help.
18. As a player, I want my blood loss to be the sum of my wounds' bleeding, so that two wounds are
    worse than one.
19. As a player, I want the forecast of when my bleeding stops and how low my blood will fall to keep
    working, so that I can judge how urgent a wound is.
20. As a player, I want getting bloody and smelling of blood to follow my bleeding as they do today, so
    that predators still notice a bleeding person.

### Infection and fever

21. As a player, I want an open injury left undressed to become infected over time, so that a cut has
    a reason to be covered.
22. As a player, I want a larger wound to become infected faster, and a dirty or bloody body faster
    still, so that washing and keeping clean matter.
23. As a player, I want a dressing to stop a wound becoming infected while it is on, and a fibre wrap
    only to slow it, so that better dressings are worth their makings.
24. As a player, I want a mild infection to fade as the wound closes, so that a small slip is not a
    sentence.
25. As a player, I want an infection past a point to stop fading, keep growing and hold the wound open
    and unhealed until I clean it, so
    that a festering wound demands action.
26. As a player, I want an infected wound to hurt, so that I notice it.
27. As a player, I want a bad infection to bring a fever, and the fever to go once every infection is
    back below that point, so that the illness follows its cause.
28. As a player, I want a fever to heat my body, dry me out and cloud my mind, rather than kill me
    outright, so that an untreated fever kills through hyperthermia and thirst I can see coming.
29. As a player, I want a dirty, undressed deep cut to turn serious in about two days and a dressed one
    never to, so that the danger has a pace I can act within.

### Frostbite

30. As a player, I want frostbite to come from cold air and wind on my hands and feet, so that it
    strikes when I am out in the cold with them poorly covered.
31. As a player, I want mittens and boots to protect against frostbite by how warm they are, so that
    the gear I make matters.
32. As a player, I want wet hands and feet to frostbite sooner, so that wet mittens are a danger.
33. As a player, I want frostbite to damage the skin first and the muscle beneath when it goes deep,
    so that a long exposure is worse than a short one.
34. As a player, I want frostbite to heal like any wound, with no floor, so that it is not a permanent
    mark from a single cold night.
35. As a player, I want frostbite to be an open wound that can become infected, so that it needs care
    after the cold is over.
36. As a player, I want the narration to say "frostbitten fingers" or "frostbitten toes", so that I
    know what the cold did.

### Cough

37. As a player, I want a long, deep chill to bring on a cough, so that being hypothermic has an
    after-effect.
38. As a player, I want a cough to spoil my sleep and cost a little breath, so that it wears on me.
39. As a player, I want a cough to fade slowly on its own and faster with pine needles, so that it is
    an inconvenience I can answer.

### Nausea

40. As a player, I want meat butchered from a decaying carcass to carry that decay, so that what I
    butchered late is worse than what I butchered fresh.
41. As a player, I want eating meat that has gone off to risk nausea, more the further gone it is, so
    that scavenging an old carcass is a gamble.
42. As a player, I want raw meat to carry a small chance of nausea, and cooking to clear that chance,
    so that cooking is worth the fire.
43. As a player, I want cooking not to make spoiled meat safe, so that heat is not a cure for rot.
44. As a player, I want nausea to make me lose part of what I ate and some water, and cloud my mind, so
    that bad meat costs more than it gave.
45. As a player, I want severe nausea to stop me eating until it passes, so that I cannot eat through
    it.
46. As a player, I want nausea to pass in hours and juniper to ease it, so that it is a setback and
    not a death.

### Remedies

47. As a player, I want remedies to be items in my pack, so that I can make bandages before a hunt and
    carry them.
48. As a player, I want to use a remedy the way I eat food, picking one from a list that shows what it
    would do to me now, so that treatment is quick and clear.
49. As a player, I want only remedies that would help something I have to be offered, with what they
    would do, so that the list answers my state.
50. As a player, I want to choose which injury a dressing or cleanser goes on, with the worst bleeding
    or infected one first, so that I treat the wound that matters.
51. As a player, I want a dressing to cut the bleeding of the injury it goes on at once and help it clot
    faster while it is on, so that a bandage stops an emergency.
52. As a player, I want a dressing to wear off over hours and need changing, the sealed bandage lasting
    longest and staying on when wet, so that care continues after the first dressing.
53. As a player, I want the strength of each dressing to follow its makings (fibre wrap weakest, then
    polypore poultice, moss bandage and sealed bandage), so that better materials make better care.
54. As a player, I want a cleanser (usnea poultice, resin seal) to cut an injury's infection at once and
    keep it dressed, so that a festering wound can be saved.
55. As a player, I want willow bark tea to ease pain's penalties and halve a fever's heat for hours
    without ending the pain, so that I can press on while it lasts.
56. As a player, I want chaga tea to bring a fever down, so that a fever has a cure besides cleaning
    the wound.
57. As a player, I want pine needle tea to make a cough fade faster, so that a cough has an answer.
58. As a player, I want chaga and pine needle tea to leave me nourished, healing a little faster for
    hours, so that a warm tea after a hard day helps.
59. As a player, I want every tea to count as the water it was brewed with, so that drinking it is
    drinking.
60. As a player, I want raw medicines to be usable straight from the pack, weaker than the made
    remedies (moss as a weak dressing, amadou to stanch a bleed, usnea or resin against infection,
    chewed willow bark for pain, juniper for nausea, chewed pine needles for a cough), so that what I
    forage is useful in the field.
61. As a player, I want birch polypore and chaga to need their recipe, so that the hard fungi are worked
    before use.
62. As a player, I want rose hips to be food, not medicine, so that each foraged thing has one clear
    role.
63. As a player, I want drinking a tea or chewing a herb to take about a minute, and dressing a wound
    five, shown on the item, so that I know what treatment costs.
64. As a player, I want the remedy recipes to be made as the recipes spec sets (teas at a fire, in a
    water container, with 0.3 L of water; bandages with a cutting tool), so that making them makes
    sense.

### Effects answered by time

65. As a player, I want shock, dazedness, hypothermia and pain to fade on their own, with no remedy
    needed, so that no effect waits on a treatment the game lacks.
66. As a player, I want shock to fade faster while I sleep or rest by a burning fire, so that lying down
    warm is the treatment for shock.

### NPCs

67. As an NPC, I want health on my ladder, first in rank among equal priorities, so that a wound is
    weighed against cold, thirst and hunger.
68. As an NPC bleeding out, I want to use a carried dressing at once, wherever I am, so that I do not
    die with a bandage in my pack.
69. As an NPC bleeding out with no dressing, I want to keep today's choice (sleep by the fire at camp)
    or head for camp, so that I still do the best I can.
70. As an NPC with a bleed, an infection or a fever, I want to use a remedy that answers it, or make
    one from what I carry, or forage for medicine, so that I look after myself as a person would.
71. As an NPC healing at camp, I want to dress any undressed wound and keep two dressings in stock, so
    that I am ready for the next one.
72. As a player watching NPCs, I want them to treat their wounds through the same actions I have, so
    that what they do is what I could do.

### Animals

73. As a player, I want an animal's wounds to be injuries too, so that a wounded animal bleeds and
    heals from its real wounds.
74. As a player, I want an animal's healing to keep its pace from today, so that the change of record
    does not change the hunt.

### Developers and tuning

75. As a developer, I want tissue condition read from injuries to equal today's stored condition for
    the same blows, so that combat balance is untouched by the change.
76. As a developer, I want each injury kind's healing time, each remedy's strength and the infection
    and illness rates in content tables, so that tuning is a data change.
77. As a developer, I want what a remedy would do and what it does to come from one rule, so that the
    preview shown never differs from the outcome.
78. As a developer, I want a save to hold injuries and not tissue condition, so that there is one
    record.
79. As a developer, I want the effects table's load checks to forbid a floor on any effect no remedy
    clears, so that the decision record is enforced.
80. As a developer, I want reading a body's condition to stay cheap for every animal on the map each
    minute, so that the world does not slow down.

## Implementation Decisions

### Order

The work goes in seven tickets, in this order; the first blocks the rest.

1. **Injuries are the record.** The model, bleeding per injury, healing per kind, animals included.
   It follows the deprivation rethink's healing change (#206), which rewrites the same healing code.
2. **Dressings for bleeding.** The remedy use activity and screen, remedy items and recipes for the
   dressings, the dressed state, and the NPC health rank with its Critical response.
3. **Infection, cleansers and fever.** With the NPC's Need responses.
4. **Frostbite as an injury.** The Frostbite effect row and its floor go.
5. **Draughts.** Eased, Nourished, Soothed, the teas, raw medicines taken by mouth, and shock fading
   faster at rest.
6. **Nausea.** Meat carries its carcass's decay; the raw-meat chance; juniper.
7. **Cough.**

In the restore order (`dev/active/porting.md`) the series sits in step 4, consequences, alongside wear
and final death. The recipes it needs belong to step 3, reachability.

### The injury model

- An **injury** has: the region it is on; a site within it; a kind; the damage it did to each layer it
  reached (skin, muscle, bone, and any organ); its bleeding; whether it is dressed and for how long;
  and its infection.
- **Kinds** come from the blow: sharp makes a cut, pierce a puncture, burn a burn, and blunt a bruise,
  or a fracture when it damages bone. Frostbite comes from the cold (below). Harm from starvation,
  thirst, cold to the organs and poison is **internal**: it is kept the same way, but it has no site,
  is never open and takes no remedy.
- **Sites** are chosen within the region when the blow lands, weighted by size: hand, forearm, upper
  arm; foot, shin, thigh; and the region itself for head, chest and abdomen. Combat's hit tables do not
  change. The clothing slots still cover regions as today (hands and feet slots cover the arms and legs).
- **Open:** a cut, puncture, burn or frostbite injury is open while its skin damage is above a small
  threshold. A bruise, a fracture and internal harm are never open.
- **Tissue condition is derived.** A tissue's condition is one minus the damage of the injuries on it,
  floored at zero. Nothing stores it. Damage resolution (a tissue stops no more than it has left) reads
  the derived condition, so the same blows give the same conditions as today. The body may cache the
  derived values, invalidated by a new injury or by healing, so long as no reader can see a stale one.
- **A destroyed tissue stays destroyed** where it is today: destroyed organs never heal.
- **Healing** works per injury, all of a body's injuries at once. Each kind has a healing time in a
  content table (bruise 3–4 days; shallow cut or puncture 2–3; deep cut or burn 6; frostbite 10;
  fracture 14–21). Time scales with how deep the injury went. The times are calendar time for a body
  living a normal day: fed, watered, warm, sleeping eight hours. The base rate is set so that such a
  day, with sleep's bonus, gives those times; sleeping longer or in a better bed heals faster, and the
  deprivation rethink's scaling slows it. Bone's greater toughness no longer slows healing on its
  own: the fracture's healing time says how long bone takes.
- **A fully healed injury is removed.**
- **Bleeding** is set on the injury when the blow lands, by today's rule (what it destroyed, how deep,
  sharp and pierce bleeding however small, blunt bleeding only from bone and organs). Each injury clots
  at today's rate. The body's bleeding is the sum of its injuries', capped as today. The Bleeding effect
  row goes; every reader of bleeding (blood loss, Bloody, scent, predator boldness, forecasts,
  narration) reads the body's bleeding.
- **Closed injuries can bleed.** A bruise or fracture whose bone or organ bleeds is a closed bleed: it
  bleeds inside, clots on its own and takes no dressing. Only open injuries take dressings.
- **Pain, shock and dazedness** are still triggered by the blow as effects, as today.
- **Animals** move onto injuries too: one healing path. Their flat mending rate becomes their healing
  pace, so a hunt goes as it does today.
- **Saves** hold injuries, and no longer hold tissue condition. The fixture is regenerated per
  `documentation/save-format.md`.

### Infection and fever

- Each open injury has an **infection** from 0 to 1.
- While **undressed**, it builds at a rate set by the injury's open size, multiplied up by the body's
  Dirty and Bloody. A fibre wrap halves the rate; every other dressing stops it.
- **Below 0.4** it fades as the wound closes. **Above 0.4** it stops fading and grows, and the injury
  stops healing, so it stays open, until a cleanser brings the infection below 0.4; then healing
  resumes. This is the floor the decision record allows: the cleansers clear it.
- An injury infected above the threshold adds pain.
- **Fever** is an effect. It builds while the worst infection is above about 0.5 and fades once every
  infection is below. It adds heat to the core, drains water and costs consciousness. It never kills
  directly.
- The rates are set so that a dirty, undressed deep cut turns serious in about two days.

### Frostbite

- Each minute, each hand and foot reads the cold it feels: the air and the wind at it, less the
  insulation worn in its slot, worse when the body is wet. Past a threshold it adds frostbite damage to
  an injury at that site, to the skin first and the muscle when it goes on.
- Frostbite is an open injury kind and heals at its kind's pace. The Frostbite effect row, which was
  driven by the core and kept a floor, goes.

### Cough

- **Cough** is an effect. It builds each hour in proportion to how hypothermic the body is, the way
  Bloody follows bleeding.
- It lowers sleep quality, as being wet does, and costs a little breathing. It fades slowly on its own.
- Noise, once a noise signal exists, comes later.

### Nausea

- Meat butchered from a carcass carries the carcass's decay at the time it was cut. Carried meat does
  not decay further. This is new item state.
- Eating meat past a set decay has a chance of **Nauseous**, rising with the decay. Raw meat has a small
  chance of its own, and cooking clears that chance but not the decay's.
- Nauseous loses part of the stomach's contents and some water when it comes on, costs consciousness,
  and refuses eating while it is severe. It fades in hours.

### Remedies

- **Remedy items:** the made remedies are new items. The raw medicines are the existing Medicine
  resources, used by weight.
- **Recipes:** the remedy recipes are as in the recipes spec, in a new Medicine making category. The
  teas need a fire and a water container. A water-container capability is added, and the waterskin has
  it.
- **Kinds:**
  - A **dressing** goes on one injury, cuts its bleeding at once and leaves it dressed for a time.
    While dressed, it clots three times as fast, and its infection stops building (slows, for the fibre
    wrap). Wetness wears a dressing out twice as fast, except the sealed bandage.
  - A **cleanser** goes on one injury, cuts its infection at once and leaves it dressed.
  - A **draught** acts on the whole body through an effect that lasts hours.
- **Starting strengths** (the numbers are tuned later by a probe; the order and shape are decided):

  | Remedy | Kind | At once | While it lasts |
  |---|---|---|---|
  | Fibre wrap | dressing | bleeding −30% | dressed 12 h; infection slowed |
  | Polypore poultice | dressing | bleeding −50% | dressed 12 h |
  | Moss bandage | dressing | bleeding −60% | dressed 24 h |
  | Sealed bandage | dressing | bleeding −80% | dressed 48 h; stays on when wet |
  | Usnea poultice | cleanser | infection −0.4 | dressed 12 h |
  | Resin seal | cleanser | infection −0.3 | dressed 48 h |
  | Willow bark tea | draught | — | Eased 6 h |
  | Chaga tea | draught | fever down | Nourished 6 h |
  | Pine needle tea | draught | — | Soothed 12 h; Nourished 6 h |
  | Sphagnum, 50 g | dressing | bleeding −30% | dressed 6 h |
  | Amadou, 20 g | dressing | bleeding −40% | none |
  | Usnea, 20 g | cleanser | infection −0.15 | none |
  | Pine resin, 20 g | cleanser | infection −0.1 | none |
  | Willow bark, 30 g, chewed | draught | — | Eased 3 h at half strength |
  | Juniper, 20 g | draught | nausea −0.3 | none |
  | Pine needles, 30 g, chewed | draught | — | Soothed 6 h |

  Each tea also counts as 0.3 L drunk. Birch polypore and chaga have no raw use; rose hip is food.
- **New effects:** **Eased** makes pain's penalties much smaller and halves fever's heat. **Nourished**
  is healing +10%, and it stacks with WellFed's +5%. **Soothed** makes a cough fade three times as
  fast.
- **One rule for preview and outcome.** One function says what a remedy would do to a body (and to a
  chosen injury); the use screen shows it, and using the remedy applies it.
- **Using a remedy** is one new activity, like eating. It takes the remedy and, for a dressing or
  cleanser, the injury. Draughts take a minute; dressings and cleansers take five. It is offered when
  the body has something a carried remedy would help. The screen lists only those remedies, with what
  each would do, the time it takes, and for injury remedies the injuries it could go on, the worst
  bleeding or infected first.
- **The effects table's load check** refuses a treatment floor on any effect that no remedy clears.

### Effects answered by time

- Shock, Dazed, Hypothermia and Pain keep no floor and take no remedy beyond Eased's masking.
- Shock fades twice as fast while asleep, or at rest by a burning fire.

### NPCs

- **Health** becomes a stat on the ladder, first in order, so a tie in priority goes to the wound. Its
  priority keeps today's thresholds, extended: an infection above the threshold or a fever is Need, and
  an undressed open injury is Want.
- **Critical** (bleeding out): use a carried dressing at once, wherever it is. With none, today's rule
  (sleep by the fire at camp), else head for camp. Lands with ticket 2.
- **Need** (bleeding, an infection, a fever): use a carried remedy that answers it; else make one from
  materials in hand; else forage with Medicine focus. Lands with ticket 3.
- **Want:** at camp, dress any undressed open injury, and keep two dressings in stock.
- The NPC treats itself through the same activity the player uses.

## Testing Decisions

- **Good tests here** test what a body, a world or an NPC does, through the interfaces a caller uses:
  a blow lands, minutes pass, a remedy is used, and the test reads injuries, effects, faculties, blood
  or life. They do not reach into the derived-condition cache, the healing arithmetic or the order of
  the per-minute steps.
- **Three seams, all existing:**
  1. **The body:** a blow, the per-minute update with its survival context, and the body's reads.
     Injuries, healing per kind, bleeding, infection, fever, frostbite, cough and nausea are tested
     here. Prior art: `DirtyTests`, `SurvivalTests`, `BodyTests`, `DamageUnitTests`, and the effects
     property tests.
  2. **The world stepping an activity:** a bare test world, an agent and an activity. Using remedies,
     their time, making them through recipes, and eating risky meat are tested here. Prior art: the
     washing tests in `DirtyTests`, `MakeTests`, `FoodTests`.
  3. **The NPC mind's decision:** where health ranks and what an NPC does at each tier. Prior art:
     `NpcNeedTests`, `NpcWashTests`, `PrioritiesTests`.
- **The equality guard** (ticket 1): for the same blows on the same bodies, the condition read from
  injuries equals the condition stored today. It is checked against a recorded table of blows and
  results taken from the current code before the change, and the combat golden tests must pass
  unchanged.
- **Healing times** are checked against the table for a body living a normal day, stepped with the
  body's own update, not a world. Multi-day heals that cost more than the quick-test budget are tagged
  long-run.
- **The decision record** is checked by the effects table's load check: a test that a floor without a
  remedy is refused, and that the shipped table passes.
- **Preview equals outcome:** for each remedy, what the screen says it would do equals what using it
  did.
- **Saves:** the save round-trip tests and the regenerated fixture cover injuries and meat decay.
- Run tests in Release, and the test classes in hand for each cycle, as `CLAUDE.md` says.

## Out of Scope

- **Noise** from a cough, and noise as a signal at all.
- **Smoke** in shelters as a cause of cough.
- **Stagnant water** as a cause of nausea: it would need a water-quality model.
- **Carried food spoiling.** Meat carries its carcass's decay at butchering and no more.
- **Destroyed organs healing** (BOD-16), scars, amputation, and splints for fractures.
- **Treating someone else:** using a remedy on another person or a companion.
- **Final balance numbers.** The starting strengths and rates are tuned by a probe in the balance pass.
- **A blood trail from a wounded animal's real wound** (CMB-9): made possible, not built.
- **Resin waterproofing of gear** (BOD-2), and rose hip tea.
- **Events** that cause or resolve illness.

## Further Notes

- The old game's treatment numbers were never tested for balance or fun; only the remedies' names and
  themes carry over. Its bugs are not ported: the Treat button that appeared only for Bleeding, Burn or
  a non-existent "Infected", the Inflamed effect that nothing applied, the Nourished buff whose healing
  multiplier nothing read, and the NPC medicine stock that rounded to zero.
- An infection above the threshold stops its wound healing (decided after review, 2026-11-27); a slower
  heal while infected, or an infection that outlives the closed wound, were the alternatives, and may
  be revisited after the first probe.
- The roadmap's open item on a Bloody treatment floor is stale: Bloody has no floor, and washing is
  built.
