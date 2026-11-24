# Recipes that make sense

Spec for issue #53, part of the map #49. Written 2026-11-24 from the grilling with Jack. A dated record of
what was planned; lasting terms are in `GLOSSARY.md` (stick, branch, firewood, hide strip, piece,
recipe), and it builds on the decision records "One Make activity for crafting and building",
"Resources are discrete or continuous", "No special cases" and "No effect needs a treatment the game
doesn't have". It settles the parity-audit items ITM-8, ITM-18 and ITM-20, and reverses FIR-5.

## Problem Statement

The rebuild has 25 item recipes and 8 camp builds against the old game's 71 recipes, and many of the ones
it has would strike a player as wrong. A spear takes 1.5 kg out of a 2 kg log. A knife eats 0.7 kg of
material and weighs 0.3 kg, while fire-hardening a spear adds 0.7 kg from nowhere. Item recipes count
kilos and camp builds count pieces, and they disagree on what a piece weighs, so a sleeping bag eats
about six hides. A shelter frame is four sticks. The missing recipes include the stone axe, bone knife,
bone shovel, tents, pouch and belts, every remedy and every repair.

The old list is no cure either: teas brewed with no fire or water, a fire striker of flint and amadou
with nothing to strike a spark, rose hips that treat pain, raw hide sewn onto finished clothes, bags
and tents sewn with no needle, an axe with exactly a knife's inputs. And the materials themselves are
too scarce to use: a day's foraging yields about 165 g of plant fibre, so one rope costs two days.

## Solution

Every recipe passes one test: an informed person sees nothing wrong in it. Sub-steps are left to the
imagination (a knife's hafting, a tea's hot stones), but nothing is made from the wrong thing, without
the tool it obviously needs, or out of thin air.

Things you count by hand are pieces. A recipe asks for a number of them, and any piece of the kind will
do. Things you gather by the handful are amounts. Wood comes as sticks and branches you pick up, and
firewood you can only cut with an axe. Stone is for crude edges, clubs, hammering and fire rings. A real
blade needs flint, or obsidian if you find the rare outcrop. An axe can be ground from plain stone over
an afternoon, or knapped from flint in half an hour. Everything sewn needs a needle, everything brewed
needs a fire and water, and repairs patch a share of the damage, never all of it.

Foraging gives realistic amounts, so everyday things (a rope, a torch, a snare) cost about an hour of
gathering and camp comforts about half a day.

## User Stories

### Making in general

1. As a player, I want every recipe to look like something a person could really make from what it
   takes, so that crafting feels like survival and not like a menu of arbitrary combinations.
2. As a player, I want recipes to ask for whole pieces (2 branches, 1 flint), so that I can see at a
   glance whether I have enough.
3. As a player, I want any piece of the right kind to work in a recipe whatever it weighs, so that I am
   never refused because my branch is 1.4 kg instead of 1.5.
4. As a player, I want handfuls of material (fibre, bark, resin) shown in grams below 500 g and in kilos
   to one decimal above, so that small amounts read naturally.
5. As a player, I want a made thing never to weigh more than what went into it, so that the pack's
   weight stays believable.
6. As a player, I want tools to wear with use but never vanish into a recipe, so that a knife is a knife
   and not an ingredient.
7. As a player, I want a recipe that needs a tool to name the capability (cutting, striking, digging,
   sewing), so that any tool that has it will do.
8. As a player, I want camp builds to work like every other recipe, in pieces and by capability, so that
   one menu and one set of rules covers all of making.
9. As a player, I want a thing I make to record the actual pieces it was made from, so that later
   repairs and salvage can use them.
10. As a player, I want material processing to give back a share of what went in (a big hide scrapes to
    more than a small one), so that a big find is worth more.
11. As a player, I want each product made from raw materials, with no hidden upgrade chain, so that I
    always know what a thing costs.
12. As a player, I want two recipes for two different products, but one recipe with alternatives when
    only the input differs, so that the list stays short and the choices are real.

### Wood

13. As a player, I want to pick up sticks and branches with my hands, so that I can build a fire and a
    shelter before I have any tools.
