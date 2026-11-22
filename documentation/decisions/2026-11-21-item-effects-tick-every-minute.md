# Item effects tick every minute, until that is a slowdown problem

Every item stack (on the ground, in a stash, carried, worn) has its effects updated eagerly every world minute, the same as bodies. That is the simplest exact option: results never depend on how often anything is looked at or how a caller chunks its steps. A benchmark (2026-11-21, about 200 stacks today, 400-840 in a world with 10 NPC travellers) put the cost at about +16 s on a 15 s sim year, which is microseconds per game minute and invisible in play, but doubles headless runs such as the settling pass. We accept that and revisit when it becomes a real slowdown.

**Considered options:**
- **Lazy catch-up from exposure history** (rejected): cheapest, but not exact (wetness is clamped and freezing and drying branch on state, so the answer changed 1.1% with read frequency) and not derivable from the seed (fire heat, shelters and carried paths would have to be saved).
- **Quarter-hour cadence with one shared exposure per tile and an index of stack tiles** (the fallback): about +2 s a year, still exact. This is the first thing to reach for when per-minute is too slow, then skipping stacks with nothing happening to them.
