namespace Survival;

/// The closed vocabulary of what a thing is, for access and (build step 4) verbs
/// (emergent-world-design §3.3, with the catalog's refinements: Ripe folds into Harvestable,
/// Crossing moves to edges, Hostile, Unaware and Wounded are added). Computed, never stored.
/// Adding a tag for one situation is the failure mode; adding one at all is a visible code change.
[Flags]
public enum Tag : ulong
{
    None = 0,
    Occupied = 1UL << 0, Owned = 1UL << 1, Guarded = 1UL << 2, Sealed = 1UL << 3, Buried = 1UL << 4,
    FrozenIn = 1UL << 5, Elevated = 1UL << 6, Unstable = 1UL << 7, ScentSource = 1UL << 8, Edible = 1UL << 9,
    Harvestable = 1UL << 10, Sheltering = 1UL << 11, Burnable = 1UL << 12, Fresh = 1UL << 13, Contested = 1UL << 14,
    Depleted = 1UL << 15, Beacon = 1UL << 16, Hostile = 1UL << 17, Unaware = 1UL << 18, Wounded = 1UL << 19,
}

/// What carried gear lets someone do to a thing. Each gear row in Content/items.json lists its own.
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
    // Light from a burning torch and Bait from meat in the pack are not gear rows; they join when
    // verbs need them (build step 4). Smoke has no source yet.
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
/// Derived today: Occupied (another living agent on a shelter's tile), Owned (made by a living
/// person other than the viewer), Guarded (a living predator herd within guard radius of anything
/// lying there) and Contested (a guarded carcass someone else is at too), Buried (a hidden stock or shelter find under snow deeper than it stands), ScentSource,
/// Edible, Harvestable, Sheltering, Burnable, Fresh (a carcass under 4 effective hours, prints at
/// least half fresh), Depleted, Hostile (the other side of the viewer's fight), Unaware (a herd as a
/// hunt or ambush would open on it), Wounded, Beacon (the carrion flock on the tile of flesh: a
/// carcass or a dead person not yet a skeleton). Bones are only Harvestable while bone is left.
/// Not derivable yet: Sealed (no lids, lodges or dens to force), FrozenIn (Ground has no ice that
/// holds a thing; a frozen carcass is frozen meat, which butchering already charges), Elevated (no
/// vantage), Unstable (thin ice is a tile's, and moves to edges), Occupied by small game (small game is not a density yet), Buried for a carcass,
/// stash or body (no placement elevation: the snow they lie on and the snow on them are one depth).
public static class Tags
{
    /// How far a guard holds its ground around what it guards, in tiles (0: its own tile).
    public const int GuardRadiusTiles = 0;
    /// A carcass is fresh through the old "fresh" band: decay under 0.2, the first four effective hours.
    public const double FreshDecayLevel = 0.2;
    /// Prints read as fresh while at least this much of them is left.
    public const double FreshPrintsPct = 0.5;
    /// Worked forage reads as depleted below this share of its base density.
    public const double DepletedForagePct = 0.5;
    /// A member this hurt is wounded (the aftermath's threshold for a trail to follow).
    public const double WoundedLevel = 0.05;

