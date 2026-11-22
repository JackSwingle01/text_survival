using System.Text.Json.Serialization;

namespace Survival;

// ---- what a person believes ----

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
    /// The last place each animal group or person was seen, predator or not. Forgotten after
    /// Knowledge.SightingMemoryMinutes.
    public SortedDictionary<AgentId, Sighting> Sightings { get; init; } = [];
    /// Every tile ever in view (Sight.Visible), whether or not anything was there, with its ground as
    /// last seen: what the map draws as known rather than unexplored, and the snow, mud and ice it
    /// draws there out of sight. Null until the first look. Only grows.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public GroundMemory? Explored { get; set; }
    /// What is in view from where the person stands, with the budget it was cast with: the same tile
    /// and budget give the same view (terrain never changes), so it is cast again only when either
    /// moves. Never saved: after a load the first look casts it afresh, to the same tiles.
    internal SightView? View;
}

/// The tiles in view from one tile with one budget (Sight.ViewOf), and the ones among them that a
/// look always has something to note on: standing timber's pools and water to drink, which the ice season changes.
sealed class SightView(TileId from, double budgetTiles, bool iceSeason, IReadOnlyList<TileId> tiles)
{
    public bool IceSeason { get; } = iceSeason;
    public TileId From { get; } = from;
    public double BudgetTiles { get; } = budgetTiles;
    public IReadOnlyList<TileId> Tiles { get; } = tiles;
    public HashSet<TileId> Set { get; } = [.. tiles];
    /// Tiles in view with a tree pool or open water, in view order: where a look may note trees or water.
    public List<TileId>? TreesOrWater;
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
    /// How picked-over the ground was, as of VisitedMinute. 0 with no forage.
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

/// "What happened here since you last saw it". Happened is the tile's
/// journal over the time away, only at camp, where allies tell you everything (Full). Changes are
/// memory against what is there now; Signs are prints made since, still readable. In the field there
/// is no one to tell you: only the changes and the signs, and old or snowed-over prints tell nothing.
public sealed record Receipt(AgentId Who, TileId Tile, long? SinceMinute, bool Full,
    IReadOnlyList<WorldEvent> Happened, IReadOnlyList<Change> Changes, IReadOnlyList<TrackSign> Signs)
{
    [JsonIgnore] public bool IsEmpty => Happened.Count == 0 && Changes.Count == 0 && Signs.Count == 0;
}

/// What a person can see from where they stand: the sight rays.
/// One model for everything that sees: the explored map, place memory, sightings and NPC decisions
/// (InSight) all read the same view.
public static class Sight
{
    /// The sight budget over open ground, in tiles; a vantage adds up to MaxVantageBonusTiles more.
    public const int OpenGroundTiles = 11, MaxVantageBonusTiles = 8, CaveTiles = 6;
    /// The farthest anyone can see: open ground from the best vantage, on a clear day.
    public const int MaxTiles = OpenGroundTiles + MaxVantageBonusTiles;

    /// Every tile in view of this person now (Visible's order), from the view their looks keep: cast
    /// again only when their tile, eyes or the sky's visibility change.
    public static IReadOnlyList<TileId> InView(World world, Agent agent) => ViewOf(world, agent).Tiles;

    /// Whether the tile is in view of this person now.
    public static bool InSight(World world, Agent agent, TileId tile) => ViewOf(world, agent).Set.Contains(tile);

    /// What this person has in view now, cast from their tile with their eyes and today's sky; kept on
    /// their beliefs while tile and budget hold.
    internal static SightView ViewOf(World world, Agent agent)
    {
        double sight = Body.GetCapacities(agent.Body).Sight;
        double budget = BudgetTiles(world, agent.Tile, sight);
        var beliefs = (agent.Mind as PersonMind)?.Knows;
        bool ice = Map.IsIceSeason(world.Minute);
        if (beliefs?.View is { } kept && kept.From == agent.Tile && kept.BudgetTiles == budget && kept.IceSeason == ice) return kept;
        var view = new SightView(agent.Tile, budget, ice, Visible(world, agent.Tile, sight));
        if (beliefs != null) beliefs.View = view;
        return view;
    }

