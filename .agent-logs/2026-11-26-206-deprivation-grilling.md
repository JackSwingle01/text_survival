# #206 Deprivation rethink: grilling and spec

Hypothesis: the issue's Python-copy numbers are close enough to the real sim that the grilling can run
on them; the real probe only confirms them.

Revised: the probe (Body.Update minute by minute, fresh human, no cold) showed the hour-16 faculties
and the two-day case were outdated, because #200 removed vitality from the faculties after the copy was
made. It also showed starvation runs to day 63 (species-average body) or day 88 (game-start body), not
49: sleep halves the burn, and the muscle floor shrinks with current weight. Thirst's lethal brain
damage, not the Thirsty effect, takes consciousness to zero.

Revised: the grilling agreed to fold the animals' sleep-depth factor into the deep-debt repay rate. The
code says that factor is about where an animal lies, not about its debt, so the spec keeps it and flags
it in Further Notes.

Decided with Jack, 2026-11-26 and 2026-11-27, Q1-Q15: the three routes (consciousness, blood,
wasting); multiplicative factors that never reach zero, with explicit endings; a sleep debt felt from
18 h and full at 48 h; tired ×0.5, thirsty ×0.7 plus blood read as 15% lost, hungry ×0.9 consciousness;
Forbes' split; the repay rate from 2 to 4 a minute; a short start kept; the side channels (no shared
30%, healing scaled, collapse from 36 h, lethal thirst unchanged, starvation death at half fresh
muscle, around day 60); the NPC wants reading the body's onsets; animals by the same rules; derived,
not stored; a balance report and property tests; the glossary and decision record.

Follow-up: split the spec into implementation tickets (Jack asked to hold this until the spec is
reviewed).