    /// The thing's tags now. A viewer makes the relative ones relative to them: Owned and Guarded
    /// never count the viewer, Hostile needs one.
    public static Tag Of(World world, ThingRef t, AgentId? viewer = null)
    {
        Things.Require(world, t);
        var state = world.Tiles.GetValueOrDefault(t.Tile);
        var tags = t.Kind switch
        {
            ThingKind.Agent => OfAgent(world, world.Agent(new AgentId(t.Index)), viewer),
            ThingKind.Body => OfBody(world, world.Agent(new AgentId(t.Index))),
            ThingKind.Carcass => OfCarcass(world, t.Tile, Carcasses.Find(world, t.Tile, t.Index)!, viewer),
            ThingKind.Pool => OfPool(world, new PoolId(t.Tile, t.Index)),
            ThingKind.Stash => OfInventory(state!.Stash!) | OwnedBy(world, state.StashCause, viewer),
            ThingKind.Fire => OwnedBy(world, world.Fires[new FireId(t.Index)].Cause, viewer),
            ThingKind.Shelter => OfShelter(world, t.Tile, Shelters.Peek(world, t.Tile)!, viewer),
            ThingKind.Track => t.TrailDirection != null ? Tag.None // a desire path is a way, not a thing to act on
                : Tracks.Read(world, t.Tile).First(s => (int)s.Maker == t.Index).FreshnessPct >= FreshPrintsPct ? Tag.Fresh : Tag.None,
            ThingKind.Sign => Tag.None, // none stored yet
            ThingKind.Item => Tag.None, // none named yet
        };
        bool holdable = t.Kind switch
        {
            ThingKind.Agent => false, // an agent is met, not held: Hostile says what it means to you
            ThingKind.Body or ThingKind.Carcass or ThingKind.Pool or ThingKind.Stash or ThingKind.Fire or ThingKind.Shelter => true,
            ThingKind.Track => false, // prints and paths are not held
            ThingKind.Sign => true,
            ThingKind.Item => false, // carried: held by whoever carries it
        };
        if (holdable && Guards(world, t, viewer).Count > 0) tags |= Tag.Guarded;
        return tags;
    }

    /// A dead person: scent while there is flesh on them, and a beacon while the carrion flock circles.
    static Tag OfBody(World world, Agent dead)
    {
        if (Remains.StageOf(world, dead) != RemainsStage.Flesh) return Tag.None;
        return Tag.ScentSource | (Fauna.FlockAt(world, dead.Tile) != null ? Tag.Beacon : Tag.None);
    }

