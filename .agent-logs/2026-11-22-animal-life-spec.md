# animal-life-spec: grill #50 with Jack, then write the spec (PR #77)

Hypothesis:
- The brief in the roadmap ("Animal life", design session 4) settles most of #50: shared body needs and one planner for NPCs and herds. The grilling only has to fill gaps: what a herd knows, what "worth it" is measured in, snow as water, how ravens fly, and whether the tier ladder covers herds.
- Output: glossary terms, decision records where a choice is hard to reverse, and a spec in a new `documentation/specs/` folder (the repo had none).
Revised: Jack questioned the herd concept itself, so animals became individuals and the herd became derived, with no stored herd and no fixed territory. That replaced most of the brief's herd-mind lines; the roadmap brief is marked superseded.
Revised: Jack asked for a performance check before accepting individuals. I split every herd into one-animal agents with today's mind (seeds 17 and 23, 30 days, Release): 3.1-3.6 s to 6.4-6.5 s, 4.0-4.4 GB to 6.2-7.0 GB. That measured the number of agents, not the planner. The probe was scratch and isn't committed. The container's apt SDK is 10.0.115 against global.json's 10.0.400, so I relaxed global.json locally for the run and restored it.
Revised: an independent review of the two decision records found that the benchmark didn't price the planner, vigilance was scored in the planner but paid by detection, there was no knowledge-view parameter, and the cohesion gate was weak. Jack then put a base cohesion level in place of the vigilance forces: (1 + cohesion) on going alone, with yield divided by the grazers present.
Revised: the stat-tiers decision record said food beats rest, but the code and glossary said rest beats food. Jack chose rest; the decision record is amended. Tier is renamed Priority in the glossary (it had listed "priority" under Avoid).
Checked: no code changed. The PR is docs only.
Follow-up: rename Tier to Priority in the rebuild code (Tiers.cs, Tier.*, TiersTests), queued as a suggested task.
Follow-up: FIR-13 forbids people eating snow; by the same physics as animals it should cost body heat instead. Flagged, not decided.
Follow-up: deferred from the spec: bears denning for the winter, births from body condition, wolverines, mammoth families, migration off the map.
