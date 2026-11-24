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

    /// Its herd: it, then its kin on its tile in id order, whether or not a fight holds them (Combat.Side leaves those out).
    public static List<Agent> HerdOf(World world, Agent animal)
    {
        var herd = new List<Agent> { animal };
        foreach (var other in Nearby.At(world, animal.Tile))
            if (other.Tile == animal.Tile && IsKin(animal, other)) herd.Add(other);
        return herd;
    }

    /// The one rule for "one per herd": a group per herd and per person, each in id order.
    public static List<List<Agent>> GroupsAt(World world, TileId tile)
    {
        var groups = new List<List<Agent>>();
        var placed = new HashSet<AgentId>();
        foreach (var a in Nearby.At(world, tile))
        {
            if (placed.Contains(a.Id)) continue;
            var group = a.Mind is HerdMind && Sim.IsAlive(a) ? HerdOf(world, a).Where(k => !placed.Contains(k.Id)).ToList() : [a];
            placed.UnionWith(group.Select(k => k.Id));
            groups.Add(group);
        }
        return groups;
    }

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
        if (!IsClosedGroup(mother.Species)) return;
        var its = MindOf(young).Familiar = new SortedDictionary<AgentId, double>(MindOf(mother).Familiar ?? []) { [mother.Id] = SeededFamiliarity };
        MindOf(mother).Familiar ??= [];
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
        if (Following(world, animal) is ({ } going, { } lead))
        {
            // Where the one it follows is making for, so that when it leads in turn it leads them on, not back.
            var theirs = MindOf(lead);
            mind.Foray = theirs.Foray;
            if (theirs.Territory.Count > 0 && mind.Territory.IndexOf(theirs.Territory[theirs.TerritoryIndex]) is int at and >= 0) mind.TerritoryIndex = at;
            return (new Travel(animal.Id, going), UntilArrived);
        }
        if (SideCount(world, animal) > 1 || world.Minute % KinLookEveryMinutes != 0) return null;
        var near = Nearby.Within(world, animal.Tile, KinSenseTiles).Where(k => IsKin(animal, k)).ToList();
        if (near.Count == 0) return null;
        if (near.Any(k => k.Crossing is { } c && Map.Step(c.From, c.Dir) == animal.Tile && k.Tile == c.From)) return Wait(animal);
        return TowardNearest(world, animal, near.Select(k => k.Tile).ToHashSet());
    }

    static int SideCount(World world, Agent animal)
    {
        int count = 1;
        var here = Nearby.At(world, animal.Tile);
        for (int i = 0; i < here.Count; i++)
            if (Combat.OnSide(world, animal, here[i])) count++;
        return count;
    }

    /// Run every minute by every animal, so counted, not listed; the first way in id order wins a tie.
    static (Direction Way, Agent Lead)? Following(World world, Agent animal)
    {
        Span<int> going = stackalloc int[4];
        (Direction Way, Agent Lead)? best = null;
        static void Count(Span<int> going, ref (Direction Way, Agent Lead)? best, Direction dir, Agent other)
        {
            if (++going[(int)dir] > (best is var (b, _) ? going[(int)b] : 0)) best = (dir, best is var (d, l) && d == dir ? l : other);
        }
        var here = Nearby.At(world, animal.Tile);
        for (int i = 0; i < here.Count; i++)
        {
            var other = here[i];
            if (Leaving(other, animal.Tile) && Combat.OnSide(world, animal, other)) Count(going, ref best, other.Crossing!.Dir, other);
        }
        // Kin that just left (a flight ends within its own turn); those before it in id order see it a minute late.
        foreach (var n in MapGen.Neighbors(animal.Tile))
            foreach (var other in Nearby.At(world, n))
            {
                if (other.LastLeft is { } left && left.From == animal.Tile && left.Minute >= world.Minute - 1 && other.Tile == n
                    && IsKin(animal, other) && !Sim.Held(world, other.Id)) Count(going, ref best, left.Dir, other);
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