14. As a player, I want branches to be the wood for spears, hafts, poles and frames, so that I never make
    a spear from three twigs or a shelter from a log.
15. As a player, I want firewood to come only from axe work, so that the axe is a real step up in fuel
    supply.
16. As a player, I want branches and firewood to come in pine, birch and oak, so that the kind of wood
    still shapes my fire.
17. As a player, I want a felled tree to stay where it fell with its chopping progress, so that I can
    work it over several trips and carry the firewood home in loads.
18. As a player, I want to find dry deadfall often, so that I can gather branches by hand and, once I
    have an axe, cut firewood faster than felling a live tree.
19. As a player, I want deadfall and fallen trunks to run out, so that wood near camp is a resource I
    use up.
20. As a player, I want the starting camp to hold a little firewood, so that the first night is not a
    scramble.

### Fire

21. As a player, I want the hand drill and bow drill made as before, so that I can make fire from wood
    alone.
22. As a player, I want a strike-a-light from flint and pyrite with no tool and no light needed, so that
    I can pair them in the dark.
23. As a player, I want birch bark and amadou to be better tinders than plain tinder on their own,
    amadou the best, so that finding them pays off with no crafting.
24. As a player, I want to make resin tinder from tinder and a little pine resin, so that I have the
    surest tinder for a hard start.
25. As a player, I want no fire-starting chance above 95%, so that lighting a fire is never quite
    certain.
26. As a player, I do not want a flint-and-amadou "striker", so that the game does not teach me that
    flint sparks on flint.

### Stone, flint, bone

27. As a player, I want a crude edge from smashing one stone, with no tool, so that I can cut something
    in my first hour.
28. As a player, I want a well-chosen stone to serve as my hammer, my throwing stone and my ice-breaker,
    so that I need no special hammerstone recipe.
29. As a player, I want proper knives to need flint or obsidian, so that finding flint is my first
    reason to explore.
30. As a player, I want a bone knife, so that I have a hafted blade before I find flint.
31. As a player, I want an obsidian knife that is sharper and more fragile than flint, so that obsidian is
    a real choice and not just a better flint.
32. As a player, I want to grind a stone axe over about four hours from plain stone, so that I can have
    an axe on day one if I pay for it in time.
33. As a player, I want a flint axe that is quick to make, chops faster and also cuts, but wears faster,
    so that flint improves my axe too.
34. As a player, I want no obsidian axe, so that the game respects that glass shatters on wood.
35. As a player, I want obsidian only at a few rare places, such as the cave where loose shale used to
    be, so that exploring is rewarded.
36. As a player, I want a bone shovel from a shoulder blade on a branch, so that I can dig pits and snow
    shelters.
37. As a player, I want a bone needle, so that I can sew.

### Weapons

38. As a player, I want a staff from a branch in two minutes with no tool, so that I have something in
    hand from the start.
39. As a player, I want a wooden spear sharpened and fire-hardened in one recipe from one branch, so
    that my first real weapon is one step.
40. As a player, I want flint-tipped and obsidian-tipped spears as separate recipes, so that each point
    material makes its own weapon.
41. As a player, I want the ivory-tipped spear back, so that a mammoth's tusk becomes the best point.
42. As a player, I want a stone club from a stone, a branch and a binding, so that plain stone gives me
    a blunt weapon that is as good as a spear in its own way.
43. As a player, I want the club to knock down people, wolves and caribou about as fast as a spear but
    bleed them less and do worse against bears, so that choosing between them depends on what I face.

### Traps and fishing (made when those systems arrive)

44. As a player, I want a simple snare from a stick and some plant fibre, and a stronger one from a
    stick and a sinew, so that trapping starts cheap.
45. As a player, I want a fishing rod from a branch, sinew and a bone hook, and a net from fibre or rope
    and sticks, so that fishing gear is made from what the valley gives.

### Processing

46. As a player, I want to scrape a hide with a blade, so that the tool the work needs is asked for.
47. As a player, I want to cut a cured hide into two or three hide strips, so that small garments and
    repairs do not use up a whole hide.
