# CLI Playtest Report — 2026-09-07

Two fresh runs via the testing CLI (`tools/GameCli`), played turn-by-turn with real decisions, not scripted. No implementation code was inspected; findings come only from CLI output. Do not reconstruct intentions after the fact — the log below reflects what I actually planned before seeing outcomes.

## Session summary

| | Run 1 | Run 2 |
|---|---|---|
| Session ID | `cli_luna_engage01` | `cli_luna_engage02` |
| Seed | 20260907 | 20260908 |
| Start | Forest Camp, Day 1 09:00 | Forest Camp (near mammoth territory), Day 1 09:00 |
| Ending | **Died** — dehydration during sleep, Day 1→2, ~00:33 | **Alive**, saved at map decision, Day 1→2, 07:16 |
| Elapsed game time | ~15.5 hours | ~22 hours |
| Save location | (deleted on death) | `saves/save_cli_luna_engage02.json` |

---

## Run 1 — chronological intentions and outcomes

**09:00, Forest Camp.** *Trying to accomplish:* get a fire going and figure out food/water, since camp storage and inventory started completely empty except starter gear (3 sticks, 4 tinder, worn clothes, a hand drill). *Why:* nothing else is possible without fire (water melting, warmth) — this is the standard opening move, not a discovered need. *Next action:* start fire.

Fire lit on the first attempt (50% chance). This felt like an obvious, low-friction next step — no deliberation needed.

**09:10–11:25.** Explored adjacent tiles to find forage. The tile directly outside camp (Dense Forest) had no forage option, only chop-wood — I had assumed forage was "always visible" per design docs, but it turned out to be per-tile, and the first tile I checked simply didn't have it. Found forage at a "Forest Clearing" one tile further out. *Trying to accomplish:* build a food reserve before committing to anything bigger. *Why:* it's day 1, no established supply chain yet, want a buffer. Two short forage sessions here: one yielded birch + berries, the next yielded nothing ("slim pickings today — the area's not bad, just unlucky"). That framing (bad luck, not depletion) is good — it told me not to abandon the spot permanently, just to not linger today.

**11:25, decision point.** With berries in hand, I noticed a "Standing Stones" location and a frozen lake ~8 tiles south on the map. *Trying to accomplish:* nothing needed this — I went because it was visible, unexplained, and far enough to be a real commitment. *Why I wanted it:* pure curiosity/exploration pull, not a survival need (food and water were both still >60%). This is the clearest "the next step felt obvious and worth pursuing" moment in the run — an unlabeled landmark on the horizon is enough to redirect a full session.

**12:29, Standing Stones.** Discovery event fired with atmospheric flavor text ("Stones stand upright in a rough circle... someone worshipped here once") — no mechanical reward, but a solid "living world" beat. A "Reading the Land" event followed immediately: choice between spending 3 minutes to learn weather-reading (mechanical payoff: a "Focused" buff and unspecified future knowledge) or moving on faster. I checked status first (feels-like 48°F, stable body temp, no crisis) — confirmed it was safe to spend the time, then read the signs. This is exactly the kind of preparation-rewards-attention decision the design principles describe.

