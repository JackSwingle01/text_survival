# #222: Ratchet: comments — Agent.cs, Body.cs

Hypothesis:
- Changes: comments only in rebuild/Sim/Agent.cs (24%) and rebuild/Sim/Body.cs (11%); both entries leave OverCommentLimit in CommentHygieneTests.
- Affects: nothing at run time. No renames of saved members (Agent, BodyState, Mind records are saved as JSON and their property names are the save format).
- Stays green: CommentHygieneTests, then the fast suite with no test changed but the two list entries.
- First red: none; a comment sweep, checked by CommentHygieneTests.SimComments_StayUnderTheRatchet.

Revised: scout and adversarial design-review sub-agents skipped, decided unattended → a two-file comment sweep with no behaviour change, as #111 did; what the check counts was read off CommentHygieneTests.CommentShare (any line holding a comment counts, trailing ones too).
Revised: tdd seams, decided unattended → none; the existing CommentHygieneTests is the check, the fast suite guards behaviour.
Revised: Agent.cs kept only reasons (kind state never on Agent, why Members is a list, Crossing/LastLeft/Doing's nulls, herds never stored, why Remove drops streams) → at 10% the file holds eight comment lines; the summaries that restated names (PlayerMind, Spawn, SpawnGroup, Index "never saved") went.
Revised: Body.cs's `// muscle` trailing note stays → it says why Moving gains 1 per region and a rename would need a constant used once.
Checked: CommentHygieneTests 4/4; the ratchet's remaining command prints neither file; fast suite 1648 passed, 1 skipped (Release).
Revised: review → `WoundedBy`'s rewrite said bleed-outs only and "on an animal" (wrong: set on every blow, cleared on any mended body), fixed; `Damage` keeps "the one way anything hurts a body", `BaselineWeightKg` names what scales from it, `Remove` says it drops the rng streams, each still one line. Not taken: `Spawn`'s den default (Fauna.NewMind's to say), `Skills`' "only a person learns" (PersonMind says it).
