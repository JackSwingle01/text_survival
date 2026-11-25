# #190: Drying follows the weather and the fire

Hypothesis:
- Changes: Exposure (a minute-addressable exposure for a thing standing on a tile: air with the tile's cold, the pit's air and radiant heat, wind past walls and shelter, precipitation reaching it under the tile's or shelter's roof), Stations (DryingRatePct from that exposure replaces the constant 1; the rack's minute catches the tile's pit up first; RackFinishMinute walks the weather forecast and Fire.Ahead; Slowed gains Snowing/Raining, StationProgress gains what speeds it), Narration (the why words), Web ViewBuilder (speeding words when nothing slows).
- Affects: racks dry at exactly the recipe's time in every weather (RackTests' Mild sky is 5 °C, which the new rule would speed up); the rack's menu has no why line; Stations.Minute catches up only fires with station work of their own.
- Stays green: RackTests (with the dry, still sky moved below the reference air), StationTests, ForecastTests, StationSteppingTests, ArchitectureTests (the one exposure calculation), CommentHygieneTests, GameHostTests.
- First red: RackTests.Meat_MakesNoProgress_InFallingSnow fails on the rack finishing in 24 h under a blizzard.

Revised (review): reading Weather.At at future minutes would show a finish that already knows when the snow stops, and the glossary's Forecast assumes everything else stays as it is now; no weather forecast module exists → the rack's forecast holds the sky as it is now and varies only the fire (Fire.Ahead), walking minute by minute with Advance so it matches the real minute exactly; rate 0 with no fire to change it is no finish.
Revised (review): at 32 °F reference with a 36 °F span a steady birch fire (about 37 °F of heat at the rack) barely speeds a rack on a typical winter day (−4 °F gives 1.04) → reference 14 °F (−10 °C, the middle of the overlap of winter's −30..−5 °C and spring's −15..5 °C ranges, Weather.SeasonalRanges), span 36 °F (20 °C: a steady fire at the reference doubles it; a 10 °C day without one does too), wind +0.2 per gale level (the body's DryingPctPerHour gives 1.5 per level, "a lot"; the spec says "a little").
Revised (review): a max(tile, shelter overhead) ≥ 0.5 rule would disagree with Make's Dry requirement → one rule in Exposure, Make's own: rain or snow reaching the tile (scaled by its overhead) falls on it unless a shelter's roof is at least 0.5; Make and the rack both call it.
Revised (review): Fire.CatchUpOn catches torches too and can remove a spent one mid-scheduler → the rack's minute catches up only the tile's pit (PitAt, CatchUp).
Revised (review): Shelters.CoverNow re-melts a snow shelter copy from its last settle on every call → the rack's minute reads the shelter through Shelters.CoverFor (settles it); the forecast reads CoverNow once.
Revised (review): Fire.Around requires the pit current, so it can't take Fire.Ahead's copies → a FiresAround built from one pit state, which Around shares; after Ahead ends the forecast keeps its last (cold) copy, as the real tick would read.
Revised: a fire inside a shelter dries no faster than outdoors (insulation 0 for a thing), and a shelter's walls cut the wind term → accepted: the rack has no body heat to hold in.
Revised: the issue names the menu's why line only → Slowed gains Snowing and Raining; StationProgress gains Sped (FireBeside, WarmAir, Wind, the largest term) shown when nothing slows it.
Follow-up: a rack that stops for rain or snow raises no TransformationStopped event (its Fire field is a FireId; changing it is a saved-type change); #185's stop event for racks is not built here.
Revised: RackTests' Mild sky (5 °C, wind 0.1) is above the reference air and windy → it is now −12 °C and still, the dry, still day the 24 h tests mean.
Revised: RackTests.Meat_DriesAtFullRate_InFallingSnow asserted #187's interim rule → replaced by Meat_MakesNoProgress_InFallingSnow_AndLosesNothing.
Revised: GameHostTests' rack test asserted the seeded world's finish at 24 h − 1 → it asserts the page shows the sim's own forecast, since the seeded sky now sets the rate.
Revised: Make's Falling and DryRoofLevel → moved into Exposure (FallsOn, DryRoofLevel), so Make's Dry requirement and the rack read one roof rule.
Revised (review): a drying rate up to 2 and added terms broke principles-code's Pct rule (0 to 1, multiplies) → DryingFactor, MaxDryingFactor, GaleDryingDelta, WarmDelta.
Revised (review): SpedBy repeated DryingRatePct's formula → both read one DryingDeltas (air, fire, wind).
Revised (review): SlowedBy's default arm called any precipitation snow → Snow and Blizzard named, anything else throws.
Revised (review): the drying constants' comment pointed at the work log → it says the reason in place.
Revised (review): the predators spec makes an item's cover additive with the tile's, where FallsOn takes Make's rule (the tile's overhead thins what reaches it; only a shelter's roof of 0.5 keeps it off) → kept: no item cover is built yet (#124), and changing Make's Dry requirement is outside this ticket.
Follow-up: when items' cover lands (#124, predators spec "Cover is additive with the tile"), the rack and Make's Dry requirement should read it, so a rack under a rock overhang tile stays dry without a built shelter.
Follow-up: the stepping property covers a rack beside a fire in a dry sky; one with weather that starts and stops and a melting snow shelter would need the seeded timeline (ConservationWorlds.Seeded) and a longer horizon.