    static Tag OfAgent(World world, Agent a, AgentId? viewer)
    {
        var tags = Tag.None;
        if (a.Members.Any(b => Body.IsAlive(b) && Combat.WoundLevel(b) > WoundedLevel)) tags |= Tag.Wounded;
        if (a.Members.Any(b => Effects.Severity(b, EffectKind.Bloody) > 0) || Items.PackOf(a) is { } pack && Items.Have(pack, Resource.RawMeat) > 0)
            tags |= Tag.ScentSource;
        var enc = Combat.EncounterOf(world, a.Id);
        if (viewer is AgentId v && v != a.Id && enc != null && Combat.EncounterOf(world, v) == enc)
        {
            int mine = Combat.UnitOf(enc, v).Team;
            if (enc.Units.Any(u => u.Agent == a.Id && !u.Released && u.Team != mine)) tags |= Tag.Hostile;
        }
        // A herd unaware as a stalk would open on it (Fauna.BeginHunt): anything but running. In a
        // fight, the units say: unaware while every one of its own still is.
        if (a.Mind is HerdMind mind)
        {
            var units = enc?.Units.Where(u => u.Agent == a.Id && !u.Released && u.InField).ToList();
            bool unaware = units is { Count: > 0 } ? units.All(u => u.Awareness == Awareness.Unaware) : mind.State != HerdState.Fleeing;
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
        // The carrion flock circling flesh: findable from far off (emergent-world-design §4).
        if (Fauna.FlockAt(world, tile) != null) tags |= Tag.Beacon;
        // Guarded (Of), and someone else wants it too: another predator herd or a person on the tile,
        // the viewer included.
        var guards = Guards(world, new ThingRef(ThingKind.Carcass, tile, c.Id), viewer);
        if (guards.Count > 0 && Nearby.At(world, tile).Select(id => world.Agents[id]).Any(a => !guards.Contains(a.Id) && Sim.IsAlive(a)
                && (a.Mind is PersonMind || a.Mind is HerdMind && Fauna.IsPredator(a.Species))))
            tags |= Tag.Contested;
        return tags;
    }

    /// The living predator herds within guard radius of a thing that hold their ground against the
    /// viewer (left out itself). None hold it against the viewer (the exemptions are implemented,
    /// as the gate is, and wired with it): over what the viewer made (MadeBy: a fire, a stash, a
    /// shelter); on a tile where a fire burns, or when the viewer carries a burning torch (fire as
    /// defence); and a herd that is afraid, running, or whose attention is elsewhere (in a fight, or on
    /// a hunt) holds nothing. A kill is not made: a pack on the viewer's kill guards it (the wolves
    /// took my kill).
    internal static HashSet<AgentId> Guards(World world, ThingRef t, AgentId? viewer)
    {
        if (viewer is AgentId v && (MadeBy(world, t) == v || FireDefends(world, t.Tile, v))) return [];
        return Nearby.Within(world, t.Tile, GuardRadiusTiles)
            .Where(a => a.Id != viewer && a.Mind is HerdMind mind && Fauna.IsPredator(a.Species) && Sim.IsAlive(a) && Attending(world, a, mind))
            .Select(a => a.Id).ToHashSet();
    }

    /// Learned fear past this and a herd holds nothing.
    public const double AfraidLevel = 0.5;

    static bool Attending(World world, Agent a, HerdMind mind) =>
        mind.Fear <= AfraidLevel && mind.State is not (HerdState.Fleeing or HerdState.Hunting) && Combat.EncounterOf(world, a.Id) == null;

    /// A fire burning on the tile, or a torch burning in the viewer's hand.
    static bool FireDefends(World world, TileId tile, AgentId viewer) =>
        world.Fires.Values.Any(f => (f.CarriedBy == viewer || f.CarriedBy == null && f.Tile == tile) && Fire.Burns(Fire.Phase(f)));

    /// Who made a thing, as it keeps it: someone's work, not what they killed.
    static AgentId? MadeBy(World world, ThingRef t) => t.Kind switch
    {
        ThingKind.Agent => null, // a living agent is met, not made
        ThingKind.Body => null, // a death, not a making
        ThingKind.Carcass => null, // a kill, which a pack takes from its killer
        ThingKind.Pool => null, // the land's own
        ThingKind.Stash => world.Tiles.GetValueOrDefault(t.Tile)?.StashCause?.Who,
        ThingKind.Fire => world.Fires.GetValueOrDefault(new FireId(t.Index))?.Cause?.Who,
        ThingKind.Shelter => Shelters.Peek(world, t.Tile)?.Cause?.Who,
        ThingKind.Track => null, // prints are no one's work
        ThingKind.Sign => null, // none stored yet
        ThingKind.Item => null, // none named yet
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
            default:
                throw new InvalidOperationException($"Tags has no reading for pool {def.GetType().Name}");
        }
    }

    /// A thing the world was born with sits on the ground; snow at least as deep as it stands buries it.
    static Tag BuriedUnder(World world, TileId tile, double? heightM) =>
        heightM is double h && Ground.SnowDepthNowM(world, tile) >= h ? Tag.Buried : Tag.None;

    static Tag OfInventory(Inventory inv)
    {
        var tags = Tag.None;
        foreach (var (r, kg) in inv.Kg)
        {
            if (kg <= 0) continue;
            if (Items.Resources[r].Category == ResourceCategory.Food) tags |= Tag.Edible;
            if (Fire.IsFuel(r)) tags |= Tag.Burnable;
            if (r is Resource.RawMeat or Resource.RawFish) tags |= Tag.ScentSource;
        }
        return tags;
    }

    static Tag OfShelter(World world, TileId tile, ShelterState s, AgentId? viewer)
    {
        var tags = OwnedBy(world, s.Cause, viewer) | (Shelters.IsDestroyed(s) ? Tag.Depleted : Tag.Sheltering);
        if (Nearby.At(world, tile).Any(id => id != viewer && Sim.IsAlive(world.Agents[id]))) tags |= Tag.Occupied;
        return tags;
    }

    /// Made by a living person who is not the viewer.
    static Tag OwnedBy(World world, Cause? cause, AgentId? viewer) =>
        cause?.Who is AgentId who && who != viewer && world.Agents.TryGetValue(who, out var a) && a.Mind is PersonMind && Sim.IsAlive(a)
            ? Tag.Owned : Tag.None;
}
