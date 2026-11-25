# Main red: the haul report names torch recipes that are gone

- Task: `rebuild quick` fails on main at fc602a5 (the merge of #238):
  `HaulReportTests.HaulNeeds_EachIsGatheredOnSomeGround`, `Recipes.All.Single(r => r.Id == "simple_torch")` finds none.
- Hypothesis: #242 (the haul report) reads `simple_torch` and `birch_bark_torch` by id, and #238 replaced
  them with `torch`; each PR was green alone and they did not conflict in text.
- Changes: `Report.HaulNeeds` reads `torch` and `resin_torch`, and reads the birch-bark pouch from its
  recipe (`small_pouch`, added by #238, the same 100 g bark and 50 g fibre the spec row had).

Revised: none.

Follow-up: Slow `ActionSequenceTests.LongScripts_SameWorld_HoweverSteppedOrSaved` fails on main
(2d47a0d, seed 5tlblaFEyAj1: Travel then Gather, "chunking changed the world", minute 62 vs 61).
Not traced here.
