# Companion reliability pass

Status: implementation completed in steps; validation results and remaining scenario/manual limits are documented in [companion-reliability-report.md](companion-reliability-report.md). This pass builds on the existing companion implementation and F01–F38 scenario catalogue; it does not replace them.

## Outcome

One to four companions should reliably travel with an actor without constant supervision. They may finish committed work, meet survival needs, decline requests, lose a trail, or leave a deteriorating relationship. Each separation should have an intelligible cause and a bounded outcome. Player-led and NPC-led travel use the same rules.

Success is not maximum time on the same tile. It is prompt pursuit when free to pursue, sensible self-care, evidence-based recovery, and honest communication when the agreement ends.

## What the current implementation tells us

- NPCs finish their current action; needs precede pursuit, and pursuit precedes optional work. These priorities are intentional and remain the baseline.
- Existing simulations count following minutes, colocated minutes, and lost agreements. They cannot explain separation. The earlier four-actor runs reported roughly 29–38% colocation, but autonomous survival runs do not establish whether companions can keep up with a traveling leader.
- Search expires after 90 minutes without a sighting, but expiry is evaluated inside `Following.Pursue`, which need handling can postpone. This mixes time away meeting needs with time actively searching.
- Route budget exhaustion and unreachable routes share failure handling. A waiting retry is returned as `BudgetExceeded`. Callers cannot clearly distinguish these situations.
- Relationships influence joining, sharing, and combat assistance, but there is no continued-willingness departure policy.
- Need requests wrap movement actions, offer only water or cooked meat, and apply a pressure memory on every request to stay. Pending requests can delay action selection even as conditions worsen; emergency handling needs an explicit contract.
- Player escape resolves the remaining fight before the player crosses a world edge. Escape destinations are the first available neighboring tile.
- Existing tests establish useful individual contracts, but do not exhaust the scenario catalogue or replace manual playtesting.

## Ownership and design constraints

| Owner | Responsibility during this pass |
| --- | --- |
| Following | Agreement lifecycle, observer-owned evidence, bounded pursuit/search results; no human relationship formula |
| NPC decision policy | Whether survival/work/pursuit executes next; explain the chosen priority |
| Companion interactions | Human willingness, requests, responses, cooldowns, incident consequences; no UI calls |
| Sight, tracks, navigation | Observable evidence, legal routes and crossings; never hidden target coordinates for decisions |
| Combat/world integration | Exclusive action ownership, timed escape, continued background combat and encounter cleanup |
| UI adapters | Render current valid requests and understandable state; no independent decision rules |
| Diagnostics | Observe decisions and transitions without changing RNG, simulation behavior, or save state |

Use small typed results at these boundaries where they clarify behavior. Avoid creating a second NPC scheduler or a general party subsystem. Following remains actor-neutral; human social policy belongs in the NPC layer. Diagnostic snapshots may inspect ground truth to measure separation, but that information must never enter pursuit decisions.

## Step 1 — Establish explainable baselines

Add an opt-in structured trace and summary report to the simulation harness. Record transitions rather than unrestricted per-minute prose. Each record identifies game minute, actor, followed actor, action and elapsed work, current need, evidence source/age, pursuit result, and agreement end reason.

Distinguish the primary execution reason from secondary constraints:

- committed work;
- local self-care or survival detour;
- waiting for a response or honoring a short stay;
- pursuing a sighting or trail;
- investigating a lead with no continuation;
- route retry delay, unreachable route, or exhausted computation budget;
- combat ownership or escape;
- reunited and available for optional work;
- agreement ended, with its reason.

These labels describe actual production decisions. Do not reconstruct decision causes from action names: a movement action can serve either thirst or pursuit.

Report separation episodes, durations by reason, time until pursuit starts after work/need completion, reunion time after the target stops, route attempts, reversal loops, request counts, and termination reasons. Keep deaths, ended agreements, and unreunited episodes in the report so early termination cannot improve the apparent success rate.

Acceptance: tracing on/off produces identical fixed-seed outcomes; repeated serial runs reproduce the same report. Save/load does not require diagnostics to resume correctly.

Commit: companion decision diagnostics and reproducible baseline reports.

## Step 2 — Add traveling-leader acceptance scenarios before behavior fixes

Extend `CompanionWorld` and the harness with leaders that move through real timed production crossings. Existing instantaneous arrangement helpers remain useful for isolated perception tests, but cannot establish end-to-end travel reliability. Controlled fixtures remove unrelated threats and provide deliberate needs/supplies; survival experiments retain production weather, terrain, costs, and depletion.

