# #285: The flint knife wears slowly, obsidian hits harder, and an NPC sharpens its blade

Hypothesis:
- Changes: `items.json` (flint_knife maxDurability 30, obsidian damage 3.89 and its comments), `recipes.json` (sharpen repairs flint_knife), `Npc.cs` (a sharpen step in `Mind.Decide` when no ladder stat is at Need or above), the sample-save fixture (two flint knives -1/-1 become 30/30).
- Affects: a flint knife wears as a recipe's Cutting tool (`Make.cs` tool use) and on a block (`Combat.cs:848`); `Verbs.At` lists "Sharpen flint knife" once it has lost a use; a found flint knife's loot roll (`Pools.cs:274`) now applies, out of 30; the NPC, which never ran a Make before, may now Make sharpen; fights in the tuner wear the knife on blocks.
- Stays green: MakeTests, RepairTests, ContentTests, ContentStrictTests, StoneToolsTests, CombatReachTests, NpcFireTests, SaveTests, the fast suite.
- First red: a new `FlintKnifeWearTests` fails on a made flint knife having Durability -1, not 30.
Revised: `MakeTests.BreakTool_CostsWholeUses_OrBreaksItOutright` broke a flint knife as the tool that never wears → it now breaks the stone (Striking, never wears) on a flint-tipped spear.
Revised (review): a sharpen cut short holds the knife inside its project, out of `Making.Held`, so the NPC would never get it back → `GoOnSharpening` resumes a begun sharpen before the Need loop, with no Need gate; tested by `AnNpc_GoesOnWithASharpenCutShort`.
Revised: placement → the sharpen goes in `Mind.Decide` after the in-hand Needs and before the kept plan, so a held Want plan pauses ten minutes for it; `Keep()` would never be reached by an NPC that always has a Food Want.
Revised: "no stat at Need" → the four ladder stats (`PrioritiesNow`); Health stays out, as Decide handles it on its own line above. `sharpen` asks for no Light, so the NPC sharpens away from camp and at night too, as the spec allows.
Revised: the obsidian knife's thrown damage (1.30) → left alone: the spec names only the melee numbers, and "no other blade's numbers change".
Revised: the NPC tests found a fresh NPC in a bare world holds Warmth at Need (no fuel for tonight) → the test world puts tinder, sticks and oak on the camp's pile.
Revised: sample-save fixture regenerated: its two flint knives go from -1/-1 to 30/30.
Revised: the sample save moves more than its two knives: a cache's flint knife now rolls its condition at map generation, which draws from the generator's rng, so seed 17's camp and what the sample steps through shift → regenerated as is.
Revised: tuner eval at 4000 fights (seed 1), branch against main: the knife and axe orders hold on both; two orders fail on main as on the branch, at the same values ("Weapon order against a lone wolf" 0.018, "The obsidian-tipped spear above the axe" 0.004) → no number changed. The obsidian knife goes 78.8% → 79.1%, still under the ground stone axe's 80.6%.
Follow-up: the `baselines` NPC bench is from d5188a77 (2026-11-25), before #270's fire plan, so the bench beside it (16 alive at day 60 against 0) measures main's newer merges more than this ticket; the bench counts no sharpens, and a spawned NPC holds no stone, so sharpening only follows a stone picked up.
Revised (review): "no stat at Need or Critical" read as the ladder alone let an NPC bleeding at Health Need sharpen → Health counts too; tested by `AnNpc_WithHealthAtNeed_DoesNotSharpen`.
Revised (review): no test showed a found flint knife's condition → `AFoundFlintKnife_KeepsItsRolledCondition_OutOfThirty` rolls every loot row that gives one.
Revised (review): the sharpen choice copies the repair filter `Verbs.At` lists → kept: `Verbs.At` builds every verb on each decision, and `Sim.Refusal` stays the one check; renamed `Sharpen` to `BladeToSharpen` and the test helper `Npc` to `WithNpc`.
Revised (review): `ContentStrictTests`' chopFactor row matched the obsidian knife by its damage → its text follows 3.65 to 3.89. The first red test class shipped as `BladeWearTests`, not `FlintKnifeWearTests`.
Revised: the bench follow-up miscounted → 15 of 20 alive at day 60 on the branch, not 16; the bench ran before the Health check was added.
