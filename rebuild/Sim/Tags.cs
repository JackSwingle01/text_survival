namespace Survival;

/// The closed vocabulary of what a location feature is, for access and verbs (emergent-world-design §3.3). Computed, never stored.
/// Adding a tag for one situation is the failure mode; adding one at all is a visible code change.
[Flags]
public enum Tag : ulong
{
    None = 0,
    Inhabited = 1UL << 0, Owned = 1UL << 1, Occupied = 1UL << 2, Sealed = 1UL << 3, Buried = 1UL << 4,
    FrozenIn = 1UL << 5, Elevated = 1UL << 6, Unstable = 1UL << 7, ScentSource = 1UL << 8, Edible = 1UL << 9,
    Harvestable = 1UL << 10, Sheltering = 1UL << 11, Burnable = 1UL << 12, Fresh = 1UL << 13, Contested = 1UL << 14,
    Depleted = 1UL << 15, Beacon = 1UL << 16, Hostile = 1UL << 17, Unaware = 1UL << 18, Wounded = 1UL << 19,
}

/// What carried gear lets someone do to a location feature. Each gear row in Content/items.json lists its own.
[Flags]
public enum Capability
{
    None = 0,
    Cutting = 1 << 0, Striking = 1 << 1, Prying = 1 << 2, Digging = 1 << 3, Smoke = 1 << 4, Light = 1 << 5,
    Reaching = 1 << 6, Bait = 1 << 7, Spark = 1 << 8, Binding = 1 << 9, Carrying = 1 << 10, Piercing = 1 << 11, Sewing = 1 << 12,
}

public static class Capabilities
{
    /// Everything the working gear in the pack can do, OR'd over its gear rows. Broken gear does nothing.
    // Light (a burning torch) and Bait (meat) are not gear rows; they join when verbs need them.
    public static Capability Of(Inventory pack)
    {
        var caps = Capability.None;
        foreach (var g in pack.Gear)
            if (Items.Works(g))
                foreach (var c in Items.Def(g).Capabilities) caps |= c;
        return caps;
    }
}

/// Tags read off the world as it is now. Reads only: decay, snow and fading are worked out on
/// copies, as Report's are, and nothing is caught up in place.
/// Not derivable yet: Sealed (no lids or dens to force), FrozenIn (a frozen carcass is frozen meat, which
/// butchering already charges), Elevated, Unstable (thin ice is a tile's, moves to edges),
/// and Buried for a carcass, pile or body (no placement elevation: the snow under and over them is one depth).
public static class Tags
{
    /// How far an occupant holds its ground around what it occupies, in tiles (0: its own tile).
    public const int OccupantRadiusTiles = 0;
    /// Decay under 0.2 is the first four effective hours.
    public const double FreshDecayLevel = 0.2;
    public const double FreshPrintsPct = 0.5;
    public const double DepletedForagePct = 0.5;
    /// Matches the aftermath's threshold for a trail to follow.
    public const double WoundedLevel = 0.05;

    /// A viewer makes the relative tags relative to them: Owned and Occupied never count the viewer, Hostile needs one.
    public static Tag Of(World world, LocationFeature t, AgentId? viewer = null)
    {
        LocationFeatures.Require(world, t);
        var state = world.Tiles.GetValueOrDefault(t.Tile);
        var tags = t.Kind switch
        {
            LocationFeatureKind.Agent => OfAgent(world, world.Agent(new AgentId(t.Index)), viewer),
            LocationFeatureKind.Body => OfBody(world, world.Agent(new AgentId(t.Index))),
            LocationFeatureKind.Carcass => OfCarcass(world, t.Tile, Carcasses.Find(world, t.Tile, t.Index)!, viewer),
            LocationFeatureKind.Pool => OfPool(world, new PoolId(t.Tile, t.Index)),
            LocationFeatureKind.Pile => OfInventory(state!.Pile!) | OwnedBy(world, state.PileCause, viewer),
            LocationFeatureKind.Fire => OwnedBy(world, world.Fires[new FireId(t.Index)].Cause, viewer),
            LocationFeatureKind.Shelter => OfShelter(world, t.Tile, Shelters.Peek(world, t.Tile)!, viewer),
            LocationFeatureKind.Track => t.TrailDirection != null ? Tag.None // a desire path is a way, not a location feature to act on
                : Tracks.Read(world, t.Tile).First(s => (int)s.Maker == t.Index).FreshnessPct >= FreshPrintsPct ? Tag.Fresh : Tag.None,
            LocationFeatureKind.Sign => Tag.None,
            LocationFeatureKind.Item => Tag.None,
        };
        bool holdable = t.Kind switch
        {
            LocationFeatureKind.Agent => false, // an agent is met, not held: Hostile says what it means to you
            LocationFeatureKind.Body or LocationFeatureKind.Carcass or LocationFeatureKind.Pool or LocationFeatureKind.Pile or LocationFeatureKind.Fire or LocationFeatureKind.Shelter => true,
            LocationFeatureKind.Track => false,
            LocationFeatureKind.Sign => true,
            LocationFeatureKind.Item => false,
        };
        if (holdable && Occupants(world, t, viewer).Count > 0) tags |= Tag.Occupied;
        return tags;
    }

