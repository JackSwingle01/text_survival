# #336: Healing scales with hunger and thirst

Hypothesis:
- Changes: `Sim/BodySurvival.cs` `HealingAmount` (the fed, watered and rested conditions and the calorie-fullness scaling go; × (1 − 0.5 hungry)(1 − 0.5 thirsty); stops at an empty water store or a core below 95 °F). `Body.RegenMinCaloriesPct` is left to Fauna's mending only, which animals keep (they feel no deprivation).
- Affects: every person's healing and blood regrowth (a baseline body at 75% calories heals 1/0.75 faster than today, since fullness no longer scales it), awake healing with low energy, healing on a nearly empty calorie or water store, `LowestBloodPct`'s forecast, and through it the health priority.
- Stays green: sleep's factor, WellFed's 5%, bedding, the bleeding forecast tests, the cold and thirst death tests.
- Breaks (scout): SurvivalTests FedWateredAndRested_Heals_AndRegrowsBlood (energy gate), SleepHeals_FasterThanWaking_ButOnlyFedWateredAndWarm (dry and hungry cases), Healing_IsLinearInFood (fullness scaling), NoHealing_OutpacesTheColdThirstOrHunger (starving case), and possibly the starvation-death tests, since a starving body now heals at half rate against the heart damage.
- First red: an awake body at 100 minutes of energy heals as much as one at full energy; today it heals nothing.
