# NPC AI sees only what the player's interface shows

NPC decision code knows the game only through the surface the player uses: the actions on offer
(`Verbs.At`), their refusals, odds and trip terms, the forecasts the owning modules expose (body,
fire, trip), and what the NPC has seen and remembers (`Sight`, `Beliefs`). It never re-states a rule
that belongs to a system, such as what a fire needs to catch, what a blade is required for, or how
long a walk takes. When the AI needs to know why it can't do something, the refusal says so in data:
a reason plus, where fetching something would fix it, what is missing and how much. The sentence the
player reads is built from that same record. The old NPC code, and the first rebuild of it, kept
private copies of the fire, body and travel rules. Each copy slowly drifted from the real system,
and most NPC failures were an NPC acting on a copy that disagreed with what the game actually did.
Under this rule a system that changes carries the AI with it, and anything the AI is missing is
something the player's interface is missing too. The cost is that refusals and forecasts become a
real interface: they are designed and tested, not just display strings.
