# Flaky FireHandlerTests.ManageFire_AddingFuel_ReachesTheLocationFire (#167)

Hypothesis: the fuel is refused, not misrouted. `HeatSourceFeature.CanAddFuel` gates a log on fire
temperature, so something random between lighting and adding (weather, an event) cools the fire.

Revised: confirmed, and the something is a random event. Starting from an ember passes 5 minutes
through `Pacing.PassTime` with events allowed. 30,000 fresh games in one process: 54 rolled an event,
18 failed, every failure after Old Ache, Fugue State or Melting Reveal, which cost 29 to 125 minutes.
The 0.5 kg of kindling burns out, the fire is ash or embers below birch's minimum temperature, and the
log stays in the pack (one fire on the location, nothing lost). The game is right; the test left the
event roll to chance. `ManageFire_StartingFromEmber_PutsTheFireOnTheLocation` has the same exposure
(it asserts the fire is still burning).

Revised: the rate I measure is about 1 in 1,700 per fresh game (0 failures in 680 fresh processes
before the fix), not the 1 in 25 reported. I found no second cause. Event cooldowns are thread-static
and outlive a game, so within one process the events stop after the first few; that lowers the rate
in a long-lived process, it does not raise it. Other test classes seed the thread's random stream
(`Utils.Seed`) and leave it seeded, so in the full suite the roll depends on which class ran before
on that thread; that may make the suite rate differ from the alone rate. Not measured.

Fix: seed the stream in the class constructor (as `DamageProcessorTests` does) and assert in both
ManageFire tests that no event was shown, so a seed that stops avoiding events fails by name.

Checked: `FireHandlerTests` 200 fresh processes, 200 passes; `text_survival.Tests` 750 passed.
Not checked: the 1 in 25 figure, as above.

Follow-up: `FireHandler.AddFuelWithResult` returns "Added Birch to fire." and credits
`RelationshipEvents.TendedFire` when the fire refused the fuel (too cool, or full).
Follow-up: `GameEventRegistry` cooldowns are thread-static and cleared only by `GameContext.CreateNewGame`
(`GameContext.cs:372`) or by hand, so `SmallGame` tests inherit cooldowns from
whichever test ran before them on the thread.
