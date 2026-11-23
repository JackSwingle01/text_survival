namespace Survival;

/// The carrion flock (Species.Raven: ravens, a fox and a wolverine folded into one kind,
/// emergent-world-design §4). It hunts nothing and guards nothing. Every interaction goes through the owning system (Carcasses.EatMeat,
/// Remains.Pick, Items.TakeFromStash) via Access. Its signs (Tags.Beacon, RavensOver) are derived.
public static partial class Fauna
{
    public const int CarrionRangeTiles = 10;
    /// Feeding, it looks only at its own tile.
    const int CarrionLookEveryMinutes = 10;
    /// Judgment: about one raid in eight hours of a camp left alone.
    const double RaidChancePerMinute = 0.002, RaidKgPerMember = 0.05;
    const double CarrionDriftChance = 0.05, ForageWhenHungrier = 0.5;

    static (Activity, int) DecideCarrion(World world, Agent herd, HerdMind mind)
    {
        if (CanFeed(world, herd))
        {
            if (!PredatorAt(world, herd.Tile))
            {
                if (mind.State != HerdState.Feeding) Become(world, mind, HerdState.Feeding);
                return (new Feed(herd.Id), 1);
            }
            return Perch(world, herd, mind);
        }
        if (mind.State == HerdState.Feeding) Become(world, mind, HerdState.Resting);
        if (RaidRefusal(world, herd) == null && Roll(world, herd, RaidChancePerMinute)) return (new Raid(herd.Id), 1);
        if (StateMinutes(world, mind) % CarrionLookEveryMinutes != 0) return Wait(herd);
        if (Attractor(world, herd) is TileId goal)
        {
            if (goal == herd.Tile) return Perch(world, herd, mind);
            if (mind.State != HerdState.Patrolling) Become(world, mind, HerdState.Patrolling);
            return TowardNearest(world, herd, [goal]) ?? Wait(herd);
        }
        if (mind.State != HerdState.Patrolling) Become(world, mind, HerdState.Patrolling);
        if (Roll(world, herd, CarrionDriftChance) && PatrolStep(world, herd, mind) is { } drift) return drift;
        return Hunger(herd) > ForageWhenHungrier && GrazeRefusal(world, herd) == null ? (new Graze(herd.Id), 1) : Wait(herd);
    }

    static (Activity, int) Perch(World world, Agent herd, HerdMind mind)
    {
        if (mind.State != HerdState.Resting) Become(world, mind, HerdState.Resting);
        return Wait(herd);
    }

    /// Flesh, else predator herd, else burning fire. Ties go to the lower tile so the choice does not
    /// depend on iteration order.
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

    /// A predator herd holds the carcass, so the flock waits overhead until it has gone.
    static bool PredatorAt(World world, TileId tile) =>
        AnimalsAt(world, tile).Any(h => IsPredator(h.Species) && Sim.IsAlive(h));

    public static Agent? FlockAt(World world, TileId tile) =>
        AnimalsAt(world, tile).FirstOrDefault(h => BehaviorOf(h.Species) == HerdBehavior.Carrion && Sim.IsAlive(h));

    static Refusal? RaidRefusal(World world, Agent herd)
    {
        if (BehaviorOf(herd.Species) != HerdBehavior.Carrion) return new("only the carrion flock raids");
        if (LocationFeatures.StashAt(world, herd.Tile) is not LocationFeature stash) return new("no stash here");
        var inv = world.Tiles[herd.Tile].Stash!;
        if (!RaidFood.Any(r => Items.Have(inv, r) > 0)) return new("nothing in the stash it eats");
        if (Nearby.At(world, herd.Tile).Any(id => world.Agents[id] is { Mind: PersonMind } p && Sim.IsAlive(p))) return new("someone is keeping it");
        return Access.Refusal(world, herd.Id, stash);
    }

    static readonly Resource[] RaidFood = [Resource.RawMeat, Resource.RawFish, Resource.CookedMeat, Resource.CookedFish, Resource.DriedMeat];

    static void RaidMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        double want = RaidKgPerMember * herd.Members.Count, taken = 0, kcal = 0;
        foreach (var r in RaidFood)
            if (want > taken)
            {
                double kg = Items.TakeFromStash(world, herd.Tile, r, want - taken);
                taken += kg;
                kcal += kg * Items.Resources[r].KcalPerKg;
            }
        if (taken <= 0) return;
        Nourish(herd, kcal); // what it carries off beyond its room is spoiled and scattered
        mind.LastFedMinute = world.Minute;
    }
}
