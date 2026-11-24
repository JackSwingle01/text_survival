# Grilling #55: crippling injuries and incapacitation

**Hypothesis.** A small ticket: one audit item (BOD-17). The rebuild refuses only walking at moving
≤ 0.1 where the old game blocked everything; the likely answer is "keep the rebuild's rule" plus a
matching gate for hands and a continuous slowdown in place of the two steps at 0.5. Output: a short
spec, three or four glossary terms.

Revised: hunger, thirst and tiredness subtract from a capacity, so with a hands gate one destroyed arm
plus deprivation is refused → opened how deprivation reaches the limbs at all.

Revised: tracing store → severity → capacity → faculty → verb showed vitality inside all four faculty
formulas, counting consciousness two or three times; sixteen hours awake halves speed and strength →
the question became how a body is read, not what a threshold is.

Revised: Jack set the structure: tiers (state and effects, blood and breath, capacities, faculties,
vitality), faculties as the body's only interface, refusal when a faculty is at zero → the 0.1
threshold, the hands gate on a capacity and a fifth faculty for the mind were all dropped.

Revised: I proposed "ability" for the four, which the glossary already uses for what a species can get
at → Jack chose "faculty".

Revised: I said the zero rule refuses every verb to an unconscious body; it does not refuse a verb that
needs no faculty → eating and drinking need dexterity above zero.

Revised: enforcement by a source-text architecture test → a dependency-rule library reading compiled
code, with the tiers as separate types (Jack asked whether such tests exist).

Revised: I said property tests run only nightly → they run on a fixed seed in PR CI and on fresh seeds
nightly.

- Charts and the two-day model were a Python copy of the sim's formulas. No `dotnet` in the container,
  so nothing was run in the sim and no test was run.
- Written: nine glossary terms, the decision record "A body is read in tiers", the spec.

Follow-up: new grilling ticket "Deprivation rethink" (sizes and route of hunger, thirst and tiredness;
tiredness bank; sleep debt; fat before muscle; healing gate BOD-18; exhaustion sleep; lethal thirst and
hunger; the shared 30% threshold), starting with a sim probe. Filed as #206.

Follow-up: #153 takes what a body with zeroed faculties does in a fight.

Follow-up: the `mp:verification` skill's technique table has no row for architecture or dependency
rules; it lives in Jack's plugin, outside this repo.

Follow-up: five details in the spec's Further Notes are my picks, not Jack's: vitality as a mean, the
ratio held at one, no floor on crossing time, digestion's place, and the eating gate for animals.

Follow-up: implementation tickets #197 to #205 filed; blocking is in each body, native links not set.
