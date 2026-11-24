# An animal-free test world

**Hypothesis.** Loading the cached world costs about a quarter of the quick tier's summed test time
(2,640 loads, 24.5 s). The tests that strip the animals after loading would load faster from a cached
template that never had them.

Revised: stripping is not where the loads are (32 of them). One helper, `VerbsTests.AssertEveryVerbIsTrue`,
makes 1,042: a fresh copy of the world for every verb it lists. And the animals are nearly all of a save:
516 of seed 17's 520 KB. A world with only the player loads in 0.19 ms against 9.5 ms and saves in 0.08 ms
against 6 ms -> `TestWorlds.Bare(seed)` is for every test whose subject is not the animals, not only the
ones that removed them.

- `TestWorlds.Bare(seed)`: `World.New(seed)` with every agent but the player removed, cached per seed.
  `TestWorldsTests` checks it saves the same as doing that by hand.
- `KillChainTests.Bare`, `NetProgressTests` and `ConservationWorlds` did the removal themselves and now
  ask for it.
- The world helper of 19 classes now starts from it: Verbs, StepOutcome, Narration, Work, ForageRestore,
  Memory, Skills, Carcass, Shelter, Pools, Items, Feels, Exposure, Fire, FireBody, StepRules, Combat
  (`Field`), CombatReach, Substrate.

Revised: `ForageFollowupTests.WithPools` stays on the populated world: two of its tests lay a carcass of
an animal that ranges there.

Revised: 28 tests tagged LongRun are under 0.8 s (most of `VerbsTests`' among them) -> untagged. 37 remain.

Measured locally on four cores, `Sim.Tests` fast suite: 144 s -> 112 s of CPU, load time 24.5 s -> 10.9 s
before the combat and substrate helpers moved.

Revised: went through the tests that call `TestWorlds.New` directly. 84 more calls in 24 classes moved
(weather, skills, step rules, pools, carcass, odds, trip, surface, items, ground, crossing, work, fire,
survival, sight, shelter, priorities, feels, convection, and parts of travel and memory), summed test time
173 s -> 136 s. Three more tests lost the LongRun tag; 34 remain.

Left on the populated world on purpose: the herd, individuals, renewal, kill-chain, herd-budget, NPC, combat,
settle, report, golden, gate, core-determinism, architecture and route-search tests; `TravelTests.RunRoute`
(a route through a save and load, the same world either way); `MemoryTests.ANewWorld_HasLookedAroundOnce`
and `NpcMemory_IsTheSharedModel`; `SkillsTests.AnNpc_LearnsByTheSameRules`.

Follow-up: a test that passes on an animal-free world no longer shows that the thing it checks holds with
herds living around it. The classes moved here are about the player's own verbs, work, shelter, fire and
gear; Jack may still want a populated-world run of some of them, nightly.