48. As a player, I want to render fat into tallow at a fire, so that the rendering needs the heat it
    obviously needs.
49. As a player, I want to turn willow bark into plant fibre, and plant fibre into rope, so that cordage
    comes from what I gather.

### Medicine (what remedies are made of; what they treat comes with treatment)

50. As a player, I want teas to need a fire, a water container and water, so that brewing makes sense.
51. As a player, I want rose hips to be food, not a painkiller, so that the game does not teach me
    something false.
52. As a player, I want bandages made of plant fibre and sphagnum moss (and resin for a sealed one), not
    raw hide, so that I do not wrap a wound in a raw skin.
53. As a player, I want willow bark for pain and fever, birch polypore for bleeding, usnea with fat for
    inflammation, resin with fat to seal a wound, and pine needle tea for a cough, so that the remedies
    match what people really used.

### Clothing

54. As a player, I want gloves and caps sewn from hide strips, and boots, wraps and leggings from whole
    cured hides, all with sinew and a needle, so that what each garment takes matches its size.
55. As a player, I want mammoth hide cured like any hide before I sew it into a coat or hood, so that I
    never sew a raw skin.

### Lighting

56. As a player, I want a torch from a stick and birch bark or tinder, and a resin torch that burns
    longer, so that the two torches are different things.

### Carrying

57. As a player, I want a birch-bark pouch, a rope belt, a hide belt, a large bag and a mammoth hide
    pack, each made with the tool its work obviously needs, so that more carrying capacity has a clear
    path.

### Camp

58. As a player, I want a lean-to from six branches, so that my first shelter is quick and needs nothing
    else.
59. As a player, I want a branch hut from twelve branches and two ropes, so that the better shelter
    needs binding as well as wood.
60. As a player, I want building a hut where my lean-to stands to give me the lean-to's branches back,
    so that I never lose wood by improving my camp.
61. As a player, I want a mound fire pit dug with a digging tool, and a stone ring of ten stones on top
    of it, so that a fire pit takes believable work.
62. As a player, I want tents with branch poles, sewn with sinew and a needle, so that a tent is made
    like a tent.
63. As a player, I want a sleeping bag sewn with a needle, so that every sewn thing needs one.
64. As a player, I want bedding made with a cured hide, so that it does not rot under me.

### Repairs

65. As a player, I want mending a hide garment with a hide strip and sinew to restore most of the
    missing condition (80%), so that patching helps but never makes it new.
66. As a player, I want sharpening a blade with a stone to restore half of the missing condition, so
    that edges can be kept up.

### Foraging

67. As a player, I want a day's foraging to yield realistic amounts of fibre, bark, resin and flint, so
    that a rope costs an hour, not two days.
68. As a player, I want birch bark on forest ground, so that I can gather it where birches grow.
69. As a player, I want rocky ground to give flint at a useful rate, so that a day on the rocks yields a
    blade's worth even without a lucky find.

### Developers and tuning

70. As a developer, I want a content check that refuses gear weighing more than its lightest possible
    inputs, so that mass from nowhere never comes back.
71. As a developer, I want every resource declared discrete or continuous, with a weight range for each
    discrete one, so that every table counts the same way.
72. As a developer, I want a report of a day's haul per material and of herd numbers after the forage
    change, so that the animals can be retuned from data.

## Implementation Decisions

### Prerequisite: camp builds move into Make

- Camp builds (beddings, frames, the snow shelter, pits, the tent pitch) become recipes made by the one
  Make activity, as the decision record and the roadmap's step 8 already intend. The separate Structure
  recipe path and its build activity go. A recipe whose result is a structure places it on the tile, and
  a recipe can require that a structure is or is not already there.
- The shovel check becomes the Digging capability, like every other tool requirement.
- This lands first, so every recipe below is tested through one seam.

### Resources: discrete or continuous, in data

- Each resource row in the item data states whether it is discrete or continuous. A discrete resource
  states the weight range of its pieces. The inventory keeps discrete resources as individual pieces,
  each with its own weight, and continuous ones as one amount, as the decision record says. Pieces are
  moved whole, never split.