| Scenario | Required outcome |
| --- | --- |
| 1. Leader leaves during forage | Work settles once; pursuit starts at the next eligible decision boundary, ahead of optional work |
| 2. Leader stops several tiles ahead | Follower with sufficient evidence and no unmet need reunites within a bound derived from remaining work, actual crossing durations, and scheduler overhead |
| 3. Continuous travel, matched speeds | Measure stable lag under the fixture's conditions; no growing delay from optional chores |
| 4. Leader is faster | Follower may lose contact; no teleport, speed bonus, or guaranteed reunion |
| 5. Local water or food is available | Self-care completes without a needless detour or supply popup, then pursuit resumes |
| 6. Water requires a detour | Request when communication is possible, fetch independently, then attempt evidence-based reunion |
| 7. Long rest or warming stop | No repeated movement/rest thrash; stale evidence is handled explicitly when the actor can resume |
| 8. Full inventory and tempting nearby work | No unrelated camp-unload loop; necessary load handling is distinguished from optional gathering |
| 9. Leader reverses into sight | New observation replaces the stale lead at the next observation boundary |
| 10. Trail forks or includes own prints | Only local evidence influences choices; investigation and retry remain bounded |
| 11. Trail erodes or leader dies unseen | No hidden notification; eventual loss has a recorded reason |
| 12. Route becomes blocked | No illegal crossing; bounded retries and a meaningful termination result |
| 13. Pathfinder exhausts its budget | Distinguish computational uncertainty from proven no-route; retries remain bounded |
| 14. A follows B follows C | Each actor follows only its direct target's evidence; no shared leader coordinates |
| 15. Need becomes urgent during a pending request/stay | Self-preservation takes precedence; stale UI responses cannot restart waiting |
| 16. Two actors ask for the last available supply | Resolve against current inventories; no duplicate transfer or repeated incident consequence |
| 17. Relationship deteriorates | Warning/departure follows a stable policy; no invitation/departure oscillation |
| 18. Player or NPC escapes before allies | Timed world movement and the remaining fight progress together; later reunion uses real evidence |
| 19. Save/load mid-work, detour, search, or request | Preserve identity, progress, evidence, deadlines and once-only effects |
| 20. One to four companions, mixed needs | One follower's request or detour does not freeze the others; costs and outcomes are per actor |

For applicable cases, run both player and NPC targets. Retain non-human contract tests without implementing animal behavior. Map these cases back to F01–F38 and explicitly label automated, simulation-only, manual, or deferred coverage.

Acceptance: write regression tests before each associated production fix. Bounds come from fixture travel/action costs, not arbitrary assertions that every actor must reunite within a universal time limit. Record known failing cases before fixing them; completed commits leave their required tests passing.

Commit: timed leader harness and passing baseline scenarios; land each new failing regression with its subsequent fix in Steps 3–5.

## Step 3 — Repair pursuit and survival handoffs

Use Step 1 evidence to select fixes. Preserve completed-work semantics and urgent survival priority; do not globally shorten work, weaken needs, or extend sight to raise colocation.

Separate agreement status from current execution reason. A temporary detour retains the agreement but does not create fresh knowledge. Make search outcomes explicit enough to distinguish waiting, lead exhaustion, route failure, and search termination.

Proposed search policy: evidence continues aging during work/self-care; active search effort is budgeted when the actor can actually search. On resumption, investigate only remembered locations and still-readable trails. Bound active search and stale lead retention separately, so neither a long water trip causes an unexplained immediate abandonment nor repeated detours preserve an impossible pursuit forever. Choose initial limits from the baseline traces, document them centrally, and test the boundaries. This changes the current single 90-minute rule and deserves a dedicated regression.

Avoid repeatedly selecting optional work at a last-known position when the target is still missing. Clear or reset pursuit failure state only on meaningful new evidence or a relevant route change. Distinguish exhausted pathfinding budget from no-route and retry delay without introducing unbounded work.

Acceptance: scenarios 1–14 and 19 pass; no hidden-position leakage; actual survival emergencies remain higher priority. Every controlled stalled episode either recovers or reaches an explicit bounded outcome.

Commit(s): evidence/search lifecycle, then measured NPC scheduling fixes if needed.

## Step 4 — Complete consent and departure reliability

Keep the existing scalar opinion, personality, and memories. Add continued-willingness evaluation at bounded social decision points and after meaningful relationship changes. Use a lower stay threshold than join threshold, with a cooldown, to avoid oscillation. Low opinion triggers a human social decision, not a rule in generic pursuit.

