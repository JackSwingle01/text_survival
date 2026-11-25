# #199: The body's tiers become separate types

Hypothesis:
- Changes: `Body.cs` (the capacity, faculty, vitality and life code moves out into tier types in a new
  `BodyTiers.cs`: `TissueReads` (state and effects), `BloodAndBreath`, `Capacities`, `Faculties`, `Vitality`,
  `Alive`, and the screen's `BodyReadOut`); `Body`'s public reads stay as the one-line interface the sim
  and tests call today; `Web/Host/ViewBuilder.cs` (health panel and impaired chips read `BodyReadOut`);
  `Architecture/CapacityReadTests.cs` (the body's own code is now the tier types; the two screen entries
  come off); a new `Architecture/BodyTierTests.cs` (tier order with its exception list, no cycles,
  read-out callers); `documentation/clean-room-architecture.md`.
- Affects: no behaviour. Every capacity, faculty, vitality and life read returns the same double, bit
  for bit; the cascades (blood scales consciousness, moving, manipulation; breath below 0.3 scales
  consciousness; consciousness below 0.1 cuts the limbs) keep their order of operations.
- Stays green: the fast suite with no test changed but CapacityReadTests' body set; the save fixture.
- First red: `BodyTierTests` fails to compile until the tier types exist; then the tier-order rule
  lists Faculties.Strength, Speed, Dexterity, Perception and DexterityCosts reading Vitality until they
  are listed.
- Checked by: a throwaway probe (not committed) printing every faculty, vitality, life and capacity
  with "R" precision for the reference bodies and 3,000 random bodies, before and after: same file.
  The balance report (#197) before and after.

Revised: the scout was done in this session rather than a sub-agent: Body.cs, CapacityReadTests and ViewBuilder were the whole blast radius, and a full-precision probe was cheaper than a report → probe output kept in /tmp for the diff.
Revised: the screen reads capacities in two members (BodyOf, the health panel, and Alerts, the impaired chips), both on the #198 list → both move to the read-out, so both screen entries come off; the read-out's rule allows exactly those two members.
Revised: callers keep `Body.Strength` and the rest; the tier types hold the code and `Body` forwards → chosen over moving ~200 call sites, since the issue says callers keep working and the next tickets change formulas, not callers.
Revised (review): a tier could reach a higher tier through `Body`'s forwarders, unseen by the tier rules → `Body` is ranked as the interface above every tier, and the rule fails any tier that depends on it; the tiers' helpers (`Clamp01`, `IsLeg`) move into `TissueReads`.
Revised (review): `BloodAndBreath` and `TissueReads` reads from outside would slip past #198's rule, which looks only for `Capacities` → `ReadBy` counts them as capacity reads too; the read-out has its own caller rule.
Revised (review): one combined blood-and-breath factor could move the last bit → circulation and the breath ratio are carried apart and applied in today's order behind today's branches.
Revised (review): faculties calling `Vitality.Of(body)` would read the capacities again → an internal `Vitality.Of(Capacities)` is what they call, as today.
Revised (review): `Alive` reusing vitality's least-of would be a tier breach → `Alive` takes its own least of breathing, pumping and consciousness read without effects, ranked beside the faculties.
Revised (review): compiler-made nested types (lambdas' closures) and by-ref struct names could hide a dependency → a tier includes its nested types, by-ref names are normalised, and the guard sample breaches through a lambda and an `in` parameter.
Revised: CapacityReadTests changed (a pre-existing test, following the moved code): the body's own code is now the tier types and the read-out, reads of `TissueReads` and `BloodAndBreath` count as capacity reads, the rule's scan leaves out BodyTierTests (it names the types), and the two screen entries come off.
Revised: the comment-share ratchet failed both Body.cs (11%) and BodyTiers.cs (15%) → comments trimmed to 10% and under; the forwarders carry none.
Revised: the probe compared bit for bit (R format) every faculty, vitality, life, cost list and capacity record for the 13 reference bodies and 3,000 random bodies under three contexts: identical before and after, and again after the comment trim.
