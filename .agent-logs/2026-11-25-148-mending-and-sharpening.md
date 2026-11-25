# #148: Mending and sharpening

Hypothesis:
- Changes: `rebuild/Sim/Make.cs` (a repair result: `RecipeResult.Repair` naming the gear kinds it takes and the share of missing condition it restores; `Make` and `Abandon` name a target gear kind; `MakeProject.Target`; the target piece is the most worn held piece of that kind, in the pack or worn; Complete restores condition and adds the materials to the piece's parts record), `Content.Tables.cs` (result row `repairs`, `restoresPct`, `minutesFor`, and their checks), `Content/recipes.json` (mend, sharpen), `Verbs.cs` (one verb per held kind a repair recipe takes), `Narration.cs`, `Web/Host/ViewBuilder.cs`, `Dto.cs` and `main.js` (the make drawer keys a verb by recipe and target), the save fixture.
- Affects: every recipe's Make verb is listed whatever is held (repairs only per held target); one project per recipe (per recipe and target); nothing raises a piece's condition today.
- Stays green: `MakeTests`, `ContentStrictTests`, `ConservationMakeTests`, `VerbsTests`, `ArchitectureTests`, `SaveFixtureTests` (regenerated), `GameHostTests`.
- First red: a MakeTests test mending gloves at 50% fails on no recipe `mend`.

Revised (review): the most-worn piece of a kind, looked up again each session, can be a different piece by the end (a worse pair picked up mid-work) → the target piece goes into the project's materials when the work begins, as a consumed item does, and comes back repaired (or as it was, on a failure or abandoning); a worn garment comes off for the mending and is put back on after if its slot is still free (`MakeProject.Worn`).
Revised (review): a per-kind minutes override touches every `r.Minutes` reader → two rows, `mend` (15 min) and `mend_wrap` (20 min), both named Mend; the loader keeps a kind in at most one repair recipe.
Revised (review): "a wrap" is unspecified → the wraps by name: hide_wrap, fur_chest_wrap, worn_fur_chest_wrap, fur_leg_wraps (hide_handwraps are small and take 15). Reversible data.
Revised (review): "any hide garment" → every garment of hide or fur by name (fur is hide with the hair on, and three of the four starting garments are fur); worn_coat (no material named) is left out.
Revised (review): max-80 garments cannot land on 98% in whole points (72 + 6.4) → restore round(missing × share), away from zero, at least 1; the 90%→98% check runs on a bear hide chest (135 → 147 of 150), and a hide wrap at 72 lands on 78 (97.5%).
Revised (review): no listed target gives Sewing or Striking, so a "can't be its own tool" refusal never fires → a loader check that no repair target gives a capability its recipe uses.
Revised (review): the garments' `waste_material:HideStrip:0.25` rounds one strip to none → mend wastes `HideStrip:1`; sharpen has no outcomes.
Revised: full-condition pieces are not offered (nothing to restore), broken ones are (a share of all of it).
Follow-up: `Items.Make(kind, durability)` sets MaxDurability to the override too, so the starting fur leg wraps at 60 are 60/60, whole, and can never be mended; fixing it lowers starting warmth (balance).
Revised: `Making.Complete` gains a trailing `target` parameter rather than a new position, so the existing tests' calls stand.
Revised: pre-existing `MakeTests.Verbs_ListEveryRecipe_WithItsOddsAndWhyItIsRefused` listed every recipe one-for-one → it lists every recipe but the repairs, which are offered per held piece (RepairTests).
Revised: pre-existing `MakeTests.EveryRecipe_CanBeMade_FromWhatTheLandAndAKillGive` would try a repair with nothing to repair → `Carried` leaves the repairs out.
Revised: pre-existing `ConservationMakeTests` and `ActionGen` named only a recipe → they name a repair's target (a project's, or a held piece it takes), and the conservation scene's tools include a half-worn hide gloves and stone knife.
Revised: the sample save had no repair → `SampleWorld` begins mending worn gloves without a minute passing, so the fixture's only other change is the new fields.
Revised: CommentHygieneTests' ratchet put Activity.cs at 11% and Make.cs at 12% → comments trimmed.
Revised: sharpen sits with the cutting tools in recipes.json, so the make drawer keeps one heading per category.
Follow-up: the make drawer's per-piece entries (main.js keys a repair by recipe and target) were not clicked through in a browser; `node --check` and Host.Tests pass.
Revised (review): a repair's label was built in Verbs, ViewBuilder.MakeOf and the project list → one `Making.Label(recipe, target)`.
Revised (review): `pack.Gear.Concat(agent.Body.Worn)` three times → one `Making.Held`.
Revised (review): abandoning a repair with none begun read "you have no unfinished mend" → "you have no unfinished mend hide gloves", by the label.
Revised (review): a mend stalled by the dark leaves the garment in the project, off the body, until it goes on or is abandoned → kept: the piece is in the work, which keeps its identity, and the unfinished list names it.
Revised (review): `MakeProject.Target` could be read off the piece in its materials → kept: ProjectFor matches recipe and target without opening the materials.
Follow-up: `Making.Doing` adds "ing" to a repair's name, right for Mend and Sharpen only; a repair named Cut or Sew needs its gerund in data.
Follow-up: blades of 3 to 5 points restore by whole points (a bone knife at 2/5 sharpens to 4/5, 80%), so the shares are exact only on pieces of 10 points or more; whether short-lived blades want more points is a balance choice.