Distinguish temporary self-care detours from ending the following agreement. When nearby and able to communicate, an NPC can request a short accommodation or announce a firm departure. Asking them to stay is a bounded request they can refuse. If out of communication range or in immediate danger, they may act without a popup; show only information the recipient could know.

Treat requests as incidents with an identity and a single resolution. Revalidate recipient, target, need, urgency, resources, and expiry on response. Emergencies cancel waiting immediately. Combat, separation, death, dismissal, and changed needs invalidate inappropriate requests. Saving must preserve deadlines and prevent rerolls or duplicate consequences.

Offer usable supplies through existing consumption eligibility rather than a hard-coded cooked-meat check. If warmth/rest lacks a valid transferable solution, offer only meaningful choices. A respectful request need not always damage opinion; record pressure when an actor is pushed to accept a costly delay or their refusal is challenged. Apply the chosen consequence once per incident and keep the rule inspectable.

Expose concise following status and relevant opinion/memory changes through the existing NPC UI. Do not add party orders or a relationship dashboard.

Acceptance: scenarios 5–7 and 15–17/19 pass; no indefinite waiting, repeated penalties, gift farming, or immediate voluntary rejoining after an explicit departure. Player and NPC recipients use the same consent rules.

Commit(s): reliable need incident lifecycle, then relationship-based departure and UI explanations.

## Step 5 — Make escape continuous with the world

After tactical escape, transfer the remaining encounter to background ownership exactly once. Begin the escaping actor's timed world travel while the same world clock advances remaining combat, needs, and other followers. Apply aftermath exactly once when the encounter ends; late-arrival eligibility must not pull the escaped actor immediately back into the same fight.

Use a shared escape destination policy for players and NPCs: legal reachable neighbor, evaluated using perceived threat direction and known traversal cost, with deterministic ties. Do not consult hidden enemy positions or force all companions onto the same exit. Keep following evidence consistent with completed physical crossings.

Acceptance: scenario 18 plus combat ownership/checkpoint regressions pass. A long remaining fight does not keep an escaped actor stationary; each world minute and crossing is charged once; NPC-initiated fights work without a player. Preserve the existing restriction on saves during an interactive combat turn unless this change makes a further checkpoint strictly necessary.

Commit: concurrent escape travel and background encounter continuity.

## Step 6 — Validate the experience and publish the evidence

Run the focused companion suite after each change, relevant subsystem tests at each seam, and the full suite before completing the pass. Verify the committed changes independently from unrelated workspace edits where feasible.

Use a fixed published seed list (initially 1–10), serial within each simulation process. Compare before/after for one leader with 1, 2, and 4 followers; include solo survival as a control and an 8-follower smoke run only for bounded execution. Keep controlled travel trials separate from two-day autonomous survival trials. Report distributions and worst episodes, not only averages. These samples reveal regressions; they do not establish overall game balance.

Manual play pass: invite and travel, leave someone foraging, wait for reunion, help with thirst, refuse/accept a delay, experience a departure, enter combat with a late arrival, retreat, and save/load at supported checkpoints. Check message timing and explainability as well as final actor positions.

Completion gates:

1. All controlled acceptance scenarios pass or have an explicit agreed deferral; no hidden-target access, resource duplication, illegal crossing, or double action ownership.
2. An eligible follower starts pursuit at the next decision boundary after committed work/self-care; reunion in reachable controlled fixtures meets the derived timing bound.
3. Every reported prolonged separation has a recorded cause and recovery/termination path; bounded searches and retry counts are verified.
4. No request blocks emergency self-care, rerolls after load, or applies a consequence twice.
5. Relationship departures and combat escapes satisfy their lifecycle tests and are understandable in the UI.
6. Survival changes and runtime costs are reported against the baseline; any regression is investigated rather than hidden by a better colocation percentage.
7. Scenario coverage and remaining limitations are updated in the implementation documentation.

Commit: validation reports, coverage map, and any final independently tested fixes.

## Integration constraints and deferred work

At planning time, the workspace contains unrelated event/predator changes touching `GameContext`, `CombatAftermath`, `SaveManager`, travel runners, and test UI support. Re-read their current state before implementation; preserve those edits and stage only this pass's changes. Continue the requested stepwise commits to `main` during implementation.

Do not expand this pass into A*, gear lending, dog AI, factions, party-size limits, a full relationship simulation, or general encounter/save-system redesign. Performance findings may justify a follow-up, but the reliability work should first improve the existing interfaces and demonstrate their behavior.
