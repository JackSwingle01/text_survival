# Rebuild roadmap (re-ordered 2026-11-21: playable MVP first)

Nothing below was dropped by the re-ordering; it only moved. Design is in
`documentation/emergent-world-design.md`, decisions in `documentation/decisions/`, open deferrals in
`dev/active/rebuild-deferred.md`.

## The MVP: a run you can play

A desktop run where you survive, prepare, hunt, get hunted, and try the pass.

1. **Finish the fix rounds in flight.**
   - Exposure and NPC stat-tier review fixes, including the two body-code bugs.
   - Conservation fixes: butchering waste, per-fuel burn, fuel dust, the mammoth by hand, rounding.
   - Commit the travel determinism fix and the random action-sequence tests with them.
2. **Predators engage the player.** One general engagement rule, through the herd mind's boldness,
   with fire's heat and light as a boldness term. Applies to NPCs and the player alike.
3. **Hunting that can work.** Retune per `dev/active/rebuild-combat-balance.md`: detection over the
   approach, spear lethality, following a wounded animal. A careful, prepared hunter usually gets
   meat.
4. **Crafting core.**
   - The Make system as designed (§3.7): recipes and outcome tables as data, projects, materials
     recorded on items, skill on odds.
   - About 25 recipes: fire-starting, cutting tools, spears, torches, basic hide clothing, cordage.
5. **Desktop UI, first slice** of `dev/active/ui-design.md`, in parallel with 4 once the
   player-facing core API is stable:
   - the map drawn from the player's knowledge;
   - the always-on layer (clock, vitals, place);
   - the tile's action list;
   - odds sheets for fire and crossing;
   - the combat screen;
   - save and load, death and win.

Injuries heal slowly on their own in the MVP (no treatment yet).

## After the MVP (order as listed)

6. **Body review and verification** (task 14).
   - The review is already running.
   - Grill the user on its design questions.
   - Coverage and mutation baseline, fixes, laws to 90%+.
7. **Step 4, the rest** (task 6).
   - 4.3: the effects kernel for bodies and things, ticked per minute, meeting the verification bar.
   - 4.4: conditions and clearing actions with tool tiers, Contest and Ask, Interact, folding tags
     into conditions, crossing as odds (the parts not already pulled into the MVP).
8. **Crafting, the rest** (task 10): the remaining old recipes, repair and salvage recipes, bear and
   mammoth gear.
9. **Settling pass** (step 3b): opt-in world-generation settle, with distant agents stepping
   hourly.
10. **Emergent steps 5-6** (task 7): the full interaction table (dens, contested kills, seasonal
    stocks), then NPC travellers. Travellers depend on NPC competence.
11. **Parity systems** (task 11): treatment and medicine, small game and snares, fishing and ice
    holes, curing.
12. **NPCs in the world** (task 12): NPCs placed in a real game, companions, bodies to loot or bury,
    relationship memory.
13. **Desktop UI, the rest, and retiring the old project** (task 13):
    - full parity per `documentation/feature-parity-checklist.md`;
    - port or retire the old tools (GameCli, HudPreview, UiPreview, NpcSim, TerrainPreview);
    - then retire the old project.
14. **Verification 4-6** (task 19): fire properties, odds, loader and save fuzzing, the NPC
    regression floor.

## Design decisions still open (raise when their step comes up)

- **Food balance:** NPCs survive 60 days but end near starvation; the forage focus gain; the NPC's
  daily schedule (`rebuild-npc-food-and-forage-review.md`).
- **Hunting thresholds:** bison only when starving; prey worth computed from the animal
  (`rebuild-npc-hunting.md`).
- **Mammoth sinew:** about 300 kg, uncapped.
- **The Bloody effect:** its treatment floor contradicts the "dries on its own" comment.
- **Combat balance retune:** folds into MVP step 3.
- **World-density checks:** measures and floors (parked).
- **The body review's design questions** (step 6).