- Discrete: stick, branch (pine, birch, oak), firewood (pine, birch, oak), stone, flint, obsidian,
  pyrite, bone, ivory, sinew, hide, scraped hide, cured hide, mammoth hide (raw, scraped and cured), hide
  strip, rope, chaga, birch polypore, raw fat.
- Continuous: plant fibre, birch bark, amadou, willow bark, pine needles, sphagnum moss, usnea, rose
  hip, tinder, resin tinder, pine resin, tallow, water.
- Continuous amounts are held in grams in data. The UI shows grams below 500 g and kilos to one decimal
  above.
- Every table that yields a discrete resource (forage, discoveries, locations, loot, butchering) yields
  whole pieces with weights in the kind's range.

### Wood

- The log resources (pine, birch, oak) are replaced by branch and firewood resources in each species.
  Kindling is renamed to stick. Each is a fuel with its species' fire properties. Branches and firewood
  of one species burn alike per kilo for now.
- Foraging yields sticks and branches. Firewood only comes from axe work on a pool. Felling a tree
  (existing) yields firewood, and a felled tree stays on its tile as a pool with its progress.
- A new common discovery, Deadfall: branches by hand, and firewood with an axe at about three times the
  rate of felling. The existing wood finds (the Massive Fallen Pine and others) get the same split. All
  of them deplete.
- The starting camp holds a few pieces of firewood.

### Recipe shape

- Inputs are counts of pieces, or grams of a continuous resource, or alternatives between them. An
  input never names a weight for a discrete resource.
- A product's weight is set in data. For gear, the content check holds it at or below the lightest
  possible inputs. For a material output, the output is a share of the actual input weight.
- The parts record holds the actual pieces used.
- A recipe whose result is a repair targets a held piece of gear and restores a share of its missing
  condition: mending 80%, sharpening 50%.

### The recipe list

Times are as today unless given. Tools are capabilities. "Binding" means sinew or plant fibre.

