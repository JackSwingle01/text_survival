# NPC AI uses only the simulation's public interface, never its internals

NPC decision code is a client of the simulation, not part of it. It knows the game only through
what the systems publish: the actions available to an agent (`Verbs.At`) with their refusals, odds
and trip terms; forecasts exposed by the module that owns each rule (body, fire, trip); and what the
agent has seen and remembers (`Sight`, `Beliefs`). It never re-states a rule that belongs to a
system, such as what a fire needs to catch, what a blade is required for, or how long a walk takes.
When the AI needs to know why an action can't be done, the refusal carries it as data: a reason plus,
where fetching something would fix it, what is missing and how much. The NPC code before this kept
private copies of the fire, body and travel rules. Each copy slowly drifted from the real system,
and most NPC failures were an agent acting on a copy that disagreed with what the game did. Under
this rule a system that changes carries the AI with it, and the AI can be rewritten or replaced
without touching game logic. The cost is that refusals and forecasts become a real interface: they
are designed and tested, not just display strings.
