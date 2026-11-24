namespace Survival;

/// Ravens and foxes (Opportunist) eat forage, piles and carrion on the planner when hungry; these are the rules over it.
public static partial class Fauna
{
    public const int CarrionRangeTiles = 10;
    const int CarrionLookEveryMinutes = 10;
    /// Judgment: about one raid in eight hours of a camp left alone.
    const double RaidChancePerMinute = 0.002, RaidKgPerMember = 0.05;

    public static bool Flies(Species s) => SpeciesTable.Of(s).FlightMinutesPerTile != null;

    /// Ravens: an opportunist on the wing. The ravens' signs and the raid are theirs, not a fox's.
    public static bool IsRaven(Species s) => BehaviorOf(s) == HerdBehavior.Opportunist && Flies(s);

    /// A fox goes to no flesh a predator holds, here or seen from afar.
    static bool WalkerKeepsOff(World world, Agent herd, TileId tile) =>
        BehaviorOf(herd.Species) == HerdBehavior.Opportunist && !Flies(herd.Species) && PredatorAt(world, tile);

    /// An opportunist's rules: off a held kill, the raid, and on the look clock to an attractor, perching there.
    static (Activity, int)? Scavenge(World world, Agent herd, HerdMind mind, List<WorldEvent> events)
    {
        if (BehaviorOf(herd.Species) != HerdBehavior.Opportunist) return null;
        if (HeldKill(world, herd)) return KeepOff(world, herd, mind, events);
        if (CanFeed(world, herd)) return (new Feed(herd.Id), 1);
        if (RaidRefusal(world, herd) == null && Roll(world, herd, RaidChancePerMinute)) return (new Raid(herd.Id), 1);
        if (world.Minute % CarrionLookEveryMinutes != 0 || Attractor(world, herd) is not TileId goal) return null;
        return goal == herd.Tile ? Perch(herd) : TowardNearest(world, herd, [goal]);
    }

    static (Activity, int) KeepOff(World world, Agent herd, HerdMind mind, List<WorldEvent> events)
    {
        if (Flies(herd.Species)) return Perch(herd);
        StartFleeing(world, mind, herd.Tile);
        return Flee(world, herd, mind, events);
    }

    /// A scavenger (an opportunist, a hyena) at meat a predator of another kind is on gives the kill up to it.
    static bool HeldKill(World world, Agent herd) =>
        BehaviorOf(herd.Species) is HerdBehavior.Opportunist or HerdBehavior.Scavenger && CanFeed(world, herd) && PredatorAt(world, herd.Tile, herd.Species);

    static (Activity, int) Perch(Agent herd) => Wait(herd);

    /// Flesh, else (a flyer's) a predator herd, else a burning fire; a walker shuns held flesh. Ties go to the lower tile.
    static TileId? Attractor(World world, Agent herd)
    {
        bool flies = Flies(herd.Species);
        var flesh = MeatSensed(world, herd.Tile, CarrionRangeTiles);
        if (world.Tiles.GetValueOrDefault(herd.Tile)?.Carcasses != null && MeatHere(world, herd.Tile) > 0) flesh.Add(herd.Tile);
        var near = Nearby.Within(world, herd.Tile, CarrionRangeTiles);
        foreach (var a in near)
            if (a.Mind is PersonMind && a.Id != AgentId.Player && !Sim.IsAlive(a) && Remains.StageOf(world, a) == RemainsStage.Flesh) flesh.Add(a.Tile);
        flesh.RemoveWhere(t => WalkerKeepsOff(world, herd, t));
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

    static bool PredatorAt(World world, TileId tile, Species? other = null) =>
        AnimalsAt(world, tile).Any(h => IsPredator(h.Species) && h.Species != other && Sim.IsAlive(h));

    public static Agent? RavensAt(World world, TileId tile) =>
        AnimalsAt(world, tile).FirstOrDefault(h => IsRaven(h.Species) && Sim.IsAlive(h));

    /// The ravens' raid, the carrion flock's unchanged: #124 replaces it.
    static Refusal? RaidRefusal(World world, Agent herd)
    {
        if (!IsRaven(herd.Species)) return new("only ravens raid");
        if (LocationFeatures.PileAt(world, herd.Tile) is not LocationFeature pile) return new("no pile here");
        var inv = world.Tiles[herd.Tile].Pile!;
        if (!RaidFood.Any(r => Items.Have(inv, r) > 0)) return new("nothing in the pile it eats");
        if (Nearby.At(world, herd.Tile).Any(p => p.Mind is PersonMind && Sim.IsAlive(p))) return new("someone is keeping it");
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
        Nourish(herd, kcal);
        mind.LastFedMinute = world.Minute;
    }
}
