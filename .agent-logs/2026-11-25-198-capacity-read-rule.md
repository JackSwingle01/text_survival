# #198: Dependency rules, and the list of what reads a capacity today

Library: ArchUnitNET (TngTech.ArchUnitNET 0.13.5, Apache-2.0, TNG, releases through 2025, reads the
compiled assemblies through Mono.Cecil, has "should not depend on" rules and slice cycle rules
(`BeFreeOfCycles`)). Against: NetArchTest.Rules (MIT) has had no release since 2021 and no cycle rule;
its maintained fork NetArchTest.eNhancedEdition is smaller and less used. ArchUnitNET also gives
dependencies per member with lambdas and local functions folded into their method, which the
exception list needs.

Hypothesis:
- Changes: Sim.Tests.csproj (ArchUnitNET, and a reference to Web/Host so the screen's reads are
  seen), a new `Architecture/CapacityReadTests.cs`, `documentation/clean-room-architecture.md`.
- Affects: no behaviour. Today 12 sim members (Travel, Work, Making, Fire, Combat, Fauna, Senses,
  Sight, Shelters), 2 in the screen (Web/Host ViewBuilder) and about 50 test members read a capacity.
- Stays green: everything; no existing test changes.
- First red: `CapacityReadTests.OnlyTheBodyReadsACapacity` fails listing every read before the list
  is written; the guard test fails on an empty list against a breaching sample class.

Revised: ArchUnitNET keeps one dependency per member and target, so a member's reads cannot be counted → an entry names the member and the capacities it reads ("Work.ImpairmentFactor: Breathing, Manipulation, Moving"). A read in another member, or of another capacity in a listed member, fails; the same capacity read again in the same member does not. Chosen over counting IL calls through Mono.Cecil beside the library: one mechanism, and the capacity names are what the later tickets replace with faculties.
Revised (review): `Body.RegionCapacities` is a public read of capacities (as an array) → counted as a read; BodyTests' two uses are listed.
Revised (review): lambdas, local functions and iterators could have come out under compiler-made names → the breaching sample holds each kind, and its test checks they come out under the member they are written in. They do.
Revised: the rule's own test class names the `Capacities` type (its static constructor) → it is left out of the scan with the breaching sample.
Revised: `WhereThingsGoTests.SetRatchet` was private → made internal and reused; visibility only, no test changed.
Revised: "the screen" is `Web/Host`, its own assembly → Sim.Tests references Host so its compiled code is read too. Cli and Tools are not read; neither reads a capacity today.
Revised: the list holds 12 sim, 2 screen and 37 test members (the issue's "about fifty" counted calls, of which several share a member).
