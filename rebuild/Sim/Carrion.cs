namespace Survival;

/// The carrion flock (Species.Raven: ravens, a fox and a wolverine folded into one kind,
/// emergent-world-design §4). It hunts nothing and guards nothing; it is drawn to whatever is dead or
/// killing within its range and lives off it. Every interaction goes through the systems that own
/// the thing: carcass meat through Carcasses.EatMeat, a body through Remains.Pick, a stash through
/// Items.TakeFromStash, each reached through Access.
/// What it leaves in the world is derived, never stored: a flock on a tile with flesh on it is
/// circling it (Tags.Beacon); a flock with a predator herd and no carcass is over the predator
/// (Signs: RavensOver).
public static partial class Fauna
{
    /// How far the flock looks for something to follow, in tiles.
    public const int CarrionRangeTiles = 10;
    /// It looks again this often while nothing holds it; feeding, it looks only at its own tile.
    const int CarrionLookEveryMinutes = 10;
    /// A minute at an unwatched stash with food in it: the chance it raids it (judgment: about one
    /// raid in eight hours of a camp left alone), and what each member carries off.
    const double RaidChancePerMinute = 0.002, RaidKgPerMember = 0.05;
    const double CarrionDriftChance = 0.05, ForageWhenHungrier = 0.5;

    static (Activity, int) DecideCarrion(World world, Agent herd, HerdMind mind)
    {
        // Something on its own tile holds it: a meal it can reach, else food it can see (it circles),
        // else a predator it is following.
        if (CanFeed(world, herd))
        {
            if (!PredatorAt(world, herd.Tile)) // it eats beside a pack only once the pack has gone
            {
                if (mind.State != HerdState.Feeding) Become(world, mind, HerdState.Feeding);
                return (new Feed(herd.Id), 1);
            }
            return Perch(world, herd, mind); // circling flesh a pack is on
        }
        if (mind.State == HerdState.Feeding) Become(world, mind, HerdState.Resting);
        if (RaidRefusal(world, herd) == null && Roll(world, herd, RaidChancePerMinute)) return (new Raid(herd.Id), 1);
        if (StateMinutes(world, mind) % CarrionLookEveryMinutes != 0) return Wait(herd);
        if (Attractor(world, herd) is TileId goal)
        {
            if (goal == herd.Tile) return Perch(world, herd, mind); // over a predator or a fire: it waits on it
            if (mind.State != HerdState.Patrolling) Become(world, mind, HerdState.Patrolling);
            return TowardNearest(world, herd, [goal]) ?? Wait(herd);
        }
        if (mind.State != HerdState.Patrolling) Become(world, mind, HerdState.Patrolling);
        if (Roll(world, herd, CarrionDriftChance) && PatrolStep(world, herd, mind) is { } drift) return drift;
        // Nothing dead or killing in range: it forages where it is (berries, fungi, a fox's mice).
        return mind.Hunger > ForageWhenHungrier && GrazeRefusal(world, herd) == null ? (new Graze(herd.Id), 1) : Wait(herd);
    }

    /// Held where it is by what it follows: it perches (Resting) and waits, looking again on its round.
    static (Activity, int) Perch(World world, Agent herd, HerdMind mind)
    {
        if (mind.State != HerdState.Resting) Become(world, mind, HerdState.Resting);
        return Wait(herd);
    }

    /// The tile it is drawn to: the nearest flesh it can smell or see (a carcass, a dead person),
    /// else the nearest predator herd, else the nearest burning fire, within range. Ties go to the
    /// lower tile, so the choice is the same however the world is looked at.
    static TileId? Attractor(World world, Agent herd)
    {
        var flesh = MeatSensed(world, herd.Tile, CarrionRangeTiles);
        if (world.Tiles.GetValueOrDefault(herd.Tile)?.Carcasses != null && MeatHere(world, herd.Tile) > 0) flesh.Add(herd.Tile);
        var near = Nearby.Within(world, herd.Tile, CarrionRangeTiles);
        foreach (var a in near)
            if (a.Mind is PersonMind && a.Id != AgentId.Player && !Sim.IsAlive(a) && Remains.StageOf(world, a) == RemainsStage.Flesh) flesh.Add(a.Tile);
        if (Nearest(herd.Tile, flesh) is TileId food) return food;
        var predators = near.Where(a => a.Mind is HerdMind && IsPredator(a.Species) && Sim.IsAlive(a)).Select(a => a.Tile).ToHashSet();
        if (Nearest(herd.Tile, predators) is TileId predator) return predator;
        var fires = world.Fires.Values.Where(f => f.CarriedBy == null && Fire.Burns(Fire.Phase(f)) && MapGen.Manhattan(f.Tile, herd.Tile) <= CarrionRangeTiles)
            .Select(f => f.Tile).ToHashSet();
        return Nearest(herd.Tile, fires);
    }

    static TileId? Nearest(TileId from, HashSet<TileId> tiles) =>
        tiles.Count == 0 ? null : tiles.OrderBy(t => MapGen.Manhattan(t, from)).ThenBy(t => t).First();

    /// A living predator herd on the tile: it holds the carcass, so the flock waits overhead.
    static bool PredatorAt(World world, TileId tile) =>
        HerdsAt(world, tile).Any(h => IsPredator(h.Species) && Sim.IsAlive(h));

    /// The flock on this tile, circling or eating flesh there (a beacon), if any.
    public static Agent? FlockAt(World world, TileId tile) =>
        HerdsAt(world, tile).FirstOrDefault(h => BehaviorOf(h.Species) == HerdBehavior.Carrion && Sim.IsAlive(h));

    // ---- raiding a stash ----

    /// Why the flock cannot raid here: no food in the stash, someone alive keeping it, or Access.
    static string? RaidRefusal(World world, Agent herd)
    {
        if (BehaviorOf(herd.Species) != HerdBehavior.Carrion) return "only the carrion flock raids";
        if (Things.StashAt(world, herd.Tile) is not ThingRef stash) return "no stash here";
        var inv = world.Tiles[herd.Tile].Stash!;
        if (!RaidFood.Any(r => Items.Have(inv, r) > 0)) return "nothing in the stash it eats";
        if (Nearby.At(world, herd.Tile).Any(id => world.Agents[id] is { Mind: PersonMind } p && Sim.IsAlive(p))) return "someone is keeping it";
        return Access.Refusal(world, herd.Id, stash);
    }

    /// What a raid takes, in this order.
    static readonly Resource[] RaidFood = [Resource.RawMeat, Resource.RawFish, Resource.CookedMeat, Resource.CookedFish, Resource.DriedMeat];

    static void RaidMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        double want = RaidKgPerMember * herd.Members.Count, taken = 0;
        foreach (var r in RaidFood)
            if (want > taken) taken += Items.TakeFromStash(world, herd.Tile, r, want - taken);
        if (taken <= 0) return;
        mind.Hunger = Math.Max(0, mind.Hunger - taken / Math.Max(1, MassKg(herd) * 0.03));
        mind.LastFedMinute = world.Minute;
    }
}
