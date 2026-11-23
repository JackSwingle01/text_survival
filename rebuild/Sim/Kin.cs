namespace Survival;

/// What holds animals that each decide alone together as herds: decision record "Animals are individuals".
public static partial class Fauna
{
    const double SeededFamiliarity = 1.0;
    const int KinSenseTiles = 8;
    const int KinLookEveryMinutes = 10;

    public static bool IsClosedGroup(Species s) => SpeciesTable.Of(s).Group == Group.Closed;

    public static double Familiarity(Agent animal, AgentId other) =>
        animal.Mind is HerdMind { Familiar: { } known } && known.TryGetValue(other, out var level) ? level : 0;

    /// A living animal of its species, known to it if its group is closed. Combat.Side is its kin on its tile.
    public static bool IsKin(Agent animal, Agent other) =>
        other.Id != animal.Id && other.Species == animal.Species && animal.Mind is HerdMind && other.Mind is HerdMind
        && (!IsClosedGroup(animal.Species) || Familiarity(animal, other.Id) > 0) && Sim.IsAlive(other);

    internal static void Acquaint(World world, IReadOnlyList<AgentId> ids)
    {
        if (ids.Count == 0 || !IsClosedGroup(world.Agent(ids[0]).Species)) return;
        foreach (var id in ids)
        {
            var known = MindOf(world.Agent(id)).Familiar ??= [];
            foreach (var other in ids)
                if (other != id) known[other] = SeededFamiliarity;
        }
    }

    /// A newborn knows whom its mother knows, as well as she does, and her; each of them knows it as they know her.
    static void Inherit(World world, Agent mother, Agent young)
    {
        if (MindOf(mother).Familiar is not { } hers) return;
        var its = MindOf(young).Familiar = new SortedDictionary<AgentId, double>(hers) { [mother.Id] = SeededFamiliarity };
        foreach (var (id, _) in its)
            if (world.Agents.TryGetValue(id, out var other) && other.Mind is HerdMind { Familiar: { } theirs })
                theirs[young.Id] = id == mother.Id ? SeededFamiliarity : Familiarity(other, mother.Id);
    }

    internal static void Forget(World world, AgentId gone)
    {
        foreach (var a in world.Agents.Values)
            if (a.Mind is HerdMind { Familiar: { } known }) known.Remove(gone);
    }

    static bool SideFoughtThisMinute(World world, Agent animal) =>
        Combat.Side(world, animal).Any(a => MindOf(a).LastEncounterMinute == world.Minute);

    /// Before its state is asked: kin leaving its tile take it with them (the interrupt); alone, it waits for kin
    /// coming to it or goes to the nearest it can make out. Feeding and hunting hold it.
    static (Activity, int)? Cohere(World world, Agent animal, HerdMind mind)
    {
        if (SpeciesTable.Of(animal.Species).Cohesion <= 0 || mind.State is HerdState.Feeding or HerdState.Hunting || !CanMove(animal)) return null;
        if (Following(world, animal) is Direction going) return (new Travel(animal.Id, going), UntilArrived);
        if (SideCount(world, animal) > 1 || world.Minute % KinLookEveryMinutes != 0) return null;
        var near = Nearby.Within(world, animal.Tile, KinSenseTiles).Where(k => IsKin(animal, k)).ToList();
        if (near.Count == 0) return null;
        if (near.Any(k => k.Crossing is { } c && Map.Step(c.From, c.Dir) == animal.Tile && k.Tile == c.From)) return Wait(animal);
        return TowardNearest(world, animal, near.Select(k => k.Tile).ToHashSet());
    }

    static int SideCount(World world, Agent animal)
    {
        int count = 1;
        foreach (var id in Nearby.At(world, animal.Tile))
            if (Combat.OnSide(world, animal, world.Agents[id])) count++;
        return count;
    }

    /// Run every minute by every animal, so counted, not listed; the first way in id order wins a tie.
    static Direction? Following(World world, Agent animal)
    {
        Span<int> going = stackalloc int[4];
        Direction? best = null;
        foreach (var id in Nearby.At(world, animal.Tile))
        {
            var other = world.Agents[id];
            if (!Leaving(other, animal.Tile) || !Combat.OnSide(world, animal, other)) continue;
            var dir = other.Crossing!.Dir;
            if (++going[(int)dir] > (best is Direction b ? going[(int)b] : 0)) best = dir;
        }
        return best;
    }

    static bool Leaving(Agent animal, TileId tile) => animal.Crossing is { } c && c.From == tile && animal.Tile == tile;

    /// The herd's drift chance, a share each; a step where no kin are or are going costs 1 + cohesion times as much.
    static (Activity, int)? Drift(World world, Agent animal, HerdMind mind, double chancePerMinute)
    {
        if (!Roll(world, animal, chancePerMinute / SideCount(world, animal))) return null;
        var step = PatrolStep(world, animal, mind);
        if (step is not ({ } activity, _) || activity is not Travel t) return step;
        var to = Map.Step(animal.Tile, t.Dir);
        bool kinThere = Nearby.Within(world, to, 1).Any(k => IsKin(animal, k) && (k.Tile == to && !Leaving(k, to) || k.Crossing is { } c && Map.Step(c.From, c.Dir) == to));
        double cohesion = SpeciesTable.Of(animal.Species).Cohesion;
        return kinThere || cohesion <= 0 || Roll(world, animal, 1 / (1 + cohesion)) ? step : null;
    }
}
