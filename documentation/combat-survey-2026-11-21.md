# Combat survey, 2026-11-21

A measurement of the rebuild's combat grid (`rebuild/Sim/Combat.cs`, `CombatAI.cs`,
`CombatAftermath.cs`). It asks why most fights end as "can't catch it" or "brawl to the death", and
what fixing the grid would cost compared with replacing it with an abstract model. It measures; it
does not decide.

**Short answer.** Both bad patterns come from a few all-or-nothing switches, not from fine-grained
emergence:

- Which side has the bigger per-round movement allowance wins every chase.
- One wound flips any fighter's morale from "attack" to "leave".
- Any weapon one-shots on the body's 0-1 scale.

When I changed two of those numbers, outcomes swung from one bad pattern to the other with nothing
in between (§5). Most of what a player would expect to matter barely moves the result: how
carefully you approach, which weapon you carry, cold, a hurt arm, a 30 kg load, and fire.

---

## 1. Method

**Harness.** A probe project, not tracked:
`~/.claude/jobs/2c768bb5/tmp/combat-survey/` (`probe/Program.cs`, `agg.py`, `run.sh`).

- It builds against a `git archive` snapshot of `rebuild/Sim` at HEAD. Combat, Fauna and Body are
  unchanged from the working tree.
- The snapshot adds four behaviour-neutral hooks (`Probe.OnAction/OnHit/OnDodge/OnRound`) to record
  actions, hits and distances.
- Runs use Release builds, with seeds split across 8 processes.
- Each world is `World.New`, with every populated herd and NPC removed. Fights take place on a quiet
  Plain tile.

**Two drivers.**

- **AI**: a headless person, an `Agent` with a `PlayerMind`, which is not the player. The headless
  path supports a person, so both sides run `CombatAI`. This is also how NPCs fight.
  `Combat.ResolveHeadless` caps a fight at 20 rounds (`Combat.cs:102`).
- **Scripted player**: the real player path, `Sim.Step(new Fight(...), 1)`. Each round is a lived
  minute: survival ticks, the wound ticks and the AI turns. Policies:
  - `aggressive`: Attack if allowed, else Advance.
  - `cautious`: as aggressive, but once wound > 0.3, blood < 85% or Bleeding > 0.4, it moves a full
    allowance toward the nearest edge and Flees when it can.
  - Hunts, which all start at `Fauna.BeginHunt` (34 m, prey Unaware):
    - `careful_*` steps 1 m a round and freezes up to 3 rounds while any animal is Alert.
    - `quick_*` uses Advance, a full allowance.
    - `*_throw` throws the weapon at 8 m (careful) or 10 m (quick), then closes unarmed.
    - `*_melee` never throws.

**Scenarios** (600 fights per config; about 210,000 fights in all):

