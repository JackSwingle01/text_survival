# Resources are discrete or continuous

Each resource is set in data as discrete or continuous. A discrete resource is counted in pieces, each
with its own weight (logs, sticks, stones, bone, hides), and a piece is moved whole, never split. A
continuous resource is one amount (water, tinder, resin, tallow). The rebuild's port gave every
resource one weight, so a log could be split to fit a nearly full fire and piece counts were lost; the
old game made everything pieces, and making that work for water would need a "divisible" flag, an
extra rule we rejected. A consequence the user accepted: tinder is continuous, so a strike takes its
amount from the total rather than using up a whole pinch.

**Considered Options:** one weight per resource (the rebuild's port); all pieces with a "divisible"
flag (the old game had all pieces); discrete or continuous per resource (chosen).
