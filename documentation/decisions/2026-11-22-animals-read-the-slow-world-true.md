# Animals read the slow world true and sense what moves

Animals have no Memory. Anywhere on the map, they read the true state of anything that isn't an agent
and changes over days or longer: terrain, water, ground, how grazed a tile is, dens, trail wear.
Agents, and what agents leave that matters within hours (carcasses, prints, fires, stashes), they
know only by sensing them, plus a sightings list that keeps an agent for a few hours and a carcass
until the animal sees it gone. The line is pragmatic, not principled.

Full Memory per animal would cost tens of MB of save. A learned range was rejected because real
ranges are at or beyond the size of the map (wolf territories run 100-1,000+ km², and the map is
about 92 km²). People keep Memory because the player sees and reasons about what a person knows. The
visible cost is that every caribou knows where the best pasture is; distance and crowding are what
keep them from all heading there.

So that no system grows two variants, every source listing and the planner take a knowledge view as
a parameter: Memory for people, the true world plus sensing for animals. The test that keeps NPCs to
the Sim's public interface covers the planner too, so no NPC is handed the true world. The fixed
territory is gone with this; a den is only a place to rest.