| Recipe | Inputs | Tools / requires | Result and notes |
|---|---|---|---|
| Hand drill | 2 stick | cutting, light | as today |
| Bow drill | 3 stick + 1 sinew, fibre or rope | cutting, light | as today |
| Strike-a-light | 1 flint + 1 pyrite | none, no light | 5 min |
| Resin tinder | 40 g tinder + 10 g pine resin | none | 50 g resin tinder, +45% catch |
| Crude edge | 1 stone | none, light | 5 min; replaces sharp rock and the old crude edge |
| Stone | 1 stone | none | 1 min; throwing, Striking, ice-breaking; replaces the hammerstone |
| Flint knife | 1 flint + 1 stick + 1 binding | striking, light | |
| Obsidian knife | 1 obsidian + 1 stick + 1 binding | striking, light | sharper, more fragile |
| Bone knife | 1 bone + 1 stick + 1 binding | striking, light | |
| Ground stone axe | 1 stone + 1 branch + 1 binding | striking, light | about 4 h project; durable, chops slower |
| Flint axe | 1 flint + 1 branch + 1 binding | striking, light | about 30 min; chops faster, wears faster, also Cutting |
| Bone shovel | 1 bone + 1 branch + 1 binding | striking, light | Digging |
| Bone needle | 1 bone | cutting, light | Sewing |
| Staff | 1 branch | none | 2 min |
| Wooden spear | 1 branch | cutting, fire, light | sharpened and fire-hardened in one |
| Flint-tipped spear | 1 branch + 1 flint + 1 binding | cutting, striking, light | |
| Obsidian-tipped spear | 1 branch + 1 obsidian + 1 binding | cutting, striking, light | |
| Ivory-tipped spear | 1 branch + 1 ivory + 2 sinew | cutting, striking, light | 60 min |
| Stone club | 1 stone + 1 branch + 1 binding | cutting, light | about 20 min; blunt, reach 1.0 m, not thrown, high durability; damage tuned later |
| Simple snare | 1 stick + 30 g fibre | cutting | ships with trapping |
| Sinew snare | 1 stick + 1 sinew | cutting | ships with trapping |
| Fishing rod | 1 branch + 2 sinew + 1 bone | cutting | ships with fishing |
| Net | 200 g fibre or 2 rope + 2 stick | cutting | 60 min; ships with fishing |
| Scrape hide | 1 hide | cutting, light | 0.8 × the hide |
| Dry hide | 1 scraped hide | fire | 0.9 × the scraped hide; stays until the curing rack (design session 3) |
| Hide strips | 1 cured hide | cutting, light | 2 strips, 50% chance of a third; 10 min |
| Render fat | 1 raw fat | fire | 0.6 × the fat as tallow; 20 min |
| Willow fibre | 100 g willow bark | none | 0.8 × as plant fibre; 10 min |
| Cordage | 300 g plant fibre | none | 1 rope |
| Willow bark tea, pine needle tea, chaga tea | 1 piece or a handful + 0.3 L water | fire, water container | 10–15 min |
| Polypore poultice | 1 birch polypore | none | |
| Usnea poultice | usnea + 1 raw fat | none | |
| Resin seal | resin + 1 raw fat | none | |
| Fibre wrap | 50 g plant fibre | none | |
| Moss bandage | 50 g fibre + sphagnum moss | cutting | replaces the hide bandage |
| Sealed bandage | 50 g fibre + sphagnum moss + resin | cutting | |
| Hide gloves, hide cap | 2 hide strip + 1 sinew | sewing, light | |
| Hide boots | 1 cured hide + 1 sinew | sewing, light | |
| Hide wrap | 2 cured hide + 2 sinew | sewing, light | |
| Hide leggings | 2 cured hide + 1 sinew | sewing, light | |
| Scrape and dry mammoth hide | as for a hide | as for a hide | cured mammoth hide |
| Mammoth hide coat | 1 cured mammoth hide + 1 cured hide + 4 sinew | sewing, light | |
| Mammoth hood | 1 cured mammoth hide + 2 sinew | sewing, light | |
| Torch | 1 stick + 50 g birch bark or tinder | none | |
| Resin torch | 1 stick + 30 g resin + 20 g tinder | none | burns about twice as long |
| Birch-bark pouch | 100 g birch bark + 50 g fibre | cutting | +0.5 kg |
| Rope belt | 2 rope | none | +3 kg |
| Hide belt | 2 hide strip | cutting | +4 kg |
| Large bag | 3 cured hide + 2 sinew + 2 rope | sewing | +10 kg |
| Mammoth hide pack | 1 cured mammoth hide + 2 sinew + 1 rope | sewing | +15 kg |
| Makeshift bedding | none | none | as today |
| Padded bedding | 1 kg fibre + 1 cured hide | light | |
| Moss bedding | 1.2 kg moss + 240 g pine needles + 1 cured hide | light | |
| Lean-to | 6 branch | light | coverage up to 50% |
| Branch hut | 12 branch + 2 rope | light | coverage up to 80%; built over a lean-to it takes it down and returns its branches whole |
| Mound fire pit | none | digging | about 3 h |
| Stone ring | 10 stone | requires a mound pit; digging speeds it | |
| Snow shelter | none | digging, frost, light | as today |
| Hide tent | 4 cured hide + 4 branch + 2 rope + 4 sinew | sewing | gear, pitched as today |
| Mammoth hide tent | 2 cured mammoth hide + 4 branch + 4 sinew | sewing | gear, pitched as today |
| Sleeping bag | 4 cured hide + 6 sinew | sewing, light | |
| Mend (any hide garment) | 1 hide strip + 1 sinew | sewing, light | restores 80% of missing condition |
| Sharpen (any blade) | none | striking | 10 min; restores 50% of missing condition |

Removed: sharp rock, hammerstone, stone knife, shale knife, crude spear, heavy spear, stone-tipped spear
(becomes flint-tipped), the flint-and-amadou striker, the tinder bundle, rose hip tea, the hide bandage,
the branch frame and log frame (become lean-to and hut), the old "rebuild shelter".

### Fire

- Tinder catch bonuses: plain tinder +15%, birch bark +30%, amadou +40%, resin tinder +45%. Every
  fire-starting chance is capped at 95%.

