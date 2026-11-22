using Survival;

/// NPC benches at a stable agent id. An NPC's id is the next one free after generation's herds, so
/// adding a herd kind to Populate moved every bench's NPC to another id, and with the id its random
/// streams (World.Rng is keyed by it) and its place in the scheduler's order: the bench changed with
/// nothing about the NPC changed. Benches spawn their NPCs here instead, from a block of ids no
/// generated herd reaches.
static class NpcBench
{
    /// The first id kept for bench NPCs: generation makes a few dozen agents.
    public const int FirstId = 10_000;

    /// An NPC at FirstId, or the next id after the bench's earlier NPCs: the same id on every seed and
    /// whatever generation populated. Spawn the bench's NPCs before anything else is born in the world;
    /// what is born after them takes the ids after theirs, so they keep their place ahead of it.
    public static AgentId Spawn(World world, TileId tile, TileId? camp = null)
    {
        if (world.NextAgentId < FirstId) world.NextAgentId = FirstId;
        return Npc.Spawn(world, tile, camp);
    }
}

/// Drives NPCs by their own minds, on the world's scheduler: the world lives on and every NPC
/// chooses its own activities (Minds.Decide) as each one ends.
static class NpcDriver
{
    /// Lives the world on until the minute. Returns what happened. A choice refused more than a
    /// few times running is a bug in the mind, so it throws.
    public static List<WorldEvent> Live(World w, AgentId npc, long untilMinute)
    {
        var log = new List<WorldEvent>();
        int refusedInARow = 0;
        while (w.Minute < untilMinute && Body.IsAlive(w.Agent(npc).Body))
        {
            var events = Sim.Advance(w, 1);
            log.AddRange(events);
            var refused = events.OfType<PlanRefused>().FirstOrDefault(r => r.Who == npc);
            if (refused == null) { if (w.Agent(npc).Doing != null) refusedInARow = 0; }
            else if (++refusedInARow > 3) throw new InvalidOperationException($"{npc} keeps choosing a refused {refused.Activity}: {refused.Reason}");
        }
        return log;
    }
}