| Scenario | Setup |
|---|---|
| hunt caribou / bison | Herd of 4, Resting (and Grazing for caribou), `Fauna.BeginHunt` |
| lone wolf ambush | `Opening.Ambush`, 3 m, person Unaware |
| lone wolf aware | `Opening.Approach` (wolf placed near the grid edge), both Engaged |
| pack of 3 or 5, aware or ambush | Approach, both Engaged / Ambush at 4 m, person Unaware |
| bear | `Opening.CloseEncounter`, 8 m, both Engaged (as `Npc.RespondToThreat`'s 8 m) |
| 4 wolves vs caribou or bison | As `Fauna.PackHunt` (`Fauna.cs:332`): 15 m Alert ("noticed") or 5 m Unaware; 1 or 3 targets |
| 2 people (AI) vs 4 wolves | Approach, both Engaged |

**Gear** (all of `items.json`'s weapons):

| Weapon | Damage | Class | Source |
|---|---|---|---|
| bare hands | Human 0.15 | Blunt | `species.json:21` |
| `flint_knife` | 6 | Blade | `items.json:68`; bone and worn knives are identical |
| `wooden_spear` | 8 | Pierce | `items.json:71` |
| `stone_axe` | 12 | Blade | `items.json:67` |

There is no stone-tipped spear, club or sharp rock in the rebuild. Any held weapon can be thrown.

**Conditions** (applied to `BodyState.BaselineHuman()`, starting clothes worn):

| Condition | Change |
|---|---|
| healthy | none |
| tired | energy 48 min (5%), so Tired 0.83 |
| hungry | 0 kcal, fat 6% of weight, Hungry 1.0 |
| cold95 | core 95.0 °F, Shivering 0.4 |
| cold92 | core 92 °F, Shivering 1.0, Hypothermia 0.3 |
| leg | both legs: muscle 0.25, bone 0.5. Moving about 0.55, allowance 3 m to 2 m |
| arm | right arm: muscle 0.2, bone 0.5. Manipulation about 0.8 |
| bleeding | Bleeding 0.3 (15 ml/min) |
| meat | 30 kg RawMeat in the pack |

**Patterns, defined:**

- **Got away ("can't catch it")**: no kill, the fight ended AnimalFled or Disengaged, and the person
  is alive. The bracket gives the share that ended with the person never landing a blow.
- **Brawl to the death**: the fight ends with one side dead (Victory or Defeat), each side landed
  2 or more blows, and nobody left the field.

**Caveats:**

- A round is one minute (`Combat.cs:94`), so "minutes" equals rounds. Hunt rounds include the 34 m
  stalk.
- A kill is a `Killed` event. About 0.7% of hunts leave a carcass without one (the animal bled out
  inside the minute), so kills are slightly undercounted.
- An animal that escapes wounded and dies later off the field is not counted.
- No production code sets a predator's `Fighter.BoldnessDelta`, because herds never engage people
  yet (`Fauna.cs:52`). I left it at 0, which matches the golden tests.
- `cautious` treats a pre-existing leg or arm wound as "hurt", so in those conditions it runs from
  round one.
- "Winner wound" is the worst `WoundLevel` at the end and includes pre-existing wounds.

## 2. Outcomes: healthy adult, wooden spear

All values are percentages except n, rounds, minutes and winner wound. In the wolves-vs-prey rows,
"person dies" means a wolf died.

| Scenario / policy | n | Victory | any kill | person dies | person fled | animal fled | disengage | median rounds | mean min | winner wound | got away (untouched) | brawl | zone C/N/M/F |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| hunt caribou resting / careful_throw | 600 | 0 | 4 | 1 | 0 | 99 | 0 | 74 | 78 | 0.00 | 96 (25) | 0 | 0/1/43/56 |
| hunt caribou resting / careful_melee | 600 | 8 | **47** | 0 | 0 | 92 | 0 | 79 | 81 | 0.00 | 53 (29) | 0 | 0/4/46/50 |
| hunt caribou resting / quick_throw | 600 | 0 | **2** | 0 | 0 | 100 | 0 | 15 | 16 | 0.00 | 98 (28) | 0 | 1/3/55/40 |
| hunt caribou resting / quick_melee | 600 | 7 | **46** | 0 | 0 | 93 | 0 | 16 | 17 | 0.00 | 54 (32) | 0 | 2/16/43/39 |
| hunt caribou grazing / careful_melee | 600 | 12 | 57 | 1 | 0 | 87 | 0 | 85 | 87 | 0.00 | 43 (18) | 0 | 0/5/48/47 |
| hunt caribou grazing / quick_melee | 600 | 11 | 53 | 0 | 0 | 89 | 0 | 18 | 19 | 0.00 | 47 (18) | 0 | 2/20/43/34 |
| hunt bison resting / careful_throw | 600 | 1 | 6 | 44 | 0 | 55 | 0 | 50 | 56 | 0.00 | 50 (16) | 42 | 2/6/35/57 |
| hunt bison resting / careful_melee | 600 | 14 | 59 | 9 | 0 | 77 | 0 | 62 | 67 | 0.10 | 34 (22) | 10 | 2/7/41/50 |
| hunt bison resting / quick_throw | 600 | 1 | 4 | **66** | 0 | 33 | 0 | 15 | 16 | 0.00 | 30 (3) | 62 | 9/20/37/34 |
| hunt bison resting / quick_melee | 600 | 20 | **66** | 20 | 2 | 58 | 0 | 20 | 21 | 0.23 | 17 (7) | 22 | 8/25/42/26 |
| lone wolf ambush / ai | 600 | 0 | 0 | **75** | 0 | 2 | 23 | 15 | 14 | 0.17 | 25 (23) | 0 | 70/27/2/0 |
| lone wolf ambush / aggressive | 600 | 35 | 35 | **0** | 0 | 65 | 0 | 8 | 8 | 0.33 | **65** (0) | 3 | 11/22/60/7 |
| lone wolf ambush / cautious | 600 | 12 | 12 | 19 | 27 | 42 | 0 | 8 | 8 | 0.06 | 42 (0) | 0 | 32/29/35/4 |
| lone wolf aware / ai | 600 | 10 | 10 | 38 | 0 | 30 | 23 | 13 | 13 | 0.06 | 52 (22) | 0 | 55/20/22/4 |
| lone wolf aware / aggressive | 600 | 38 | 38 | 0 | 0 | 62 | 0 | 8 | 9 | 0.21 | 62 (0) | 4 | 17/25/52/6 |
| lone wolf aware / cautious | 600 | 32 | 32 | 3 | 20 | 46 | 0 | 9 | 9 | 0.15 | 46 (0) | 1 | 25/24/45/7 |
| pack 3 aware / ai | 600 | 0 | 5 | 96 | 0 | 4 | 0 | 9 | 9 | 0.64 | 0 | 7 | 63/12/23/2 |
| pack 3 aware / aggressive | 600 | 5 | 62 | 55 | 0 | 40 | 0 | 9 | 11 | 0.96 | 0 | **57** | 39/21/35/5 |
| pack 3 aware / cautious | 600 | 0 | 10 | 70 | 20 | 10 | 0 | 8 | 8 | - | 0 | 16 | 51/21/24/4 |
| pack 3 ambush / ai | 600 | 0 | 0 | 100 | 0 | 0 | 0 | 5 | 5 | - | 0 | 0 | 85/15/0/0 |
| pack 3 ambush / aggressive | 600 | 2 | 53 | 65 | 0 | 34 | 0 | 6 | 7 | 1.00 | 0 | 49 | 49/19/25/7 |
| pack 3 ambush / cautious | 600 | 0 | 0 | 100 | 0 | 0 | 0 | 4 | 4 | - | 0 | 0 | 82/18/0/0 |
| pack 5 aware / ai | 600 | 0 | 2 | 98 | 0 | 2 | 0 | 6 | 7 | - | 0 | 2 | 59/8/31/2 |
| pack 5 aware / aggressive | 600 | 0 | 40 | 84 | 0 | 16 | 0 | 6 | 7 | - | 0 | **70** | 47/14/35/3 |
| pack 5 aware / cautious | 600 | 0 | 5 | 92 | 2 | 5 | 0 | 6 | 6 | - | 0 | 14 | 53/14/31/2 |
| pack 5 ambush / ai | 600 | 0 | 0 | 100 | 0 | 0 | 0 | 4 | 4 | - | 0 | 0 | 98/2/0/0 |
| pack 5 ambush / aggressive | 600 | 0 | 29 | 88 | 0 | 12 | 0 | 3 | 4 | - | 0 | 52 | 70/11/13/6 |
| bear close / ai | 600 | 4 | 4 | 82 | 0 | 4 | 10 | 12 | 12 | 0.00 | 14 (10) | 0 | 74/22/4/0 |
| bear close / aggressive | 600 | 52 | 52 | **0** | 0 | 48 | 0 | 9 | 9 | 0.32 | 48 (0) | 4 | 26/21/53/0 |
| bear close / cautious | 600 | 24 | 24 | 31 | 27 | 18 | 0 | 9 | 8 | 0.18 | 18 (0) | 1 | 47/31/22/0 |
| 4 wolves vs 1 caribou, noticed | 600 | 100 | **100** | 0 (wolf) | 0 | 0 | 0 | 5 | 6 | 0.00 | 0 | 0 | 0/12/88/0 |
| 4 wolves vs 1 caribou, unnoticed | 600 | 100 | 100 | 0 (wolf) | 0 | 0 | 0 | 2 | 2 | 0.00 | 0 | 0 | 0/59/41/0 |
| 4 wolves vs 3 caribou, noticed | 600 | 42 | 100 | 0 (wolf) | 0 | 58 | 0 | 11 | 11 | 0.00 | 0 | 0 | 0/12/77/10 |
| 4 wolves vs 1 bison, noticed | 600 | 86 | 86 | 0 (wolf) | 2 | 8 | 4 | 5 | 7 | 0.05 | 12 | 0 | 4/22/69/5 |
| 2 people (AI) vs 4 wolves | 600 | 13 | 66 | 68 | 0 | 23 | 6 | 13 | 13 | 0.93 | 0 | 54 | 38/28/31/3 |
| 1 person (AI) vs 4 wolves | 600 | 0 | 2 | 98 | 0 | 2 | 0 | 7 | 8 | - | 0 | 3 | 61/8/29/3 |

**Person with a torch or fire: nothing in combat reads fire.** `Combat.cs`, `CombatAI.cs` and
`CombatAftermath.cs` never mention fire, light or torches. `Combat.Boldness` (`Combat.cs:215`) reads
temperament, pack size, hunger, defending, the target's wounds, meat and night. Terrain enters only
through tile visibility in detection (`Combat.cs:819`). A fire scenario on today's code would equal
the baseline, so I prototyped the proposed deterrence instead (§2.1).

### 2.1 Prototype: fire deterrence by distance, on the grid

The design input: deterrence = fire-fear × intensity × f(predator's distance from the fire), where
f = min(1, (3 m / d)²), an inverse square capped at 1 within 3 m. A predator presses while boldness
minus deterrence beats the target's threat.

**Prototype** (`varC/`, 12 lines):

- `UnitBoldness` subtracts the deterrence for animals (fire-fear 8).
- The fire sits on the person's starting cell. A torch would be the same term with the fire cell
  following its holder.
- Nothing else changed. In particular, no "hold" rule was added.
- The person stands at the fire and strikes only when the wolf is in reach (policy `hold`).
- 600 fights per row, a 60-round cap. "Held off" means the fight was still going at round 60.

| Scenario | Weapon | Wolf killed | Person dies | Bitten at all | Held off at 60 | Median wolf-fire gap, rounds 5-30 | Rounds within 4 m |
|---|---|---|---|---|---|---|---|
| lone wolf, no fire (I 0) | none | 4 | **96** | 100 | 0 | 1 m | 100% |
| lone wolf, I 0.25 | none | 0 | 7 | 74 | 92 | 4 m | 54% |
| lone wolf, I 0.5 | none | 0 | 1 | 40 | 99 | 6 m | 41% |
| lone wolf, I 1.0 | none | 0 | **0** | 8 | 100 | 9 m | 17% |
| lone wolf, I 1.0 → 0 over 30 rounds | none | 6 | 62 | 72 | 30 | 5 m | 48% |
| lone wolf, I 1.0, person walks 12 m from the fire at round 10 | none | 0 | 10 | 20 | 90 | 7 m | 21% |
| pack of 3, I 1.0 | none | 0 | 80 | 98 | 20 | 2 m | 89% |
| pack of 3, I 1.0 → 0 | none | 0 | 100 | 100 | 0 | 1 m | 96% |
| lone wolf, I 1.0 | spear | 4 | 0 | 8 | 55 | 9 m | 22% |
| pack of 3, I 1.0 | spear | 54 | 0 | 87 | 45 | 6 m | 42% |
| pack of 3, I 1.0 → 0 | spear | 89 | 14 | 92 | 1 | 4 m | 55% |

**What emerged:**

- **The hold at the edge emerges** from today's advance/retreat steering with no new rule. The
  equilibrium distance grows with intensity (4, 6, 9 m). A dying fire lets the wolf in (first bite
  at a median of round 11). Walking away from the fire draws it in.
- **Packs press harder**, as they should. Ally boldness outweighs the deterrence, and a pack of 3
  reaches an unarmed person at full fire 80% of the time.

**But the hold is a bang-bang pacing, not a circle.** A typical wolf-fire trace at I 1.0:

> 16, 11, 5, 11, 5, 11, 5, 11, 5, 12, 6, 4, 10, 4, 10 …

- The wolf steps a full 6 m allowance in, finds itself inside the ring, and steps 6 m out, around
  the equilibrium (about 8 m).
- Each inward step can overshoot into strike range, which is where the bites come from.
- Motion is purely radial: nothing makes it go round.
- A smooth "pace at the edge" needs either a step scaled to the size of the imbalance, or a
  tangential (circling) term. Both are small rules, not emergence.

**Actions chosen** (healthy, spear; shares of all actions):

| Scenario | Person / team A | Animal / team B |
|---|---|---|
| lone wolf ambush, AI person | Block 38%, Move 34%, Shove 16%, Dodge 6%, Wait 5%, **Attack 0%** | Attack 70%, Move 30% |
| lone wolf aware, AI person | Move 50%, Block 27%, Shove 12%, Attack 8% | Move 50%, Attack 45% |
| lone wolf, aggressive player | Advance 66%, Attack 34% | **Move 72%**, Dodge 17%, Attack 9% |
| pack 3 aware, AI person | Move 50%, Block 28%, Shove 13%, Attack 7% | Move 52%, Attack 47% |
| bear, AI person | Block 38%, Move 38%, Shove 17%, Attack 3% | Attack 70%, Move 27% |
| bear, aggressive player | Advance 58%, Attack 42% | Move 65%, Dodge 27%, Attack 2% |
| caribou hunt, careful | Wait 56%, Move 35%, Advance 6%, Attack 4% | Move 72%, Wait 28% |
| 4 wolves vs caribou | Move 92%, Attack 8% | Move 100% |
| 4 wolves vs bison | Move 80%, Attack 15% | Move 62%, Dodge 27%, Shove 9%, Attack 1% |

**Distance over time** (median gap to the nearest enemy at round r):

| Scenario | Gap by round |
|---|---|
| caribou, careful stalk | r0 33 m, r10 24, r20 20, r40 14, r60 10. Unaware prey wanders 1-3 m most rounds, so 1 m steps barely gain. |
| caribou, quick | r0 31, r5 15, r10 3, r20 4. The tail is a 3-4-3-4 m oscillation: the bolting caribou and the hunter both move 3 m. |
| lone wolf ambush, AI person | 1 m from r0 to the end. The person never attacks and is bitten to death. |
| lone wolf ambush, aggressive | r0 1 m, r2 8 m, r5 8 m. One spear hit and the wolf is gone. |
| pack, any | 1 m by round 2 and stays there. |

## 3. Weapon × condition

### 3.1 Caribou hunt (resting herd), kill %

The grazing herd and the bison are in §3.3.

| careful_melee | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat |
|---|---|---|---|---|---|---|---|---|---|
| none | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| flint_knife | 50 | 22 | 15 | 46 | 48 | 16 | 48 | 23 | 53 |
| wooden_spear | 47 | 16 | 12 | 47 | 45 | 12 | 46 | 19 | 49 |
| stone_axe | 46 | 11 | 10 | 51 | 48 | 9 | 48 | 14 | 50 |

| quick_melee | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat |
|---|---|---|---|---|---|---|---|---|---|
| none | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| flint_knife | 48 | 26 | 22 | 45 | 49 | 21 | 51 | 45 | 45 |
| wooden_spear | 46 | 19 | 16 | 42 | 44 | 16 | 42 | 46 | 44 |
| stone_axe | 44 | 16 | 16 | 42 | 42 | 14 | 40 | 40 | 44 |

| careful_throw | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat |
|---|---|---|---|---|---|---|---|---|---|
| flint_knife | 5 | 8 | 6 | 5 | 6 | 4 | 5 | 6 | 5 |
| wooden_spear | 4 | 6 | 3 | 4 | 4 | 2 | 4 | 4 | 5 |
| stone_axe | 4 | 3 | 3 | 7 | 5 | 2 | 6 | 2 | 5 |

| quick_throw | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat |
|---|---|---|---|---|---|---|---|---|---|
| flint_knife | 3 | 5 | 6 | 3 | 4 | 4 | 4 | 2 | 3 |
| wooden_spear | 2 | 3 | 3 | 1 | 3 | 2 | 1 | 3 | 2 |
| stone_axe | 2 | 2 | 2 | 3 | 2 | 2 | 3 | 2 | 3 |

Nobody dies hunting caribou (person-dies ≤ 1% everywhere).

### 3.2 Lone wolf ambush: person dies % / Victory %

| dies: ai | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat |
|---|---|---|---|---|---|---|---|---|---|
| none | 90 | 92 | 90 | 88 | 88 | 98 | 94 | 89 | 88 |
| flint_knife | 72 | 73 | 74 | 74 | 76 | 88 | 80 | 73 | 77 |
| wooden_spear | 75 | 73 | 73 | 73 | 73 | 85 | 80 | 74 | 71 |
| stone_axe | 75 | 76 | 75 | 70 | 73 | 88 | 76 | 71 | 75 |

| dies: aggressive | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat |
|---|---|---|---|---|---|---|---|---|---|
| none | 98 | 100 | 100 | 99 | 99 | 100 | 98 | 97 | 97 |
| flint_knife | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| wooden_spear | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| stone_axe | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

| dies: cautious | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat |
|---|---|---|---|---|---|---|---|---|---|
| none | 42 | 90 | 88 | 42 | 48 | 95 | 35 | 45 | 40 |
| flint_knife | 17 | 41 | 41 | 18 | 24 | 96 | 32 | 17 | 17 |
| wooden_spear | 19 | 39 | 40 | 18 | 26 | 95 | 33 | 20 | 20 |
| stone_axe | 20 | 38 | 43 | 19 | 23 | 97 | 36 | 22 | 20 |

| Victory: aggressive | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat |
|---|---|---|---|---|---|---|---|---|---|
| none | 2 | 0 | 0 | 1 | 1 | 0 | 2 | 3 | 3 |
| flint_knife | 41 | 44 | 41 | 39 | 43 | 36 | 40 | 39 | 42 |
| wooden_spear | 35 | 37 | 34 | 32 | 37 | 27 | 34 | 33 | 36 |
| stone_axe | 32 | 26 | 26 | 28 | 28 | 24 | 31 | 28 | 35 |

The AI person wins 0-2% in every cell. The cautious policy never wins with a leg or arm wound,
because it runs from round one.

### 3.3 One factor at a time (kill % / person-dies %)

Condition columns use the wooden spear; weapon columns use a healthy body. The healthy spear column
is the baseline.

| Scenario | Policy | healthy | tired | hungry | cold95 | cold92 | leg | arm | bleeding | meat | none | knife | axe |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| bison hunt | careful_throw | 6/44 | 9/50 | 5/42 | 5/44 | 8/46 | 2/39 | 6/46 | 7/47 | 6/40 | 0/49 | 8/41 | 6/44 |
| bison hunt | careful_melee | 59/9 | 62/9 | 54/9 | 58/11 | 62/10 | 44/13 | 58/12 | 58/11 | 59/11 | 0/53 | 60/7 | 56/14 |
| bison hunt | quick_throw | 4/66 | 6/68 | 3/62 | 4/63 | 5/64 | 3/62 | 3/68 | 4/66 | 3/66 | 0/73 | 6/63 | 6/62 |
| bison hunt | quick_melee | 66/20 | 70/12 | 61/12 | 64/19 | 72/17 | 57/12 | 64/20 | 69/17 | 65/19 | 0/72 | 74/13 | 59/27 |
| caribou grazing | careful_melee | 57/1 | 21/1 | 20/4 | 60/0 | 60/1 | 16/2 | 60/0 | 28/1 | 61/0 | 0/1 | 62/1 | 56/2 |
| caribou grazing | quick_melee | 53/0 | 27/0 | 22/0 | 55/0 | 58/0 | 23/0 | 58/0 | 56/0 | 59/0 | 0/0 | 62/0 | 58/0 |
| lone wolf aware | ai | 10/38 | 11/40 | 10/38 | 11/37 | 11/34 | 4/58 | 8/47 | 13/33 | 10/35 | 1/76 | 14/38 | 10/37 |
| lone wolf aware | aggressive | 38/0 | 40/0 | 34/0 | 42/0 | 40/0 | 32/0 | 42/0 | 40/0 | 40/0 | 6/93 | 48/0 | 39/0 |
| lone wolf aware | cautious | 32/3 | 31/14 | 22/14 | 32/3 | 33/2 | 0/40 | 0/3 | 8/8 | 30/1 | 0/20 | 33/3 | 30/4 |
| pack 3 aware | ai | 5/96 | 4/97 | 6/95 | 6/95 | 7/93 | 1/99 | 2/98 | 6/95 | 6/95 | 0/100 | 6/94 | 4/96 |
| pack 3 aware | aggressive | 62/55 | 71/44 | 70/46 | 62/53 | 71/52 | 62/54 | 64/56 | 66/54 | 60/58 | 0/100 | 72/44 | 55/64 |
| pack 3 aware | cautious | 10/70 | 16/84 | 8/92 | 8/71 | 10/78 | 0/98 | 0/32 | 2/70 | 8/72 | 0/89 | 16/67 | 6/73 |
| pack 3 ambush | aggressive | 53/65 | 64/52 | 55/62 | 49/71 | 58/62 | 48/65 | 46/71 | 55/66 | 52/65 | 0/100 | 58/55 | 49/79 |
| pack 5 aware | aggressive | 40/84 | 54/67 | 43/81 | 42/82 | 43/80 | 38/83 | 38/87 | 36/87 | 38/84 | 0/100 | 51/75 | 31/92 |
| pack 5 ambush | aggressive | 29/88 | 44/75 | 36/80 | 29/87 | 37/79 | 30/85 | 25/89 | 34/86 | 30/87 | 0/100 | 42/78 | 23/93 |
| bear | ai | 4/82 | 11/61 | 9/63 | 6/79 | 4/81 | 0/97 | 0/84 | 3/81 | 4/81 | 0/98 | 6/78 | 4/80 |
| bear | aggressive | 52/0 | 46/0 | 33/0 | 52/0 | 51/1 | 27/0 | 50/1 | 50/0 | 52/1 | 1/99 | 59/0 | 50/0 |
| bear | cautious | 24/31 | 21/40 | 17/40 | 21/34 | 20/40 | 0/92 | 0/24 | 6/42 | 22/30 | 0/51 | 24/30 | 23/29 |

The AI and cautious rows of packs 3 and 5 under ambush are 0-5 / 95-100 in every cell.

### 3.4 Which factors move the outcome

**Matter a lot:**

- **Armed or not.** This is binary. Bare hands lose every fight (93-100% death against a wolf
  attacking aggressively). Any weapon against a lone wolf or a bear, used aggressively, gives 0%
  death.
- **Policy.**
  - Throwing is a trap: caribou kills drop from 46% to 2%, and bison deaths rise from 20% to 66%.
  - Charging beats retreating: against a lone wolf, death is 0% charging and 19% cautious.
  - The AI person is far worse than either scripted policy: 75% death against a lone wolf with a
    spear.
- **Opening awareness.** Against an ambush, the AI person dies 100% to any pack. A scripted player
  still kills something half the time.
- **Tired, hungry and leg, for caribou hunts only** (47% falls to 12-16%). They cut Moving, so the
  allowance falls from 3 m to 2 m, and they cut the blow through Strength and Vitality
  (`Combat.cs:657`). A first blow that does not kill means a bolting caribou that is never caught
  again.
- **Leg, for escape.** The cautious policy with a leg wound dies 95% to a lone wolf.
- **Bleeding, only through time.** A careful 80-minute stalk while bleeding drops caribou kills
  from 47% to 19%; a 16-minute quick stalk is unaffected (46%). This is the one interaction that
  behaves the way the design wants.

**Barely matter, or invert, where a player would expect an effect. Each of these is a finding:**

- **Weapon type.** Knife (6), spear (8) and axe (12) are within a few points of each other, and the
  order is *inverted*: the knife beats the axe against the lone wolf (41% vs 32% wins), against the
  pack (72% vs 55% kills) and on bison. Everything above roughly 6 damage is already past one-shot
  lethality on the 0-1 body. The spear's reach does nothing: there is no reach rule, and Attack
  works at ≤ 3 m for everything. `blockChance` is unused (`Combat.cs:697-702`).
- **Care on the approach.** Careful and quick melee stalks kill at the same rate (47% vs 46%). The
  careful stalk takes 80 minutes instead of 16. Detection is a hazard per metre, so step size
  cancels out (`Combat.cs:793`). Freezing cannot help, because Alert never decays.
- **Cold**, at both 95 °F and 92 °F: no measurable effect anywhere.
- **Arm wound** (Manipulation 0.8): no effect on aggressive play. Manipulation enters only
  Strength, as 0.8 + 0.2c.
- **Carrying 30 kg of meat**: no effect anywhere.
  - `Allowance` reads Moving only (`CombatAI.cs:89-93`).
  - `DodgeChance` calls `Body.Speed(body)` without the load (`Combat.cs:694`).
  - Nothing in combat passes `targetCarriesMeat` to `Combat.Boldness`.
- **Bleeding 0.3 in a fight**: no effect. Fights last 8-15 minutes, about 150 ml.
- **Tired and hungry in pack fights** improve the aggressive player's results (pack 5: 40% to 54%
  kills, 84% to 67% deaths). I did not isolate why. The likely cause is that a slower person is
  reached by fewer wolves at once.
- **Fire or torch**: not read (§2). The distance-based prototype in §2.1 makes it the strongest
  single factor against a lone wolf: unarmed deaths fall from 96% to 0% at full fire.

## 4. Diagnosis

### 4.1 "Can't catch it"

1. **Flight is a fixed-allowance race with no stamina or variance.**
   - `Allowance = round(MoveAllowanceM × Moving)` (`CombatAI.cs:89-93`).
   - Caribou and human are both 3 m, bison 3, wolf 6, bear 5 (`species.json:21-46`).
   - A skittish animal that is Engaged bolts its full allowance directly away every turn
     (`CombatAI.cs:175-176`). The hunter's Advance is also a full allowance (`Combat.cs:409`). The
     gap therefore never closes: the 3-4-3-4 m traces in §2.
   - A wounded wolf moves 6 m a round against the person's 3, so it always escapes. A person never
     outruns a wolf.
   - Whoever is faster wins every chase, every time. Wolves catch caribou 100% of the time with no
     wolf ever hurt.
2. **One wound flips morale to "leave".**
   - `UnitBoldness` subtracts `HealthPenalty` = (1 − worst region) × 1.5 + blood lost + 0.3 if
     bleeding (`CombatAI.cs:23-32`). The first blow also clears `InitialOptimism`, worth up to +1.0
     (`CombatAI.cs:67`).
   - With spear damage of 8, one hit destroys a region: penalty 1.5 + 0.3 against a wolf's 1.3 base.
     The movement vector turns into a ×2 retreat (`CombatAI.cs:110-116`), and the wolf leaves the
     50 m grid.
   - Among lone wolves that escaped unkilled after an aggressive player hit them, 339 of 390 left
     after exactly one hit. This is the "you hit it once and it was gone" ending, 65% of armed
     lone-wolf fights.
3. **Alert never lapses.** Detection only escalates, Unaware to Alert to Engaged
   (`Combat.cs:829-834`). A hunter who waits gains nothing: a 300-round freeze stays Alert for all
   300 rounds. The first version of the careful policy never finished a hunt.
4. **A thrown weapon is gone.**
   - The weapon lands on the ground (`Combat.cs:587-588`), and combat has no pick-up action.
   - 34% of thrown spears hit, but only 8% of hits kill: the target is often Alert by 8-10 m, so it
     takes ×1 damage, not ×2 (`Combat.cs:682`). The hunter is then left at 0.15 bare-hand damage.
   - Melee hunters get repeated strikes, and 71% of their hits end in a kill.
   - An oddity: `Throwing` gives stone-tipped range to anything whose name contains "Stone"
     (`Combat.cs:593-594`), which includes the Stone Axe.
5. **The grid and the flee rule.**
   - Escape needs a unit within 3 m of the edge of a 50 m square (`Combat.cs:101,103,334`), and the
     person starts at the centre (`Combat.cs:251`), 22 m from the flee band: 7-8 rounds at 3 m.
   - Leaving the field is the only way a fight "ends by flight". `ChooseAction` has no Flee action
     (`CombatAI.cs:186-203`): animals flee by steering off the map.
   - The overview's "boldness < 0.3 flees" is not in the code.

### 4.2 "Mutual brawl to the death"

1. **Morale only breaks on wounds, and a fighter that is not hurt never breaks.**
   - `WantsToAttack` compares boldness (about 2.0-2.7 at the start, `CombatGoldenTests:110`) with a
     perceived threat below 1 (`CombatAI.cs:82-83`). Every healthy unit attacks.
   - When the person cannot wound (bare hands, 0.15 a blow), the animal never takes a
     morale-breaking wound and stays until the person dies: 97-100% brawls, unarmed against a wolf
     or a bear.
   - Packs: morale is split across bodies. The pack's `AllyBonus` (+0.15 × threat × cohesion 2 per
     ally) keeps unhurt wolves in, and `AllyKilled` (−2 × cohesion) only cascades once one dies. So
     pack fights are 50-70% brawls in which the winner ends near death (winner wound 0.86-1.00).
2. **The AI person's morale collapses after one bite, and the AI then neither attacks nor flees.**
   - The same `HealthPenalty` takes a person (boldness 1.0) below the attack threshold after the
     first bite: perceived threat 0.65 × (1 − 0.5 aggression) = 0.33.
   - `ChooseAction` then picks Block, Shove or Move at Close and Near range, never Flee. Its
     retreat vector moves 3 m against the wolf's 6.
   - Result: Attack 0% in an ambush and 75% death with a spear. A scripted "always attack" player
     with the same body and spear dies 0%.
   - This is how NPCs fight today: `Npc.RespondToThreat` (`Npc.cs:1558`) and `Combat.Join`.
3. **Damage scales (body review B9).**
   - Weapons do 6-12; bites do 0.25-0.6 (`species.json`).
   - Every armed hit is near-lethal or crippling, and the inner-organ threshold of 5
     (`Body.cs:600`) is reachable only by weapons.
   - Armed fights therefore end in one or two blows (a kill, or the morale flip above). Unarmed
     fights are 10-15 rounds of bites that cannot be answered. There is no middle.
4. **A round is a minute** (`Combat.cs:94`, `Sim.cs:250`). A 12-round brawl is 12 minutes of
   melee. Stalks take 80 minutes because detection is spread over metres, not time. Bleeding works
   on the minute clock, which is why bleeding matters on a long stalk and not in a fight.

### 4.3 Too easy for predators

4 wolves against a caribou: 100% kills, zero wolves hurt, 2-6 rounds. The causes:

- 6 m against 3 m allowance.
- The skittish prey never fights (`Species.cs:33`; `ChooseAction` returns Move).
- The pursuer strikes in the same turn it closes (`CombatAI.cs:218-221`).

Against a bison, 86% kills. Real wolf packs fail most chases. The golden test pins this (old game:
100%).

## 5. Sensitivity: two cheap changes on the grid

I ran two variants of the snapshot (600 fights per row):

- **A (speeds)**: human 4, wolf 7, bear 6, caribou 8, bison 6.
- **A + B**: A, plus `HealthPenalty` at one third (0.5 per part, 0.1 bleeding) and weapon damage
  ÷ 10 (spear 0.8, knife 0.6, axe 1.2), as B9(a) proposes.

Cells are kill / person-dies / got away / brawl (%) / median rounds.

| Scenario | Weapon | Policy | Baseline | A | A + B |
|---|---|---|---|---|---|
| lone wolf ambush | spear | aggressive | 35/0/65/1/7 | 49/0/51/3/8 | 89/11/0/**94**/6 |
| lone wolf aware | spear | aggressive | 40/0/60/2/8 | 52/0/48/3/8 | 94/6/0/**96**/7 |
| lone wolf ambush | spear | ai | 0/73/26/0/15 | 0/70/29/0/16 | 83/17/0/97/7 |
| bear | spear | aggressive | 54/0/46/5/9 | 69/0/31/7/8 | 85/15/0/96/6 |
| pack 3 aware | spear | aggressive | 63/59/0/60/9 | 64/56/0/56/9 | 41/87/0/83/8 |
| caribou hunt | spear | quick_melee | 40/0/60/0/16 | **7**/0/93/0/11 | 2/0/98/0/11 |
| caribou hunt | spear | careful_melee | 46/0/54/0/78 | 4/0/96/0/76 | 2/0/98/0/76 |
| bison hunt | spear | quick_melee | 68/18/16/20/20 | 51/25/25/23/14 | 32/54/23/50/13 |
| 4 wolves vs 1 caribou | - | ai | 100/0/0/0/6 | **2**/0/98/0/4 | 2/0/98/0/4 |
| 2 people vs 4 wolves | spear | ai | 64/71/0/60/12 | 67/67/0/60/12 | 73/79/0/82/11 |

**What this shows:**

- **Speeds:** making caribou faster than wolves takes wolves from 100% to 2% kills, and takes the
  player's hunts from 46% to 4-7%. Flight is a deterministic comparison of two integers. A grid with
  no stamina, no terrain and no variance in sprinting cannot produce the middle ("the pack runs down
  one in three").
- **Morale + damage:** make the wolf stop leaving after one hit, and the lone-wolf fight goes
  straight from 65% "got away" to 94-96% "brawl to the death".

**The two bad patterns are the two ends of one morale knob. The grid offers no intermediate
behaviour:** no "give ground, circle, test, break off". The steering vector (`CombatAI.cs:97-123`)
has only two modes: advance in proportion to advantage, or retreat at ×2.

## 6. Options

### (a) Fix the grid model: minimal changes

| # | Change | Where | Size |
|---|---|---|---|
| 1 | Sprint with stamina: each round of running at full speed costs energy, and allowance falls as the sprint wears on. Prey can then outlast a pack early and lose late. A person runs from a wolf for a round or two, not forever. Add a small random term per round. | `Allowance` (`CombatAI.cs:89`), `CombatUnit` (stamina field), `species.json` (sprint and endurance per species) | about 60 lines plus tuning |
| 2 | Load and wounds in speed: pass `EncumbrancePct` into `Allowance` and `DodgeChance`. | `CombatAI.cs:89`, `Combat.cs:694` | about 10 lines |
| 3 | Morale as a threshold with an explicit Flee action: replace the ×1.5 per destroyed part with vitality-relative terms; give the AI a Flee or "give ground" choice below a species threshold and a "hold" for defending or cornered animals; let a person flee as well as block. | `CombatAI.cs:23-32,82-83,110-116,186-203` | about 80 lines plus tuning |
| 4 | One 0-1 damage scale (B9 a): weapons 0.3-0.8, organ threshold on the same scale. Re-pin `CombatGoldenTests`. Needed whatever model is chosen. | `items.json`, `Body.cs:600`, goldens | about 20 lines plus re-pinning |
| 5 | Alert decays back to Unaware after quiet rounds; stalk detection reads time spent as well as metres. | `Combat.cs:793,829-834` | about 20 lines |
| 6 | Throwing: a second weapon, or picking up the thrown one; drop the "Stone" name rule. | `Combat.cs:581-594`, `Refusal` | about 30 lines |
| 7 | Reach: a spear strikes at Near before a wolf can (first strike when it closes). Use `blockChance`. | `Combat.cs:381,697-702` | about 30 lines |
| 8 | Fire: boldness penalty within some metres of a fire or torch on the tile. | `Combat.Boldness`, `UnitBoldness` | about 20 lines |
| 9 | Shorter rounds (10-15 s) so fights take minutes, not a quarter hour: a fight ticks the body for a fraction of a minute. | `Sim.cs:250` Fight coupling, `Combat.EndRound` | medium; touches the step contract |

The total is roughly 300-400 changed lines in `Combat.cs` and `CombatAI.cs` (1,070 lines today),
plus a full re-pin of `CombatGoldenTests` and `HerdGoldenTests`. The cost is mostly tuning, not
code. §5 shows the grid's outcomes jump between extremes as single numbers move. Every one of these
fixes adds a coupled parameter to an emergent loop that is already hard to read. Each change needs a
sweep like this one. The probe harness is reusable for that.

### (b) An abstract model

**State per fight:**

- **Range band** between each attacker and its target: Far (> 15 m), Near (3-15 m) or Close
  (≤ 3 m). No cells, and no positions except "who is on whom".
- **Per fighter:** intent, morale (boldness − threat, kept from today), stamina, and the body as
  today.

**A round** (10-15 s; the body ticks a fraction of a minute):

1. Each side picks an intent: **press**, **hold**, **give ground**, **flee**, **strike**, **throw**
   (Near or Far), **intimidate**, **guard**.
2. **Range changes as a speed contest.** Pressing against giving ground or fleeing compares sprint
   speed: species speed × Moving × (1 − load penalty) × stamina, with one random term.
   - The winner moves the band one step.
   - Fleeing from Far with a winning roll leaves the fight.
   - Stamina drains while sprinting, so a long chase favours the endurance animal (the wolf), and a
     short one favours the sprinter (the caribou).
3. **Exchange at Close.**
   - Initiative goes to speed plus reach. A spear set at Near gets a free strike on a fighter
     pressing into Close.
   - Each blow is hit chance against guard or dodge, then `Body.Damage` on one 0-1 scale.
   - Several attackers on one defender split its guard (flanking): this is the whole pack bonus,
     without geometry.
4. **Morale.** The morale events are kept: TookDamage, AllyKilled, AllyFled and so on. Below a
   species threshold a fighter gives ground, and further below it flees. A "hold" floor applies
   for defending or cornered animals.

**Deleted:**

- `Cell`, `MapSize`, `Place`, `Formation`, `SelectFormation`, `TakeFree`.
- `Pursue` (Dijkstra), `MovementVector`, `MovePosition`, `ResolveDestination`.
- Edge fleeing (`CanFlee`, `FleeEdgeM`), `Shove`, and dodge-as-displacement.
- `Join`'s edge entry: a late arrival joins at Far.
- The metre-based zone table (`ZoneOf`).

This is about 450-550 of the 1,070 lines. `CombatAftermath.cs` (109 lines), `Combat.Boldness`,
detection odds (per band, stalk = closing bands), Intimidate, Signals and the Fight activity stay.

**Kept as emergent properties:**

- **Packs**: more attackers split the guard, and ally morale terms cascade.
- **Morale cascade**: AllyKilled and AllyFled, unchanged.
- **Wounds slow you**: Moving feeds speed, so a crippled animal can be run down and a mauled person
  cannot flee.
- **Load**: the meat you carry slows the flight contest. It matters, and it is readable.
- **Terrain**: escape terrain (trees, rocks) and cover give terms in the flight and detection
  contests. Today it enters only detection.
- **Fire**: a boldness penalty and a "cannot press into the fire" rule on the band nearest the
  fire. Neither is possible today without spatial fire on the grid.
- **Stalking**: closing Far to Near to Close with a detection roll per band. Alert decays.

**Lost:** formations (pincer, surround) as geometry, cell-by-cell steering, and any visual grid
display. The rebuild has no combat UI yet; the old desktop `CombatScreen` drew the grid. If a
visible tactical map is a product goal, that is the real cost of (b).

### Fire at a distance, per option

The requirement: the fire has a position, and each round both sides' distance from it counts, with
an inverse-square falloff.

| | How naturally it holds this | Does the hold at the edge emerge? | Cost |
|---|---|---|---|
| **(a) grid** | Natural: the fire is a cell, and distances are already Euclidean. The person stepping away, a torch moving with its holder, and a wolf on the far side of the person all follow from geometry. | **Yes**, measured in §2.1, from today's advance/retreat rule. It comes out as a 5↔11 m radial pacing that overshoots into bites, because steps are full allowances and the steering has no hold mode. A smooth hold needs step damping (step ∝ imbalance, about 15 lines). Circling needs a tangential term (about 15 lines, a rule). | Fire cell placed in `Create` from the tile's fire (about 10 lines); deterrence (12, prototyped); torch following its holder (about 5); damping (about 15); circling (about 15, optional). About 60 lines in all. |
| **(b) three range bands** | Poorly. Bands are pairwise between fighters, and the fire is a third point. You would need a second band per fighter (fighter to fire). Then inverse-square collapses into three values (in the light, the edge, dark), and "the person stepped 4 m from the fire" is either invisible or a whole band. | **No**: it needs a rule. A predator must look one band ahead and refuse a press that would take its boldness below the threat. That is simple, and it is exactly the authored behaviour the design wants to emerge. | About 40 lines plus a second band per fighter. The continuous falloff is lost. |
| **(b′) abstract, metres on a line** | Natural. Each fighter has one scalar position in metres on a line through the fire (fire at 0). Range between fighters = the difference; distance from the fire = the scalar. Speed contests move the scalars. Inverse-square and "the person steps away" are exact. | **Yes**: the same "press while boldness − deterrence > threat" emerges as a hold. Moves are speed-contest metres, not fixed allowances, so a step proportional to the imbalance comes for free (no bang-bang). Circling has no geometry; it is narration of a fighter holding at the ring. | Same as (b), plus about 30 lines. One dimension cannot place two wolves on opposite sides of a person (flanking stays the guard-split rule) or a wolf beyond the fire. |

## 7. Recommendation

I would build the abstract model, in the form **(b′)**: range as metres on a line through the fire,
resolved by speed contests and explicit intents, not three bands. The fire-at-a-distance
requirement is what makes me prefer (b′) over plain bands:

- Bands cannot carry an inverse-square falloff, and their hold at the edge would be an authored rule.
- Metres keep the falloff, and the hold emerges, as the grid prototype shows it does. The
  speed-contest step also removes the grid's bang-bang pacing.

Make the number decisions first, because every option needs them:

- the damage scale (B9 a);
- per-species sprint and endurance;
- a morale threshold for giving ground and for fleeing;
- the round length.

**Evidence:**

1. **Nearly every pathology sits in a parameter the abstract model would state directly:**
   flight-speed ordering, the one-wound morale flip, the damage scale, Alert permanence, the lost
   throw, and minute-long rounds. The grid adds no decision of its own on top of these. Measured:
   - the best player policy is "Advance, Attack" everywhere;
   - care on the approach changes nothing (47% vs 46%);
   - retreating is worse than charging;
   - the spear's reach is not a rule.
2. **The grid hides the balance knobs inside a steering vector.** §5 shows one knob moving lone-wolf
   fights from 65% "got away" to 95% "brawl" with nothing in between. The behaviours the design
   wants (an animal that tests and breaks off, a pack that runs down one of three, a person who
   gives ground with a spear set) are explicit intents in (b). On the grid they would have to
   emerge from vector weights.
3. **What the grid uniquely provides is not reaching the player.**
   - Formations exist only in `Approach` openings.
   - Distance traces show fights collapse to Close within two rounds or sit at a fixed chase gap.
   - Terrain and fire are absent from combat entirely.
4. **The NPC path is the biggest practical bug,** and it is model-independent. The AI person
   attacks almost never after a bite and dies 75% of the time to a lone wolf with a spear. Fix it in
   whichever model wins.

**The counter-case.** If a visible tactical grid is wanted for the player's screen, then option (a)
fixes 1-4 plus 5-6 are a bounded week of work. With the harness, they would remove both bad
patterns. Expect more §5-style knife-edges while tuning, and keep the sweep in CI.

The fire requirement also favours the grid somewhat more than anything else measured here:

- It is the one feature where 2-D geometry carries real meaning: a wolf on the far side of the
  fire, two wolves on opposite sides of the person.
- On the grid it costs about 60 lines.
- The prototype shows the hold at the edge already emerges there, once the full-allowance
  overshoot is damped.

If the user weighs that 2-D picture highly, (a) plus the fire work is a defensible choice.

## 8. Reproduce

```bash
cd ~/.claude/jobs/2c768bb5/tmp/combat-survey
(cd probe && dotnet build -c Release)
./run.sh                     # baseline groups -> out/*.tsv  (run2.sh: hunt groups)
python3 agg.py "out/wolf1.*.tsv" table      # or: matrix kill|dies|win, actions, dist
```

The variants are `varA/` and `varB/` (the same probe on a modified snapshot), each run with group
`var`. The fire prototype is `varC/`, run with group `fire` (`out/fire.tsv`; column 30 is the
nearest predator's distance from the fire, per round).
