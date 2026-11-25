# #186: The fire melts snow

Hypothesis:
- Changes: recipes.json (melt_snow: station Fire, driver Heat, no inputs, 10 min, 1000 g of Water, capG 4000), the loader (a station recipe is one discrete piece in and one piece out, or nothing in and grams out up to a cap; a no-input one skips the weight check, a declared source), Recipe/RecipeResult (CapKg), Stations.cs (Transformation.Input nullable; PutOn of a no-input recipe takes nothing from the pack, one melt a fire; Minute adds a litre per full share of work and goes on, idle while the cap waits; the station is dropped only when nothing works and nothing waits), TakeBack (Input nullable: "Stop melting snow"), Verbs.FireVerbs ("Melt snow" through PutOn; the old activity's verb relabelled "Melt snow by hand"), ViewBuilder.KindOf, the conservation tests (melt as a declared water source), SaveFixtureTests.SampleWorld and the fixture.
- Affects: the fire menu (a second melt verb beside the old MeltSnow activity, which stays); Stations.Used (water waiting takes no place of its own: the melt's one place is its transformation); station removal after Take/Take back (today a station holding only continuous Ready would be dropped with its water).
- Stays green: StationTests, StationSteppingTests, ConservationMakeTests, ConservationItemsTests, ContentStrictTests, SaveTests/SaveFixtureTests, GameHostTests, WorkTests (old MeltSnow), the fast suite.
- First red: a new StationTests "snow at a good fire gives 1 L after 10 minutes and 3 L after 30" fails on no recipe 'melt_snow'.

Revised (review): a nullable Transformation.Input would break five call sites that read the piece (Verbs, ConservationWorlds, ConservationMakeTests, InputOf) → StationState.Ongoing holds each no-input recipe at work (recipe, setter, share of the current unit done); Transformation stays one piece.
Revised (review): ViewBuilder would give the melt verb kind "puton", and GameHostTests takes the single puton → a no-input PutOn is kind "melt", the old activity's icon.
Revised (review): the min(unit, cap − waiting) can never bite, since Take takes all → dropped; a unit is added whole while less than the cap waits.
Revised (review): kept, with reasons: an event and a log line per litre, as the old Melt snow told each litre; only shown while the player stands there.
Revised: the hypothesis's nullable TakeBack input → a StopWork activity names the no-input recipe ("Stop: melt snow"): the ticket's "while it is set" needs a way to unset it, and what was made stays waiting.
Revised: a second melt at one fire is refused ("the fire is already set to melt snow"): two would share one cap; refused after the room check, so a full fire names its room.
Revised: the station is dropped only when nothing works, nothing is ongoing and nothing waits (Stations.Empty): Used counts no continuous amount, so water alone would have been dropped.
Revised: pre-existing tests changed: StationTests' Offer(PutOn) singles now name cook_meat (the fire offers Melt snow beside it); StationSteppingTests compares every finish, the melt's litres too; ConservationMakeTests and ActionGen gain StopWork, and a no-input finish declares only its result; SaveFixtureTests.SampleWorld sets snow melting (fixture regenerated); GameHostTests gains a melt-and-take test.
Revised: the old activity's verb is relabelled "Melt snow by hand", so the menu does not offer two "Melt snow"; #188 removes it.
Revised: the finish narrates "1.0 kg water waits at the fire." and takes the water icon; grams read as kilos for water, as everywhere (Names.Grams).
Revised: comment ratchet (CommentHygieneTests) over on Activity.cs and Make.cs → doc lines folded.
Follow-up: water reads in kilos ("1.0 kg water"), not litres, in the menu and the log; a litre word for water is a Names change outside this ticket.
