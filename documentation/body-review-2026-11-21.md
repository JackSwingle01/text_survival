# Body and survival review (2026-11-21)

A read-only design review of `rebuild/Sim/Body.cs` and `rebuild/Sim/BodySurvival.cs`, done before
any of it is pinned with tests. It also covers what those files call: `Effects.cs` with
`Content/effects.json`, the parts of `Exposure.cs` the body reads, the activity profiles in
`Sim.Surroundings`, and the body side of `Combat.cs`.

Line numbers are for `HEAD` (0df09dd). `BodySurvival.cs` is being changed while this was written.
Three changes are treated as decided:

- temperature steps go to one minute;
- drying from a fire and drying from warm air are combined (the working tree takes the larger of the two);
- the square-root wind curve also applies to wind through clothing.

The temperature change moves `BodySurvival.cs` lines after about 240 down by about 45.

Every probe ran on an extract of `HEAD` from a throwaway console project, built in Release. The
probes used `Body.Update` minute by minute, a baseline human (75 kg, 15% fat, 30% muscle, the
starting furs at 1.33 clo and 3.05 kg), and the `SurvivalContext` shown. They used no shelter or
bedding unless stated.

Classes: **(A)** clear bug, just fix it. **(B)** design question for the user. **(C)** fine as
is, pin it.

---

## 1. Behaviour map

### 1.1 Metabolism, calories, fat and muscle

- **Basal rate.** `MetabolismKcalPerDay` (BodySurvival.cs:150) uses Katch-McArdle on lean mass:
  370 + 21.6 × (weight − fat). The baseline is 1,747 kcal/day. Damaged organs reduce it by up to
  30% (× (0.7 + 0.3 × mean organ condition)). Source: physics, plus the old-game fix that switched
  from muscle mass to lean mass.
- **Activity factors.** The rate is multiplied by an activity factor (Sim.cs:243-253):
  - sleep 0.5;
  - rest, fire work, eating, building 1.0;
  - travel 1.5;
  - gather and butcher 1.5;
  - chopping 1.8;
  - fighting 2.0.

  Source: the old `ActivityConfig`. Metabolism is also the body's only heat source (§1.5).
- **Calorie store.** `CaloriesKcal` is a store capped at 2,000 kcal (BodySurvival.cs:66). Old game.
- **Eating.** `Eat` (Body.cs:727) absorbs 50-100% of the food, depending on digestion with no
  effects applied. Whatever is over the cap becomes fat at 7,700 kcal/kg. The rebuild fixed an old
  bug here: the surplus used to be discarded.
- **Fasting.** `ProcessStarvation` (BodySurvival.cs:349) covers the shortfall in this order:
  1. fat, down to 3% of *current* weight;
  2. muscle, at 1,320 kcal/kg, down to 15% of current weight;
  3. then internal damage of 0.5/h to one of heart, liver, brain or lungs, picked by rng.

  The floors and order come from the old game. Probe: a baseline human fasting with water dies on
  day 56.6. Fat reaches its floor around day 41, then muscle goes.

### 1.2 Hydration and sweating

- **Base loss.** 2,500 ml/day of base loss (urine, breath, skin), × 0.7 asleep (BodySurvival.cs:70,
  203). The store is capped at 4,000 ml. The old game used 4,000 ml/day, which double-counted sweat.
- **Sweat.** `Sweat` (BodySurvival.cs:167) starts only once skin vessels are fully dilated
  (core ≥ 99 °F). It ramps to 1,000 ml/h at 103 °F. What evaporates scales with
  (1 − waterproofing) × (1 − wetness). The rest soaks the clothing at 2,000 ml per full soak. Each
  ml evaporated removes 0.58 kcal. Source: physics, as the old game had it.
- **Dehydration.** At hydration 0 the body takes internal damage of 1.0/h to brain, heart or liver
  (BodySurvival.cs:385). Probe: dead **30 minutes** after reaching zero. The comment says ~5 hours;
  `SurvivalTests.Dehydration_KillsWithinAnHourOrSo` pins the 30 minutes.

### 1.3 Energy and sleep

- **Energy.** 960 minutes at most. Awake costs −1 per minute and sleep restores +2 per minute
  (BodySurvival.cs:64, 72, 202). Old game.
- **Tick interrupts.** `Tick` reports `exhausted` once, when energy reaches 0 while awake, and
  `fully rested` once, when a sleeper reaches 960 (BodySurvival.cs:497-506).
- **At zero energy.** Nothing else happens: energy 0 is never lethal and never makes you sleep.
  Probe: a 56-day fast with no sleep at all (energy 0 from hour 15) ran to day 56.
- **Sleep healing.** `Update` heals 0.1 × quality per minute asleep (BodySurvival.cs:445-452).
  Quality = (1.0 if energy is 0, else 0.7) × bedding quality (0.3 on bare ground), × (1 − wet/2)
  when wet > 0.2. Old `Body.Rest`. **None of the regeneration gates apply.** See A1.

### 1.4 Regeneration

- **Waking regeneration.** `ProcessRegeneration` (BodySurvival.cs:408) needs all of:
  - calories above 10% (200 kcal);
  - hydration above 10% (400 ml);
  - energy at or above 50% (480 min).

  The old code had `Energy < 50%`, inverted. The rebuild flipped it without a comment.
- **Rate.** amount = 0.1/h × nutrition × digestion. Tissue gets amount × nutrition, so nutrition
  counts twice (old `HealingInfo.Quality`). Blood gets amount × 0.5.
- **Blood regrowth.** Blood regrows at 0.05/h at full nutrition, or 250 ml/h. Probe: blood from
  55% to 99% in 9.8 h.
- **Heal.** `Heal` (Body.cs:702) puts the whole amount into the single worst region: its layers,
  then its organs that are not destroyed. Healing undoes toughness, so bone heals at one third of
  the rate. A destroyed organ (condition 0) never comes back.

### 1.5 Heat balance

- **Balance.** `CalculateHeatBalance` (BodySurvival.cs:177): heat change = (metabolism − sensible
  loss − evaporative loss) / (weight × 0.83 kcal/kg°F).
