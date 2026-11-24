# #142: Discrete resources are pieces

Hypothesis:
- Changes: Items (Inventory gains Pieces for discrete resources; Kg keeps continuous only; one
  take rule: lightest piece first, whole pieces, within a limit), Content.Tables (resource rows declare
  `form` and a discrete row's `pieceKg`; recipe inputs `count` for discrete, `kg` for continuous;
  gear-weight check), Make (count inputs, parts record as pieces, material outputs a share of the
  actual input weight), Fire (whole pieces, a nearly full pit refuses a piece), Work/Pools/Carcass
  (forage, stock, loot, felling and butchering yield whole pieces in range), Npc/Verbs/Shelter/Combat/
  Scavenging (takes in whole pieces), Names/Items.Describe/ViewBuilder (counts and g/kg), save fixture.
- Affects: every add or take of a resource. Today all are kilos, so a log is split to fit a nearly full
  fire, Make takes 1.5 kg out of a 2 kg log, Receive splits a stack at pack capacity, forage pieces are
  scaled by the yield factor, loot by its outcome scale, salvage hauls merge into one summed unit,
  butchering releases any kilos as one lump, the shelter improvement eats PieceKg/10 a minute.
- Stays green: Conservation* (Items, Make, Butchering, Fire), ActionSequence (whole vs chunked vs saved),
  ContentTests, MakeTests, ItemsTests, FireTests, NpcTests, Web Host.Tests, SaveFixtureTests (regenerated).
- First red: ContentStrictTests fails because a resource row with no `form` still loads.

Revised: the issue asks old saves to load with kilos turned into pieces; DR 2026-11-24 "No save migrations until the rebuild is finished" says such a ticket is built without that criterion → no migration; the save format changes (derived hash) and the fixture is regenerated.
Revised: the spec's lists leave out Peat, Shale, Charcoal, Feather, JuniperBerry and today's Kindling/Pine/Birch/Oak (renamed by #143) → Kindling, Pine, Birch, Oak, Shale and Feather are discrete (sticks, logs, stones, feathers are counted); Peat, Charcoal and JuniperBerry are continuous (cut by the spadeful, lumps by the handful, berries like berries). Foods follow the comment on #142.
Revised (review): Feather is continuous after all: a bird's feathers come off as a few grams, not as countable pieces worth tracking → Feather continuous.
Revised (review): renaming Inventory.Kg was the only way to find every reader of discrete stacks (empty-pile checks, Keep, WeightKg, menus, NPC fuel) → Kg became Amounts (continuous) and Pieces (discrete); every reader goes through Items.Totals/IsEmpty/KgByResource.
Revised: piece ranges are not a choice made here: each kind's range spans every table that yields it today (forage, locations, discoveries, loot, felling, starting pack) plus butchering (a cave bear's hide whole) and the hide chain's shares → no yield table changed; the loader checks every table stays inside.
Revised: with those ranges the gear check fails 21 of 25 gear recipes (a sharp rock from the lightest 80 g stone, gloves from a 70 g cured hide) → product weights set to their lightest possible inputs, as the issue says; hide clothing loses most of its weight (wrap 1.5 → 0.31 kg), which is its thermal mass (Items.ClothingWeightKg). The heavy spear comes to 1.0 kg, as one log.
Revised (review): conversion of kilos to counts: nominal pieces from recipes.json's header and shelters.json (cured hide 0.7, as the recipes' own comment says, not shelters.json's 1.0), rounded half away from zero, at least one → leggings 2 cured hides, wrap 3, sleeping bag 6, heavy spear 1 log.
Revised (review): the spec says continuous amounts are grams in data → recipe inputs and continuous results are "g"; a discrete result is "count" + "sharePct" of the actual input weight.
Revised: waste_material/save_material on a discrete input act on round(share x count) whole pieces, lightest first → a 0.25 share of one hide wastes nothing; abandoning keeps round(0.8 x count) pieces.
Revised: Move's one rule (lightest first, at least one piece if it fits the hard limit, more within the wanted kilos) serves put down, pick up, receive, eat, fuel and Make; ravens instead peck toward a whole piece (HerdMind.PeckedKg), hand cooking turns a piece once its minutes are covered (WorkSession.CookingKg), shelter improvement takes a whole piece and works it in over ten minutes (ShelterState.WorkingPiece).
Revised: forage's yield and clue factors would have scaled a piece's weight out of range → for a discrete find they set how many pieces (floor plus a roll on a new "yield" stream); loot's scale counts discrete pieces and no longer also scales their weight.
Revised: names.json read "plant fiber"; the spec and the acceptance criterion say "plant fibre" → renamed.
Revised: Content.NullsIn read every public property, and ResourceDef's derived MinPieceKg throws for a continuous row → NullsIn reads only settable (deserialized) properties.
