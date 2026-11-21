using System.Text.Json.Serialization;

namespace Survival;

// ---- what a person believes (plan decision 7) ----

/// A person's beliefs about the world: the player's and an NPC's are one structure, kept by one set
/// of rules (Knowledge). What hidden pools they have found, the search put into the rest, every tile
/// they have seen with anything worth remembering on it, and the animals and people they have seen.
/// Never copies of world objects: a summary of what was notable, as it was when seen.
public sealed class Beliefs
{
    public List<PoolId> Discovered { get; init; } = [];
    /// Perception-weighted hours spent searching for each hidden pool still unfound.
    /// Sorted for the same reason as Inventory.Kg: found pools are removed from it.
    public SortedDictionary<PoolId, double> SearchHours { get; init; } = [];
    /// Tiles stood on, and tiles seen with something notable (trees, open water, a fire, a shelter,
    /// a den). A bare tile seen from a distance is not kept: the save stays small.
    public SortedDictionary<TileId, TileMemory> Places { get; init; } = [];
    /// The last place each animal group or person was seen (old PredatorObservations, for every
    /// agent, not only predators). Forgotten after Knowledge.SightingMemoryMinutes.
    public SortedDictionary<AgentId, Sighting> Sightings { get; init; } = [];
}

/// What one tile looked like when last seen. From a distance a person sees the trees, open water,
/// the fire's flame or smoke, a shelter and a den; standing on it, everything (VisitedMinute).
public sealed class TileMemory
{
    public long SeenMinute { get; set; }
    /// When they last stood here and took it all in: what a receipt counts from (Knowledge.Receipt).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? VisitedMinute { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Trees { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Water { get; set; }
    /// How picked-over the ground was, as of VisitedMinute (old NPC ResourceMemory). 0 with no forage.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? ForageDensity { get; set; }
    /// The pit's phase as seen; null when there was no pit.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public FirePhase? Fire { get; set; }
    /// Hours of fire in what was on the pit (Fire.FuelHours), as of VisitedMinute: only someone beside
    /// it can judge the fuel. Null when it was not burning. From a distance (Knowledge.Glance) a person
    /// sees whether it burns, not how much is on it: a fire seen out has none left (null), and a fire seen
    /// burning keeps the reckoning from beside it, which whoever reads it ages from VisitedMinute.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? FireHours { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SeenCarcass>? Carcasses { get; set; }
    /// The stash by resource, to a tenth of a kilo.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SortedDictionary<Resource, double>? Stash { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Shelter { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public Danger Danger { get; set; }
    [JsonIgnore] public bool Visited => VisitedMinute != null;
}

public readonly record struct SeenCarcass(int Id, string Animal);

/// An animal group or person, seen on Tile at Minute: how many, and of what kind.
public sealed record Sighting(TileId Tile, Species Species, int Count, long Minute);

/// What a person learned to fear about a place.
[Flags]
public enum Danger
{
    None = 0,
    /// It was hurt here (an Injured event of its own).
    Hurt = 1,
    /// A predator's den: the first tile of a predator herd's land.
    Den = 2,
}

// ---- what a receipt says ----

public enum ChangeKind { Fire, Stash, Carcass, Shelter }

/// One difference between memory and what is there now. Was or Now is null for something that
/// appeared or went: a carcass, a shelter, a resource in the stash, a fire pit.
public sealed record Change(ChangeKind Kind, string Subject, string? Was, string? Now);

/// "What happened here since you last saw it" (short-loop plan, feature 4). Happened is the tile's
/// journal over the time away, only at camp, where allies tell you everything (Full). Changes are
/// memory against what is there now; Signs are prints made since, still readable. In the field there
/// is no one to tell you: only the changes and the signs, and old or snowed-over prints tell nothing.
public sealed record Receipt(AgentId Who, TileId Tile, long? SinceMinute, bool Full,
    IReadOnlyList<WorldEvent> Happened, IReadOnlyList<Change> Changes, IReadOnlyList<TrackSign> Signs)
{
    [JsonIgnore] public bool IsEmpty => Happened.Count == 0 && Changes.Count == 0 && Signs.Count == 0;
}

/// What a person can see from where they stand. A fixed radius for now; the old sight rays
/// (terrain, weather, hills) replace it here, behind this one function.
public static class Sight
{
    public const int RadiusTiles = 5;

    /// Every tile in sight, the agent's own included, in a fixed order.
    public static IEnumerable<TileId> Visible(World world, Agent agent)
    {
        var m = Map.Of(world);
        var at = agent.Tile;
        for (int dx = -RadiusTiles; dx <= RadiusTiles; dx++)
            for (int dy = Math.Abs(dx) - RadiusTiles; dy <= RadiusTiles - Math.Abs(dx); dy++)
            {
                var t = new TileId(at.X + dx, at.Y + dy);
                if (m.InBounds(t)) yield return t;
            }
    }

