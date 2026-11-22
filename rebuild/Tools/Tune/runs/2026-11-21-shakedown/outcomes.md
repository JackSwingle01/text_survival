# Outcomes, before and after

1000 fights per scenario on report seeds.

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

## Scenarios

### caribou_careful_spear

| Metric | Before | After |
|---|---|---|
| aDies | 0% | 0% |
| bKilled | 53.4% | 47.2% |
| aFled | 0% | 5.5% |
| bFled | 89.9% | 82.1% |
| aWinsOrDrivesOff | 100% | 90.1% |
| bLeaves | 46.6% | 43.4% |
| rounds | 80.344 | 63.753 |
| endings | AnimalFled 899, Victory 101 | AnimalFled 821, Fled 55, Unfinished 44, Victory 80 |

### caribou_careful_axe

| Metric | Before | After |
|---|---|---|
| aDies | 0% | 0% |
| bKilled | 52.5% | 45.7% |
| aFled | 0% | 4.6% |
| bFled | 92.3% | 85% |
| aWinsOrDrivesOff | 100% | 91.8% |
| bLeaves | 47.5% | 46.6% |
| rounds | 79.434 | 60.348 |
| endings | AnimalFled 923, Victory 77 | AnimalFled 850, Fled 46, Unfinished 36, Victory 68 |

### caribou_careful_knife

| Metric | Before | After |
|---|---|---|
| aDies | 0% | 0% |
| bKilled | 55.3% | 44.8% |
| aFled | 0% | 3.7% |
| bFled | 91.1% | 86.2% |
| aWinsOrDrivesOff | 100% | 92.8% |
| bLeaves | 44.7% | 48% |
| rounds | 78.81 | 71.75 |
| endings | AnimalFled 911, Victory 89 | AnimalFled 862, Fled 37, Unfinished 35, Victory 66 |

### caribou_quick_spear

| Metric | Before | After |
|---|---|---|
| aDies | 0% | 0% |
| bKilled | 53.2% | 39.8% |
| aFled | 0% | 26.8% |
| bFled | 91.7% | 64.5% |
| aWinsOrDrivesOff | 100% | 69.4% |
| bLeaves | 46.8% | 31.2% |
| rounds | 16.347 | 20.406 |
| endings | AnimalFled 917, Victory 83 | AnimalFled 645, Fled 268, Unfinished 38, Victory 49 |

### wolf_spear

| Metric | Before | After |
|---|---|---|
| aDies | 0% | 0.2% |
| bKilled | 51.8% | 70% |
| aFled | 0% | 0.2% |
| bFled | 48.2% | 29.6% |
| aWinsOrDrivesOff | 100% | 99.6% |
| bLeaves | 48.2% | 29.6% |
| rounds | 8.33 | 7.682 |
| endings | AnimalFled 482, Victory 518 | AnimalFled 296, Defeat 2, Fled 2, Victory 700 |

### wolf_axe

| Metric | Before | After |
|---|---|---|
| aDies | 0% | 0.2% |
| bKilled | 53% | 68.3% |
| aFled | 0% | 0.2% |
| bFled | 47% | 31.3% |
| aWinsOrDrivesOff | 100% | 99.6% |
| bLeaves | 47% | 31.3% |
| rounds | 8.138 | 7.57 |
| endings | AnimalFled 470, Victory 530 | AnimalFled 313, Defeat 2, Fled 2, Victory 683 |

### wolf_knife

| Metric | Before | After |
|---|---|---|
| aDies | 0% | 13% |
| bKilled | 55.5% | 0.1% |
| aFled | 0% | 0.1% |
| bFled | 44.5% | 86.8% |
| aWinsOrDrivesOff | 100% | 86.9% |
| bLeaves | 44.5% | 86.8% |
| rounds | 8.193 | 9.556 |
| endings | AnimalFled 445, Victory 555 | AnimalFled 868, Defeat 130, Fled 1, Victory 1 |

### wolf_hands

| Metric | Before | After |
|---|---|---|
| aDies | 97.2% | 26.9% |
| bKilled | 2.7% | 4.1% |
| aFled | 0% | 0.1% |
| bFled | 0.1% | 68.9% |
| aWinsOrDrivesOff | 2.8% | 73% |
| bLeaves | 0.1% | 68.9% |
| rounds | 15.356 | 8.701 |
| endings | AnimalFled 1, Defeat 972, Victory 27 | AnimalFled 689, Defeat 269, Fled 1, Victory 41 |

### pack3_spear

| Metric | Before | After |
|---|---|---|
| aDies | 41.8% | 51.4% |
| bKilled | 73.5% | 54.6% |
| aFled | 0% | 0% |
| bFled | 54.3% | 47.3% |
| aWinsOrDrivesOff | 58.2% | 48.6% |
| bLeaves | 0% | 1.5% |
| rounds | 10.428 | 8.332 |
| endings | AnimalFled 543, Defeat 418, Victory 39 | AnimalFled 473, Defeat 514, Victory 13 |

### bear_stand

| Metric | Before | After |
|---|---|---|
| aDies | 100% | 100% |
| bKilled | 0% | 0% |
| aFled | 0% | 0% |
| bFled | 0% | 0% |
| aWinsOrDrivesOff | 0% | 0% |
| bLeaves | 0% | 0% |
| rounds | 24.689 | 5.881 |
| endings | Defeat 1000 | Defeat 1000 |

### bear_attack

| Metric | Before | After |
|---|---|---|
| aDies | 0.4% | 48.5% |
| bKilled | 64.4% | 51.5% |
| aFled | 0% | 0% |
| bFled | 35.2% | 0% |
| aWinsOrDrivesOff | 99.6% | 51.5% |
| bLeaves | 35.2% | 0% |
| rounds | 8.062 | 3.484 |
| endings | AnimalFled 352, Defeat 4, Victory 644 | Defeat 485, Victory 515 |

### bear_run

| Metric | Before | After |
|---|---|---|
| aDies | 27.6% | 53.9% |
| bKilled | 0% | 0% |
| aFled | 72.4% | 46.1% |
| bFled | 0% | 0% |
| aWinsOrDrivesOff | 0% | 0% |
| bLeaves | 0% | 0% |
| rounds | 8.378 | 4.212 |
| endings | Defeat 276, Fled 724 | Defeat 539, Fled 461 |

### bear_hands

| Metric | Before | After |
|---|---|---|
| aDies | 99.5% | 99.6% |
| bKilled | 0.5% | 0.4% |
| aFled | 0% | 0% |
| bFled | 0% | 0% |
| aWinsOrDrivesOff | 0.5% | 0.4% |
| bLeaves | 0% | 0% |
| rounds | 10.48 | 3.763 |
| endings | Defeat 995, Victory 5 | Defeat 996, Victory 4 |

### wolves_vs_caribou

| Metric | Before | After |
|---|---|---|
| aDies | 0% | 0% |
| bKilled | 100% | 100% |
| aFled | 0% | 0% |
| bFled | 0% | 0% |
| aWinsOrDrivesOff | 100% | 100% |
| bLeaves | 0% | 0% |
| rounds | 5.558 | 3.542 |
| endings | Victory 1000 | Victory 1000 |

