# #151: Shelters, fire pits, tents and bedding

Hypothesis:
- Changes: `Sim/Content/recipes.json` camp section and two new tent recipes; `Sim/Content/shelters.json` and
  `Shelter.cs` (ShelterKind BranchFrame/LogFrame renamed LeanTo/BranchHut in place, same numbers; a built
  shelter keeps its materials); `Make.cs` (a shelter result may name the kind it replaces; a pit result;
  Requirement.MoundPit); `Content.Tables.cs` loader rules; `Fire.cs` (a pit dug cold, or its type raised);
  `Npc.cs` (a strike at a Mound or Stone pit uses the pit's type); `locations.json` and the forage-before-152
  fixture (Collapsed Lean-to kind); `names.json` (the new requirement's phrase).
- Affects: the branch and log frames (become the lean-to, 6 branches, and the branch hut, 12 branches + 2
  rope, with today's times, caps and light); beddings take a cured hide; the sleeping bag takes 4 cured hides
  and Sewing; tents become makeable; a pit's type can now be other than Open, so StartFire's "the pit here is
  Mound, not Open" refusal becomes reachable for NPCs.
- Stays green: ShelterTests (apart from the renamed recipes and inputs), MakeTests, ContentTests,
  ContentStrictTests, ConservationMakeTests (with a declared event for a lean-to's branches coming back),
  FireTests, StationTests, SaveFixtureTests, VerbsTests.
- First red: a new CampBuildsTests test `Hut_OverALeanTo_ReturnsItsSixBranches_AndLeavesOneHut` fails because
  no `branch_hut` recipe exists.

Design:
- Hut over lean-to: the result says `"replaces": "LeanTo"`. NoShelter passes when the standing shelter is that
  kind. The lean-to stands while the hut is worked; at completion it is taken down (finds stay waiting, since a
  hut is going up at once), its kept pieces go back to the builder (Items.Receive, overflow to the pile), and a
  `ShelterTakenDown` event declares them. Reason: the shelter keeps working while the hut is built, and the
  returned pieces are the very branches it was built from ("come back whole").
- Pits: result `{ "pit": "Mound" | "Stone" }`, placed. Fire.Dig makes a cold pit or raises the one here. Refused
  where the pit is already as good. Stone ring requires MoundPit.
- Tents: gear results, Camp category, Sewing, the table's inputs and times, no light (the table lists none).

Revised (review): Shelters.Remove stands a waiting find, which would then block the hut's Place → a TakeDown that leaves finds waiting.
Revised (review): the Collapsed Lean-to find becomes kind LeanTo and a hut could replace it, returning nothing → only a built lean-to (not from a find) is replaced; a found one still refuses as a shelter standing.
Revised (review): renaming the enum would break old saves holding a frame → kept the rename: enums save as numbers in the same order, and saves have no migrations until the rebuild is finished (decision record).
Revised (review): the pit-type mismatch refusal at StartFire becomes reachable for NPCs → StartFire's Pit now only types a new pit; striking at an existing pit uses its own type, so Npc.Strike needs no change.
Revised (review): digging or ringing a pit with a fire in it, and someone else's claimed pit → refused while the fire burns ("put the fire out first"), and Access is asked of an existing pit.
Revised (review): a find found while a hut goes up over a permanent lean-to was queued behind it → queued only behind a temporary shelter or an empty tile.

Checks: an acceptance test per criterion in a new CampBuildsTests (Cycle); a content test that no camp build takes a raw hide and no shelter or tent takes sticks (Cycle); ConservationMakeTests' ledger gains the take-down's returned pieces, so its random search covers a hut over a lean-to (Cycle, few examples); SaveFixtureTests for the new shelter field (Commit); the fast suite before the PR.
Revised: seams under test decided without a person (unattended run) → Sim.Step with a Make activity (as ShelterTests does) for every build, the loaded content (Recipes) for the content rule, and ConservationMakeTests for the ledger.
Revised: ShelterTests.Bedding_NoBetterThanTheOneThere_IsRefused made a sleeping bag with no needle → gives it a bone needle, since the spec's sleeping bag needs Sewing.
Revised: the pre-existing ShelterTests, SubstrateTests, VerbsTests, ContentTests, ContentStrictTests and MakeTests lines that named the branch and log frames → name the lean-to and branch hut, with branches for sticks (the spec's renamed builds); none changes what it checks.
Revised: MakeTests.EveryRecipe_CanBeMade_FromWhatTheLandAndAKillGive could not make the hide tent: the branches filled the pack before its rope was made, and the rope fell to the pile → the tents list their heavy poles last, which changes nothing for a player.
Revised: the same test then could not cure the second mammoth hide: its bags went to the pile, and two cured mammoth hides at the harness's nominal 7.2 kg each overfill the bare 15 kg pack → the harness keeps a bag on, as a player preparing would.
Follow-up: a mammoth hide tent's inputs at nominal weights (2 × 7.2 kg cured mammoth hide + 4 branches of about 2 kg) are more than the bare 15 kg pack holds, and Make takes only from the pack: it needs a bag first. Jack to judge whether that is the intended gate.
Revised: StepRulesTests.StartFire_OnAPitOfAnotherType_IsRefused held that a strike naming another pit type is refused → it now holds that the fire burns in the pit already there, the design review's change (an NPC strikes naming no pit at a mound or stone pit).
Revised: the first red was a compile failure (no LeanTo kind, no ShelterTakenDown), and the pit, tent and bedding tests were written after their code and passed on the first run → each was checked against the spec's table, not the code, and AFireStruck_InAStoneRing_BurnsInIt fails on main's refusal of a strike at a pit of another type.
Revised (review): the pit checks (fire out, as good, someone else's) ran only at the start, so a fire lit mid-dig had its type changed under it → checked every minute in Unmet; CampBuildsTests.MoundPit_StopsWhenAFireIsLitInItMidWork.
Revised (review): a hut could take down someone else's lean-to and keep its branches → Access is asked of the lean-to it replaces (latent while access is unwired in play; not tested).
Revised (review): Making.Standing and Shelters.Replaces read the wrong way round → BlockingShelter and ReplacedBy; TakeDown throws on a shelter with no pieces rather than a bare null dereference.
Revised (review): ShelterTests.LeanTo_TakesItsBranchesAndHalfAnHour checks "already a shelter" with the snow shelter, not the log frame, because a branch hut now goes up over a lean-to by design.
Follow-up: a frame built before this change has no kept pieces, so a hut over it is refused; saves have no migrations until the rebuild is finished, so none was written.
Follow-up: GLOSSARY.md has no entry for fire pit, mound pit or stone ring.
