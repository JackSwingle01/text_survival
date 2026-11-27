# Injuries are the record; tissue condition is read from them

A hit leaves an injury on a region: its kind (cut, puncture, bruise, burn, frostbite, fracture), a
site within the region (a hand, a foot), how deep it reaches, and whether it is open, bleeding,
dressed or infected. A tissue's condition is read from the injuries on it and never stored, healing
closes injuries one by one at a pace set by their kind, and a body's bleeding is the sum of its open
injuries' bleeds. Treatment needs wounds to be things: a dressing goes on one wound, an infection
festers in one wound, and frostbite lands on a hand without the body gaining hand regions. The unit
of damage does not change (decision "Damage is measured in tissue condition"); only where condition
lives does.

**Considered options:** keeping tissue condition with body-wide bleeding, infection and dressing
could not say which wound is open, since a bruise and a cut both only lower skin. A wound list kept
beside stored tissue condition was cheaper to build but left two records to keep in step: healing
mends a region, and nothing says which of its wounds that closes.

**Consequences:** damage resolution, healing, bleeding, crippling and the save all change, and animals
heal through injuries too or keep a second path. The derived condition must equal today's stored
condition for the same blows, so combat balance holds. Decided in the treatment grilling (#56).