    static Tag OfBody(World world, Agent dead)
    {
        if (Remains.StageOf(world, dead) != RemainsStage.Flesh) return Tag.None;
        return Tag.ScentSource | (Fauna.RavensAt(world, dead.Tile) != null ? Tag.Beacon : Tag.None);
    }

    static Tag OfAgent(World world, Agent a, AgentId? viewer)
    {
        var tags = Tag.None;
        // It stands for its herd (LocationFeatures.At).
        var bodies = Fauna.HerdOf(world, a).Select(k => k.Body).ToList();
        if (bodies.Any(b => Body.IsAlive(b) && Combat.WoundLevel(b) > WoundedLevel)) tags |= Tag.Wounded;
        if (bodies.Any(b => Effects.Severity(b, EffectKind.Bloody) > 0) || Items.PackOf(a) is { } pack && pack.Kg.Any(s => s.Value > 0 && Scent.OfFood(s.Key) >= Scent.ScentSourceLevel))
            tags |= Tag.ScentSource;
        var enc = Combat.EncounterOf(world, a.Id);
        if (viewer is AgentId v && v != a.Id && enc != null && Combat.EncounterOf(world, v) == enc)
        {
            int mine = Combat.UnitOf(enc, v).Team;
            if (enc.Units.Any(u => u.Agent == a.Id && !u.Released && u.Team != mine)) tags |= Tag.Hostile;
        }
        // Unaware as Fauna.BeginHunt would open it (anything but running); in a fight, while every unit still is.
        if (a.Mind is HerdMind mind)
        {
            var units = enc?.Units.Where(u => u.Agent == a.Id && !u.Released && u.InField).ToList();
            bool unaware = units is { Count: > 0 } ? units.All(u => u.Awareness == Awareness.Unaware) : !Fauna.Fleeing(mind);
            if (unaware) tags |= Tag.Unaware;
        }
        return tags;
    }

    static Tag OfCarcass(World world, TileId tile, Carcass c, AgentId? viewer)
    {
        var tags = OwnedBy(world, c.Cause, viewer);
        if (Remains.StageOf(world, tile, c) != RemainsStage.Flesh)
            return tags | (Carcasses.AllCut(c) ? Tag.None : Tag.Harvestable); // bones: no scent, nothing to eat
        tags |= Tag.ScentSource;
        if (Carcasses.MeatKg(c) > 0) tags |= Tag.Edible;
        if (!Carcasses.AllCut(c)) tags |= Tag.Harvestable;
        if (Carcasses.DecayLevelNow(world, tile, c) < FreshDecayLevel) tags |= Tag.Fresh;
        // Ravens circling flesh: findable from far off (emergent-world-design §4).
        if (Fauna.RavensAt(world, tile) != null) tags |= Tag.Beacon;
        // Contested: another predator herd or a person (the viewer included) is there too.
        var occupants = Occupants(world, new LocationFeature(LocationFeatureKind.Carcass, tile, c.Id), viewer);
        if (occupants.Count > 0 && Nearby.At(world, tile).Any(a => !occupants.Contains(a.Id) && Sim.IsAlive(a)
                && (a.Mind is PersonMind || a.Mind is HerdMind && Fauna.IsPredator(a.Species))))
            tags |= Tag.Contested;
        return tags;
    }

    /// The living predator herds within occupant radius of a location feature that hold their ground against the
    /// viewer. None hold it over what the viewer made, on a tile where a fire burns or while the
    /// viewer carries a burning torch (fire as defence); a herd that is afraid, running, or busy
    /// (in a fight, or on a hunt) holds nothing. A kill is not made: a pack on the viewer's kill occupies it.
    internal static HashSet<AgentId> Occupants(World world, LocationFeature t, AgentId? viewer)
    {
        if (viewer is AgentId v && (MadeBy(world, t) == v || FireDefends(world, t.Tile, v))) return [];
        return Nearby.Within(world, t.Tile, OccupantRadiusTiles)
            .Where(a => !Ours(world, a, viewer) && a.Mind is HerdMind mind && Fauna.IsPredator(a.Species) && Sim.IsAlive(a) && Attending(world, a, mind))
            .Select(a => a.Id).ToHashSet();
    }

