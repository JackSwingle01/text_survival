# Rebuild roadmap (re-ordered 2026-11-21: playable MVP first)

Nothing below was dropped by the re-ordering; it only moved. Design is in
`documentation/emergent-world-design.md`, decisions in `documentation/decisions/`, open deferrals in
`dev/active/rebuild-deferred.md`.

## Restoring the old game (2026-11-22)

The rules are in `dev/active/porting.md`: the old game is the default, improvements are flagged for
the user, nothing reachable becomes unreachable. The audit's 130 items are in
`dev/active/parity-audit-items.json`.

The order:

1. Foraging and discovery.
2. Discrete and continuous resources (decision "Resources are discrete or continuous").
3. Reachability: recipes, gear, picking gear back up.
4. Consequences: wear, final death and autosave (decision "Death is final").
5. The remaining screens: fire, body, butchering, combat feedback, travel, the tile sheet.

Decided by the user on 2026-11-22:

- **Foraging (the first restore).** The forage screen in the prototype's style, with the old game's
  behaviour:
  - focus: Fuel, Food, Medicine, Materials or General, offered only where that category grows;
  - time: 15, 30 or 60 minutes, or Keep Walking (5 minutes, new clues);
  - the ground's quality word, and how much of the hidden finds is searched;
  - finds appearing as you search, with counts;
  - the luck mood line, and the failure and discovery text.

  Focus works as the old filter: the focus category is kept whole, the others cut to 40-60%. The
  rebuild's `FocusGain` 1.3 reverts (an agent tuned it toward a survival number). The Foraging skill
  stays (the user asked for it). The old clues are ported faithfully: the same texts, terrain pools,
  weighting and multipliers (x1.2-1.5), followed by choosing the matching focus. Later, as a separate
  decision: a clue could place a real patch (an extra stock of those resources on the tile, sized to
  give the old multiplier, and depleting), so clues become true and lasting.
- **Discovery.** Discoveries that place real things in the world come back. For the others, work out
  whether each can become something real rather than an event. Each new one gets a design session
  with the user (decision "Authored discoveries stay until the world produces them", amended
  2026-11-22).
- **Wear.** Tools and clothing wear out, as in the old game.
- **Crippling injuries.** Keep the rebuild's current behaviour until a design session with the user.
  *To do: hold that session.*
- **The stomach.** Decision "The stomach holds a fixed amount": eating is refused at full, and food
  goes to fat as it digests.

Also decided by the user on 2026-11-22 (audit ids in brackets):

- **Camp storage (ITM-14).** The old caches come back: the camp cache holds 1000 kg; natural caches
  have a capacity and are predator-safe or weatherproof. Predators eat exposed food again. Design
  session (1) below comes first.
- **Ember carriers (ITM-15).** Restored, and made to work: you carry an ember and light a fire from
  it with no strike. Deferred to later in the port.
- **Crafting in the dark (ITM-17).** The old behaviour: darkness, wet hands and injury slow crafting,
  never refuse. The slowdown is named and shown in the UI, with no warning text. Recipes that truly
  need a fire still need one.
- **Recipe inputs (ITM-18), missing tool recipes (ITM-8).** Settled by design session (2), 2026-11-24:
  `documentation/specs/2026-11-24-recipes-that-make-sense.md`.
- **Crafted gear (ITM-19).** New gear goes into the pack. The rebuild's change stays.
- **Camp builds and recipes (ITM-20).** Settled by decision "Resources are discrete or continuous":
  both count discrete resources in pieces.
