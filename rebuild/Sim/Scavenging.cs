namespace Survival;

/// Ravens and foxes (HerdBehavior.Opportunist) eat forage, piles and carrion (carcass meat, a dead person's flesh), on
/// the planner (Fauna.Needs) when hungry; these are the rules over it. Every take goes through its owning system via Access.
public static partial class Fauna
{
    public const int CarrionRangeTiles = 10;
    const int CarrionLookEveryMinutes = 10;
    /// Judgment: about one raid in eight hours of a camp left alone.
    const double RaidChancePerMinute = 0.002, RaidKgPerMember = 0.05;
    const double CarrionDriftChance = 0.05, ForageWhenHungrier = 0.5;

    public static bool Flies(Species s) => SpeciesTable.Of(s).FlightMinutesPerTile != null;

    static (Activity, int) DecideOpportunist(World world, Agent herd, HerdMind mind, List<WorldEvent> events)
    {
        if (CanFeed(world, herd))
        {
            if (!PredatorAt(world, herd.Tile))
            {
                if (mind.State != HerdState.Feeding) Become(world, mind, HerdState.Feeding);
                return (new Feed(herd.Id), 1);
            }
            return KeepOff(world, herd, mind, events);
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

    /// A flyer perches over a kill a predator holds until it has gone; a walker gets off the tile.
    static (Activity, int) KeepOff(World world, Agent herd, HerdMind mind, List<WorldEvent> events)
    {
        if (Flies(herd.Species)) return Perch(world, herd, mind);
        StartFleeing(world, mind, herd.Tile);
        return Flee(world, herd, mind, HerdBehavior.Opportunist, events);
    }

    static bool HeldKill(World world, Agent herd) =>
        BehaviorOf(herd.Species) == HerdBehavior.Opportunist && CanFeed(world, herd) && PredatorAt(world, herd.Tile);

    static (Activity, int) Perch(World world, Agent herd, HerdMind mind)
    {
        if (mind.State != HerdState.Resting) Become(world, mind, HerdState.Resting);
        return Wait(herd);
    }

    /// Flesh, else (a flyer's) a predator herd, else a burning fire; a walker shuns held flesh. Ties go to the lower tile.
    static TileId? Attractor(World world, Agent herd)
    {
        bool flies = Flies(herd.Species);
        var flesh = MeatSensed(world, herd.Tile, CarrionRangeTiles);
        if (world.Tiles.GetValueOrDefault(herd.Tile)?.Carcasses != null && MeatHere(world, herd.Tile) > 0) flesh.Add(herd.Tile);
        var near = Nearby.Within(world, herd.Tile, CarrionRangeTiles);
        foreach (var a in near)
            if (a.Mind is PersonMind && a.Id != AgentId.Player && !Sim.IsAlive(a) && Remains.StageOf(world, a) == RemainsStage.Flesh) flesh.Add(a.Tile);
        if (!flies) flesh.RemoveWhere(t => PredatorAt(world, t));
        if (Nearest(herd.Tile, flesh) is TileId food) return food;
        if (!flies) return null;
        var predators = near.Where(a => a.Mind is HerdMind && IsPredator(a.Species) && Sim.IsAlive(a)).Select(a => a.Tile).ToHashSet();
        if (Nearest(herd.Tile, predators) is TileId predator) return predator;
        var fires = world.Fires.Values.Where(f => f.CarriedBy == null && Fire.Burns(Fire.Phase(f)) && MapGen.Manhattan(f.Tile, herd.Tile) <= CarrionRangeTiles)
            .Select(f => f.Tile).ToHashSet();
        return Nearest(herd.Tile, fires);
    }

    static TileId? Nearest(TileId from, HashSet<TileId> tiles) =>
        tiles.Count == 0 ? null : tiles.OrderBy(t => MapGen.Manhattan(t, from)).ThenBy(t => t).First();

    static bool PredatorAt(World world, TileId tile) =>
        AnimalsAt(world, tile).Any(h => IsPredator(h.Species) && Sim.IsAlive(h));

    public static Agent? RavensAt(World world, TileId tile) =>
        AnimalsAt(world, tile).FirstOrDefault(h => BehaviorOf(h.Species) == HerdBehavior.Opportunist && Flies(h.Species) && Sim.IsAlive(h));

    /// The ravens' raid, the carrion flock's unchanged: #124 replaces it.
    static Refusal? RaidRefusal(World world, Agent herd)
    {
        if (BehaviorOf(herd.Species) != HerdBehavior.Opportunist || !Flies(herd.Species)) return new("only ravens raid");
        if (LocationFeatures.PileAt(world, herd.Tile) is not LocationFeature pile) return new("no pile here");
        var inv = world.Tiles[herd.Tile].Pile!;
        if (!RaidFood.Any(r => Items.Have(inv, r) > 0)) return new("nothing in the pile it eats");
        if (Nearby.At(world, herd.Tile).Any(id => world.Agents[id] is { Mind: PersonMind } p && Sim.IsAlive(p))) return new("someone is keeping it");
        return Access.Refusal(world, herd.Id, pile);
    }

    static readonly Resource[] RaidFood = [Resource.RawMeat, Resource.RawFish, Resource.CookedMeat, Resource.CookedFish, Resource.DriedMeat];

    static void RaidMinute(World world, Agent herd)
    {
        var mind = MindOf(herd);
        double want = RaidKgPerMember * herd.Members.Count, taken = 0, kcal = 0;
        foreach (var r in RaidFood)
            if (want > taken)
            {
                double kg = Items.TakeFromPile(world, herd.Tile, r, want - taken);
                taken += kg;
                kcal += kg * Items.Resources[r].KcalPerKg;
            }
        if (taken <= 0) return;
        Nourish(herd, kcal); // what it carries off beyond its room is spoiled and scattered
        mind.LastFedMinute = world.Minute;
    }
}