    public static bool InSight(World world, Agent agent, TileId tile) =>
        MapGen.Manhattan(agent.Tile, tile) <= RadiusTiles && Map.Of(world).InBounds(tile);
}

/// The one set of rules every person's beliefs are kept by. Memory changes only by looking: the
/// player looks every minute they are awake (Witness), an NPC whenever it decides (Observe). Asleep
/// or away, memory stands still, and the map drawn from it shows things as last seen.
public static class Knowledge
{
    /// How often a person awake looks around at the tiles in sight, on the clock. They also look
    /// on arriving somewhere or waking. The tile underfoot is taken in every minute.
    public const int GlanceMinutes = 15;
    public const long SightingMemoryMinutes = 3 * 1440;

    public static Beliefs Of(Agent agent) =>
        (agent.Mind as PersonMind ?? throw new ArgumentException($"{agent.Id} is not a person and remembers nothing", nameof(agent))).Knows;

    /// The camp a person reports back to: where allies tell everything they did.
    public static TileId CampOf(World world, Agent agent) => agent.Mind switch
    {
        NpcMind npc => npc.Camp,
        PlayerMind => Map.Of(world).Camp,
        _ => throw new ArgumentException($"{agent.Id} is not a person and has no camp", nameof(agent)),
    };

    /// The player's minute of looking, while awake: the fires in reach caught up, a look around when
    /// due, the minute's events filed, and the tile underfoot taken in. Coming back to a tile it
    /// remembers (or waking on one) with something to tell, it adds a Noticed event with the receipt.
    public static void Witness(World world, Agent agent, List<WorldEvent> events, ref int filed)
    {
        var mem = Of(agent).Places.GetValueOrDefault(agent.Tile);
        bool back = mem?.VisitedMinute != world.Minute - 1;
        bool knew = mem?.VisitedMinute != null;
        Fire.CatchUpAround(world, agent, events);
        if (back || world.Minute % GlanceMinutes == 0) Glance(world, agent, events);
        Journal.File(world, events, ref filed); // what catching up found is in the journal before the receipt reads it
        var receipt = Receipt(world, agent, agent.Tile);
        if (back && knew && !receipt.IsEmpty) events.Add(new Noticed(world.Minute, agent.Id, agent.Tile, receipt));
    }

    /// An NPC's look as it decides: around it, then the tile underfoot. What it would be told is
    /// not reported: nothing reads an NPC's receipts yet.
    public static void Observe(World world, Agent agent, List<WorldEvent> events)
    {
        Fire.CatchUpAround(world, agent, events);
        Glance(world, agent, events);
        Receipt(world, agent, agent.Tile);
    }

    /// Looks at every tile in sight but the one underfoot: what can be told from a distance.
    /// Fire pits in sight are caught up first, so the flame seen is the flame there now.
    public static void Glance(World world, Agent agent, List<WorldEvent> events)
    {
        var beliefs = Of(agent);
        var m = Map.Of(world);
        long now = world.Minute;
        var pits = new Dictionary<TileId, FireId>();
        foreach (var (id, f) in world.Fires.ToList())
            if (f.CarriedBy == null && f.Tile != agent.Tile && Sight.InSight(world, agent, f.Tile))
            {
                Fire.CatchUp(world, id, events);
                pits.TryAdd(f.Tile, id);
            }
        var dens = Dens(world);
        foreach (var t in Sight.Visible(world, agent))
        {
            if (t == agent.Tile) continue;
            bool trees = Trees(world, t), water = m.TerrainAt(t).IsPassable() && Work.HasOpenWater(world, t), den = dens.Contains(t);
            FirePhase? fire = pits.TryGetValue(t, out var pit) ? Fire.Phase(world.Fires[pit]) : null;
            string? shelter = Shelters.Peek(world, t)?.Name;
            var mem = beliefs.Places.GetValueOrDefault(t);
            if (mem == null && !trees && !water && !den && fire == null && shelter == null) continue;
            if (mem == null) beliefs.Places[t] = mem = new TileMemory();
            mem.SeenMinute = now;
            (mem.Trees, mem.Water, mem.Fire, mem.Shelter) = (trees, water, fire, shelter);
            if (!Fire.Burns(fire)) mem.FireHours = null;
            mem.Danger = den ? mem.Danger | Danger.Den : mem.Danger & ~Danger.Den;
        }
        foreach (var other in world.Agents.Values)
        {
            if (other.Id == agent.Id || !Sight.InSight(world, agent, other.Tile)) continue;
            int alive = other.Members.Count(Body.IsAlive);
            if (alive > 0) beliefs.Sightings[other.Id] = new Sighting(other.Tile, other.Species, alive, now);
        }
        foreach (var (id, s) in beliefs.Sightings.ToList())
            if (now - s.Minute > SightingMemoryMinutes) beliefs.Sightings.Remove(id);
    }

