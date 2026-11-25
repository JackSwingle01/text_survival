# #122: Three diets, and a grazing list per species

Hypothesis:
- Changes: `Diet` (Fauna.cs) becomes Carnivore, Omnivore, Herbivore; species.json gains a per-species `grazes` list and its `diets` table becomes the foods each diet eats from piles, caches and carcasses; `SpeciesStats.Grazes`; Fauna's grazing (GrazeMinute, GrazeAt, Load, GrazeRefusal) and Renewal's ForageScore and ValleyScore read the species' list; the loader's SpeciesProblems; World.ValleyForageCache keyed by species.
- Affects: today a species grazes its diet's list (Browser: Usnea, Stick, BirchPolypore; Grazer: Tinder; Omnivore: nine forage resources; Carnivore: none, so a meat-eater with a graze rate lives on small game on any ground). Bear, cave bear, fox and raven graze the Omnivore list, so they take it as their grazing list to graze as today. Hare is Carnivore with a graze rate: as Herbivore with a list it grazes only where its list grows; the world spawns no hare agents (Renewal has no row for it), so this shows only in tests. The ravens' raid keeps its own food list until #124. No saved type changes.
- Stays green: FoodTests, HerdTests, RenewalTests, ForecastTests, ContentTests, ContentStrictTests, SaveFixtureTests, the fast suite.
- First red: ContentStrictTests, a herbivore with no grazing list loads without complaint.

Revised: the skill asks for scout, design-review and code-review sub-agents; this session has no tool to start one → scouted and reviewed in the session itself, noted in the PR.
Revised: ContentTests.Loader_AllTheRealFilesParse_ToTheTablesLoaded compared species rows by record equality, which a list field breaks (two parses hold two lists) → the species table is compared field by field (Assert.Equivalent, strict); the other tables as before.
Revised: ContentOverrideTests.Overrides_ChangeOnlyTheNamedNumbers compared the bear row by record equality, broken the same way → compared field by field.
Revised: bear, cave bear, fox and raven graze the Omnivore list today, and the diets table now means pile food → each took today's Omnivore list as its own grazing list, and the glossary's Diet entry says omnivores that forage have one too.
Revised: the diets table had no pile foods to start from → Carnivore is the six meat and fish foods, Omnivore every resource of category Food, Herbivore none; the loader refuses a non-food in a list, an empty Carnivore or Omnivore list, and a Herbivore list that is not empty.
Revised: nothing reads the diets table until piles feed animals (#124); the ravens' raid keeps its own list as its comment says → `Fauna.Eats(Diet)` removed rather than left with no caller; the table stays in `Content.Tables.Diets`.
Revised: hare had to start from some list → the Browser list (twigs, lichen, polypore), as the other browsers.
Revised: a grazing list on a species that never grazes would be a dead row → the loader refuses a list with grazeKcalPerMinute 0.
Revised: Fauna.cs is at its comment-share ceiling (10 %) → no new comments there; the field is explained in species.json's header.
Revised: fixed-seed check done as a throwaway probe, not a kept test (pinned capacities would break at the first rebalance, #116) → on seeds 1, 7 and 123, every species' Capacity and ValleyCapacity, the first herds' GrazeHere, and the SHA-256 of the save after three days are identical before and after (caribou capacity 35.226/36.748/40.952, bison 67.488/71.170/79.221).
Follow-up: HerdTests.ThirtyPopulatedDays_StayCheap (Slow, nightly) is red on main before this change: 6,830 MB against its 6,600 MB cap, seed 17; with this change 6,848 MB, same 45,114 moves (the forage score is now cached per species, nine sums instead of three). Not re-baselined here.
Follow-up: #124 should read `Content.Tables.Diets` for what an animal takes from a pile and drop `Fauna.RaidFood`; the Omnivore list is a starting list.
Follow-up: `SpeciesStats` no longer has value equality (it holds a list); two tests compared rows with it. A value-equal list type in content records would restore it if more tests want it.
