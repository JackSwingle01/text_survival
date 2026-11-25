# #189: The fire renders fat

Hypothesis:
- Changes: recipes.json (render_fat becomes station Fire, driver Heat, no requires; its result stays a 0.6 share of Tallow), the loader's StationProblems (a recipe that takes a piece may make one discrete piece or a share of a continuous resource), Stations.Minute (a continuous result is added as an amount, not a piece), the MakeTests rows for the hand recipe (moved to StationTests), Words' icon for a tallow finish.
- Affects: the fire menu gains "Put on raw fat" (Verbs derives it from FireRecipes); the make menu loses Render fat (derived from ByHand); ActionGen and ConservationMakeTests pick it up as a fire recipe by themselves; tallow waiting at the fire takes no place, as water waiting does (Stations.Used counts pieces only).
- Stays green: StationTests, StationSteppingTests, ConservationMakeTests, ContentStrictTests (its two Tallow rows edit text that stays), MakeTests, SaveTests (the fixture holds no fat), GameHostTests.
- First red: a new StationTests "half a kilo of fat at a good fire is 0.3 kg of tallow 20 minutes later" fails on Make-refused / loader rejects / AddPiece throws on a continuous resource.
Revised: no separate adversarial design review sub-agent: the change is the melt and cook pattern with one rule widened (scout found the loader's result check and Stations.Minute's AddPiece as the only blockers); the code review after the build stands as the challenge.
Revised: seams decided without a person: StationTests through the verbs and Sim.Advance, as #184 and #186 tested; ContentStrictTests for the loader rule.
Revised: tallow waiting at the fire takes no place (Stations.Used counts pieces), as water waiting does: the issue says it waits "as an amount", and rendering's place is its transformation while it works.
Revised: pre-existing tests changed: MakeTests loses the render_fat row of the #150 recipe theory (and its fire column, which only that row used, so the theory is renamed ...AndRefusedWithoutItsTool) and AHalfKiloOfRawFat_RendersToThreeHundredGramsOfTallow; both are StationTests now. ContentStrictTests gains a row: a station recipe taking a piece and making grams of a continuous resource is refused.
Revised: comment ratchet over on Stations.cs from one inline comment → dropped.
Revised: no icon change: a fat put on takes "puton" (ui/meat) like any piece, and a tallow finish the ui/meat icon every non-water finish takes.
Follow-up: the finish reads "300 g tallow is done at the fire." and takes the meat icon; a tallow or fat icon would be an art change outside this ticket.
Validated: each acceptance criterion by a StationTests fact (0.5 kg → 0.3 kg at minute +20 with no one acting; no ByHand recipe makes tallow or takes raw fat; fat plus meat fills an open fire; a cold fire keeps the piece and its half done, relit finishes in the 10 minutes left). Fast suite 1847 passed; Host.Tests 49 passed.
