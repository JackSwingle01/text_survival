# Main red: SticksOnly_WaterCritical_ItDoesNotPace (#311)

## Hypothesis

#297 and #305 each passed CI alone; merged together, `NpcNeedTests.SticksOnly_WaterCritical_ItDoesNotPace` paces
(19 reversals). #305 changes world generation, so the scene's forage differs, and the NPC fills its pack to 0.43 kg
of room. That is above `FullPackKg` (0.3), so it is not "full", but below a stick's pieces (0.2-0.6 kg). At a stick
pile on its own tile the take is refused, `MakeRoom` finds nothing to put down, `packFull` is set and it explores; a
step away the pile, known only by its kilos, is a leaf again, and it walks back. The same here-versus-there split
as #297's trunk.

Fix: a pack is full below forage's biggest piece (0.6 kg), a rule that reads the same from any tile.

## Log

- Revised: first tried walking home when the leaf here failed on `packFull`. That fixed this scene but tipped
  `AtNeed_ItDoesNotPace(Water, OneTile)` to 5: home "full" by one rule, not by the other, so it turned straight back.
  Dropped for one threshold.
- Revised: with 0.6 the scene fell to 4, each a trip home that drank or fed the fire and went out again.
  `Reversals` counted only fire lit, tree felled, ate and gathered as work done; it now also counts drinking,
  fuel added, putting on a pile, picking up and washing. Not a looser threshold: with 0.3 restored the scene
  still shows 17.
- Follow-up: the pacing tests are sensitive to any change in world generation. Count A-B-A-B loops on one tile
  pair, and pin each scene's world (see the session's follow-ups).
