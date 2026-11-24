namespace Survival;

/// What holds animals that each decide alone together as herds: decision record "Animals are individuals".
public static partial class Fauna
{
    const double SeededFamiliarity = 1.0;
    /// Familiar enough to be kin (a closed group's company, its side, its pack); under it, a stranger and a rival.
    public const double KinFamiliarity = 0.25;
    /// A look together takes this share of what is left to know: near full within a day on one tile.
    const double FamiliarGrowthPerLook = 0.02;
    /// A look apart keeps this much: halved in about 30 days apart, a stranger in about 60.
    const double FamiliarFadePerLook = 0.99984;
    const double ForgottenFamiliarity = 0.01;
    const int KinSenseTiles = 8;
    const int KinLookEveryMinutes = 10;

    public static bool IsClosedGroup(Species s) => SpeciesTable.Of(s).Group == Group.Closed;

    public static double Familiarity(Agent animal, AgentId other) =>
        animal.Mind is HerdMind { Familiar: { } known } && known.TryGetValue(other, out var level) ? level : 0;

    /// A living animal of its species, known to it if its group is closed. Combat.Side is its kin on its tile.
    public static bool IsKin(Agent animal, Agent other) =>
        other.Id != animal.Id && other.Species == animal.Species && animal.Mind is HerdMind && other.Mind is HerdMind
        && (!IsClosedGroup(animal.Species) || Familiarity(animal, other.Id) >= KinFamiliarity) && Sim.IsAlive(other);

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

    /// Each look it knows those it knows on its tile better, the rest less; strangers stay strangers, so rival packs never merge.
    static void Acquainting(World world, Agent animal)
    {
        if (world.Minute % KinLookEveryMinutes != 0 || animal.Mind is not HerdMind { Familiar: { Count: > 0 } known }) return;
        List<AgentId>? forgotten = null;
        List<(AgentId, double)>? changed = null;
        foreach (var (id, level) in known)
        {
            double next = world.Agents.TryGetValue(id, out var other) && other.Tile == animal.Tile
                ? level + (1 - level) * FamiliarGrowthPerLook : level * FamiliarFadePerLook;
            if (next < ForgottenFamiliarity) (forgotten ??= []).Add(id);
            else if (next != level) (changed ??= []).Add((id, next));
        }
        foreach (var (id, level) in changed ?? []) known[id] = level;
        foreach (var id in forgotten ?? []) known.Remove(id);
    }

    /// One attacking another: whatever they knew of each other is gone, both ways.
    internal static void Fought(World world, IEnumerable<AgentId> one, IEnumerable<AgentId> other)
    {
        var them = other.ToList();
        foreach (var a in one)
            foreach (var b in them)
            {
                if (world.Agents.TryGetValue(a, out var x) && x.Mind is HerdMind { Familiar: { } ofX }) ofX.Remove(b);
                if (world.Agents.TryGetValue(b, out var y) && y.Mind is HerdMind { Familiar: { } ofY }) ofY.Remove(a);
            }
    }

    internal static void Forget(World world, AgentId gone)
    {
        foreach (var a in world.Agents.Values)
            if (a.Mind is HerdMind { Familiar: { } known }) known.Remove(gone);
    }

    static bool SideFoughtThisMinute(World world, Agent animal) =>
        Combat.Side(world, animal).Any(a => MindOf(a).LastEncounterMinute == world.Minute);

    /// Before its role's rules: kin leaving its tile take it with them (the interrupt); alone, it waits for kin
    /// coming to it or goes to the nearest it can make out. Feeding and hunting hold it.
    static (Activity, int)? Cohere(World world, Agent animal, HerdMind mind)
    {
        if (SpeciesTable.Of(animal.Species).Cohesion <= 0 || mind.Choice is Feed or Hunt || !CanMove(animal)) return null;
        if (Following(world, animal) is ({ } going, _)) return (new Travel(animal.Id, going), UntilArrived);
        if (SideCount(world, animal) > 1 || world.Minute % KinLookEveryMinutes != 0) return null;
        var near = Nearby.Within(world, animal.Tile, KinSenseTiles).Where(k => IsKin(animal, k)).ToList();
        if (near.Count == 0) return null;
        if (near.Any(k => k.Crossing is { } c && Map.Step(c.From, c.Dir) == animal.Tile && k.Tile == c.From)) return Wait(animal);
        return TowardNearest(world, animal, near.Select(k => k.Tile).ToHashSet());
    }

    static bool WithKin(World world, Agent animal) => IsPrey(animal.Species) ? KinAt(world, animal, animal.Tile) : SideCount(world, animal) > 1;

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
}
