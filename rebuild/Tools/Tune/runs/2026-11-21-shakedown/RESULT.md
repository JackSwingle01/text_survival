# Combat tuning run

Command: `dotnet run -c Release --project rebuild/Tools/Tune -- combat --budget 1950 --seed 1 --out /Users/jackswingle/Documents/GitHub/text_survival/rebuild/Tools/Tune/runs/2026-11-21-shakedown`

Seed 1, budget 1950 evaluations (150 generations of 13), 300 fights per scenario per candidate, wall time 25.1 min.

Fitness on 1000 report fights per scenario: **1.838 before, 0.408 after** (best candidate seen).

Bands and penalties met after: 10 of 13.
Missed: Careful spear hunt on caribou: meat; Quick approach on caribou: meat; Armed vs a lone wolf: win or drive off.
Not scored (today's rules cannot express them): Bear, stand and intimidate: bear leaves; Bear, stand and intimidate: deaths.

## Bands

| Band | Scenario | Target | Before | After | Distance before | Distance after |
|---|---|---|---|---|---|---|
| Careful spear hunt on caribou: meat | caribou_careful_spear | 50%-70% bKilled | 53.4% | 47.2% | 0 | 0.028 |
| Quick approach on caribou: meat | caribou_quick_spear | <= 20% bKilled | 53.2% | 39.8% | 0.332 | 0.198 |
| Armed vs a lone wolf: win or drive off | wolf_spear | 70%-85% aWinsOrDrivesOff | 100% | 99.6% | 0.15 | 0.146 |
| Armed vs a lone wolf: deaths | wolf_spear | <= 5% aDies | 0% | 0.2% | 0 | 0 |
| Armed vs a lone wolf: rounds | wolf_spear | 3-8 rounds | 8.33 | 7.682 | 0.033 | 0 |
| Unarmed vs a lone wolf: deaths | wolf_hands | 25%-35% aDies | 97.2% | 26.9% | 0.622 | 0 |
| Pack of 3 in the open, armed: deaths | pack3_spear | 45%-55% aDies | 41.8% | 51.4% | 0.032 | 0 |
| Bear, stand and intimidate: bear leaves (not scored) | bear_stand | 45%-55% bLeaves | 0% | 0% | 0.45 | 0.45 |
| Bear, stand and intimidate: deaths (not scored) | bear_stand | 10%-20% aDies | 100% | 100% | 0.8 | 0.8 |
| Bear, attack: deaths | bear_attack | 35%-50% aDies | 0.4% | 48.5% | 0.346 | 0 |
| Bear, run: deaths | bear_run | 50%-65% aDies | 27.6% | 53.9% | 0.224 | 0 |
| penalty: A wolf killed by caribou | wolves_vs_caribou | <= 2% aDies | 0% | 0% | 0 | 0 |
| penalty: A bear lost to one unarmed person | bear_hands | <= 2% bKilled | 0.5% | 0.4% | 0 | 0 |
| penalty: A wolf lost to one unarmed person | wolf_hands | <= 5% bKilled | 2.7% | 4.1% | 0 | 0 |
| penalty: A person killed by the caribou they stalk | caribou_careful_spear | <= 1% aDies | 0% | 0% | 0 | 0 |
| order: Weapon order against a lone wolf | | wolf_spear > wolf_axe > wolf_knife > wolf_hands on aWinsOrDrivesOff | 100% > 100% > 100% > 2.8% | 99.6% > 99.6% > 86.9% > 73% | 0.04 | 0.02 |
| order: Weapon order on a careful caribou hunt | | caribou_careful_spear > caribou_careful_axe > caribou_careful_knife on bKilled | 53.4% > 52.5% > 55.3% | 47.2% > 45.7% > 44.8% | 0.059 | 0.016 |

## Parameters

| Parameter | Before | After | Range |
|---|---|---|---|
| human.attackDamage | 0.15 | 0.298 | 0.02-1.5 log |
| human.threat | 1 | 0.944 | 0.2-4 log |
| human.moveAllowanceM | 3 | 6 (at the edge) | 2-6 |
| wolf.attackDamage | 0.45 | 0.688 | 0.05-3 log |
| wolf.threat | 0.7 | 0.133 | 0.1-4 log |
| wolf.boldness | 1.3 | 0.404 | 0.2-4 log |
| wolf.aggression | 0.8 | 0.984 (at the edge) | 0.05-1 |
| wolf.cohesion | 2 | 1.061 | 0.1-4 log |
| wolf.moveAllowanceM | 6 | 8 | 2-10 |
| bear.attackDamage | 0.6 | 3.937 (at the edge) | 0.05-4 log |
| bear.threat | 2.5 | 6 (at the edge) | 0.3-6 log |
| bear.boldness | 1.6 | 3.396 | 0.2-5 log |
| bear.aggression | 0.4 | 0.388 | 0.05-1 |
| bear.moveAllowanceM | 5 | 8 | 2-10 |
| caribou.threat | 0.3 | 0.209 | 0.05-3 log |
| caribou.moveAllowanceM | 3 | 2 (at the edge) | 2-10 |
| spear.damage | 8 | 15 (at the edge) | 0.1-15 log |
| axe.damage | 12 | 2.542 | 0.1-15 log |
| knife.damage | 6 | 0.177 | 0.1-15 log |
| sightings.resting.0 | 0.9 | 2.945 (at the edge) | 0.2-3 log |
| sightings.resting.1 | 0.85 | 2.727 | 0.2-3 log |

## Applying it

Nothing here changes a content file. `best.overrides.json` is the chosen parameters as content overrides; check it with
`dotnet run -c Release --project rebuild/Tools/Tune -- eval --overrides /Users/jackswingle/Documents/GitHub/text_survival/rebuild/Tools/Tune/runs/2026-11-21-shakedown/best.overrides.json`,
then copy the values into the tables by hand, in a reviewed change. Files: `outcomes.md` (every metric, before and after), `convergence.tsv`.
