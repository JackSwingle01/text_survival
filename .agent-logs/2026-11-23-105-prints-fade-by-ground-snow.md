# #105: Prints fade by the real ground

Hypothesis:
- Changes: Ground (a cumulative snow-fallen counter on each class column, read per tile), Tracks (print freshness from snow fallen since the stamp plus a sky weathering clock without the precipitation term; trail wear keeps the erosion clock), Track (the counter at the stamp), Save (format 23 → 24, fixture regenerated).
- Affects: print freshness and lifetime (Tracks.Read, Sweep, Stamp's carry-over), and so signs, narration "fresh/old", tags, receipts, map tracks. Today every print erodes by 0.5 still air + 6 × precipitation + 3 × wind + 2 thaw a minute, against 2880 × Depth units, whatever the tile's ground holds.
- Stays green: trail wear and erosion-clock tests (Erosion_*, TrailTimelines_MatchOld, AbandonedPath_*), calm-cold print tests and scripted passages, save and determinism tests, Memory field-tile tests.
- First red: TracksTests.SnowDeeperThanAPrint_RemovesIt fails on a print still read after 10 cm of pinned heavy snow.

Revised (review): rain falls at 30–34 °F where the thaw term is zero (hysteresis, freezing rain), so dropping the precipitation term would let prints survive rain → the weathering clock keeps the precipitation term for liquid precipitation (rain, freezing rain); only snow moves to the ground counter.
Revised (review): a backwards counter (SetClassState, replay) could make freshness rise → snow since the stamp is floored at 0.
Revised (review): a sky-only per-cover snowfall timeline would avoid the stored field and the format bump → rejected: the ticket and spec say the snowfall comes from the class column, "not the sky clock"; the counter is read without copying when the class is cached at the minute.
Revised: the ticket sets no print depth in snow → 5 cm per unit of Depth (a person 5 cm, a fox at Depth 0.5 2.5 cm, a mammoth at Depth 3 15 cm): real boot depth in loose snow, and the 3× spread the Depth rule already gives. Light snow (0.5–2 cm/h fresh) now buries a person's print in 2.5–10 h instead of 17 h; a blizzard in about an hour.
Revised: "prints last on bare frozen ground, wind aside" → the still-air term stays (a lone person's print lasts ~4 days in dead calm cold, a mammoth herd's ~12), so the save stays bounded; wind and thaw stay as today.
