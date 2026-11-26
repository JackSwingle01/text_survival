# #265: Gear made worn keeps its kind's full durability, so it can be mended

Hypothesis:
- Changes: `Items.Make` (Sim/Items.cs); the starting kit's loader check (Content.Tables.cs); the sample save fixture.
- Affects: the starting fur leg wraps (60 of 60 → 60 of 100: ConditionPct 0.6, so their clo and weight fall and mending is offered); three test callers that pass a durability (MakeTests crude_edge 1, WorkTests ground_stone_axe 10, CombatStateTests wooden_spear 100000). Loot (`Pools` durabilityPct) already keeps the kind's max and does not pass a durability; found gear copies pieces, so neither changes.
- Stays green: RepairTests, MakeTests, the save round-trip tests once the fixture is regenerated.
- First red: a new ItemsTests case, `Make("fur_leg_wraps", 60)` has ConditionPct below one, fails on ConditionPct 1.