    /// What changed on this tile since the person last stood on it, and then their memory of it,
    /// brought up to now. The tile is the one they stand on, and its fires are caught up.
    /// Coming back for the first time there is nothing to compare: the receipt is empty.
    public static Receipt Receipt(World world, Agent agent, TileId tile)
    {
        if (tile != agent.Tile) throw new ArgumentException($"{agent.Id} stands on {agent.Tile}, not {tile}: a receipt is for where you stand", nameof(tile));
        var beliefs = Of(agent);
        long now = world.Minute;
        var mem = beliefs.Places.GetValueOrDefault(tile);
        long? since = mem?.VisitedMinute;

        // The tile as it is now.
        var pitId = Fire.PitAt(world, tile);
        var pit = pitId is FireId pid ? world.Fires[pid] : null;
        if (pit != null && pit.LastUpdatedMinute != now) throw new InvalidOperationException($"The fire on {tile} is at minute {pit.LastUpdatedMinute}: catch it up first");
        FirePhase? fire = pit != null ? Fire.Phase(pit) : null;
        var state = world.Tiles.GetValueOrDefault(tile);
        var carcasses = state?.Carcasses is { Count: > 0 } cs ? cs.Select(c => new SeenCarcass(c.Id, c.Animal)).OrderBy(c => c.Id).ToList() : null;
        SortedDictionary<Resource, double>? stash = null;
        if (state?.Stash is { } st)
            foreach (var (r, kg) in st.Kg)
                if (Math.Round(kg, 1) > 0) (stash ??= [])[r] = Math.Round(kg, 1);
        string? shelter = Shelters.Peek(world, tile)?.Name;

        var happened = new List<WorldEvent>();
        var changes = new List<Change>();
        var signs = new List<TrackSign>();
        bool full = tile == CampOf(world, agent);
        if (mem != null && since is long s)
        {
            if (mem.Fire != fire) changes.Add(new Change(ChangeKind.Fire, "fire", mem.Fire?.ToString(), fire?.ToString()));
            foreach (var r in (mem.Stash?.Keys ?? Enumerable.Empty<Resource>()).Union(stash?.Keys ?? Enumerable.Empty<Resource>()).Order())
            {
                double was = mem.Stash?.GetValueOrDefault(r) ?? 0, nowKg = stash?.GetValueOrDefault(r) ?? 0;
                if (Math.Abs(was - nowKg) >= 0.1 - 1e-9)
                    changes.Add(new Change(ChangeKind.Stash, r.ToString(), was > 0 ? Kg(was) : null, nowKg > 0 ? Kg(nowKg) : null));
            }
            var before = mem.Carcasses ?? [];
            var after = carcasses ?? [];
            foreach (var c in before.Where(c => after.All(a => a.Id != c.Id))) changes.Add(new Change(ChangeKind.Carcass, c.Animal, "there", null));
            foreach (var c in after.Where(c => before.All(b => b.Id != c.Id))) changes.Add(new Change(ChangeKind.Carcass, c.Animal, null, "there"));
            if (mem.Shelter != shelter) changes.Add(new Change(ChangeKind.Shelter, "shelter", mem.Shelter, shelter));
            // Only the time away: what happened this minute, they saw happen.
            if (full) happened.AddRange(Journal.Between(world, tile, s, now));
            // People's prints are left out: yours and another's are the same mark.
            signs.AddRange(Tracks.Read(world, tile).Where(x => x.Maker != TrackMaker.Human && x.AgeMinutes < now - s));
        }

        if (mem == null) beliefs.Places[tile] = mem = new TileMemory();
        var pools = Pools.At(world, tile);
        mem.SeenMinute = now;
        mem.VisitedMinute = now;
        mem.Trees = Trees(world, tile);
        mem.Water = Work.HasOpenWater(world, tile);
        mem.ForageDensity = pools.Count > 0 && pools[0] is ForagePool ? Pools.ForageDensity(world, new PoolId(tile, 0)) : 0;
        mem.Fire = fire;
        mem.FireHours = pit != null && Fire.IsActive(pit) ? Math.Round(Fire.FuelHours(pit), 1) : null;
        mem.Carcasses = carcasses;
        mem.Stash = stash;
        mem.Shelter = shelter;
        mem.Danger = Dens(world).Contains(tile) ? mem.Danger | Danger.Den : mem.Danger & ~Danger.Den;
        return new Receipt(agent.Id, tile, since, full, happened, changes, signs);
    }

    /// The person was hurt here: a place to fear (from the event stream, Journal.File).
    internal static void Hurt(Agent agent, TileId tile, long minute)
    {
        var beliefs = Of(agent);
        if (!beliefs.Places.TryGetValue(tile, out var mem)) beliefs.Places[tile] = mem = new TileMemory { SeenMinute = minute };
        mem.Danger |= Danger.Hurt;
    }