### Stone and obsidian

- Shale is removed as a resource and from every table. The Loose Shale cave find becomes an obsidian
  find, and the Shale Outcrop location becomes a rare obsidian outcrop. Obsidian appears nowhere else.

### Foraging

- Material forage rates rise to realistic levels, every resource including those animals eat (player
  balance first). Starting targets, per first hour on fresh good ground: plant fibre about 300 g, birch
  bark about 200 g (newly on forest ground), willow bark about 100 g, pine resin about 30 g, flint about
  50 g on rocky ground. The rest rise in proportion to what a person would really gather. Flint sites
  and finds keep their larger yields. Recipe amounts stay as listed.

### Combat numbers

- The stone club's damage starts around 3.5 blunt, measured to match the wooden spear's hits to down on
  a person, wolf and caribou and to fall short on a bear. Its final number comes from the combat tuner
  after the body-damage rethink.

### Roadmap amendments

- FIR-5 reversed: no tinder bundle; better natural tinders and resin tinder instead.
- ITM-8 and ITM-18 settled by this spec. ITM-20 is delivered by the camp-build merge.
- ITM-22 amended: a scraped hide is 0.8 × the hide's own weight, not a fixed 0.8 kg.

## Testing Decisions

- A good test drives the game the way a player does and checks what the player can see: start an
  activity on a world, step its minutes, then read the pack, the tile, the gear and its parts record, or
  the refusal. No test reaches into a recipe's internals.
- **Activities stepped on a world** are the main seam, where the existing Make tests already sit. They
  cover each recipe's inputs and refusals by capability and place, outputs that scale with the input's
  weight, the hide strip roll, mending and sharpening, camp builds through Make (lean-to, hut salvage,
  pits, tents), felling and deadfall work yielding firewood and running out, and fire starting with the
  new tinders and the 95% cap. Prior art: the Make tests (materials taken, work done, parts record,
  projects that resume and survive a save), the shelter tests for what a structure does once built, and
  the fire tests for start odds.
- **Content load checks** hold the static rules: every recipe resolves, every resource is declared
  discrete or continuous, every discrete one has a weight range, no recipe takes part of a piece, no
  gear weighs more than its lightest possible inputs, and no table names a removed resource. Prior art:
  the content and strict-content tests, and the Make conservation test beside them.
- **The forage report** measures a day's haul per material on good ground against the targets above,
  and herd numbers after the change. It reports and never gates. Prior art: the report and its tests.
- Tests run in Release with the class filter during the build, and the fast suite before a push.

## Out of Scope

- Trapping and fishing systems. Their recipes are settled here and ship with them.
- What remedies treat: the illness effects come with treatment (roadmap step 11).
- The curing rack and hide drying (design session 3). The fire-drying recipe stays until then.
- The ember carrier and lighting a fire from a carried ember: its own ticket.
- Antler as a resource. Today the shed antler find gives bone.
- Hauling a felled tree on a sled.
- Refitting held gear (repointing a broken spear).
- Firewood burning differently from branches.
- The body-damage rethink (hits across several parts, what "destroyed" means, blunt and pierce
  bleeding, the pierce damage cap): its own grilling ticket. The stone club's tuning and the point
  spears' real gain over the wooden spear wait on it.
- Fighters choosing a weapon by damage type and reach: its own ticket.
- Retuning animals after the forage change.

## Further Notes

- The club measurement (4000 trials a species, the sim's own damage code) found that blunt and pierce at
  equal damage are not even: blunt is stronger on everything but bears, because any destroyed region
  bleeds as if cut. It also found that pierce saturates at the wooden spear's 4.84, so a stone point
  adds almost nothing except against bears. Both go to the body-damage rethink.
- Flint today takes a median 15 hours of blind searching on rocky ground to reach a blade's worth, and
  stone and bone about half an hour. The ground stone axe and the flint forage rise are the answer: an
  axe on day one if you pay for it in hours, and flint still the thing worth exploring for.
- Raising the resources animals eat (tinder as grass, sticks as browse, the fungi) raises what the land
  can feed. The forage report counts herds so the animals can be retuned from data.