    /// The sight budget from a tile on a clear day with sound eyes: 6 in a cave, otherwise open
    /// ground plus the tile's vantage (its own visibility, or the hills' if it stands on hills).
    // Regional weather elevation is not a heightmap, so it gives no local vantage.
    public static int RangeTiles(GeneratedMap m, TileId from, double sightCapacity = 1)
    {
        if (m.CaveAt(from) == Cave.Floor) return (int)Math.Round(CaveTiles * Math.Clamp(sightCapacity, 0, 1));
        double hills = m.TerrainAt(from) == Terrain.Hills ? Terrain.Hills.Row().Visibility : 0;
        return VantageRangeTiles(Math.Max(Map.Visibility(m, from), hills), sightCapacity);
    }

    /// Open ground's budget, plus (vantage - 1.3) x 20 tiles for a high place, at most 8 more.
    public static int VantageRangeTiles(double vantageVisibility, double sightCapacity = 1)
    {
        double bonus = Math.Clamp((vantageVisibility - 1.3) * 20, 0, MaxVantageBonusTiles);
        return (int)Math.Round((OpenGroundTiles + bonus) * Math.Clamp(sightCapacity, 0, 1));
    }

    /// The budget now: the range, cut by the weather's visibility.
    public static double BudgetTiles(World world, TileId from, double sightCapacity = 1) =>
        RangeTiles(Map.Of(world), from, sightCapacity) * Weather.At(world, world.Minute).VisibilityFactor;

    /// Every tile in view from here now, by column then row. sightCapacity is the eyes' (Capacity.Sight).
    public static IReadOnlyList<TileId> Visible(World world, TileId from, double sightCapacity = 1)
    {
        var m = Map.Of(world);
        double budget = BudgetTiles(world, from, sightCapacity);
        int range = (int)Math.Ceiling(budget);
        var seen = new List<TileId>();
        for (int x = Math.Max(0, from.X - range); x <= Math.Min(m.Width - 1, from.X + range); x++)
            for (int y = Math.Max(0, from.Y - range); y <= Math.Min(m.Height - 1, from.Y + range); y++)
            {
                var t = new TileId(x, y);
                if (CanSee(m, from, t, budget)) seen.Add(t);
            }
        return seen;
    }

    static bool InCave(GeneratedMap m, TileId t) => m.CaveAt(t) == Cave.Floor;

    /// Budget a tile takes per tile-length of ray: infinite through a mountain, through nothing to see
    /// through, and through another cave's floor; otherwise 1/visibility², and never less than 1.
    static double Absorption(GeneratedMap m, TileId t, int? caveId)
    {
        double v = Map.Visibility(m, t);
        return m.TerrainAt(t) == Terrain.Mountain || v <= 0 || InCave(m, t) && m.CaveIdAt(t) != caveId
            ? double.PositiveInfinity
            : Math.Max(1, 1 / (v * v));
    }

    static bool Opaque(GeneratedMap m, TileId t, int? caveId) => !m.InBounds(t) || double.IsPositiveInfinity(Absorption(m, t, caveId));