    /// Standing timber on the tile (old NPC memory: a stand of trees is plain to see from a distance).
    static bool Trees(World world, TileId t)
    {
        var pools = Pools.At(world, t);
        for (int i = 0; i < pools.Count; i++)
            if (pools[i] is TreePool && Pools.HasTrees(world, new PoolId(t, i))) return true;
        return false;
    }

    static HashSet<TileId> Dens(World world) =>
        world.Agents.Values.Where(a => a.Mind is HerdMind { Territory.Count: > 0 } && Fauna.IsPredator(a.Species))
            .Select(a => ((HerdMind)a.Mind).Territory[0]).ToHashSet();

    static string Kg(double kg) => kg.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
}

/// A bounded per-tile list of world events, kept only for tiles someone has a stake in: a camp, a
/// fire pit, a stash, a shelter or project of their own making, a carcass a person knows of.
/// Written from the event stream, a minute at a time (Sim), so how steps are chunked cannot change it.
public static class Journal
{
    public const int MaxEntriesPerTile = 32;
    public const long KeepMinutes = 7 * 1440;

    /// What is worth telling: things that happened to the place, not a body's own changes, a walk
    /// through, or a step's tally (which depends on how the work was chunked into steps).
    public static bool Files(WorldEvent e) => e is not (Moved or PlanRefused or EffectStarted or EffectEnded or Exhausted
        or FullyRested or Gathered or Dropped or Butchered or Drank or SkillLeveledUp or Noticed);

    /// Files events[filed..] and moves filed past them. Once a day it forgets what is older than KeepMinutes.
    public static void File(World world, List<WorldEvent> events, ref int filed)
    {
        for (; filed < events.Count; filed++)
        {
            var e = events[filed];
            if (e is Injured hurt && world.Agents.TryGetValue(hurt.Who, out var who) && who.Mind is PersonMind)
                Knowledge.Hurt(who, hurt.Tile, hurt.Minute);
            if (!Files(e) || !Staked(world, e.Tile)) continue;
            if (!world.Journal.TryGetValue(e.Tile, out var list)) world.Journal[e.Tile] = list = [];
            list.Add(e);
            if (list.Count > MaxEntriesPerTile) list.RemoveAt(Evicted(list));
        }
        if (world.Minute % 1440 == 0 && world.Journal.Count > 0) Forget(world);
    }

    /// Chores: what someone does at a fire or a stash over and over through a night. They make
    /// way for anything else, so a night of feeding the fire cannot push out the fire going out.
    public static bool IsRoutine(WorldEvent e) => e is FuelAdded or Stashed or Unstashed;

    /// Which entry a full journal lets go: the earliest-happened chore, or failing that the
    /// earliest-happened entry. By minute, not by filing order: catch-up files what happened
    /// hours ago after what happened just now. Ties go to the first filed.
    static int Evicted(List<WorldEvent> list)
    {
        int oldest = -1;
        bool routine = list.Any(IsRoutine);
        for (int i = 0; i < list.Count; i++)
            if ((!routine || IsRoutine(list[i])) && (oldest < 0 || list[i].Minute < list[oldest].Minute)) oldest = i;
        return oldest;
    }

    /// The tile's entries strictly after one minute and strictly before another, in the order filed.
    public static IReadOnlyList<WorldEvent> Between(World world, TileId tile, long afterMinute, long beforeMinute) =>
        world.Journal.TryGetValue(tile, out var list) ? list.Where(e => e.Minute > afterMinute && e.Minute < beforeMinute).ToList() : [];

    public static bool Staked(World world, TileId tile)
    {
        if (tile == Map.Of(world).Camp || Fire.PitAt(world, tile) != null || world.Projects.ContainsKey(tile)) return true;
        if (world.Tiles.TryGetValue(tile, out var state) && state.Stash is { } stash && (stash.Kg.Count > 0 || stash.Gear.Count > 0)) return true;
        if (world.Shelters.TryGetValue(tile, out var shelter) && !Shelters.Kinds[shelter.Kind].IsNatural) return true;
        foreach (var a in world.Agents.Values)
        {
            if (a.Mind is not PersonMind p) continue;
            if (a.Mind is NpcMind npc && npc.Camp == tile) return true;
            if (p.Knows.Places.TryGetValue(tile, out var mem) && mem.Carcasses is { Count: > 0 }) return true;
        }
        return false;
    }

    static void Forget(World world)
    {
        long cutoff = world.Minute - KeepMinutes;
        foreach (var (tile, list) in world.Journal.ToList())
        {
            list.RemoveAll(e => e.Minute < cutoff);
            if (list.Count == 0) world.Journal.Remove(tile);
        }
    }
}
