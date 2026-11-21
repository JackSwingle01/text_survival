namespace Survival;

/// One gate for reaching a thing (emergent-world-design §3.5). Every refusal that acts on a thing
/// resolves it (the thing is there: its own wording) and then asks here before anything else about
/// it, so buried, guarded, frozen-in and across-the-water mean the same everywhere.
public static class Access
{
    /// The conditions the gate can enforce.
    [Flags]
    public enum Gate
    {
        None = 0,
        /// Buried (Tags) and no Digging in the pack.
        Buried = 1 << 0,
        /// A living predator herd guards it (Tags) and this is not the confrontation.
        Guarded = 1 << 1,
        /// Frozen in (Tags) and nothing to strike or cut it free with.
        FrozenIn = 1 << 2,
        /// On a neighbouring tile with open water between and no crossing.
        AcrossWater = 1 << 3,
        All = Buried | Guarded | FrozenIn | AcrossWater,
    }

    /// The conditions in force. None of the four was enforced before this gate existed, and this
    /// step changes no behaviour, so none is wired: each is implemented and available (Refusal with
    /// gates) until the step that wants it switches it on here.
    public const Gate Wired = Gate.None;

    public const string BuriedReason = "it is buried under the snow", FrozenInReason = "it is frozen in",
        AcrossWaterReason = "there is open water in the way", OutOfReach = "it is out of reach";

    /// Why who cannot reach t now, or null. The thing must be there (Things.Exists): callers find it
    /// first. confronting: the activity is the fight with whatever guards it, so a guard is no bar.
    public static string? Refusal(World world, AgentId who, ThingRef t, bool confronting = false) =>
        Refusal(world, who, t, world.AccessGates, confronting);

    /// As Refusal, with these conditions in force instead of the wired ones.
    public static string? Refusal(World world, AgentId who, ThingRef t, Gate gates, bool confronting = false)
    {
        Things.Require(world, t);
        var actor = world.Agent(who);
        if (actor.Tile != t.Tile)
        {
            // Only a neighbour can be reached at all, and not across water.
            if (MapGen.Manhattan(actor.Tile, t.Tile) != 1) return OutOfReach;
            if (gates.HasFlag(Gate.AcrossWater) && AcrossWater(world, actor.Tile, t.Tile)) return AcrossWaterReason;
        }
        if ((gates & (Gate.Buried | Gate.Guarded | Gate.FrozenIn)) == 0) return null;
        var tags = Tags.Of(world, t, who);
        var caps = Items.PackOf(actor) is { } pack ? Capabilities.Of(pack) : Capability.None;
        if (gates.HasFlag(Gate.Buried) && tags.HasFlag(Tag.Buried) && !caps.HasFlag(Capability.Digging)) return BuriedReason;
        if (gates.HasFlag(Gate.Guarded) && !confronting && tags.HasFlag(Tag.Guarded)) return GuardedReason(world, t, who);
        if (gates.HasFlag(Gate.FrozenIn) && tags.HasFlag(Tag.FrozenIn) && (caps & (Capability.Striking | Capability.Cutting)) == 0) return FrozenInReason;
        return null;
    }

    static string GuardedReason(World world, ThingRef t, AgentId who)
    {
        var guard = world.Agents.Values.First(a => a.Id != who && a.Mind is HerdMind && Fauna.IsPredator(a.Species) && Sim.IsAlive(a)
            && MapGen.Manhattan(a.Tile, t.Tile) <= Tags.GuardRadiusTiles);
        return $"{Cause.Describe(world, guard.Id)} guards it";
    }

    /// Open water between two neighbours: either is a lake or deep water, a river runs along both,
    /// or the edge between them is a river.
    static bool AcrossWater(World world, TileId from, TileId to)
    {
        var m = Map.Of(world);
        static bool Wet(Terrain t) => t is Terrain.Water or Terrain.DeepWater;
        return Wet(m.TerrainAt(from)) || Wet(m.TerrainAt(to)) || m.BesideRiver(from) && m.BesideRiver(to) || m.EdgeBetween(from, to) == EdgeType.River;
    }
}