    /// Walks every cell a centre-to-centre ray crosses, charging each its exact segment length; the
    /// target itself is never charged, so a wall is seen though nothing behind it is.
    static bool CanSee(GeneratedMap m, TileId from, TileId to, double budget)
    {
        if (to == from) return true;
        int? caveId = m.CaveIdAt(from);
        if (InCave(m, to) && m.CaveIdAt(to) != caveId) return false; // a cave's floor is seen only from that cave, its mouth included
        int dx = to.X - from.X, dy = to.Y - from.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (budget <= 0 || distance > budget) return false;

        int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
        double strideX = dx == 0 ? double.PositiveInfinity : distance / Math.Abs(dx);
        double strideY = dy == 0 ? double.PositiveInfinity : distance / Math.Abs(dy);
        double nextX = strideX / 2, nextY = strideY / 2, travelled = 0;
        int x = from.X, y = from.Y;
        while (x != to.X || y != to.Y)
        {
            var here = new TileId(x, y);
            double exit = Math.Min(nextX, nextY);
            budget -= (exit - travelled) * Absorption(m, here, caveId);
            if (budget <= 0) return false;
            travelled = exit;
            bool crossX = nextX <= exit + 1e-9, crossY = nextY <= exit + 1e-9;
            // A diagonal cannot peek through the seam between two opaque tiles.
            if (crossX && crossY && Opaque(m, new TileId(x + stepX, y), caveId) && Opaque(m, new TileId(x, y + stepY), caveId)) return false;
            if (crossX) { x += stepX; nextX += strideX; }
            if (crossY) { y += stepY; nextY += strideY; }
            var entered = new TileId(x, y);
            // Out of a cave's floor, sight goes only along that cave's floor and to its mouth.
            if (InCave(m, here) && m.TerrainAt(entered).IsPassable() && (m.CaveIdAt(entered) != m.CaveIdAt(here) || m.CaveAt(entered) == Cave.None))
                return false;
        }
        return true;
    }
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
    public static bool Witness(World world, Agent agent, List<WorldEvent> events, ref int filed)
    {
        var mem = Of(agent).Places.GetValueOrDefault(agent.Tile);
        bool back = mem?.VisitedMinute != world.Minute - 1;
        bool knew = mem?.VisitedMinute != null;
        Fire.CatchUpAround(world, agent, events);
        bool nearer = (back || world.Minute % GlanceMinutes == 0) && Glance(world, agent, events);
        Journal.File(world, events, ref filed); // what catching up found is in the journal before the receipt reads it
        var receipt = Receipt(world, agent, agent.Tile);
        if (back && knew && !receipt.IsEmpty) events.Add(new Noticed(world.Minute, agent.Id, agent.Tile, receipt));
        return nearer;
    }

    /// An NPC's look about while it keeps at something, as often as the player looks.
    public static bool Watch(World world, Agent agent, List<WorldEvent> events) =>
        world.Minute % GlanceMinutes == 0 && Glance(world, agent, events);

    /// When the person last saw this agent, while they remember it.
    public static long? LastSeenMinute(Agent person, AgentId other) =>
        Of(person).Sightings.TryGetValue(other, out var s) ? s.Minute : null;

    /// An NPC's look as it decides: around it, then the tile underfoot. What it would be told is
    /// not reported: nothing reads an NPC's receipts yet.
    public static void Observe(World world, Agent agent, List<WorldEvent> events)
    {
        Fire.CatchUpAround(world, agent, events);
        Glance(world, agent, events);
        Receipt(world, agent, agent.Tile);
    }

