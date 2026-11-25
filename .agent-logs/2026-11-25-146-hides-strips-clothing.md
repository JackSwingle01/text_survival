# #146: Hides, strips and clothing

Hypothesis:
- Changes: `rebuild/Sim/Content/recipes.json` (dry_hide 0.9, cut_hide_strips, garment inputs, mammoth scrape/dry, coat, hood), `items.json` (HideStrip, ScrapedMammothHide, CuredMammothHide; CuredHide range to 0.9 of scraped; garment weights; coat and hood gear), `names.json`, `foraging.json` categories, `Items.cs` (Resource enum, appended), `Make.cs` and `Content.Tables.cs` (an `extra_product` of the recipe's own discrete result is that many more pieces sharing the same made weight).
- Affects: scrape_hide already gives 0.8 with Cutting (stays); dry_hide gives 0.875 (→ 0.9); gloves and cap take 1 cured hide (→ 2 strips); wrap takes 3 cured hides (→ 2); a discrete extra_product is refused at load today; no recipe uses MammothHide.
- Stays green: `MakeTests`, `ContentStrictTests`, `ContentTests`, `ItemsTests`, conservation tests, `SaveFixtureTests` (enum appended, numbers unchanged).
- First red: a MakeTests test cutting a cured hide fails on no recipe `hide_strips`.

Revised (review): Complete's later ExtraProduct loop would also add the extra strip as 1 kg of an amount → the result's own extra pieces are counted before the result is made and skipped in that loop; the loader refuses a non-whole count for them.
Revised (review): Names.Outcome would word the extra strip "1.0 kg more hide strip" → a discrete extra reads in pieces.
Revised (review): the strip odds rise with Crafting (0.5 at level 0, 0.04 a level) → the many-runs test reads the odds at level 0 and completes directly; the rise is the existing rule for success bonuses, kept.
Revised (review): CuredHide's minimum already holds 0.08 × 0.9 → only its maximum rises, to 43.2.
Revised (review): Save.Format hashes enum names, so appending still changes the format → regenerate the sample save if SaveFixtureTests asks.
Revised (review): the mammoth coat must weigh at most its lightest inputs, 1.35 kg, against the old game's 4.0 and the loot mammoth_hide_chest's 4.5 → kept the rule and MammothHide's range (narrowing it changes butchering, outside this ticket); the coat weighs 1.35 kg.
Revised (review): EveryRecipe_CanBeMade butchers only a caribou → add loot's resources to its land, which gives MammothHide, rather than butchering a mammoth.
Follow-up: a mammoth butchers to one hide of up to 15 kg and every mammoth recipe takes one whole hide, so a coat (1.35 kg in data, matching the loot mammoth_hide_chest's insulation at under a third of its weight) is one per kill; whether MammothHide should come in several smaller pieces is a balance choice for Jack.
Revised: the lightest cured hide is 0.07 kg (the dry recipe's 0.072 floor is not CuredHide's range), so a third of it is 0.0233 → HideStrip's range starts at 0.023; gloves and cap still weigh under two of them and a sinew.
Revised: the many-runs strip test ran into the dark at 17:00 and the dark costs the hands a tenth of the odds → it works by a kept-up fire in warm air, asserting the sheet reads 0.5 each run.
Revised: pre-existing EveryRecipe_CanBeMade wore its bone needle out (Binding and Sewing both use it) before the boots → MakeOne makes a worn-out tool again from the recipe that made it, as a player would.
Revised: pre-existing BreakTool_CostsWholeUses_OrBreaksItOutright fed hide_cap a cured hide → two hide strips.
Revised: MakeTests.Noon takes an air temperature, -2 °C by default.
Revised: CommentHygieneTests' ratchet put Make.cs and Names.cs at 12% → the new code's comments trimmed to one line.
Revised (review): the loader had two near-identical "whole pieces" arms for extra_product → one arm for gear and discrete resources.
Revised (review): the coat's fail outcome wasted the cured hide, not its main input → it wastes the cured mammoth hide, as the hood does.
Revised (review): no test refused strips or a mammoth scrape without a blade → the cutting refusal test covers scrape_hide, scrape_mammoth_hide and hide_strips.
Revised (review): the third strip is a success bonus, so Crafting raises it 0.04 a level and dark or cold hands lower it → kept: the spec's 50% is the base odds, and every success bonus moves this way (decision "No special cases").
