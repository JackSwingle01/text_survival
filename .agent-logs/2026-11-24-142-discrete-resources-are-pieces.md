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
Revised: the first red was not written first: renaming Inventory.Kg made the compiler the driver, so the loader checks (ContentStrictTests' form, range and gear rows), the parts-record test and the piece invariant were written after the code they check, each seen to fail on a broken edit before passing.
Revised (term): ravens were to peck toward a whole piece across raid minutes; a raid is a single rare minute and the flock left with nothing → a raid carries off the lightest cut whole, and more only within its appetite (no saved peck state).
Revised: a caribou's hide (12 kg, whole) no longer fits beside the clothes and kit in a bare 15 kg pack, so it waits on the carcass → CarcassTests' full-pack test adds a large bag before the sessions that finish it; the bare-hands test carries bags so it tests the blade, not the pack.
Revised: a butchered resource whose last uncut grams fell below 0.01 kg could leave a crumb smaller than a piece pending forever ("your pack is full") → a crumb below a piece once the resource is cut is not kept, and comes off YieldKg, as Settle drops a yield below the minimum.
Revised: pre-existing tests changed for pieces: ItemsTests (starting kit and accessory weights less the hand drill's 0.2 kg; Receive fills to within a stone; put down takes the lightest stick; a strike takes whole sticks), MakeTests (refusal words, abandon rounds to whole pieces, waste of one flint takes it whole, scrape takes the lightest hide, a quarter of one hide wastes nothing, the recipe read as counts), CarcassTests (part-cut pieces wait on the carcass), WorkTests (the axe adds pieces, not weight), VerbsTests, NarrationTests, ForecastTests (a log refused whole; the sliver rule kept for amounts), ShelterTests, CombatTests, ConservationItemsTests (kindling declared in whole sticks).
Follow-up: dev/prototypes/npc-requirements reads pack.Kg and no longer builds; it is not in the solution or CI. Port it to Items.Totals or delete it.
Follow-up: hide clothing now weighs 0.12-0.31 kg (its lightest possible inputs), which is its thermal mass; #146 sets new garment inputs and should re-read these weights.
Follow-up: the NPC bench (NPC_BENCH=1) and the nightly Slow suite were not run; Conservation*, Carcass and ActionSequence (Slow included) were. Compare `git show FETCH_HEAD:npc-bench.txt` on the baselines branch with a nightly run of this branch (`gh workflow run nightly.yml --ref 142-discrete-resources-are-pieces`).
Revised (review): the butchering's share of what the pack took edited the piece lists by hand → Items.Without, so the lists change only through Items; Make's copy of KgByResource removed.
Revised (review): a shelter improvement switched to another material mid-piece dropped the rest of a piece already out of the pack → that piece counts in full first.
Revised (review): the web app's verb chips still read kilos for logs and sticks → they name what the activity would move ("1 pine log", "50 g birch bark") through Items.WouldMovePieces.
Revised (review): TakeReady's even last pieces need a range at least two-fold, or a piece falls out of it → the loader refuses a carcass-yielded discrete resource whose min is over half its max.
Revised (review): more pre-existing tests changed mechanically (SkillsTests, SubstrateTests, NpcTests, KillChainTests, Phase2/3GateTests, MemoryTests, ConservationButcheringTests: Items.Add → TestKit.Give, .Kg → Items.KgByResource/Totals); ConservationMakeTests' recorded parts became one 1.5 kg log, since 0.5 kg is below a log.
Follow-up: pile memory change lines (Memory.cs / Narration.Change) still read kilos, as the NPC memory of a pile is kilos rounded to 0.1; showing counts there needs memory to keep counts.
Follow-up: waste and save shares on a single discrete piece round to all or nothing (dry_hide's 0.5 wastes the whole scraped hide, gloves' 0.25 nothing); #146 rewrites these recipes and could restate the shares as pieces.
Revised (merge, 2026-11-25): main's washing (#127) met pieces → Narration keeps the pieces `Ate` line beside main's `Washed`; DirtyTests empties the pack through `Take` and `TakePieces`; the save fixture regenerated on the merged tree; TestKit.cs formatted (the `format` check's failure).