    static bool Ours(World world, Agent a, AgentId? viewer) =>
        viewer is AgentId v && (a.Id == v || world.Agents.TryGetValue(v, out var them) && Fauna.IsKin(them, a));

    public const double AfraidLevel = 0.5;

    internal static bool Attending(World world, Agent a, HerdMind mind) =>
        mind.Fear <= AfraidLevel && !Fauna.Fleeing(mind) && Combat.EncounterOf(world, a.Id) == null;

    static bool FireDefends(World world, TileId tile, AgentId viewer) =>
        world.Fires.Values.Any(f => (f.CarriedBy == viewer || f.CarriedBy == null && f.Tile == tile) && Fire.Burns(Fire.Phase(f)));

    /// Someone's work, not what they killed.
    static AgentId? MadeBy(World world, LocationFeature t) => t.Kind switch
    {
        LocationFeatureKind.Agent => null,
        LocationFeatureKind.Body => null,
        LocationFeatureKind.Carcass => null, // a kill, which a pack takes from its killer
        LocationFeatureKind.Pool => null,
        LocationFeatureKind.Pile => world.Tiles.GetValueOrDefault(t.Tile)?.PileCause?.Who,
        LocationFeatureKind.Fire => world.Fires.GetValueOrDefault(new FireId(t.Index))?.Cause?.Who,
        LocationFeatureKind.Shelter => Shelters.Peek(world, t.Tile)?.Cause?.Who,
        LocationFeatureKind.Track => null,
        LocationFeatureKind.Sign => null,
        LocationFeatureKind.Item => null,
    };

    static Tag OfPool(World world, PoolId id)
    {
        var def = Pools.Def(world, id);
        switch (def)
        {
            case ForagePool f:
                {
                    var tags = Tag.Harvestable;
                    if (f.Items.Any(i => Items.Resources[i.Resource].Category == ResourceCategory.Food)) tags |= Tag.Edible;
                    if (f.Items.Any(i => Fire.IsFuel(i.Resource))) tags |= Tag.Burnable;
                    if (Pools.ForageDensity(world, id) < f.Density * DepletedForagePct) tags |= Tag.Depleted;
                    return tags;
                }
            case TreePool:
                return Pools.HasTrees(world, id) ? Tag.Harvestable | Tag.Burnable : Tag.Depleted;
            case StockPool s:
                {
                    var units = Pools.UnitsNow(world, id);
                    var tags = units.Any(u => u > 0) ? Tag.Harvestable : Tag.Depleted;
                    for (int i = 0; i < s.Items.Count; i++)
                    {
                        if (units[i] <= 0) continue;
                        if (Items.Resources[s.Items[i].Resource].Category == ResourceCategory.Food) tags |= Tag.Edible;
                        if (Fire.IsFuel(s.Items[i].Resource)) tags |= Tag.Burnable;
                    }
                    return tags | BuriedUnder(world, id.Tile, Pools.IsHidden(s) ? s.PlacementHeightM : null);
                }
            case ShelterFind f:
                return Tag.Sheltering | BuriedUnder(world, id.Tile, f.PlacementHeightM);
            case CarcassFind or PileFind:
                return Tag.None;
            default:
                throw new InvalidOperationException($"Tags has no reading for pool {def.GetType().Name}");
        }
    }

    /// Things the world was born with sit on the ground, so snow as deep as they stand buries them.
    static Tag BuriedUnder(World world, TileId tile, double? heightM) =>
        heightM is double h && Ground.Underfoot(world, tile).SnowDepthM >= h ? Tag.Buried : Tag.None;

    static Tag OfInventory(Inventory inv)
    {
        var tags = Tag.None;
        foreach (var (r, kg) in inv.Kg)
        {
            if (kg <= 0) continue;
            if (Items.Resources[r].Category == ResourceCategory.Food) tags |= Tag.Edible;
            if (Fire.IsFuel(r)) tags |= Tag.Burnable;
            if (Scent.OfFood(r) >= Scent.ScentSourceLevel) tags |= Tag.ScentSource;
        }
        return tags;
    }

    static Tag OfShelter(World world, TileId tile, ShelterState s, AgentId? viewer)
    {
        var tags = OwnedBy(world, s.Cause, viewer) | (Shelters.IsDestroyed(s) ? Tag.Depleted : Tag.Sheltering);
        if (Nearby.At(world, tile).Any(a => a.Id != viewer && Sim.IsAlive(a))) tags |= Tag.Inhabited;
        return tags;
    }

    static Tag OwnedBy(World world, Cause? cause, AgentId? viewer) =>
        cause?.Who is AgentId who && who != viewer && world.Agents.TryGetValue(who, out var a) && a.Mind is PersonMind && Sim.IsAlive(a)
            ? Tag.Owned : Tag.None;
}