- **Curing rack (ITM-21).** The old rack comes back: 4 items; hide 48 h, meat 24 h, berries 12 h,
  fish 16 h; it cures while you are away. Design session (3) also adds a less effective drying slot
  at a campfire.
  *Amended 2026-11-24 (grilling for #54):* no drying slot at the campfire. The hours are for a dry,
  still day: the rack's rate comes from the exposure at it, so a burning fire on the tile speeds it
  (up to twice) and rain or snow on an uncovered rack stops it. Drying a hide by hand at a fire goes.
  See decision "Transformations are recipes worked by a station, at a rate set by exposure".
- **Hide scraping (ITM-22).** One whole hide becomes scraped hide. One hide is one garment's worth.
  *Amended 2026-11-24 (recipes spec):* the scraped hide is 0.8 × the hide's own weight, not a fixed 0.8 kg.
- **Firecraft (FIR-4).** Keep the rebuild's +2% per level. The playtest's fire "death spiral" was the
  player's skill, not the game.
- **Tinder bundle (FIR-5).** *Reversed 2026-11-24 (recipes spec):* no bundle. Birch bark and amadou
  are better tinders on their own (+30%, +40%), and resin tinder (tinder + pine resin) is the best
  (+45%). Every fire-starting chance is capped at 95%.
- **Washing off blood (FIR-12).** Restored: 0.5 L of water and 5 minutes removes Bloody and leaves you
  slightly wet. Bloody still dries on its own too (decision "No effect needs a treatment the game
  doesn't have", amended 2026-11-22).
- **Drinking (FIR-13).** Frozen water needs an ice hole or snowmelt, as in the old game. Drinking
  straight from open, unfrozen water stays: an improvement the user accepted.
- **The night-no-fuel alert.** Not an alert: a fact on the fire panel, such as "burns about 3 h ·
  dawn in 9 h" (decision "The UI shows what the game knows"; `dev/active/ui-design.md`).

### Design sessions with the user

Each waits until the restores before it are done.

1. **Predators and items:** what counts as exposed food, which caches protect it, how far scent
   reaches, and whether the rebuild's scavengers (ravens) stay.
2. **Recipes that make sense:** done 2026-11-24, see
   `documentation/specs/2026-11-24-recipes-that-make-sense.md`.
3. **The fire as a workbench:** the jobs in `dev/active/short-loop-plan.md` §6 and the campfire drying
   slot, together with the curing rack. Grilled 2026-11-24 (#54); the spec follows.
4. **Animal life** (after the foraging restore). The brief, decided by the user on 2026-11-22. Why:
   the user expects emergence from animals travelling to water and food, sleeping at their natural
   times, avoiding predators and seeking prey.
   - **Body needs are shared components**, used by people, NPCs and animals alike: hunger (calories,
     fat, then organ damage), thirst (hydration, then organ damage), rest (energy, sleep) and warmth
     (left off animals for now). Which components a kind of body has is set in code, not data: it
     does not vary meaningfully between species.
   - **Starvation comes from the body** for every species. It replaces the wolves-only starvation
     rule in Renewal (`Renewal.cs:68-74`) and the "shortfall ignored" floor in animal metabolism
     (`BodySurvival.cs:384-390`; animals run `Fauna.Tick` in place of `Body.Tick`, `Sim.cs:264-272`).
   - **One shared planner for seeking a need**, used by NPCs and animals: where to meet it and whether
     the trip is worth it (travel plus the time to satisfy it, judged against staying put). Routes
     and step times are already shared (`Trip`, `Travel`). Sources per need: food (grazing ground,
     prey, carcasses), water (rivers, lakes, wet tiles), rest (a den if the herd has one, else where
     it stands).
   - **Herds rank their needs** on a simple version of the tier ladder. Herd-only behaviour stays in
     the herd mind: fleeing, hunting together, pack cohesion, defending a kill.
   - *Superseded by the grilling of 2026-11-22:* animals are individuals and herds are derived, and
     there is no herd mind or fixed territory. See `documentation/specs/2026-11-22-animal-life.md`
     and the decision records "Animals are individuals; a herd is derived" and "Animals read the slow
     world true and sense what moves".
   - **Sleep follows the species' activity pattern** (diurnal, nocturnal or crepuscular; see
     "Activity patterns" under "Decided in the 2026-11-21 grilling"), at the den if there is one,
     otherwise where the herd stands.
   - **Thirst is added**, with real water sources.
   - **Grazing stops** where the forage odds are low enough. Today an eaten-out tile still feeds in
     full (`Fauna.cs:291-292`, `323-324`).
   - **Ravens fly**: they leave no prints.
   - **After the shared planner exists, review the NPC mind** so NPCs and animals do not keep two
     copies.

Crippling injuries already has one (above).

### The emergent-world work is on hold

Steps 1-3a of `documentation/emergent-world-design.md` were built before a design session with the
user, who did not know they existed. Everything built from that design goes on the user's review
list as flagged changes, and a design-review session comes before any more of it is built. Until
then no further emergent-world step starts: not step 3b (settling), step 4's remainder, or steps 5-6
(roadmap items 7, 9 and 10 below wait on the session).

The user reviewed the emergent-world overview on 2026-11-22. The design matches what was discussed,
on the condition that everything ends up integrated, not cobbled together. The ravens, renewal and
signs findings feed the Animal life (4) and Predators and items (1) design sessions above. The hold
on further steps stays until those sessions are held.

What was built:

- `2af01d6` Step 1, QA tools: find dump, distribution report and population curves (`Report`), the
  CLI report command.
- `11cedc4` Step 2, substrate: `ThingRef` and `Things.At`, `Cause` on carcasses, stashes, fire pits
  and shelters, the `Signs.At` projection used by the report, derived Tags and gear Capabilities, one
  `Access.Refusal` gate for every thing refusal, exhaustive `ThingKind` switches.
- `9cbe8cc` Step 3a, the kill chain: herd renewal toward carrying capacity (births, pack starvation,
  arrivals); ravens as scavengers with a derived Beacon and stash raids; the carcass and body pipeline
  to bones and gone on one rot timeline; kill attribution by wounder and last striker; causes of
  death; stable stash and shelter ids; canonical trail refs; feed, graze and raid through `Access`;
  a ground cache and `Nearby` index for whole-map passes.

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
15. **Embers last one minute too long.** The ember countdown subtracts 1/60 h a minute, so a pine
    fire's embers last 21 minutes where the formula says 20, and the 0.25 h floor gives 16, not 15.
    Count embers in whole minutes instead; that changes saved state, so it needs a save format bump.
16. **A cold last ember still lights tinder.** In its final minute an ember is near 0 °F, and tinder's
    minimum fire temperature is 0, so `AddFuel` relights it from embers that should be dead.

## Design decisions still open (raise when their step comes up)

- **Food balance:** NPCs survive 60 days but end near starvation; the NPC's
  daily schedule (`rebuild-npc-food-and-forage-review.md`). The forage focus gain is decided: it
  reverts to the old filter (see "Restoring the old game").
- **Hunting thresholds:** bison only when starving; prey worth computed from the animal
  (`rebuild-npc-hunting.md`).
- **Mammoth sinew:** about 300 kg, uncapped.
- **The Bloody effect:** its treatment floor contradicts the "dries on its own" comment. Washing is
  restored and Bloody still dries on its own (see "Restoring the old game"). Check the floor against
  that when washing is built.
- **Combat balance retune:** folds into MVP step 3.
- **World-density checks:** measures and floors (parked).
- **The body review's design questions** (step 6).

## Decided in the 2026-11-21 grilling

**Predators and people (MVP 2)**
- People are prey like any other. A predator herd's hunt choice includes every person in reach,
  player and NPC alike, scored by its ordinary boldness against the risk. There is no separate
  engage-the-player rule.
- The approach is ordinary herd movement toward the target, tile by tile. You see it only if you
  notice it. No stalking events and no warning text.
- Fire lowers boldness through one base fire-fear value for animals; people are not deterred.
  Tune per species later if needed.
- Each other person on the target's tile lowers boldness, the mirror of the pack-size term. NPCs
  help through their ordinary combat-help choice.
- When the predator reaches the tile and still wants you, the fight opens: at a distance and
  ready if you saw it coming, an ambush if you didn't. A predator whose boldness drops on the
  way turns back.
- Open: the rule for long actions stopping (round 2). Combat itself is under review: a headless
  outcome survey is running, and fixing the grid versus an abstract model is to be decided
  (`documentation/combat-survey-2026-11-21.md`).

**Body (B4, B5)**
- Cold, thirst and hunger damage organs at a fixed rate, not divided by toughness or condition.
  Targets, to tune: about 3 days without water at rest; starvation over weeks, after fat then
  muscle; hypothermia harms below about 95 °F core and kills around 82 °F, within hours.
- Effects disable but never kill directly. Only tissue and blood decide life, and death is named
  after the damage that crossed the line.

**Fire (round 2)**
- Each fuel burns per kilogram at its own rate, sped up by fire temperature: tinder flashes,
  oak smoulders, a mix burns between. Constants are calibrated so a typical single-fuel fire keeps
  today's burn time. A waiting log catches from the hottest burning fuel, not the mean.
- UI: the fire's time remaining shows as one bar, shaded by fuel.

**Long actions and being noticed (round 2)**
- Long actions stop when you notice a predator you didn't know about, or when a known one comes
  within 3 tiles. Asleep, a predator within 3 tiles may wake you (noise, hearing, vigilance).
  Nothing refuses to start.
- Guard against random deaths: a predator notices a person through what they give off. That means
  moving and working noise, scent (blood, bleeding, meat carried or stashed, a carcass), and the
  fire's light. A healthy, clean sleeper gives off almost nothing. A death in your sleep should
  need a cause you could have read or avoided.

**Round 2 answers**
- Engagement is a threshold, not a roll: a predator commits when its boldness toward a person beats
  that person's threat as it judges it (the comparison `CombatAI.WantsToAttack` already makes).
- Predators notice people by the signals the person gives off (movement and noise, scent, fire light).
  Fire also lowers boldness. Activity pattern per species: proposed, see the next round.
- The herd starts the fight inside the step, and the step returns why it stopped. No queue of
  pending encounters. Night reads the weather's real sunset and sunrise.
- UI: one frame loop calls `Sim.Step` for paced minutes, with no async `IGameUi` layer. The
  framework is open (ImGui doubted).
- `Verbs.At(world, who)` in Sim is the one source of "what can I do here", in design §3.5's shape.
- Core additions before the UI: an explored-tile record with sight rays behind `Sight.Visible`, a
  breakdown of trip time, and the stat tiers shared between player and NPCs.
- Save compatibility starts once the rebuild is finished (decision 2026-11-24). Until then saves
  break with each format change, but the format is designed now so that isn't a big refactor later.
- Settle: one year, every agent stepped every minute, opt-in through a saved flag that stays off
  until §8 passes. Only hard floors reject a world. Reveal thresholds are computed from the find,
  in the same step.
- Parity means every row of a fresh checklist is Done, Cut, or Replaced by something that exists.
  The rebuild CLI replaces GameCli and the other tools retire; `--shot` gives headless screenshots.
  Retirement tags the last old-game commit, deletes the old code, moves `rebuild/` to the root and
  rewrites the docs.
- Fire's deterrence depends on the predator's distance from the fire: fire-fear × intensity ×
  inverse-square falloff, capped at 1 near the fire. A predator closes in while boldness minus
  deterrence beats the target's threat, and holds where they balance. So it circles at the edge of
  the light and closes in as the fire dies or you step away from it. On the map this shows as a
  predator holding at a neighbouring tile at night. Within a tile it needs the combat model to
  give the fire a position (decided with the combat survey).
- Activity patterns: each species is diurnal, nocturnal or crepuscular, in data. Outside its
  active hours a herd rests unless hungry or threatened, and is less watchful. It is bolder in its
  active hours and warier outside them. People are diurnal. This replaces the flat night bonus.
- Saves: versioned from now, plain data records, and a fixture test loads a fixed sample save so
  any format change is deliberate. Migration code comes once saves must survive between builds.
- UI: leaning web; how to host it is being decided.
- Fire burn: the fire sets the total kg/h as today, and the burning fuels share it out by table
  rate × mass, so tinder goes first and a single-fuel fire is unchanged. Keep it simple.
- UI host: start with in-browser WebAssembly and measure (spike in `rebuild/Web.Spike/`). The UI
  talks to the game only through a narrow host boundary: new game, act, view, save and load, with
  views as plain data. That way it can move behind a server later without changing the UI.
- Fire at the edge is meant at tile level first: a strong fire keeps a predator off your tile, so
  most of the time there is no encounter at all, only eyes on the neighbouring tile. The same
  falloff holds within a fight if one starts.
- Combat: keep the grid and salvage it (survey option a). It's entertaining to watch whole
  simulated battles, and it extends naturally to fights with three or more sides. What needs fixing:
  the clunky movement and the 1v1 balance. Today the outcome is set in advance, and an animal
  that's going to run away takes several turns to do it. Targets: next grilling round.
- Combat design: continuous values (positions, morale, stamina, damage) plus a small explicit
  state per fighter, with hysteresis. Allowed actions are state ∩ the existing distance-zone lists.
  A closed-form catch-up calculation ends a flight that can't be caught at once; chasing one that
  can is the player's choice, shown with odds. One 0–1 damage scale, tuned by sweeps. Rounds of
  30 s. Watching other fights: CLI `--watch` only, for testing.
- Outcome targets to start: careful hunt 50–70% meat; quick ≤20%; armed vs a lone wolf 70–85%
  won, ≤5% dead; unarmed vs a lone wolf about 30% dead; pack of 3 in the open about 50% dead. The
  bear is more dangerous than first proposed. Weapon order: spear > axe > knife > hands.
- The state machine is written up in `documentation/combat-states.md` for the user to review before
  any code.

**Combat grilling, round 1 (2026-11-21)**
- States: Unaware, Wary, Pressing, Standoff, Giving ground, Fleeing, Gone, Down. Clash is
  Pressing with the target in reach. Down means dead or consciousness below 0.1.
- Each fighter stores only its state toward its current target; everything else is derived. The
  player's state follows from the action they choose.
- One margin, boldness − fire deterrence − perceived threat, with hysteresis bands:
  - Pressing: enter at +0.2, leave below 0;
  - Giving ground: below −0.3, recover above −0.1;
  - Fleeing: below −0.8, recover above −0.4;
  - awareness: Wary at 0.3 / back below 0.1, commits at 0.7.

  All are starting values, to be tuned.
- Allowed actions = state ∩ the code's zone lists. Flee and Pick up are allowed at any distance.
  Intimidate is also allowed at Near.
- Skittish animals always flee once engaged. Bison, megaloceros and mammoth use the bands.
- Block is no longer an action: it is an automatic defence roll when attacked.
- Combat frontier (27 questions): `/Users/jackswingle/.claude/jobs/2c768bb5/tmp/combat-frontier.md`.
- Targets: each fighter targets whoever it has the best margin against, weighted toward closer
  ones. A predator goes for the most vulnerable, a defender for the biggest threat. Hostility is
  derived from each fighter's boldness toward each other one, so fights with three or more sides
  and packs ganging up on the weakest come from the same rule. Switching target is an action, open
  in any state. It costs the round (turning to face), which also stops flip-flopping without a
  separate rule. The player already names a target.
- Defence: Block and Dodge are both folded into one automatic defence roll when attacked (a block
  if armed, a dodge otherwise). It is higher in Giving ground and Standoff, lower in Pressing.
- Tuning: a plain optimiser (an evolutionary search over the constants, scored against the
  outcome bands, with a penalty for animals that die stupidly) is the MVP's balance tool.
- Experiment, its own branch, after the combat state rules exist: learned behaviour.
  - The rules stay hand-written; the policy chooses among allowed actions and state switches.
  - Inputs are generic, never species ids. Animals have no input for weapons or thrown things.
  - Fitness is survival plus calories, minus energy spent and injuries.
  - Stage A co-evolves prey and predators and then freezes them. Stage B trains people against
    the frozen animals and against each other.
  - The policy starts as readable evolved weights over named inputs, and becomes a small network
    only if that plateaus.
  - The outcome bands are checks, not the reward. Unrealistic results are fixed by tuning stats
    and rules, not behaviour. Policies are deterministic and ship as data.
  - The report gives per-species strategies in plain words and outcomes against the hand-tuned
    baseline. It is adopted only if it beats the baseline.
- Before any combat tuning (optimiser or learned): realistic body stats per species and a fight
  energy model (stamina, metabolic cost per round, recovery, load and wounds), from sourced data.
  Research: `documentation/species-stats-research.md`.
- Prey earn calories per minute while unaware and grazing, so alertness and flight have an
  opportunity cost (the landscape of fear). A big animal can rationally ignore a small predator
  that's afraid of it. The term is part of the energy model and of the learned-behaviour fitness.
- Build order for now: MVP 5's core additions first, then MVP 4 crafting core, then MVP 2's
  map-level predator engagement (fights open on today's grid until the salvage lands). One at a
  time, each with review, regression tests and verification. The combat grilling and species
  research run alongside, and MVP 3 waits on them.
- NPC kindling at an oak camp: wood counts as fuel the NPC can keep burning only with enough
  kindling to light it, computed from the fire model. Short of kindling is a Need, restocked on
  wood trips. The NPC's lasting-harm line follows the body's cold-harm onset (95 °F): one source of
  truth.
- Endurance hunting runs on three scales:
  - the fight on the combat grid;
  - a flight that leaves the grid resolves by the catch-up calculation and moves both sides
    across tiles;
  - a persistence hunt is repeated flushes on the map, where each drains the animal and its
    recovery slows with heat and exhaustion. Deep crusted snow favours a person, more again
    on snowshoes.

  For the player it is one action, Pursue. You see its odds before committing, and it stops only
  for real decisions (the same rules that interrupt any long action). It comes after the MVP; the
  MVP keeps the catch-up inside fights. (Proposed; awaiting the user's yes.)
- Species round (2026-11-21), all yes:
  - real movement: per-species top speed, endurance speed, sprint time and a sprint reserve;
  - fights cost metabolic calories, with strikes, dodges and sprints drawing on the reserve;
  - a person can't outrun a predator, so survival comes from deterrence;
  - the cave bear becomes a den defender, and the sabre-tooth becomes Homotherium (~190 kg);
  - realistic body make-up, Kleiber scaling for animals, and a calorie store that scales with
    mass;
  - herds get a calorie budget (the landscape of fear).

  Detection ranges and speeds start from the research's values but may be balanced for play.
- Pursuit: yes, after the MVP. It must be shown playing out on the real map (both animals moving
  tile by tile, tracks and blood visible), not as a bare progress bar.

**Combat round 2**
- AI priority lists per state, as proposed, with two changes:
  - a person throws only at a 50%+ hit chance;
  - in Standoff an animal may move sideways around you, or stop; it doesn't only hold.
- Morale cost of wounds is derived from vitality and blood loss, with no hard-coded per-hit
  numbers.
- Opening optimism is deleted (revisit only if the sweeps need it).
- No weapon factor in perceived threat. Animals don't fear weapons; they learn through injury
  (morale events and the herd's learned fear).
- Morale reacts to the target's state as proposed: chase instinct, giving ground, pressing,
  standoff cost, a smaller bonus for landing a hit, and the pack cascade kept.
- Alertness rises when you're noticed and fades when you aren't, scaled by how much you move.
  Freezing helps but is not a sure thing: a wary animal can still notice you or come to look.
  Balance it in the sweeps.

**Combat round 3, all yes**
- Catch-up: runners sprint until the reserve runs out, then hold endurance speed. A pursuer
  catches if it closes the gap at one of the breakpoints. Odds come from about ±8% spread on
  speeds, and the flight is rolled once.
- Starting speeds are the research table, scaled by each body's own speed.
- Fleeing resolves at once below a 5% chance of being caught. An AI chases at 25%+. The player
  is offered the chase with its odds, time and distance. Caught: the fight resumes at contact.
  Not caught: the chasers move 1 tile per 100 m, at most 5. The blood trail gets a heading.
- A fighter standing off or giving ground that is 30 m or more from every enemy has broken off.
  This replaces leaving by the grid edge.
- The tuning harness is being built now, ahead of the combat rules.

**Combat round 4 and weapons, all yes**
- Fire in a fight: deterrence = fire-fear × intensity × min(1, (6 m/d)²), from the predator's
  distance to the fire. Fire-fear is one base value for animals, 2.0. A torch counts 0.25 and
  moves with its holder.
- The hold: in Standoff the step is proportional to the margin, with a deadband. An animal
  never comes inside the larger reach + 0.5 m and can circle sideways. At tile level, a predator
  enters your tile only if its margin at your position, fire included, reaches the Pressing entry.
- Reach per weapon and animal. A spear held in Standoff or while giving ground is braced
  automatically and strikes first.
- Damage in units of tissue condition (decisions/2026-11-21-damage-is-measured-in-tissue-condition.md): what is left after the layers reaches an organ; an unaware target takes ×1.5.

| Weapon | Hand damage | Reach | Thrown: range / accuracy / damage |
|---|---|---|---|
| Hands | 0.05 Blunt | 0.8 m | — |
| Staff (one stick, new) | 0.41 Blunt | 1.5 m | — |
| Stone (new, also thrown) | 0.65 Blunt | 0.8 m | 15 m / 0.50 / 0.55 |
| Crude spear (sharpened stick) | 0.91 Pierce | 2.5 m | 15 m / 0.55 / 1.40 |
| Knife | 3.38 Sharp | 0.8 m | 10 m / 0.40 / 1.20 |
| Wooden spear (fire-hardened) | 4.84 Pierce | 2.5 m | 20 m / 0.70 / 2.00 |
| Heavy spear | 5.56 Pierce | 2.5 m | 15 m / 0.60 / 2.30 |
| Axe | 6.00 Sharp | 1.2 m | 10 m / 0.45 / 1.50 |
| Stone-tipped spear (crafting) | 6.29 Pierce | 2.5 m | 20 m / 0.70 / 2.60 |
| Ivory-tipped spear (crafting, not built) | 0.45 Pierce on the old scale | 2.5 m | 20 m / 0.70 / 0.45 |

  Melee damage is the tuner's (2026-11-22); the outcome order spear > axe > knife > stone > staff > hands holds.
- Glance moves onto the sight rays: one sight model for noticing and the explored map.
- Progress (2026-11-21):
  - MVP 1 done.
  - MVP 5 core done (Verbs, sight and explored tiles, trip terms, versioned saves, shared tiers).
  - MVP 4 crafting core done (24 recipes).
  - Tuning tool built.
  - Combat salvage is in stages, stage 1 running.
  - The web UI slice has started.
  - MVP 2 (predators engage) goes to the combat agent after its stage 3, because it works in the
    same files.
- People start well fed for autumn: body fat drawn per person at 15–18%, deterministic from the
  world seed. Fat lost over the winter is part of the run. NPC foraging is retuned later, with the
  food-balance item.
- Running numbers (speeds, sprint, running cost, grazing intake) live in `species.json`, where the
  tuner can reach them.

**Combat round 6 and body B12/B14 (2026-11-21)**
- Several enemies: perceived pressure = target's threat + 0.5 × the summed threat of other enemies
  pressing within 5 m. The automatic defence roll guards only against your current target, so
  flanking blows land unguarded.
- Bear bands for a healthy adult with a spear, surprised bear at 8 m, no cubs, no kill:

  | Your choice | Bear leaves | Bear killed | You die |
  |---|---|---|---|
  | Stand and intimidate | 45–55% | ≤5% | 10–20% |
  | Attack | ≤10% | 10–20% | 35–50% |
  | Run | ≤10% | 0 | 50–65% |

  - Unarmed and standing: 25–35% die.
  - A bear defending a kill or den: +15 points of deaths in every row.
- B12, hunger follows reserves:
  - an empty stomach is a mild penalty, about 5%;
  - the strong penalty comes only as fat and muscle near their floors;
  - Well fed is a small matching bonus (about +5%) once 24 hours have passed without the Hungry effect (empty stomach); it ends the moment you are Hungry again. (Revised 2026-11-21: the old rule, a positive 24-hour calorie balance,
    flickered when eating at about maintenance.)
- B14, exhaustion:
  - at zero energy you don't fall asleep at once. Each minute there's about a 1% chance of falling
    asleep where you are for 15–90 minutes;
  - effects stack with diminishing returns, so hunger, thirst and tiredness together never take
    Moving below about 0.2 while there's fat in reserve.
- Giving ground: the AI may strike when the enemy is in reach (one action per round). When it
  should is a tunable threshold in the priority list, learned by the tuner rather than fixed by
  hand. A "fighting retreat" combined action can come later, only if play needs it.
- Well fed: +5% to the body's own metabolic heat and to healing. Capacities stay capped at 1.
- The stacked-discomfort floor (Moving about 0.2) holds even after fat is gone; heart damage from
  starvation can still take the body to zero.

## NPC redesign decisions (2026-11-21, after the architecture audit)

Audit: `dev/active/rebuild-architecture-audit-2026-11-21.md`. Npc.cs is a priority ladder with 86 one-failure rules, and it doesn't use the player's verbs.

- NPCs exist for a living, harsh world. Most lone NPCs die, often; half dying is fine. Groups, or a player helping, let them last longer. Decisions must be reasonable and need no babysitting, and surviving must be possible.
- NPCs act only through the player's actions (`Verbs.At` / `Sim.Refusal`). Nothing is NPC-only.
- NPCs know what the player would know: the same sight (`Sight.Visible`, not only visited tiles) and the same memory (`Beliefs`).
- Groups share things, not rules: one fire, a shared store, seeing others' current plans. There is no group planner.
- No automated decision-pathology tests. We observe NPCs instead.
- The bench reports and never gates. Use fixed seeds plus fresh random seeds each run, so no code is fitted to particular seeds.
- No hard size cap. The code must be thought out and organized.
- Comments follow principles-code.md ("Self-documenting"): only the why, never history.
- Plans may queue 1-3 steps deep, built from missing prerequisites (to craft X, first get Y), as in the old game's crafting recursion and RimWorld's job queues. A queue needs clear interrupts so it can't run on too long.
- Decision model: a priority ladder, not utility scores. The NPC works on the need in the worst priority (ties: warmth > water > rest > food > work) and commits to a goal. Its next step is re-derived from what the refusal says is missing, up to 3 links. It picks the quickest way it knows that the forecast says it will survive. It drops the goal when another need reaches a worse tier, the time budget runs out, the way home turns dangerous, or the step becomes impossible.
- The NPC AI uses only the Sim's public interface (actions, structured refusals, forecasts, beliefs), never its internals (DR 2026-11-21-npc-ai-uses-the-sims-public-interface).
- Order: (1) refusals that name what's missing (light, stoke, drink, eat, butcher, sleep warm); (2) forecasts from Body, Fire and Trip; (3) fire deterrence in predator boldness; (4) the NPC rewrite on the ladder; (5) conditions replacing Tags and Access; (6) split Fauna and move species numbers to data; (7) the rest: the effects kernel, Carcass derived from a clock, unified catch-up, trimming old-game notes from comments.
- Forecasts may be fuzzy: probable best steps, not perfect foresight. The bar is "the AI makes no obviously dumb choices". Improving the AI means improving the forecasts.
- Requirements as a tree ("all of" / "one of", built fresh each step, at most 3 deep), proven by a throwaway prototype (`dev/prototypes/npc-requirements/`) before the rewrite.
- All ice (lakes, rivers, ponds) uses the user's tuned world-space ice painter (art branch commit be75877, world.py `ice()`), ported faithfully; the per-tile ice PNGs were pre-tuning and are dropped. Standing water is reported separately from mud, so a frozen film on mud isn't drawn as a sheet.
- NPC behaviour is hand-built (the ladder), not learned. After the rewrite, the existing Tune tool tunes its few numbers (priority lines, forecast fudge factors, the abort budget) against the bench, with fresh random seeds each generation. A learned NPC policy is an experiment for after the combat learning experiment.
- After the MVP: lakes and rivers freeze and thaw with the weather. Ice thickness grows and melts per water tile in the Sim, which sets whether a crossing is safe and what the map draws: thin ice on the creek with the lake still open at first snow, ice going out in the thaw. Until then, the date decides (`Map.IsFrozen`, `terrain.json` `waterIce`): lakes and rivers are thick ice from mid-October to mid-May and open water the rest of the year, when an unnamed lake tile is waded (a foot of water: slow and soaking).
- Combat balance (from the fire-deterrence survey): standing your ground with a spear against a bear (98% death) does worse than bare-handed (40%). That's a bug, being diagnosed. The high death rates themselves (bear 98%, pack of three 92% against a person standing their ground, no fire) are for combat stage 4 (reach, damage scale, weapon table) and the tuner. Hyenas never enter a lone person's tile; check when tuning.
- Comment cleanup to 10% per file runs now, a batch a day, through the daily ratchet run (`dev/ratchets.md`, decided 2026-11-23). The ratchet test keeps enforcing ceilings, so nothing gets worse.

## MVP order (2026-11-21, re-ordered; nothing dropped)
1. Bugs: 75% injury on quick snow crossings; spear worse than bare hands.
2. Body B12/B14 (hunger pang, Well fed, exhaustion sleep). The NPC seed-survival check that held it back is removed, since the bench reports and never gates.
3. Combat stage 4 core (reach, damage scale, weapon table) plus a tuner run, so predator fights are playable.
4. MVP 2: predators engage people on the map (fire deterrence done).
5. MVP 3: hunting retune (stalking and throwing viable now that prey outrun people).
6. Blind browser playtest end to end, then fix blockers.

After the MVP, in this order: NPC step 1 (Sim forecasts and structured refusals from the prototype), NPC step 2 (rewrite on the tier ladder, then tune its numbers), the audit refactors (conditions replacing Tags and Access, split Fauna and move species data, effects kernel, Carcass from a clock, unified catch-up), water bodies freezing, then roadmap items 6-14.
- Crossing injury odds: (hazard^e)/2 for a quick crossing, rolled once per tile (hazard 1.0 → 50%). Careful carries a quarter of that, (hazard^e)/8. No cliff at 0.3. The pond-ice term scales with ice thickness. Check injury severity in the browser playtest before tuning further.
- Lakes and rivers: open water and impassable from about May to October, ice and crossable otherwise, decided by the date through one Sim function, until real freezing lands after the MVP.
- Combat moves are verbs: during a fight, the fight system offers its verbs (refusal, odds, cost in seconds) through the same interface as every other verb. The code merge of `CombatAction` into `Verb` comes after the MVP, with the audit refactors.
- Wiring between systems: the system that makes the decision owns the formula that combines inputs; the system that provides an input publishes one plain read or forecast, never its internals (example: fire deterrence lives in Combat and reads `Fire.IntensityPct`). A combination two systems both need is its own rule: a system or a forecast. This goes in the "where things go" guide.
- Summer ponds (shallow Water terrain) are open from 15 May to 15 October and can be waded: slow, soaking and cold, but passable. Deep water stays a wall all year. Ice from 15 October to 15 May.
- The pace choice is offered only where quick injury risk on the tile walked onto is above 1.5% (hills stay quick-only).
- A new run does not start Well fed: it is earned after the first 24 hours without hunger.
