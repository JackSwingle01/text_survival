# Predator state migration context

## Authored-content repair — September 6, 2026

User rejected generic-menu consolidation and authorized restoring authored content while requiring plausible situations. Original catalog restored, including Pack.cs and removed stalker/spatial scenes. 91 of the 95 archived outcome declarations are verbatim; four use corrected wording. Do not continue the old broad deletion/deferral strategy.

`Actions/Events/AuthoredPredatorScenes.cs` defines explicit scene kinds, source/feature eligibility, and shared physical action binding. Authored observations/follow/withdraw replace the three retired predator meter operations. Event execution revalidates sources; actual movement, fire, bait, and carcass harvesting replace text-only consequences. Observation selects the original scene before falling back to the generic responder. Other tension migrations remain plans only.

Current coverage: [content-coverage.md](content-coverage.md). Rendered and inspected original camp/carcass menus in `/tmp/authored-predator-preview/`. Final validation: all 619 tests pass, including end-to-end harvest-then-travel and source/situation/content coverage. `git diff --check` passes. No commit was created. Separate valley/map/rendering changes were preserved; two xUnit assertion forms in ValleyWorldTests were mechanically corrected to unblock compilation.

## Original migration progress — 2026-09-06

Implementation complete. User authorized the change after reviewing the plan.

Validation: all 571 tests pass (baseline 545); 32 predator migration cases cover identity, perception, search, food, deterrence, retreat, interruptions, old/new saves, and species defense. The production HUD and response overlay were rendered and visually inspected at `/tmp/predator-hud-preview/hud-7.png` and `/tmp/predator-hud-preview/predator-response.png`.

Main additions: `Actors/Animals/PredatorInteraction.cs`, `Actions/Events/PredatorEventFactory.cs`, and `text_survival.Tests/Animals/PredatorInteractionTests.cs`. The old pack event file was removed; shared responses now serve pack/stalker and contextual scenes. See `retired-outcomes.md` for deleted unsupported weighted branches.

## Key files

- `Actors/Animals/Herd.cs`: shared animal state, movement, boldness, members.
- `Actors/Animals/AnimalPresence.cs`: territory-based presence; needs a distinct physical/detected presence query.
- `Actors/Animals/Behaviors/{PackPredatorBehavior,SolitaryPredatorBehavior,ScavengerBehavior,HerdUpdateResult}.cs`: behavior integration and existing source-bound encounter requests.
- `Actions/GameContext.cs`: simulation ordering, encounter queue, species-only animal fallback.
- `Actions/GameEvent.cs`: event result mutations and species-only encounters.
- `Actions/Events/{GameEventRegistry.Pack,GameEventRegistry.Threats,GameEventRegistry.Locations,OutcomeTemplates,Situations,TensionEventFactory,EventQueue}.cs`: main content and event integration.
- `Actions/ConditionChecker.cs`, `Actions/EventCondition.cs`: old tension predicates.
- `Actions/Events/Variants/TrailSignVariant.cs`, `Environments/Features/EnvironmentalDetail.cs`: inspection currently creates predator tensions.
- `Combat/CombatOrchestrator.cs`, `Combat/CombatAftermath.cs`: real herd participants and persistent consequences.
- `Environments/Features/{CacheFeature,CarcassFeature,PlacedNet}.cs`: food representations and indirect consumers.
- `Desktop/UI/SurvivorPanel.cs`, `Desktop/UI/EventOverlay.cs`: tension presentation.
- `Persistence/SaveManager.cs`, `Persistence/GameInitializer.cs`: reference-preserving save graph and load initialization.

## Constraints and follow-up boundaries

The worktree was clean at implementation start; the earlier surface and HUD work had become the current baseline. Changes preserve those systems and use their perception, surface accessibility, traversal, ground-item storage, and HUD layout.

Dedicated saber-tooth/scavenger tensions remain outside this pass. Common predator encounter admission nevertheless requires a real source and prevents duplicate pending combat. Other prey, cold, shelter, and disease tensions remain.

Detection uses existing sight and adjacent food/blood cues. Search expires after 20 game minutes without contact, and approach decisions use game-time intervals. Numbers represent concrete animal behavior; no generic threat severity survives.

## Verification commands

- `dotnet test text_survival.Tests/text_survival.Tests.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false` — 571 passed.
- `dotnet run --project tools/HudPreview --no-build -- /tmp/predator-hud-preview` — production screenshots rendered and inspected. The preview was built with the game changes and now includes a predator scenario; it disables ImGui settings persistence.
- `git diff --check` — clean.

The .NET test runner required local socket access; the HUD preview required the local window service. Both ran successfully with tool-reviewed escalation. No gameplay save was loaded or modified by the preview.

## Resume

No implementation work remains for this migration. Future work can migrate dedicated species arcs or enrich perception/food behavior. Read the implementation result in the plan before extending it; do not restore compatibility severity meters.
# Content preservation follow-up — September 6, 2026

Mechanical completion does not establish narrative equivalence. User raised lost flavor; [content-restoration-plan.md](content-restoration-plan.md) now maps scene families and all 12 named staged scenes to grounded replacements or explicit dependencies. The first contextual restoration slice and per-entry coverage assessment are now implemented; specialized scenes remain deferred. Do not expand generic-menu consolidation to other tensions without the new content preservation requirement.
