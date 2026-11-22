# MVP 4: crafting core (Make, recipes, outcomes, projects)

Hypothesis:
- Changes: new `Make.cs` (Recipe, Outcome, Effect, Project-in-pack, the Make and Abandon activities, the odds sheet), `Content/recipes.json` + its loader in `Content.Tables.cs`/`Content.cs` file list, `Items.cs` (Gear.MadeOf composition, Inventory.Projects, ToolType.Hammerstone, weight of projects, AddAll moves projects), crafted gear rows in `items.json`, `Capability.Sewing` (Tags.cs), minimal dispatch lines in `Sim.cs` and `Activity.cs`, Make verbs in `Verbs.cs`, save version 1 -> 2 and fixture.
- Affects: shelter structures keep their own Build path (Shelters' `Recipe`/`Project` renamed `StructureRecipe`/`StructureProject` so the glossary names belong to Make); folding Build into Make is roadmap 8. Inventory weight now counts projects. Verb gets an optional outcome table; Here gets a Make list.
- Stays green: everything; ShelterTests needs only the type rename.
- First red: MakeTests.Parse_SplitsAlternativesEvenly fails to compile on missing `Outcomes.Parse`.
- Seams (set by the orchestrator's test list; no user to confirm with): the outcome parser (`Outcomes.Parse`), the odds sheet (`Making.Odds`), `Sim.Step` with Make/Abandon, `Verbs.At(...).Make`, `Save` round trip, `Content.Parse` for bad recipes.

Chosen recipes (kg from shelters.json piece sizes: stick 0.3, fibre 0.1, stone/flint/bone 0.3, sinew 0.1, hide 1.0, log 2.0, tinder/bark/resin 0.05). "Wood" = Pine|Birch|Oak. Injury targets LeftArm|RightArm (the body has no hands). Old numbers from NeedCraftingSystem unless noted.

| id | inputs | tools (req / faster) | requires | min | result | outcomes |
|---|---|---|---|---|---|---|
| sharp_rock | Stone 0.3 | - | Light | 5 | sharp_rock | fail:waste_material:Stone:1=.20; succeed:injury:LeftArm\|RightArm:0.03=.05 |
| hammerstone | Stone 0.6 | - | Light | 10 | hammerstone | fail=.05 |
| stone_knife | Stone 0.3, Kindling 0.3, PlantFiber\|Sinew 0.1 | Striking | Light | 20 | stone_knife | fail:waste_material:Stone:1=.10; succeed:injury:LeftArm\|RightArm:0.04=.04 |
| flint_knife | Flint 0.3, Kindling 0.3, PlantFiber\|Sinew 0.1 | Striking / Cutting | Light | 25 | flint_knife | fail:waste_material:Flint:1=.10; succeed:injury:LeftArm\|RightArm:0.05=.05; fail:break_tool:Striking=.02; succeed:extra_product:sharp_rock:1=.05 |
| fire_striker | Flint 0.3, Pyrite 0.2 | Striking | Light | 10 | fire_striker | fail:waste_material:Flint:1=.05 |
| hand_drill | Kindling 0.6 | Cutting | Light | 15 | hand_drill | fail=.05 |
| bow_drill | Kindling 0.9, PlantFiber\|Sinew\|Rope 0.1 | Cutting | Light | 25 | bow_drill | fail=.08 |
| cordage | PlantFiber 0.3 | - | - | 15 | Rope 0.2 kg | fail:waste_material:PlantFiber:0.5=.08; succeed:save_material:PlantFiber:0.33=.10 |
| staff | Wood 1.5 | - / Cutting | - | 10 | staff | fail=.03 |
| crude_spear | Wood 1.5 | Cutting | Light | 20 | crude_spear | fail:waste_material:Pine:1=.05; succeed:injury:LeftArm\|RightArm:0.04=.04 |
| wooden_spear | crude_spear x1 | - | Fire, Light | 30 | wooden_spear | fail=.10 |
| heavy_spear | Wood 2.5 | Cutting | Light | 35 | heavy_spear | fail:waste_material:Pine:1=.04; succeed:injury:LeftArm\|RightArm:0.04=.04 |
| stone_tipped_spear | wooden_spear x1, Flint 0.2, Sinew\|Rope 0.05 | Cutting / Striking | Light | 40 | stone_tipped_spear | §3.7's table: fail=.05; fail:waste_material:Flint:.5=.08; succeed:save_material:Sinew:1=.10; succeed:injury:LeftArm\|RightArm:0.05=.04; fail:break_tool:Cutting=.02 |
| simple_torch | Kindling 0.3, Tinder 0.1 | - | - | 5 | torch | fail:waste_material:Tinder:1=.05 |
| birch_bark_torch | Kindling 0.3, BirchBark 0.05 | - | - | 5 | torch | - |
| resin_torch | Kindling 0.3, Tinder 0.05, PineResin 0.05 | - | - | 10 | torch | succeed:save_material:PineResin:1=.10 |
| bone_needle | Bone 0.1 | Cutting | Light | 20 | bone_needle | fail:waste_material:Bone:1=.15 |
| scrape_hide | Hide 1.0 | Cutting | Light | 30 | ScrapedHide 0.8 kg | fail:waste_material:Hide:0.25=.05; succeed:injury:LeftArm\|RightArm:0.03=.02 |
| dry_hide | ScrapedHide 1.0 | - | Fire | 240 | CuredHide 0.7 kg | fail:waste_material:ScrapedHide:0.5=.08 |
| hide_gloves | CuredHide 0.4, Sinew 0.1 | Sewing | Light | 30 | hide_gloves | clothing table* |
| hide_cap | CuredHide 0.35, Sinew 0.1 | Sewing | Light | 25 | hide_cap | clothing table* |
| hide_wrap | CuredHide 2.0, Sinew 0.2 | Sewing | Light | 45 | hide_wrap | clothing table* |
| hide_leggings | CuredHide 1.4, Sinew 0.1 | Sewing | Light | 40 | hide_leggings | clothing table* |
| hide_boots | CuredHide 0.8, Sinew 0.1 | Sewing | Light | 35 | hide_boots | clothing table* |

*clothing table: succeed:injury:LeftArm\|RightArm:0.02=.03; fail:waste_material:CuredHide:0.25=.05; fail:break_tool:Sewing=.02.

Changes from the old numbers, and why:
- Old item counts become kilos through shelters.json's nominal pieces; clothing takes cured hide at about 1.33x the garment's weight (old: a whole 1 kg hide for a 0.3 kg glove).
- Crude edge and sharp rock merge into sharp_rock (no tool; the bootstrap). Old KnappingStone becomes `hammerstone`, a Striking tool (old knapping-stone: 2 stones, 10 min). Knives need Striking, not a named tool.
- Spears follow the roadmap weapon table: staff (new), crude spear = a sharpened pole (old wooden-spear: 3 sticks, knife, 15 min; now 1.5 kg of wood, a stick being too light for a spear), wooden spear = the crude spear fire-hardened (new step, requires Fire), heavy spear = old heavy spear (1 log, 25 -> 35 min), stone-tipped spear = §3.7's example (a wooden spear consumed, flint, sinew or cordage; old: log + stone + fibre).
- Scraping a hide needs a cutting tool (old bug, §3.7). Minimal hide preparation: scrape (old 30 min, 1 -> 0.8 kg), then dry by a fire 4 h to CuredHide (new; stands in for the curing rack, roadmap 11). Clothing needs Sewing, a new capability on the bone needle (old ToolType.Needle; Binding is also a rope belt's, which cannot sew).
- Torches all make the one `torch` kind (old: three names, one burn); a resin torch burning longer waits for the fire model.
- Crafted gear rows: sharp_rock, hammerstone, stone_knife, staff, crude_spear, heavy_spear, stone_tipped_spear, hide_gloves, hide_cap, hide_wrap, hide_leggings, hide_boots (new). flint_knife, hand_drill, bow_drill, fire_striker, wooden_spear, bone_needle, torch rows already exist and are left as they are (their -1 durability included) so combat and the NPC kit do not move.
- Weapon damage on today's scale (knife 6, wooden spear 8, axe 12), keeping the roadmap order: staff 3 Blunt, sharp rock 4 Blade, stone knife 6 Blade, crude spear 6 Pierce, heavy spear 9 Pierce, stone-tipped spear 11 Pierce. The 0-1 table values go in with combat stage 4.
- Clothing stats from the old recipes (insulation, waterproof 0.3, weight, durability 80).

Revised: the shelter code already owned the names `Recipe` and `Project` (Shelter.cs) → renamed them `StructureRecipe`/`StructureProject` (ShelterTests' one `new Project` changed to match, behaviour unchanged) so Make's types carry the glossary names.
Revised: §3.7 writes injuries as `injury:<region>` and the body has no hands → targets are BodyTarget names (LeftArm|RightArm).
Revised: a resource result had no clean mass story → the event `Made` declares Spent (wasted, or worked into a resource result) and Produced (resource result, extra resources); gear results keep every kilo in `MadeOf`; extra gear pieces are named in `ExtraGear` and carry no composition (offcuts).
Revised: breaking a tool's "what is left falls out of the tool's recorded materials" read as salvage (roadmap 8) → a broken tool stays in the pack at durability 0 with its composition, nothing falls out yet.
Revised: one hide through the chain did not add up (dry_hide took 1.0 kg of scraped hide, scraping one gives 0.8) → dry_hide takes 0.8: one hide goes 1.0 raw, 0.8 scraped, 0.7 cured.
Revised: outcomes need a forced-roll seam for effect tests → `Making.Complete(world, agent, recipe, outcome, events)` internal; the activity rolls and calls it.
Revised: Verb needed a table, not one Odds → optional `MakeOdds? Outcomes` on Verb; `Here` gains a `Make` list (every recipe, then abandoning each project).
Revised: `Requires` takes no `target` (repair) input kind yet → only consumed items; repair recipes are roadmap 8.
Revised: the save format was already at version 2 in the working tree (another agent's uncommitted change) → raised it to 3 and regenerated the fixture on top of theirs.
Revised: the random action-sequence law did not know Make → ActionGen gains Make (splittable) and Abandon.
Follow-up: Shelters' Build (structures on a tile) still has its own recipe shape and project; fold it into Make and recipes.json with roadmap 8 (the DR's "one Make activity" is half done).
Follow-up: NPCs do not choose Make yet; Npc.cs keeps its spawn knife and axe "standing in for crafting".
Follow-up: crafted rows reuse flint_knife, hand_drill, bow_drill, fire_striker, wooden_spear with durability -1 (never wears), so break_tool with an amount does nothing to them; give them finite durability when combat and the NPC kit can take it.
Follow-up: combat stage 4 replaces the weapon blocks with the roadmap's 0-1 damage, reach and throw values.
Follow-up: a resin torch should burn longer than a simple one (old intent); torches share one kind and one burn.
Revised (review): saving material shrank a resource result (the result scaled by what was left, saved kilos included) → it scales by what was wasted only; the loader now refuses a resource result heavier than its inputs less any saving.
Revised (review): a tool worn out by the finishing use let break_tool break the next tool, and one item giving two capabilities wore twice → the tools that did the work are found once, each worn once, and break_tool breaks that one.
Revised (review): FailureLike counted waste, which §3.7 does not → fail, injury and break_tool only.
Revised (review): abandoning gives consumed items back whole (80% applies to kilos: a spear cannot come back as four fifths of one) — deliberate, recorded here.
Revised (review): the outcome is rolled at the finishing minute's odds, not the sheet shown at commit → deliberate (hands and light at the end of the work count), said in the code.
Revised (review): effect fields renamed by the naming table: WasteMaterial/SaveMaterial.SharePct, Injury.Amount (DamageInfo's scale).
Revised: the reachability test runs in half a second → not tagged Slow.
