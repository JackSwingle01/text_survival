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
    public static AgentId Spawn(World world, TileId tile, TileId? camp = null, double? boldness = null)
    {
        if (world.NextAgentId < FirstId) world.NextAgentId = FirstId;
        return Npc.Spawn(world, tile, camp, boldness);
    }
}
