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
- Save compatibility starts with the first build someone else plays. Until then saves may break,
  but the format is designed now so that isn't a big refactor later.
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
- One 0–1 damage scale. A hit of 0.5 or more reaches an organ; an unaware target takes ×1.5.

| Weapon | Hand damage | Reach | Thrown: range / accuracy / damage |
|---|---|---|---|
| Hands | 0.10 Blunt | 0.8 m | — |
| Staff (one stick, new) | 0.12 Blunt | 1.5 m | — |
| Stone (new, also thrown) | 0.15 Blunt | 0.8 m | 15 m / 0.50 / 0.15 |
| Crude spear (sharpened stick) | 0.22 Pierce | 2.5 m | 15 m / 0.55 / 0.22 |
| Knife | 0.25–0.30 Sharp | 0.8 m | 10 m / 0.40 / 0.25 |
| Wooden spear (fire-hardened) | 0.30 Pierce | 2.5 m | 20 m / 0.70 / 0.30 |
| Heavy spear | 0.35 Pierce | 2.5 m | 15 m / 0.60 / 0.35 |
| Axe | 0.40–0.45 Sharp | 1.2 m | 10 m / 0.45 / 0.35 |
| Stone-tipped spear (crafting) | 0.40 Pierce | 2.5 m | 20 m / 0.70 / 0.40 |
| Ivory-tipped spear (crafting) | 0.45 Pierce | 2.5 m | 20 m / 0.70 / 0.45 |

  These are starting values for the tuning tool, keeping spear > axe > knife > stone > staff > hands.
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