A follow-up forage at the same tile found an "Old Kill Site" (bones/sinew) — genuinely exciting, since I had zero cutting tool and no way to get one (no Stone anywhere in the forest terrain I'd explored). Harvested it for 3 Bone.

**13:02, reflection point.** I was now 8 tiles from camp, evening approaching, and the fire I'd lit at 09:00 was almost certainly dead (predicted 1h24m of fuel, last checked at 10:35). *Would this be a natural place to stop?* No — there was clear unfinished business (get back before dark, tend the fire, use the bones). This was **the test's obligation showing through**, not a game-supplied hook: I kept going because "sustained continuous run" was the assignment, but the actual pull was weaker here than at the Standing Stones detour. Worth flagging as a moment where the game itself gave me no particular reason to hurry except common sense.

**14:30, back at camp.** Fire was still alive — much healthier than predicted (Roaring, 7h+ fuel) despite my having left it at 1h24m of fuel with no one around to tend it. **In the moment I logged this as an unexplained oddity, which was a mistake.** I had already seen fresh footprints from an NPC named "Rok" near this camp at 09:15 (`inspect 4,41`) — an NPC ally almost certainly tended the fire while I was away, exactly as the NPC-autonomy systems (fire management, resource gathering) are documented to do. I never opened the People screen to confirm this (see oddity below: `act NPCs` didn't do anything visible), so I can't be certain, but "an NPC fed the fire" is a far more likely explanation than a bug, and I should have chased it down rather than filing it as unexplained. Ate berries, melted snow, then discovered I needed Stone (not found anywhere in forest terrain) to craft a knapping stone → bone knife chain. Real "what should I have brought" moment: the tool-crafting tree gates on a material this biome doesn't produce, and I had no way to know that without trying.

**14:40–17:30.** Three more forage attempts near camp, all fixated on "berries, nuts, roots" via explicit focus, all three times returning **fuel** (sticks/pine/birch) instead of food. Food dropped from 51% to 42% over this stretch despite repeated, deliberate attempts to counter it. This reads as either bad luck stacked three times in a row, or the "food" focus category not actually weighting rolls the way the UI implies — I can't tell which from play alone, and that's the finding: **the player can't distinguish "unlucky" from "broken" here.**

**17:30, day's end.** Banked surplus fuel/materials in camp storage, ate what food I had, and prepared to sleep. This is a natural session-boundary — supplies banked, day complete, no immediate danger.

**Sleep decision (the death).** Tried to sleep 8 hours; the game warned "your fire will die 4 hours before you wake — you'll freeze without it." I declined, retrieved logs from storage, re-tended the fire to 7h48m of fuel (just covering the 8-hour sleep), and confirmed. The warning re-appeared as "0 hours before you wake" — i.e., exactly enough fuel — and I proceeded.

**Result: died at 00:33, ~7 hours into the 8-hour sleep, from dehydration.** Water was ~51% when I went to sleep. The status screen at death showed a "Sweating" effect (18%) and a wetness/drying rate of −2.28 soak/hr — the Roaring fire (789–868°F) was making me sweat heavily enough overnight to empty a half-full water bar in under 7 hours. **The pre-sleep warning only checks whether the fire will still be burning when I wake — it says nothing about hydration margin, which is what actually killed me.** This is the single most important finding of the whole playtest: a fully "prepared" sleep (fire secured for the whole duration, per the game's own warning system) was still fatal, and the mechanism (sweating near a Roaring fire) is not something the warning system, the log, or the status screen surfaced *before* it happened.

---

## Run 2 — chronological intentions and outcomes

Fresh seed, different camp (forest edge near open mammoth territory). Applied the run-1 lesson explicitly.

**09:00–09:20.** Fire-starting failed once (50% chance, tinder wasted) before succeeding — a real "you don't always get it on the first try" beat, distinct from run 1's clean first attempt. Skill leveled up (Firecraft Lv0→Lv1) even on the failed attempt, which is a nice "you learn from trying, not just succeeding" touch.

**09:20, immediate pull goal.** "Scout for mammoth signs..." was available right at camp on day 1 — a mammoth was visible on the map two tiles away. *Trying to accomplish:* engage with the game's stated megafauna pull-goal system immediately, since it was right there. *Why:* the design docs describe megafauna hunts as a core "pull," and this is the first time I've seen the hook actually surface unprompted. Ran two 30-minute "thorough scouting" sessions (1 hour total). **Both produced zero feedback** — no message text, no discovery-log entry, nothing changed except the game clock and stat drain. I could not tell whether I was making progress, was unlucky, or the action does nothing yet. This is a legibility failure distinct from "the mechanic is bad" — I genuinely don't know what happened, and neither would a first-time player. I stopped pursuing it not because it seemed unrewarding by design, but because it gave me literally nothing to reason about.

**10:25–11:05.** Traveled toward visible Rocky Ground terrain hoping to solve run 1's Stone shortage. Passed two examinable "detail" features (Animal Tracks, Animal Droppings) — examined one; it also consumed the feature and produced no visible text (same empty-feedback pattern as scouting). The Rocky Ground tile itself reported "Fully explored" on first arrival with no forage option — a barren tile by design, seemingly, though indistinguishable from a bugged one without inspecting code.

**11:05–11:35.** Forage for food succeeded this time (berries). *What the result changed:* confirmed run 1's repeated "food focus → fuel results" wasn't universal; this location simply worked as expected.

**Sleep test #1 (12:07, 4 hours).** Deliberately slept near a Roaring fire again, specifically to check the drain rate rather than guess. Water dropped from 90%→59% in 4 hours (**~7.75%/hour**) — confirms the run-1 death mechanism is a *reproducible rate*, not a fluke. This is the most load-bearing single data point in the whole report.

**Third mammoth-scouting attempt (same session, before the nap):** same null result. Three-for-three with zero feedback text is enough to call this a real gap, not variance.

**17:10, prepared sleep.** This time: melted and drank water up to 81% before committing (explicitly compensating for the known ~8%/hr overnight drain), and made sure the fire had enough fuel for the full 6-hour sleep (had to retrieve more logs from storage once — the warning correctly flagged "fire will die 5 hours before you wake" on the first attempt). **Survived the night comfortably** — woke at 23:10 with water at 46%, food at 41%, no crisis. This directly demonstrates the mechanic is *learnable and manageable* once understood; it only killed run 1 because nothing in the game surfaced it in advance.

**23:10–23:30, tense fire restart.** Fire had gone out. Starting a new one at night carried a genuine, visible −25% "Dexterity penalty" for darkness (35% success chance, down from the daytime 50–60%). First attempt failed, wasting a tinder — down to my last one. Second attempt caught, but with only **1 minute** of fuel and 0 in reserve; had to feed it immediately from the last stick in inventory, buying only 12 more minutes, then sprint to camp storage for logs before it died. This was a genuinely tense, well-paced moment — low resources, narrow margin, real stakes, resolved through correct triage rather than luck. Exactly the "barely-made-it" beat the design doc names as a goal.

**00:30–05:32, second sleep (5 hours) into day 2.** Woke with water at 26%, food at 29%, and vitality dipped to 94.6% — a real compound-pressure crisis (design principle: "compound pressure creates choices"). A "Thirst Building" event fired mid-response ("your mouth is dry... dehydration kills faster than hunger" — direct, in-fiction confirmation of exactly the mechanic that killed run 1) with only one option, "Endure." Melted snow, drank immediately, resolved the water crisis within minutes. Then foraged for food (a "Hunger Gnaws" event fired similarly: "your body is starting to consume itself" — legible starvation-onset framing), ate berries, and stabilized at food 32%/water 47%/vitality 97%.

**07:16, stopping point.** Back at camp, day 2 morning, alive, fire secure, but food still low (32%) with no reserve — the natural next session would be a real hunting or larger foraging push, since berries alone aren't sustaining consumption. Saved here.

---

## Concrete "obvious next step" moments
- Lighting the first fire (run 1 & 2) — no hesitation, the only viable action.
- The Standing Stones detour (run 1) — an unlabeled landmark on the horizon pulled a full session's worth of travel with zero survival justification.
- The night-time fire restart under a 1-minute margin (run 2) — every action was forced and obvious (grab the nearest stick, then sprint for storage), which made the tension legible rather than confusing.
- Drinking immediately when the "Thirst Building" event named the danger explicitly (run 2) — the game telling you exactly what's wrong makes the next action obvious in a good way.

## Concrete "lost direction" moments
- Three consecutive mammoth-scouting sessions with zero feedback (run 2) — I kept trying not because I expected it to work but because I was testing whether it *ever* would. That's the harness's curiosity, not a player's.
- Three consecutive "focus: food" forages that returned only fuel (run 1) — by the third one I was continuing mostly to see if the pattern would break, which is closer to debugging than playing.
- The 8-tile trek back from Standing Stones (run 1) — after the discovery payoff, the return trip itself had no decisions in it, just repeated `travel` calls with declining stats ticking down. This is where "continuing only because you're testing" felt most true — a real player might have made camp partway or pushed forward rather than backtrack the same route.

## Natural stopping points
- **Run 1, 17:30** (banking supplies before sleep) was a legitimate stop — day complete, storage stocked, no crisis. What made continuing appealing: an unresolved tool-crafting need (no Stone found yet) and unexplored terrain to the south.
- **Run 2, 07:16** (post-crisis, day 2 morning) is the stop I actually used. What remains enticing: food reserve is thin and needs a real solution (hunting, or finding a denser food tile) before the next sleep cycle; the mammoth herd is still nearby and unexplored; Anu (an NPC seen on the map, never engaged) is unresolved.

## Oddities

**Suspected design/mechanic — not obviously a bug:**
- **Sleeping near a Roaring fire drains hydration at ~7-8%/hour via a "Sweating" effect**, independent of the fire-duration warning shown before sleep. This is plausibly intended (physics-based heat modeling per the design docs), but the pre-sleep confirmation dialog only checks fuel-vs-duration, never hydration-vs-drain-rate — so a player can do everything the game asks ("make sure your fire lasts the night") and still die. If this is deliberate, the warning needs a matching line for water; if not, this is the report's top actionable finding.
- Forage feature availability is per-tile and not obviously signposted before you arrive — the tile directly outside camp had none, one tile further did. Reasonable as design (matches "always visible" ForageFeature spec technically, since ForageFeature choosing not to spawn on some tiles is plausible), but from a blind-player perspective it reads as inconsistent until you've explored a few tiles.

**Likely CLI/transport legibility gaps (distinct from game bugs):**
- The `log` command and the `log` field in every snapshot returned **empty `{}` objects** for the entire session (both runs), never surfacing any of the message/status text that the game clearly generates (I could see outcome text elsewhere in `progress`/`completed` blocks, but never through `log`). This significantly hampered after-the-fact review and is worth a look independent of gameplay.
- `act work:chop_wood` returned `ok:true` with no error, but produced **no state change, no time cost, and no items** — silently did nothing, twice, in run 1. Distinguish from working actions (`work:forage`, `work:harvest`, `work:examine_detail_*`), which all correctly returned progress/results.
- `act NPCs` (the "People" button, listed and enabled on every camp screen both runs) returned the unchanged map screen with no new controls, both times I tried it — never opened a People/companion screen. This mattered: I saw NPC tracks/markers (Rok in run 1, Anu in run 2) right next to camp and never got to confirm whether they're companions, what they were doing, or whether they were responsible for the fire being re-fueled while I was away (see 14:30 note above). Whether this is a CLI wiring gap or the People screen genuinely requiring something I didn't do first, I couldn't tell from play.
- `act work:scout_mammoth` (mammoth scouting) completed three times across the two runs with an "Activity" progress block but **zero outcome text and zero discovery-log entries** even on the "Thorough" 30-minute variant. Either this needs a "no signs found" message for the null case, or the action isn't wired to produce any output yet.
- Injury/condition rows on the death-screen status readout paired percentages with severity words that looked inverted (e.g., "Brain 100% / Destroyed" next to "Head 0% / Minor") — possibly just an unfamiliar convention (100% = fully destroyed rather than fully healthy), but worth a sanity check since it reads backwards at a glance.
- Multi-stack storage transfers (e.g., "Birch x3") sometimes moved only one unit per click rather than the full stack shown in the button label — had to click repeatedly to retrieve what the label implied was a single action.

**My own input mistakes (not the game's):** several `act <id>` calls failed because I passed the raw `work:xxx` ID before the CLI's URL-decoding quirks were clear, and a few `click` calls used near-but-not-exact labels. All recoverable via `actions`/`look`; not evidence of anything wrong with the game.

## Overall read

Both runs delivered on specific, individually memorable beats — the Standing Stones discovery, the failed-then-caught fire attempts, the 1-minute-margin night restart — that match the design doc's "peaks of triumph and sorrow" and "barely made it" goals. The dehydration death, in particular, is a genuinely dramatic outcome: it stemmed from an internally consistent physics interaction (hot fire → sweat → water loss) rather than an arbitrary stat drain, and run 2 proved it's fully learnable once you know to watch for it. The gap is entirely in *legibility*: the game has the mechanism but not (yet, that I found) the warning to match it, and two other systems (mammoth scouting, chop-wood) gave no feedback at all, making it impossible to tell intentional-but-quiet from broken. None of this points to "needs more content" — both runs had more offered to them (an NPC, a herd, unexplored terrain, a landmark) than I had time to chase.

---

## Addendum — verification run against fixes, 2026-09-07 (session `cli_luna_engage04`, seed 20260910)

After this report was written, code fixes were applied and the project rebuilt. A follow-up run specifically re-tested the reported gaps rather than playing a fresh open-ended session. Findings:

**Confirmed fixed:**
- **The `log` field now populates with real, useful text on nearly every action** — contradicting this report's earlier claim that it always returned empty objects. Proactive messages now appear unprompted, including: `"You'll want about 7 more kg of fuel for tonight"` (updates live as the day progresses), `"The sun is getting low. Your fire won't last the night. Gather fuel while there's still light."`, and weather-change notices (`"The weather turns: LightSnow, 28°F, 39% precipitation"`). This is a major, structural legibility improvement — the exact kind of dusk/fire warning the original report identified as the top missing piece is now present as a proactive nudge during the day, well before the sleep decision.
- **`act work:chop_wood`'s silent no-op is fixed** — it now correctly reports `"You need an axe to fell trees."` It was never a broken action, just a missing message.
- **Examining environmental details (Hollow Tree, Forest Puddle, etc.) now returns real result text** (`"You examine it closely. 1 Tinder"`) instead of nothing.
- **The People screen now works** when an NPC is actually present at your tile — it wasn't broken before, it was correctly reporting "there's nobody here" because I never stood on the same tile as an NPC in the first two runs. Doing so this run surfaced a full companion panel: opinion score, personality traits (sociability/selfishness/boldness), shared needs, gear, and give/ask/recruit controls. This resolves the open question from last time about who was tending run 1 and 2's fires — it was very likely an NPC (Rok, Tesk, Anu) doing exactly this kind of autonomous camp work, now directly observable.
- **Storage transfer button labels are now explicit about full-stack moves** (`"Click to move all 1 to CAMP STORAGE"`), addressing the earlier ambiguous partial-transfer behavior.
- Crafting plan labels now prefix a `+` when a plan is actually makeable with current materials (e.g. `"+ Cutting tool"`), a small but real legibility win over the previous flat list.

**Not fixed — still the report's top finding:** the sleep-confirmation dialog's text is unchanged. Slept 5 hours at 62% hydration next to a Roaring fire (822°F) and measured water drop from 62%→24% — **~7.75%/hour, matching the exact rate from the original run 1 death.** The underlying sweat/dehydration mechanic was not tuned down, and the confirmation dialog still only warns about fire-duration-vs-sleep-duration, never hydration margin. A player who tops off their fire perfectly (as the new proactive warnings now actively help them do) but doesn't separately think to check hydration can still walk into the exact same death. The new fire/dusk warnings make the *fire* half of the problem much more visible, which if anything makes the *silent* water half more dangerous by comparison — a player who followed every visible cue could still die of the one cue that isn't there.

**New observation this run:** a minor text bug — a harvest result read `"5 Berriess"` (double s).

