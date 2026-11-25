# #144: Fire starting: better tinders and resin tinder

Hypothesis:
- Changes: `Sim/Content/recipes.json` (fire_striker: no tool, no light, 5 min; new resin_tinder), `Sim/Content/fire.json` (bonuses; a ResinTinder fuel row), `Sim/Content/items.json` and `names.json` (a ResinTinder resource, continuous), `Sim/Items.cs` (`Resource.ResinTinder` appended), `Sim/Fire.cs` (`Fire.Tinders` takes resin tinder, best first).
- Affects: the strike odds sheet (`Fire.StartOdds`, already clamped 5-95%) for birch bark 0.25→0.30 and amadou 0.20→0.40; which tinder the Light verb and the NPC pick (the first in `Fire.Tinders` the pack holds); making the striker, which today needs Striking and light.
- Stays green: OddsTests, FireTests, ForecastTests, MakeTests, ContentStrictTests, ConservationMakeTests, SaveFixtureTests (enum appended, so the fixture's numbers hold).
- First red: a new FireStartingTests fails because the fire_striker recipe is refused in the dark with no tool, and the forecast still shows amadou at +20%.
Revised: the 95% cap already exists (`Fire.StartOdds` clamps 5-95%) → no code change for it; the property test runs through `Fire.ForecastLight` in a real world (tool, tinder, skill, time of day and its weather, wetness, dazed) so it checks the forecast the criterion names, plus one over the formula alone.
Revised: the strike-a-light already exists as `fire_striker` (Striking, light, 10 min) → changed it in place to no tool, no light, 5 min; kept its id, name and 5% flint-waste outcome.
Revised: the spec gives resin tinder no burn properties → a fuel row like tinder's (peak 520 °F, 2.0 kg/h, catches cold); reversible data.
Revised (review): putting resin tinder first in `Fire.Tinders` made the NPC plan, with no tinder held, against +45% odds it can't forage → its fallback is now plain tinder (`Npc.cs` Strike), like the Light verb's.
Revised: `foraging.json` hints needs a row for every resource → "kindling", as tinder.
Revised: `FireTests.StartChance_MatchesOldTable` and `OddsTests.FireOdds_AtTheExtremes_AreCappedAndSayWhy` hardcoded birch bark +25% and amadou +20% → the new +30% and +40%; `OddsTests.Tinders` gains resin tinder.
Revised: Make's minutes on a cold dark night came to 6, not 5 → the tests check the recipe's 5 minutes, not the elapsed time.
Revised: the "TinderBundle" loot table (`loot.json`, `discoveries.json`) yields plain tinder and is no resource or recipe → left as is; `ThereIsNoTinderBundle` checks resources, recipes and gear.
Revised (review): the parity row's "Reshaped" isn't a legend status → "Replaced", with no size.
Revised (review): the Light verb and the NPC each picked the tinder with their own copy of the rule → one `Fire.TinderToStrike(pack, kg)`; `Fire.Tinders` is now sorted by fire.json's ignitionBonusPct (ties keep the listed order), so a retune can't leave a stale order.
Revised (review): the forecast property test's comment claimed any weather, but the world's weather is fixed and it reaches the odds only through dexterity (darkness, wet hands) → the comment says that; varying the time of day and the wetness covers both routes.
Revised (review): the review asked to rename the recipe "Strike-a-light" → kept "Fire striker", the gear's name in names.json, the save fixture and the player's messages; the spec's word describes it.
Revised (review): the review asked whether the striker's 5% flint-waste outcome should stay → kept: the spec changes the striker's tool, light and minutes, not its outcomes.
Revised (review): the "TinderBundle" loot-table key → left, as above (it names a cache of plain tinder).
Revised (review): `CommentHygieneTests` failed on Npc.cs and then on Fire.cs → removed the Npc comment with the shared helper, and trimmed the Fire.cs comments to one line.
