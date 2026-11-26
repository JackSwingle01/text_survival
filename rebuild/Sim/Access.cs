namespace Survival;

/// Every refusal asks here first, so buried, occupied, frozen-in, across-the-water and shut in a cache mean the same everywhere.
public static class Access
{
    [Flags]
    public enum Gate
    {
        None = 0,
        Buried = 1 << 0,
        Occupied = 1 << 1,
        FrozenIn = 1 << 2,
        AcrossWater = 1 << 3,
        All = Buried | Occupied | FrozenIn | AcrossWater,
    }

    /// None is switched on in play yet; tests switch them on through World.AccessGates or the gates overloads.
    public const Gate Wired = Gate.None;

    public const string BuriedReason = "it is buried under the snow", FrozenInReason = "it is frozen in",
        AcrossWaterReason = "there is open water in the way", OutOfReach = "it is out of reach", ShutReason = "it is shut in a cache it cannot get into";

    /// confronting: the activity is the encounter with the occupant, so the occupant is no bar.
    public static Refusal? Refusal(World world, AgentId who, LocationFeature t, bool confronting = false) =>
        Refusal(world, who, t, world.AccessGates, confronting);

    public static Refusal? Refusal(World world, AgentId who, LocationFeature t, Gate gates, bool confronting = false) =>
        Blocker(world, who, t, gates, confronting) is { } b ? new(b.Reason, b.Missing) : null;

    /// Gate.None means plain distance.
    public readonly record struct Block(Gate Gate, string Reason, Missing? Missing = null);

    public static Block? Blocker(World world, AgentId who, LocationFeature t, bool confronting = false) =>
        Blocker(world, who, t, world.AccessGates, confronting);

    public static Block? Blocker(World world, AgentId who, LocationFeature t, Gate gates, bool confronting = false)
    {
        LocationFeatures.Require(world, t);
        var actor = world.Agent(who);
        if (actor.Tile != t.Tile)
        {
            if (MapGen.Manhattan(actor.Tile, t.Tile) != 1) return new Block(Gate.None, OutOfReach);
            if (gates.HasFlag(Gate.AcrossWater) && AcrossWater(world, actor.Tile, t.Tile)) return new Block(Gate.AcrossWater, AcrossWaterReason);
        }
        if (!GetsInto(world, who, t)) return new Block(Gate.None, ShutReason);
        if ((gates & (Gate.Buried | Gate.Occupied | Gate.FrozenIn)) == 0) return null;
        var tags = Tags.Of(world, t, who);
        var caps = Items.PackOf(actor) is { } pack ? Capabilities.Of(pack) : Capability.None;
        if (gates.HasFlag(Gate.Buried) && tags.HasFlag(Tag.Buried) && !caps.HasFlag(Capability.Digging) && !AbilitiesOf(actor).HasFlag(Ability.Digs))
            return new Block(Gate.Buried, BuriedReason, new MissingCapability(Capability.Digging));
        if (gates.HasFlag(Gate.Occupied) && !confronting && tags.HasFlag(Tag.Occupied)) return new Block(Gate.Occupied, OccupiedReason(world, t, who));
        if (gates.HasFlag(Gate.FrozenIn) && tags.HasFlag(Tag.FrozenIn) && (caps & FreesIce) == 0)
            return new Block(Gate.FrozenIn, FrozenInReason, new MissingCapability(FreesIce));
        return null;
    }

    /// The one check of who gets into what, always on: a cache by the abilities it lists unless it lies open; anything else, anyone.
    public static bool GetsInto(World world, AgentId who, LocationFeature t) =>
        t.Kind != LocationFeatureKind.Cache || world.Caches[new CacheId(t.Index)] is var c
            && (Caches.Open(world, c) || (Caches.Row(c.Kind).LetsIn & AbilitiesOf(world.Agent(who))) != 0);

    static Ability AbilitiesOf(Agent a) => SpeciesTable.Of(a.Species).Abilities;

    public const Capability FreesIce = Capability.Striking | Capability.Cutting;

    static string OccupiedReason(World world, LocationFeature t, AgentId who) =>
        $"{Cause.Describe(world, Tags.Occupants(world, t, who).Min())} guards it";

    /// What is put down lies on top, so snow or ice over a pile does not bar adding to it; only its occupant does.
    public static Refusal? PutDownRefusal(World world, AgentId who, TileId tile) => PutDownRefusal(world, who, tile, world.AccessGates);

    public static Refusal? PutDownRefusal(World world, AgentId who, TileId tile, Gate gates)
    {
        if (world.Agent(who).Tile != tile) return new(OutOfReach);
        if (!gates.HasFlag(Gate.Occupied)) return null;
        // A new pile is the putter's own, so no occupant bars it.
        var pile = LocationFeatures.PileAt(world, tile);
        if (pile == null) return null;
        return Tags.Occupants(world, pile.Value, who).Count > 0 ? new(OccupiedReason(world, pile.Value, who)) : null;
    }

    static bool AcrossWater(World world, TileId from, TileId to)
    {
        var m = Map.Of(world);
        bool Open(TileId t) => !Map.IsFrozen(m, t, world.Minute);
        bool Wet(TileId t) => Map.IsOpenWater(m, t, world.Minute) || m.TerrainAt(t) == Terrain.DeepWater && Open(t);
        return Wet(from) || Wet(to) || m.BesideRiver(from) && m.BesideRiver(to) && (Open(from) || Open(to)) || m.EdgeBetween(from, to) == EdgeType.River;
    }
}
