# #290: Water reads in litres

Hypothesis:
- Changes: `Names` (a litres rule beside `Grams`/`Kg`, read by `Amount`, `Kg(kg, r)` and `Extra`), `Narration.Change` (the pile change line's water), `Make.MaterialWords` and `ViewBuilder`'s recipe result (they call `Names.Grams` directly).
- Affects: every amount of water worded for a person: the TransformationFinished line ("1.0 kg water waits at the fire."), the fire's Take line and pile/pack lists (`Items.Describe`, `ViewBuilder.Pack`), Gathered/Dropped lines (`Items.Describe`), PutOnPile/PickedUp, the melt recipe's result in the Make drawer ("1.0 kg water"), the pile change line ("1.0 kg water in the pile."). Water is still held by weight.
- Stays green: `NamesTests`, `NarrationTests`, `VerbsTests`, `StationTests`, `GameHostTests`, the fast suite.
- First red: `NamesTests.AnAmount_IsACountOrAWeight_ByTheResourcesForm` fails on Water 1 kg reading "1.0 kg water", not "1 L water".

Revised: unattended run, scouting was a handful of greps (every caller of `Names.Amount`/`Kg`/`Grams` and the pile change line) → scouted inline rather than in a sub-agent.
Revised: `NamesTests` row `Water, 0.0002 → "<1 g water"` is a pre-existing test the ticket changes → now `"<0.01 L water"`, and 5 g is added as the ticket asks.
Revised: `StationTests` asserts "1.0 kg water waits at the fire." → now "1 L water waits at the fire.", as the ticket asks for that line.
Revised: the pile change line's `Change` carries the kilos as text ("1.0") from `Memory` → `Narration` reads the number back and words water through `Names.Litres`; other resources keep "1.0 kg", the record and the save are unchanged.
Revised: the Drank line formatted its litres itself ("0.##" L) → it reads `Names.Litres` too, so water has one rule; a sip under 0.005 L now reads "<0.01 L" where it read "0 L".
Revised: `CommentHygieneTests` ratchet put Names.cs at 14% with doc comments on the new helpers → dropped them (the names say it), Names.cs fell to 10%, and it comes off `OverCommentLimit` as the ratchet asks.
Follow-up: the web page's job row shows a forage's `foundKg` as "N kg" (main.js), a total over every resource found, water included; worded by weight on purpose here, as it sums unlike things. A ticket could word it per resource through `Names.Amount`.
