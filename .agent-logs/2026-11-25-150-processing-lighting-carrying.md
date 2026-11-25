# #150: Processing, lighting and carrying

Hypothesis:
- Changes: `rebuild/Sim/Content/recipes.json` (render_fat, willow_fibre; three torch recipes become torch and resin_torch; small_pouch, rope_belt, hide_belt, large_bag, mammoth_hide_pack), `items.json` (resin_torch gear with its burnMinutes; torch burnMinutes 60; mammoth_hide_pack; carrying gear weights held to their lightest inputs; proper_belt named Hide Belt), `Items.cs` (GearDef.BurnMinutes), `Content.Tables.cs` (a continuous result may be a sharePct of the inputs' actual weight; only a torch burns, and every torch states it), `Make.cs` (a continuous share result), `Fire.cs` (a torch lit burns its kind's minutes; TorchBurnMinutes goes), `Web/Host/ViewBuilder.cs` (a share result names no grams).
- Affects: torches all burn Fire.TorchBurnMinutes (60) and heat 3 + 2 × the share of that left; a continuous result is fixed grams scaled by what was wasted; rope_belt weighs 0.4 kg (above 2 ropes' 0.2), proper_belt 0.3, large_bag 0.8; no carrying gear, fat or willow recipe exists; ItemsTests' capacity test reads rope_belt's weight.
- Stays green: `MakeTests`, `ContentStrictTests`, `ContentTests`, `ItemsTests`, `FireTests`, `ConservationMakeTests`, `SaveFixtureTests` (no saved type changes: burn minutes live on the gear kind).
- First red: a MakeTests test rendering a 0.5 kg piece of raw fat fails on no recipe `render_fat`.
Revised (review): Fire.TorchBurnMinutes is read by StepRulesTests and Tools/Tune → both read the torch kind's burnMinutes.
Revised (review): ItemsTests' carried-weight assertion counts the bag at 0.8 kg and the belt at 0.4 → its expected weight follows the new gear weights.
Revised (review): Fire.Torch removes the torch before reading it → it takes the torch once; with both kinds carried the first in the pack is lit, as any tool is found (Items.Tool), kept deliberately: the ticket gives no choice of torch.
Revised (review): the recipes.json and items.json headers, RecipeResult's doc and the loader's message describe a continuous result as grams only → they name the sharePct form and burnMinutes; ContentStrictTests covers both new rules.
Revised (review): feature-parity rows for processing, torches and carrying are stale → updated.
Revised (review): proper_belt made by hide_belt → the gear kind is renamed hide_belt (loot.json its only other reference).
Revised (review): willow fibre as g 80 works differently from render fat's share → both are sharePct (0.8, 0.6).
Revised (review): the hide belt's 46 g looks like a typo → items.json says it is two of the lightest strips.
Revised: seams chosen without a person to ask → MakeTests through Making and the world, ItemsTests through Items.CapacityKg, FireTests through LightTorch, ContentStrictTests through broken content; each is where its existing tests sit.
Revised: pre-existing EveryRecipe_CanBeMade's pack-lightening, run inside a sub-recipe (cordage for the large bag's rope), sent back the cured hides the bag had already fetched from loot → MakeOne keeps what the recipes it is being made for use.
Revised: the carrying recipes needed a menu category → MakeCategory gains Carrying (appended; not saved).
Revised: the ticket names no outcomes for the new recipes → the pouch and hide belt can waste half their bark or strips on a fail, the bag and pack sew as the garments do (a cut, a wasted hide, a broken needle), render fat, willow fibre and rope belt roll none; the torches keep their old ones.
Revised: CommentHygieneTests put Fire.cs at 13% → the TorchFadeMinutes comment went; the name says it.
Revised: Fire.TorchBurnMinutes did not simply go → it became TorchFadeMinutes (60), the window a torch's heat fades over (3 + 2 × min(1, left / 60) °F), so a resin torch holds a torch's full heat until its last hour and FireState gains no saved field.
Revised (review): the continuous share check copied the discrete one word for word → one check after the discrete piece-range check.
Revised (review): Make.cs's comment said only a discrete result is a share, and kgMade was declared then assigned → comment names both forms, one declaration.
Revised (review): the loader's continuous-result condition mixed && and || ungrouped → grouped; TorchFadeMinutes private, only Fire reads it; TheCarryingThings_Stack reads Items.BaseCapacityKg.
Revised (review): a recipe result form switched on in the loader, Making.Complete and ViewBuilder, string lookups of the torch's burnMinutes, small_pouch's id, the test's input strings → kept: each is one line at an existing seam, and the pouch's id is what loot and saves name.
Revised (review): the last line's reason for small_pouch is wrong, nothing outside items.json and recipes.json names it → kept the id anyway: the recipe and gear ids match, and the display name says birch bark.