- **Sensible loss.** 1.8 m² × (skin − (air + fire)) °C / R × 0.86. R is in series
  (`TotalThermalResistance`, :125): clothing clo × wetness factor × wind factor, plus fat clo
  (`ColdResistance` × 1.5), all × 0.155, plus 1/h_conv. Source: physics.
  - The wetness factor is 1 − 0.7·wet, with a 0.1 floor.
  - The wind factor is 1 − wind' × (1 − sealed) × 0.4, with a 0.2 floor, where wind' is the
    square-root curve (in flight).
  - h_conv = 5 × (1 + 2·wind'), with wind' linear to 1 and the square root above.
- **Vasomotor tone** (:105):
  - 0 at core ≤ 97 °F, 0.5 at 98.6, 1 at ≥ 99.
  - The core-to-skin gap runs from 14 °F (constricted) to 2.8 °F (dilated).
  - Source: old game, and physiologically sound: constriction runs out where shivering starts and
    dilation where sweating starts.
- **Fire heat.** `FireF` (:113) adds the fire's radiant heat to the air unless core ≥ 99 °F; a
  torch's heat is always added.
- **Clothing buffer** (:240-277):
  - It holds clothing kg × 2 °F of "stored chill" (6.1 °F for the baseline furs), which is
    ≈ 380 kcal of body heat.
  - It pays for core losses while core ≤ 98.6.
  - It fills near a fire at fireF/150/capacity per minute.
  - It also fills whenever the core is warming, at coreΔ × ins/(1 − ins), without taking that heat
    from the core.
  - Source: old game.
- **Effects outside the balance.** Two effects add core heat directly, after the balance
  (`Update`, :461; effects.json): Shivering +3 °F/h at full severity, and Wet −1 °F/h.
- **Air the body feels.** `Exposure.FeltAirF` (Exposure.cs:125):
  - wind chill (NWS formula at the body's wind, gated on regional wind > 0.1);
  - sun +15 °F × exposure, weighted toward cold;
  - precipitation −5 °F;
  - then the shelter step toward a 40 °F floor (:147).
- **Comfort temperature.** `ComfortTemperatureF` (:141) assumes a resting activity factor of 1.0.

### 1.6 Wetness

`ProcessWetness` (BodySurvival.cs:311) adds three sources:

- rain 0.01, blizzard 0.005 or snow 0.003 per minute, × intensity × (1 − cover) × (1 − waterproofing);
- sweat that could not evaporate;
- ground wetting × (1 − bedding) × (1 − waterproofing).

Drying is paid for in latent heat: fire 300 + 25 × fireF kcal/h, or warm air (T − 32) × 10, × (1 +
1.5·wind) when dry. It is 0 when frozen. Wet is a level, `Set` each minute. Source: old game,
with the drying rates from its latent-heat derivation.

### 1.7 Capacities and abilities

- **Region capacities.** `RegionCapacities` (Body.cs:303) works in three steps:
  1. Sum what each organ provides, plus 1 each for Moving and Manipulation (muscle).
  2. Multiply every capacity by the **average** condition multiplier over *all* tissues in the
     region (3 layers plus every organ).
  3. Zero a capacity only if a sole provider (amount ≥ 1.0) is destroyed.

  Source: old game.
- **Condition multipliers.**
  - Skin: c.
  - Muscle: c for movement, 0.8 + 0.2c for breathing, 0.9 + 0.1c for pumping.
  - Bone: c² for moving, c for manipulation, 0.8 + 0.2c for pumping.
  - Organ: c for what it provides.
- **Moving and manipulation.** Moving is 90% legs, split evenly, and 10% everything else.
  Manipulation is 45% per arm and 2% each for the other *human* region names, so a quadruped's
  Manipulation is 0.02.
- **Effects and cascades** (Body.cs:226-252). Effects add severity-scaled deltas, clamped. Then:
  - BloodPumping is multiplied by blood.
  - Below 100% blood, consciousness, moving and manipulation are multiplied by (blood − 0.5)/0.5,
    so 50% blood is death.
  - Breathing below 0.3 drags consciousness down.
  - Consciousness below 0.1 multiplies moving and manipulation by 0.1.
- **Vitality** = min(breathing, pumping, consciousness), and IsAlive is vitality > 0. So
  **effects alone can kill**: see B5.
- **Abilities** (Body.cs:390-471), all from the old `AbilityCalculator`:
  - Strength, Speed and ColdResistance are piecewise and continuous at their breakpoints. Strength
    and Speed carry absolute-kg terms (MuscleKg/300, MuscleKg/2000, FatKg/1000).
  - Perception: see A3.
  - Dexterity is reduced by darkness ×0.5, wetness above 0.3, and vitality.

### 1.8 Damage, bleeding, blood, pain

- **Entry point.** `Damage` (Body.cs:477) is the one entry point.
- **Armor.** It reduces a hit by at most 80%:
  - cushioning against blunt;
  - toughness against sharp;
  - 0.7 × toughness against pierce.

  `Combat.Hurt` (Combat.cs:673) sums armor over *all* worn gear, whatever region is hit (old
  game).
- **Blood damage.** Blood takes only Bleed and Internal damage, in ml out of 5,000 ml for every
  body.
- **Organ targeting.** Internal damage can aim at an organ. Every other type picks a region by
  coverage.
- **Layers.** Each layer absorbs min(70% of what reaches it, toughness × condition). The tissue
  loses (absorbed − natural absorption)/(toughness × condition). That divisor makes damage run away
  as condition falls (C² falls linearly).
- **What is left over** hits an *external* organ (eye or ear) if the region has one. Otherwise it
  hits an inner organ if the *whole* hit was above 5, and otherwise muscle at 0.5×.
- **Triggered effects:**
  - region destroyed: Bleeding 1.0;
  - sharp or pierce with more than 3% skin lost: Bleeding at 0.5 × the region's loss, 0.15-1;
  - blunt with more than 0.20 total tissue lost: Bleeding at 1.5 × that loss, 0.2-0.8;
  - region loss over 0.25: Shock;
  - region loss over 0.02 from blunt, sharp, pierce or burn: Pain;
  - blunt to the head over 0.08: Dazed.
- **Bleeding.** Two wounds sum their bleeding, capped at 1 (`Effects.Add`). A full bleed loses
  3,000 ml/h and decays −0.1/h. Probe: an untreated bleed of 0.45 or less is survivable; 0.6 kills
  in 1.9 h.
- **Scales.** Weapon damage is 6-12 (items.json), animal attacks 0.02-1.2 (species.json), and
  accidents 0.05-0.10. See B9.

### 1.9 Survival effects

All in `Content/effects.json`:

| Effect | Set from | Decay | Notes |
|---|---|---|---|
| Hungry, Thirsty, Tired | (0.3 − pct)/0.3 of store, hydration and energy | −1/h | |
| Shivering | (97 − core)/5 | −2/h | +3 °F/h heat |
| Hypothermia | (95 − core)/10 | −0.5/h | |
| Frostbite | (89.6 − core)/10 | −0.02/h | needs treatment at any severity |
| Hyperthermia | (core − 100)/10 | | |
| Sweating | sweat severity | | signal only |
| Bloody | +0.15/h × bleeding | −0.02/h | needs treatment at any severity |
| Shock | | | needs treatment above 0.5 |

An effect that needs treatment fades only to a 5% floor (`Effects.Decay`). **No treatment action
exists in the rebuild.**

Each minute's effects are added first and then decayed by a minute (`Update`, :454-459).

### 1.10 Lethal processes and cause of death

- **Hypothermia damage** (BodySurvival.cs:393). Severity s = (95 − core)/15. The damage is
  0.1 + 0.4s per hour while s < 0.5, then 0.5 + 0.5(s − 0.5). That is discontinuous at 87.5 °F (see
  A13), and it reaches only 0.75/h, although its comment claims 8/h. It goes to heart, brain or
  lungs, picked by rng. Old game.
- **Death always comes through the brain.** In every cold, thirst and starvation probe the brain
  was at 0.00 and the heart at about 0.94. The brain's toughness is 0.25 (old game) and the
  runaway divisor does the rest.
- **`CauseOfDeath`** (Body.cs:377) checks in this order:
  1. a mortal wound with `WoundedBy`;
  2. blood under 50%;
  3. the largest of the cold, thirst and hunger `LethalRates` (ties go cold, then thirst, then
     hunger);
  4. energy 0, named "exhaustion";
  5. `WoundedBy`, or "its injuries".

---

## 2. Questionable behaviour

Severity describes what a player would notice:

- **critical**: breaks a core loop;
- **high**: visible and wrong;
- **medium**: wrong in a corner players reach;
- **low**: cosmetic or latent.

### (A) Clear bugs

**A1. Sleep heals through every gate and outpaces every lethal process.** Critical.
- *Does:* `Update` (BodySurvival.cs:445-452) heals 0.1 × quality per minute asleep. It does not
  check fed, watered or warm, and it mends organs. That is 1.3-4.2 condition-units/h, against
  hypothermia's 0.1-0.75/h, starvation's 0.5/h and dehydration's 1.0/h.
- *Scenario (probe):*
  - Naked, asleep, at −40 °F with wind 2.5: after 24 h the core is **−3.2 °F**, the body is alive,
    and the brain is at 1.00.
  - Asleep for 72 h at hydration 0: alive.
  - Asleep for 72 h with no reserves: alive.
  - Awake in the same −40 °F wind: dead in 1.6 h.
- *Why unintended:* sleep is the most dangerous time in the cold (principles: "the fire burns
  whether you are there or not"), and here it is the safest. Nothing wakes a player sleeper on cold
  (Sim.cs: only `died` and `fully rested` interrupt a sleep). NPC cold-bed logic avoids it only
  because NPCs plan around a hypothermia margin.
- *Fix options:*
  - (a) Sleep healing goes through `ProcessRegeneration`: the same gates, with being asleep counted
    as rested, at a multiple of the waking rate. **Recommended.**
  - (b) Keep the separate channel but gate it, and give organs no sleep healing.
  - (c) Leave it and add a lethal core floor as a backstop. That cures the symptom only.

  The rate itself is B3.

**A2. Organ and bone loss is diluted by averaging.** High.
- *Does:* `RegionCapacities` (Body.cs:314-316) averages the multiplier over every tissue in the
  region. Probe results:
  - both lungs destroyed: breathing 0.667, alive;
  - both eyes destroyed: sight 0.75;
  - brain at 0.02: consciousness 0.88;
  - heart at 0.02: pumping 0.84;
  - liver and stomach destroyed: digestion 0.71;
  - leg bone at 0.2 or 0.0: moving 0.86 (0.2) and 0.55 (0.0).
- *Why unintended:* the code's own comments disagree. Body.cs:338 says "a shattered one stops
  you". Body.cs:318 special-cases only sole providers. `BodyTests.LostLungs_AreDilutedByAveraging`
  calls it a "known weakness".
- *Fix options:*
  - (a) A capacity is Σ(provides × that organ's condition). The region's layer multipliers combine
    as a product, or the minimum, not an average. **Recommended.**
  - (b) Use the minimum multiplier over the region.
  - (c) Extend the zero override to "every provider destroyed". That is a patch and still dilutes
    partial damage.

**A3. Perception does not fall monotonically with darkness.** High.
- *Does:* Body.cs:436-437 gives perception = S(1 − d)·0.5(1 − d) + H(1 − 0.5(1 − d)).
- *Scenario:* perception is 1.000 at d = 0, 0.875 at d = 0.5, and **1.000** at d = 1. A healthy
  body in pitch dark perceives as well as at noon. This is old-game parity.
  `BodyTests.Perception_FullInDaylight_HearingCarriesItInTheDark` pins it.
- *Fix options:*
  - (a) S × 0.5(1 − d) + H × 0.5: hearing keeps its half and darkness removes sight's half.
    **Recommended.**
  - (b) Hearing's weight grows with darkness, capped so full darkness is at most about 0.6.

**A4. Head blows can never reach the brain.** Medium.
- *Does:* `OrganToHit` (Body.cs:594-601) returns an external organ whenever there is one, before
  the inner check runs. The head has 4 external organs and 1 inner one.
- *Scenario:* 2,000 head stabs at 0.5 and at 9 hit only eyes and ears, never the brain. The comment
  at :599 says "in practice only huge hits reach inner organs", but weapons (6-12) always clear 5.
- *Fix options:*
  - (a) Pick an external organ with some probability (say 30%); otherwise go inner if what remains
    clears a depth threshold on the 0-1 scale. **Recommended**, together with B9.
  - (b) Weight organs by an authored coverage.

**A5. Bloody never dries, though the code says it does.** Low today, latent.
- *Does:* BodySurvival.cs:343 says "existing blood dries on its own", but effects.json gives
  Bloody `treatmentAboveSeverity: 0`. Any blood therefore sits at a 5% floor forever, and there is
  no washing. `Tags.cs:110` tags `ScentSource` on Bloody > 0.
- *Scenario:* butcher one carcass (Work.cs:376 sets Bloody). From then on the player is a scent
  source for the whole run.
- *Fix options:*
  - (a) Set the threshold to `null`, so blood fades at −0.02/h until a wash action exists.
    **Recommended.**
  - (b) Add washing now.

**A6. Re-asserted effects lose a minute of decay before anyone reads them.** Low.
- *Does:* `Update` (:454-459) adds this minute's level, then decays it.
- *Scenario:* at core 96.9 °F Shivering is set to 0.02 and decays to 0, so it is absent until about
  96.83 °F. Sweating is absent until 99.13 °F, although the sweat physics runs from 99.0.
  Hungry, Thirsty and Tired read about 0.017 low. "The visible effect reports the same response"
  (old comment) is false at the edges.
- *Fix options:*
  - (a) Decay first, then assert. **Recommended.**
  - (b) Skip decay for kinds asserted this minute.

**A7. A person's attacker is never forgotten.** Medium.
- *Does:* `BodyState.WoundedBy` is cleared only for herd animals once they mend (Fauna.cs:153).
  `CauseOfDeath` (Body.cs:379-386) credits `WoundedBy` whenever any region is under 0.5. It also
  credits it as the fallback for any death without a lethal rate.
- *Scenario:* a wolf bite leaves a leg at 0.45. Two days later the person freezes, and the body is
  "killed by a wolf pack".
- *Fix:* clear `WoundedBy` when a person's regions and blood mend (as `Fauna.Tick` does). The wider
  naming problem is B5.

**A8. Hypothermia damage and the cause of death read different core temperatures.** Low.
- *Does:* `ProcessHypothermia` reads the *projected* core (:211-215), while `LethalRates` and the
  effects read the core as the minute began.
- *Consequence:* on the minute the core crosses 95 °F, damage is charged while `LethalRates` says 0.
- *Fix:* read one core everywhere: the start of the minute, as the effects do.

**A9. Nutrition counts twice for tissue and once for blood.** Low.
- *Does:* :417-420 multiply tissue healing by nutrition squared and blood healing by nutrition.
  This is the old `HealingInfo.Quality` applied on top of an amount that already held nutrition.
- *Fix:* count nutrition once for both.

**A10. The wind-chill cliff at 50 °F.** Medium.
- *Does:* `Weather.WindChillF` (Weather.cs:160) returns T unchanged above 50 °F. The body reads it
  through `Exposure.FeltAirF`.
- *Scenario:* in a 45 mph wind, 49.9 °F feels like about 40 °F and 50.1 °F feels like 50.1 °F: a
  10 °F jump for 0.2 °F.
- *Fix options:*
  - (a) Fade the chill to zero between 50 and 60 °F. **Recommended.**
  - (b) min(T, formula), which is still kinked.

  This becomes moot if B8 drops the chill from the body.

**A11. Stale comments promise lethal timelines the code does not have.** Low.
- *Does:* the comments promise "~5 hours" for dehydration (:387), "~10 hours" for starvation (:365)
  and "~80 °F" with "8/h" for hypothermia (:392, and old). The real figures are 0.5 h, a few hours,
  and death at 87-94 °F through the brain.
- *Fix:* correct the comments, after B4 sets the numbers.

**A12. Protection takes a damage type it never reads.** Low.
- *Does:* `Protection(Tissue, DamageType)` (Body.cs:615) ignores its type. The old multipliers were
  all 1.0.
- *Fix:* drop the parameter, or bring the multipliers back if B9 wants them.

**A13. Hypothermia damage jumps at 87.5 °F.** Medium.
- *Does:* `HypothermiaDamagePerHour` (:397) jumps from 0.30/h to 0.50/h at s = 0.5, which is core
  87.5 °F. Its peak is 0.75/h, while the comment says 8/h at about 80 °F. Old game.
- *Fix:* a continuous curve. The magnitudes are part of B4.

### (B) Design questions

**B1. Shivering heats the body for nothing.** Critical for balance.
- *Does:* Shivering adds +3 °F/h at full severity (effects.json), straight into the core and
  outside `HeatBalance`. Metabolism, and so calories, ignore it.
- *Scenario (probe):* asleep, naked, at −40 °F, the body made **4,289 kcal of shivering heat while
  burning 873 kcal**. At rest at 20 °F the core settles at 95.6 °F for ever, on 882 kcal/day of free
  heat. It also breaks `HeatBalance`'s promise that the terms answer "why am I freezing".
- *Options:*
  - (a) Shivering raises metabolism inside the balance: an activity multiplier up to about 2.5×
    basal at full severity. It warms you and burns food. **Recommended.**
  - (b) Keep it free, but as a named term in the balance.
  - (c) Keep it as it is.

**B2. Wet clothing cools you directly, on top of losing insulation.** Medium.
- *Does:* Wet adds −1 °F/h at full severity outside the balance. It also costs the clothing 70% of
  its insulation.
- *Scenario:* resting at 20 °F with wetness held at 0.9 is dead in 8 h. Dry, the same body is stable
  for 24 h at 95.6 °F.
- *Options:*
  - (a) Move it into `HeatBalance` as a named evaporative term of the same size, so the breakdown
    sums. **Recommended now.**
  - (b) Derive it from the drying rate × 0.58 kcal/ml when not frozen, which ties cooling to
    drying. **Recommended later.**
  - (c) Keep it as it is.

**B3. How fast should bodies heal?**
- *Does:*
  - Sleep heals 12-60× faster than waking regeneration.
  - A shattered leg (bone 0.2, muscle 0.5) heals in **0.7 h** in a good bed, 2.2 h on bare ground,
    and 35.5 h awake.
  - Blood comes back from 55% to 99% in 9.8 h.
  - Waking regeneration needs energy ≥ 50%, a gate the old code had inverted.
- *Options:*
  - (a) One healing rate, with sleep as a ×2-3 multiplier, tuned so that a cut takes 1-2 days, a
    fracture weeks, and a half loss of blood about 2 days. Asleep counts as rested. **Recommended.**
  - (b) Keep fast healing.
  - (c) Separate rates for each tissue.

**B4. Cold, thirst and hunger kill through the brain's toughness.** High.
- *Does:* internal damage is divided by toughness × condition. The brain's toughness is 0.25 and
  the divisor makes damage run away.
- *Scenario:*
  - From dry to dead takes 30 min, so going without water kills in about 29 h at rest, against
    about 3 days in reality.
  - Soaked at 40 °F, the body dies at core **94.0 °F**, which is mild hypothermia, 5.5 h after
    crossing 95.
  - Every cold death in the probes was brain death, with the heart at 0.94.
- *Options:*
  - (a) Internal damage takes condition directly: no toughness, no condition divisor. The rates
    then mean what the comments say. Dehydration kills in about 3 h, and mild hypothermia takes
    about a day while severe takes hours. **Recommended.**
  - (b) Give internal damage its own per-organ toughness.
  - (c) Keep it as it is.

**B5. Can effects alone kill, and what is the death named after?** High.
- *Does:* Vitality includes the effect deltas.
- *Scenario:* Tired 1 + Thirsty 0.93 + Hungry 1 (83 ml of water left) brings consciousness to 0.
  The body dies of "exhaustion". Shock, Pain and Dazed at 0.8 plus Tired at 0.9 kill a body with no
  organ harmed.
- *Options:*
  - (a) `IsAlive` reads tissue and blood without effects. Effects can collapse consciousness (you
    cannot act) but not kill. Each lethal damage carries its `Cause`, and the death is named after
    the damage that took the failing organ or the blood over the line. **Recommended.**
  - (b) Keep effect deaths, but name them after the biggest contributor.
  - (c) Keep it as it is.

**B6. Activity factors.**
- *Does:* sleep is 0.5× basal (real ≈ 0.9), walking 1.5 (real ≈ 3), chopping 1.8 (≈ 4.5-6),
  fighting 2.0 (≈ 6).
- *Scenario:*
  - Asleep in the baseline furs, the body balances only in **71.5 °F** air (56.6 °F at 0.9).
  - Walking at 20 °F settles at 96.5 °F, still shivering.
  - Moving barely warms you, so "stop and you freeze" never happens.
- *Options:*
  - (a) Physical factors: sleep 0.9, rest 1.2, walk 3, gather or butcher 3, chop 4.5, fight 6.
    Retune food yields to match. **Recommended.**
  - (b) Only raise sleep to 0.9.
  - (c) Keep them.

**B7. The clothing heat buffer is about 20× physical, and it fills for free.**
- *Does:* 3 kg of furs store about 380 kcal. Physically they would hold about 20 kcal.
- *Scenario:*
  - A full buffer adds 3.5-6 h before the core reaches 97 °F when walking at −20 °F (444 against
    102 min at 0 °F).
  - Rewarming at 75 °F with no fire fills it to 0.52 in 3 h, while the core warms at the same time,
    so the heat is counted twice.
- *Options:*
  - (a) Keep the size as the deliberate fire-tether mechanic, but fill it only from outside heat
    (fire, sun), never from the body's own warming. **Recommended.**
  - (b) Make it physical.
  - (c) Keep it as it is.

**B8. Wind is counted twice.**
- *Does:* wind lowers the air the body feels (wind chill) *and* raises convection and pushes
  through clothing.
- *Scenario:* walking at 0 °F, the core loses 1.07 °F/h in calm air, 2.01 with the chill alone,
  3.78 with convection alone, and **5.62 with both**.
- *Options:*
  - (a) Leave wind chill out of the body's air and keep it as the "feels like" display. Convection
    and clothing carry the wind. **Recommended**, tuned together with B6.
  - (b) Keep both, since the old values were tuned that way.
  - (c) Drop the convection wind term.

**B9. Damage scales.**
- *Does:* weapons do 6-12, animals 0.02-1.2, accidents 0.05-0.1. The inner-organ threshold of 5
  (Body.cs:600) sits on the weapon scale.
- *Scenario:*
  - One spear thrust destroys a human leg, and the Bleeding 1.0 that follows kills in about 50 min.
  - A wolf survives a 12-blunt blow with its torso at 0.00.
  - No animal can ever reach an inner organ.
- *Options:*
  - (a) One 0-1 scale (weapons about 0.3-0.8) with the depth threshold on it. **Recommended.**
  - (b) Keep the scales and fix only the threshold.

**B10. Blood volume does not scale with body size.**
- *Does:* every body has 5,000 ml (Body.cs:156) and bleeds 3,000 ml/h at full severity. A 2 kg
  rabbit and a 6,000 kg mammoth bleed out on the same clock.
- *Options:*
  - (a) Volume at 70 ml/kg, with a wound's bleed in absolute ml, so big animals need many wounds or
    a long follow. **Recommended.**
  - (b) Keep it as it is.

**B11. Abilities use absolute kilograms, which breaks them for animals.**
- *Does:* Strength adds MuscleKg/300 and Speed adds MuscleKg/2000 and FatKg/1000.
- *Scenario:*
  - A 4,000 kg quadruped has strength 5.2, speed 1.16 and a 70% dodge.
  - A 6,000 kg mammoth has strength about 11, which multiplies its blows about 6×.
  - A rabbit has speed 1.07.
  - Quadruped manipulation is always 0.02, because the weights are keyed to human region names.
- *Options:*
  - (a) Make the kg terms relative to the species' baseline weight. **Recommended.**
  - (b) Use separate formulas for animals.
  - (c) Keep them.

**B12. Hunger reads the day's store, not the body's reserves.**
- *Does:* Hungry is set from `CaloriesKcal`.
- *Scenario:* one day without food gives Hungry 1, which is −35% moving and manipulation. It stays
  for the whole 56-day fast, while the body still holds 11 kg of fat.
- *Options:*
  - (a) A mild penalty from an empty store (pangs), and the strong penalty only as reserves near
    their floors. **Recommended.**
  - (b) Keep it as it is.

**B13. Frostbite is a core-temperature effect that never heals.**
- *Does:* frostbite starts below 89.6 °F core, which is near death. It needs a treatment that does
  not exist, so it is a permanent 5% floor. It is a capacity penalty that damages no tissue.
  Shock above 0.5 also has a floor that can never be treated.
- *Options:*
  - (a) Frostbite comes from felt air and wind on poorly covered hands and feet. It damages those
    regions' skin and heals the normal way. As a rule, no effect needs a treatment the game does
    not have. **Recommended.**
  - (b) Keep it, and only drop the floor.
  - (c) Keep it as it is.

**B14. Exhaustion never forces sleep and never matters beyond penalties.**
- *Does:* a body runs at energy 0 indefinitely.
- *Options:*
  - (a) At 0 energy the body falls asleep where it stands, as a general mechanic.
    **Recommended.**
  - (b) Keep only the penalties.

**B15. A blunt bruise bleeds more than a spear wound.**
- *Does:* blunt bleeding is 1.5 × all tissue lost; pierce bleeding is 0.5 × the region's loss.
- *Scenario:* blunt 0.5 gives Bleeding 0.66, while pierce 0.5 gives 0.17 (probe on a wolf). Old
  game.
- *Options:*
  - (a) Open wounds bleed by depth. Blunt causes smaller internal bleeds, and only past bone or
    organ damage. **Recommended.**
  - (b) Keep it as it is.

**B16. Armor counts every piece worn, whatever is hit.**
- *Does:* armor is summed over all gear, whatever region the hit lands on (Combat.cs:673).
- *Options:*
  - (a) Each region gets only what covers it. **Recommended, low priority.**
  - (b) Keep it as it is.
- *Decided:* (a), with the damage unit (decisions/2026-11-21-damage-is-measured-in-tissue-condition.md).

### (C) Fine as is: pin these

- **Katch-McArdle basal rate** on lean mass, × (0.7 + 0.3 × organ mean).
- **Eating.** A whole gut absorbs everything; the surplus becomes fat at 7,700 kcal/kg; a ruined
  gut absorbs half.
- **Starvation order and floors.** Fat to 3% of weight (essential fat), then muscle to 15% at
  1,320 kcal/kg. Real fasts also burn some protein early, but the sequential order is simple,
  readable, and gives a plausible 56-day fast. The floors move with current weight; pin that as the
  definition.
- **Water.** 2,500 ml/day of base loss, 0.7× asleep, a 4,000 ml cap, and sweat counted separately.
- **Vasomotor switch points 97 / 98.6 / 99 °F.** These are physiological, with a narrow zone above
  the setpoint and a wider one below. Shivering starts where constriction runs out, and sweat where
  dilation tops out. The sweat ramp runs to 103 °F and hyperthermia starts at 100 °F.
- **Thermal resistance.** In series, diminishing returns, 1 clo = 0.155 m²K/W, and the comfort
  temperature defined for a resting body.
- **Wetness.** Wetness keeps 30% of clothing insulation at full soak. Waterproofing blocks rain and
  sweat evaporation alike. Wetness is a level that is set each minute, and drying is paid for in
  latent heat (frozen means no drying).
- **Wind's square-root curve** above a gale, for convection and clothing alike (decided).
- **Bleeding.** Bleeding sums and caps at 1; every other effect keeps the worse value. Bleeding
  decays −0.1/h.
- **Capacity cascades.** Blood under 50% is collapse; breathing under 0.3 drags consciousness;
  consciousness under 0.1 means near-immobility.
- **Fast path.** The `IsWhole` shortcut must equal the slow path.
- **Piecewise abilities.** The breakpoints of `ColdResistance`, `Strength` and `Speed` are
  continuous.
- **Damage mechanics.** Layer absorption (at most 70% per layer, limited by protection), the 80%
  armor cap, and armor not applying to Internal, Bleed, Burn or Poison. Pain, Shock and Dazed are
  clamped. Bleed damage goes only to blood.
- **Heal.** The worst region goes first, nothing goes above 1, and a destroyed organ is gone for
  good.
- **Energy.** 960 minutes, −1 awake and +2 asleep. `Tick` events fire exactly on crossings, and
  `MarkDead` keeps the first death.

---

## 3. Pinning proposal

Laws run through the CsCheck `Check` helper (400 iterations). The generators extend `Gens.cs`.

**New generators:**

- `BodyGen`: per-tissue condition drawn from {1 (50%), 0 (10%), U(0,1)}, blood from {1, U(0.3,1)},
  fat 2-35%, muscle 10-60%, weight 2-6,000 kg, kind Human or Quadruped.
- `EffectSetGen`: 0-6 effects at severities in (0,1].
- `HitGen`: amount on the post-B9 scale (0-1.5), else 0-12; every `DamageType`; every `BodyTarget`;
  armor levels in [0,1].
- `AbilityCtxGen`: encumbrance 0-1.5, darkness 0-1, light true or false.

**Stryker.** Add `stryker-config.bodyplan.json` mutating `Body.cs`. Widen the test filter for both
body configs to include `BodyTests`, `SurvivalTests` and `CombatTests`, or move the targeted tests
under `Verification`. Today's filter counts only `Verification` tests, so hand-written pins earn
no score.

**Mutation contributions.** The percentages are each law's share of the mutants it is expected to
kill in that file. With all three groups in, the target is ≥ 90% for each file. Exact-value pins
("goldens") are listed only where a property cannot see a constant. Those are the ones to hold back
until the decision behind them is made.

### 3.1 (C) items, as they stand

| # | Law | Generators | Catches | Misses | Score share |
|---|---|---|---|---|---|
| C1 | Every capacity, and every ability, is finite and in [0,1] (Strength ≥ 0) for any body, effects and context | BodyGen, EffectSetGen, AbilityCtxGen | clamp removals, sign flips in cascades | wrong values inside the range | Body 6% |
| C2 | The fast path equals the slow path: a whole body's capacities equal `TissueCapacities` (forced with one tissue at 1 − 1e-12, compared within 1e-9) for each kind | both kinds | `IsWhole` conditions, a stale table | | Body 3% |
| C3 | Continuity at every piecewise breakpoint: `ColdResistance` at fat 0.05, 0.15, 0.30; `Strength` at muscle 0.2, 0.4 and fat 0.05; `Speed` at fat 0.10, 0.15. The values on either side of ±1e-9 agree within 1e-6 | targeted plus random weights | almost every constant and operator mutant in the ability formulas | slopes between breakpoints | Body 12% |
| C4 | Ability goldens at three bodies (baseline, lean, heavy): `Strength`, `Speed`, `ColdResistance` exact to 1e-9 | fixed | the slopes C3 misses | | Body 6% |
| C5 | Blood cascade: lowering blood never raises any capacity; at blood ≤ 0.5 the body is not alive; at 1.0 there is no cascade | BodyGen × blood | the 0.5 and 1.0 constants, the cascade branch | | Body 4% |
| C6 | Damage never raises any tissue or blood; zero damage changes nothing; Bleed and Internal on Blood touch only blood; armor is irrelevant for Internal, Bleed, Burn and Poison (same seed, any armor, same result) | HitGen, BodyGen | armor routing, the blood branch | | Body 8% |
| C7 | More armor never costs more condition (same seed, same target); reduction ≤ 80% | HitGen | `ArmorReduction` constants | the pierce 0.7 factor (cover it with one golden) | Body 4% |
| C8 | Each layer absorbs at most 70% of what reaches it and at most its protection; absorbed ≤ amount | HitGen | the absorption arithmetic | | Body 5% |
| C9 | Triggered effects come only from the listed types, at severities inside their clamps (Pain [0.1,0.8], Shock [0.2,0.8], Dazed [0.2,0.8], Bleeding as listed); Dazed only on a blunt head hit | HitGen | trigger conditions, clamps | | Body 7% |
| C10 | Coverage: over 20k random hits, region frequencies match coverage within 2%, and destroyed regions are never picked | statistical | `RandomRegionByCoverage` | | Body 2% |
| C11 | Heal never lowers a condition, never exceeds 1, never revives a destroyed organ, and touches only the single worst region | BodyGen, amount 0-5 | the `Heal` ordering and filters | | Body 6% |
| C12 | Eat and Drink: Δstore + 7,700 ΔFat = kcal × (0.5 + 0.5 × digestion); absorption in [0.5, 1]; the 4,000 ml cap | BodyGen, kcal 0-6,000 | the `Eat` arithmetic | | Body 3% |
| S1 | Bounds (existing), extended to calories, hydration, energy, fat and muscle never negative and energy ≤ 960 | existing | | | BS 3% |
| S2 | Heat-balance identity: `DegreesFPerHour` = (M − S − E)/C exactly. S has the sign of skin − (air + fire). R ≥ 1/h_conv. R is nondecreasing in clo and waterproofing, nonincreasing in wetness and wind. `InsulationPct` ∈ [0, 0.99] | `BodyInputs`, ctx | arithmetic in the balance and resistance | the absolute calibration (C-golden: comfort 62.2 °F for the baseline furs) | BS 15% |
| S3 | Vasomotor: skin temperature is continuous and nondecreasing in the core; tone is 0 at ≤ 97, 0.5 at 98.6 and 1 at ≥ 99; the gap at the setpoint is 8.4 °F | core 85-105 | the switch constants | | BS 6% |
| S4 | Sweat: 0 below 99 °F, nondecreasing in the core, evaporated ≤ produced, evaporation nonincreasing in waterproofing and wetness, soaked = produced − evaporated ≥ 0 | core 95-106 | the `Sweat` arithmetic | | BS 6% |
| S5 | Metabolism is linear in the activity factor (M(k·a) = k·M(a)); Katch-McArdle exact at baseline (1,747.0); organ damage scales it within [0.7, 1] | BodyGen | the `MetabolismKcalPerDay` constants | | BS 4% |
| S6 | Starvation order: fat is burned only while there is fat above its floor, muscle only once fat is at its floor, and organ damage only once both are at their floors and the store is empty. Neither floor is crossed by starvation | fat 2-35%, muscle 10-45%, store 0-2,000 | the order and the floor constants | | BS 7% |
| S7 | Calorie conservation (in the working tree) | existing | | | BS 3% |
| S8 | Hungry, Thirsty and Tired severity = (0.3 − pct)/0.3 below 30%, absent above, exact | pct 0-1 | the threshold arithmetic | | BS 4% |
| S9 | Temperature effect severities: each exact at two cores and monotone in the core | core 80-106 | the effect constants | | BS 4% |
| S10 | `Tick` events: `EffectStarted` and `EffectEnded` are exactly the set difference; `exhausted` fires once at the crossing; `fully rested` once; `died` once with `MarkDead` | action sequences | `Tick` branches | | BS 5% |
| S11 | Wetness reference model (existing) | existing | | | BS 6% |

### 3.2 (A) items, after their fix

| # | Law | Catches | Score share |
|---|---|---|---|
| A1 | No healing channel outpaces lethal damage: for any core below 95 °F, hydration 0 or reserves gone, asleep or awake, a 24 h run's vital-organ condition is nonincreasing. Sleep heals only when the regeneration gates pass | a sleep heal that ignores the gates; rate mutants | BS 6% |
| A2 | Organ essentiality: destroying every provider of a capacity zeroes it (both lungs mean breathing 0, both eyes sight 0, brain consciousness 0, heart pumping 0). Any single organ's capacity is proportional to its condition: brain at 0.5 gives about 0.5. A bone at 0 makes its leg's moving 0 | the averaging | Body 6% |
| A2b | Monotonicity in tissue: lowering any one tissue's condition never raises any capacity | multiplier sign and operator mutants | Body 8% |
| A3 | Perception is nonincreasing in darkness, equals the light-source value at darkness 0, and is ≤ 0.5 + ε in full dark for a healthy body | the perception formula | Body 3% |
| A4 | A head hit can reach the brain: over N seeded deep hits, the brain is hit at least once, and the depth gate holds | the organ selection | Body 2% |
| A5 | No effect needs a treatment the game lacks: every `treatmentAboveSeverity` non-null kind has a registered treatment, so the floor is test-visible. Bloody fades to 0 | the data rule | Effects |
| A6 | Asserted levels read exactly: after `Update`, a level effect asserted this minute equals the step's computed level, so Shivering is present at 96.9 °F | the Add/Decay order | BS 2% |
| A7 | `WoundedBy` is cleared once a person mends; a death with no mortal wound is never credited to an attacker | `CauseOfDeath` | Body 2% |
| A8 | `LethalRates(body)` equals what the next minute's `Process` charges, for each process | either side drifting | BS 4% |
| A9 | Tissue and blood healing are both linear in nutrition (doubling calories within the gate doubles both) | the squared term | BS 1% |
| A10 | Felt air is continuous in air temperature at any wind (no step greater than 0.1 °F for a 0.01 °F change) | the 50 °F cliff | Exposure |
| A13 | Hypothermia damage is continuous and nondecreasing as the core falls | the jump at 87.5 °F | BS 2% |

### 3.3 (B) items, after the user decides

| # | Law, written as for the recommended answer | Blocked by | Score share |
|---|---|---|---|
| B1 | Shivering is a metabolic term in `HeatBalance`; the calorie burn covers it (extends S7: Σ heat produced = Σ kcal burned); no effect carries core heat outside the balance | B1 | BS 5% |
| B2 | The `HeatBalance` terms sum to the core change actually applied, including the buffer and wetness | B1, B2 | BS 3% |
| B3 | Healing timescale envelopes: a cut, a fracture and a half loss of blood each heal within the agreed band, asleep and awake | B3 | BS 3% |
| B4 | Lethal timelines: dry to dead in [x, y] h; no reserves to dead in [x, y] h; death from cold only below an agreed core. Deterministic once the rng is out of the target choice | B4 | BS 4% |
| B5 | `IsAlive` depends only on tissue and blood: adding any effect never kills. `CauseOfDeath` names the cause of the damage that crossed the line | B5 | Body 4% |
| B6 | Activity goldens: the balance air for each activity in the baseline furs | B6, B8 | BS 2% |
| B7 | The buffer fills only when a fire or the sun supplies heat; it never fills while the core alone is warming | B7 | BS 3% |
| B8 | `AirTempF` reaching the body holds no wind; wind enters only through convection and clothing | B8 | Exposure |
| B9-B11 | Scale laws: blood volume is proportional to weight; abilities are invariant to scaling a species' weight and its baseline together; any single blow from any species or weapon is in [0, 1.5] | B9, B10, B11 | Body 4% |
| B12 | Hungry penalty is monotone in reserves, and small while reserves are high | B12 | BS 1% |
| B13 | Frostbite is driven by felt air on the extremities and damages skin that heals | B13 | BS 2% |

**Expected totals.**

- **`Body.cs`.** About 66% from the C laws alone. About 90% once A2, A2b, A3, A4 and A7 are in.
  B5 and B9-B11 lift it to about 95%.
- **`BodySurvival.cs`.** About 63% from C. About 82% with A1, A6, A8, A9 and A13. About 90% or more
  with B1-B4 and B7.

**What stays hard to kill in `BodySurvival.cs`:**

- the drying-wind ternary already listed in `verification.md`;
- the rng organ picks in starvation and dehydration, which a deterministic B4 fix removes;
- the `Sleep`-only bedding-quality path.

---

## 4. Ordering

1. **Can be pinned now, independent of every decision:** C1-C12, S1-S5, S8-S11. Keep C4 and the
   S2 comfort golden out of `Body.Damage`, and out of anything B6 or B8 would retune.
2. **The (A) fixes, then their laws.** These three come first, because their tests already pin the
   bug and will have to change:
   - A2 changes `BodyTests.LostLungs_AreDilutedByAveraging`.
   - A3 changes `BodyTests.Perception_FullInDaylight_HearingCarriesItInTheDark`.
   - A1 changes `SurvivalTests.SleepHeals_FasterThanRegenerationAlone` and probably the NPC
     cold-bed tests.

   After those come A4-A13. A1's law (no healing outpaces lethal damage) also holds under any B3
   rate, so it can land before B3.
3. **Decisions that block tests:**
   - B4 (internal damage) blocks the B4 timelines and A11's comments. It changes
     `SurvivalTests.Dehydration_KillsWithinAnHourOrSo` and `Starvation_ThenMuscle_ThenOrgans`.
     Decide B4 before B3, because the healing rates are judged against the lethal rates.
   - B1 (shivering) and B2 (wetness) block the B2 sum law and the S7 heat extension. Decide them
     together.
   - B6 (activity), B8 (wind) and B7 (buffer) form one tuning pass. Every thermal golden, the
     `ConvectionTests`, `FireBodyTests` and the NPC warming-walk and cold-bed tests depend on them.
     Pin only the S2-S4 laws until that pass is done.
   - B5 (effects kill) blocks the B5 law, A7's second half, and every `CauseOfDeath` golden.
   - B9 (scale) blocks A4's depth gate, the combat goldens (`CombatGoldenTests`, `KillChainTests`)
     and B10 and B11. B10 and B11 can follow B9 directly.
   - B12, B13, B14, B15 and B16 are independent and low-risk. Each blocks only its own law.
4. **Suggested sequence:** pin (1); fix A1-A3 and A5-A10 and A13; decide B4, then B1 and B2, then
   B5; fix A4 and A11; decide B3; do the B6, B7 and B8 tuning pass; then B9-B11; then the rest.
   Gate `Body.cs` at 90% after step 2. Gate `BodySurvival.cs` at 90% after the tuning pass.