    /// Looks at every tile in sight but the one underfoot: what can be told from a distance.
    /// Fire pits in sight are caught up first, so the flame seen is the flame there now. True when a
    /// predator came into sight, or nearer than where it was last seen.
    public static bool Glance(World world, Agent agent, List<WorldEvent> events)
    {
        var beliefs = Of(agent);
        var m = Map.Of(world);
        long now = world.Minute;
        var view = Sight.ViewOf(world, agent);
        Explore(world, beliefs, view);
        var pits = new Dictionary<TileId, FireId>();
        foreach (var (id, f) in world.Fires.ToList())
            if (f.CarriedBy == null && f.Tile != agent.Tile && view.Set.Contains(f.Tile))
            {
                Fire.CatchUp(world, id, events);
                pits.TryAdd(f.Tile, id);
            }
        var dens = Dens(world);
        // Only tiles with something to note, or a memory to bring up to date: every other tile in view
        // would be passed over (nothing seen, nothing remembered), so it is not visited at all.
        view.TreesOrWater ??= [.. view.Tiles.Where(t => HasTreePool(world, t) || m.TerrainAt(t).IsPassable() && Work.HasOpenWater(world, t))];
        var look = new SortedSet<TileId>(view.TreesOrWater);
        look.UnionWith(pits.Keys);
        look.UnionWith(dens.Where(view.Set.Contains));
        look.UnionWith(world.Shelters.Keys.Where(view.Set.Contains));
        look.UnionWith(beliefs.Places.Keys.Where(view.Set.Contains));
        foreach (var t in look)
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
        bool nearer = false;
        var seen = beliefs.Sightings;
        foreach (var other in world.Agents.Values)
        {
            if (other.Id == agent.Id || !view.Set.Contains(other.Tile)) continue;
            int alive = other.Members.Count(Body.IsAlive);
            if (alive == 0) continue;
            // Both measured from where the person stands now: walking up to a pack is not the pack coming on.
            if (other.Mind is HerdMind && Fauna.IsPredator(other.Species) && (!seen.TryGetValue(other.Id, out var was)
                    || MapGen.Manhattan(other.Tile, agent.Tile) < MapGen.Manhattan(was.Tile, agent.Tile)))
                nearer = true;
            seen[other.Id] = new Sighting(other.Tile, other.Species, alive, now);
        }
        foreach (var (id, s) in seen.ToList())
            if (now - s.Minute > SightingMemoryMinutes) seen.Remove(id);
        return nearer;
    }

    /// Every tile in view is explored, and its ground as it is now remembered.
    static void Explore(World world, Beliefs beliefs, SightView view)
    {
        var m = Map.Of(world);
        beliefs.Explored ??= new GroundMemory(m.Width, m.Height);
        if ((beliefs.Explored.Width, beliefs.Explored.Height) != (m.Width, m.Height))
            throw new InvalidOperationException($"Explored ground is {beliefs.Explored.Width}x{beliefs.Explored.Height}, the map {m.Width}x{m.Height}");
        foreach (var t in view.Tiles) beliefs.Explored.Remember(t, Ground.Look(world, t), world.Minute);
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

    static bool HasTreePool(World world, TileId t) => Pools.At(world, t).Any(p => p is TreePool);

    /// Standing timber on the tile (a stand of trees is plain to see from a distance).
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
            if (list.Count > MaxEntriesPerTile) list.RemoveAt(Evicted(world, list));
        }
        if (world.Minute % 1440 == 0 && world.Journal.Count > 0) Forget(world);
    }

    /// What a place's story is made of: its fire going out, its shelter coming down, a death, a wound,
    /// a kill (a carcass appearing), a fight, a wounded animal getting away, something built, the pass
    /// crossed, and its stash taken by someone whose camp it is not. Everything else - feeding the fire,
    /// stashing, eating, cooking, strikes that failed, a fire lit - makes way for these, so no amount of
    /// it can push out the fire going out.
    // A carcass going (butchered away) has no filed event: Butchered is a step's tally (Files).
    public static bool IsNotable(World world, WorldEvent e) => e switch
    {
        FireDied or FireBurnedToEmbers or ShelterCollapsed or Died or Injured or Killed or FightEnded or WoundedEscaped
            or Built or CrossedThePass => true,
        // Whose camp it was when it was taken, not when the entry is weighed: a camp that moves on does not
        // turn its owner's old chores into thefts.
        Unstashed u => u.Taken,
        _ => false,
    };

    /// Which entry a full journal lets go: the earliest-happened entry that is not notable, or failing
    /// that the earliest-happened entry. By minute, not by filing order: catch-up files what happened
    /// hours ago after what happened just now. Ties go to the first filed.
    static int Evicted(World world, List<WorldEvent> list)
    {
        int oldest = -1;
        bool ordinary = list.Any(e => !IsNotable(world, e));
        for (int i = 0; i < list.Count; i++)
            if ((!ordinary || !IsNotable(world, list[i])) && (oldest < 0 || list[i].Minute < list[oldest].Minute)) oldest = i;
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
