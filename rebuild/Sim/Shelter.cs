using System.Text.Json.Serialization;

namespace Survival;

public enum ShelterKind { BranchFrame, LogFrame, RockOverhang, Cave, DenseThicket, FallenTree, SnowShelter, HideTent, NaturalShelter }

public enum ShelterStat { Insulation, Overhead, Wind }

public sealed record ShelterKindProps(double Insulation, double Overhead, double Wind,
    double InsulationCap, double OverheadCap, double WindCap, bool IsNatural, bool IsSnow);

/// Levels and caps left null are the kind's.
public sealed record ShelterDef(string Name, ShelterKind Kind,
    double? Insulation = null, double? Overhead = null, double? Wind = null,
    double? InsulationCap = null, double? OverheadCap = null, double? WindCap = null);

/// A shelter standing on a tile. Levels are 0-1. Owned by Shelters.
public sealed class ShelterState
{
    /// Id of a find shown by Peek before it stands.
    public const int Unplaced = -1;
    /// Its own id, never reused (World.NextShelterId): a ref held to it never points at a later one.
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required ShelterKind Kind { get; init; }
    public double InsulationLevel { get; set; }
    public double OverheadLevel { get; set; }
    public double WindLevel { get; set; }
    public required double InsulationCap { get; init; }
    public required double OverheadCap { get; init; }
    public required double WindCap { get; init; }
    /// Saved with the shelter, so a loaded tent can still be packed.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Gear? Tent { get; init; }
    /// Snow only: clock hours before this one are already melted.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MeltedToHour { get; set; }
    /// A find standing: the index of its pool on this tile (Pools.At), which is what it is, whatever it is called.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FromFind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Cause? Cause { get; init; }
}

/// For someone keeping still in it this minute; default (nothing) is also what it does for someone walking about.
public readonly record struct ShelterCover(double InsulationLevel, double OverheadLevel, double WindLevel);

public enum BeddingKind { Camp, Makeshift, Padded, MossPadded, SleepingBag }

/// Quality is sleep efficiency and keeps you off wet ground; warmth is felt asleep.
public sealed record BeddingProps(string Name, double QualityPct, double WarmthBonusF);

/// Shelter false is a material recipes use that cannot be worked into a shelter (sinew: sewing only).
/// Item counts become kilos through a nominal piece (PieceKg).
public sealed record ShelterMaterial(double PieceKg, double Insulating, double Waterproof, double Structural, bool Shelter = true);

public sealed record ShelterCollapsed(long Minute, TileId Tile, string Name) : WorldEvent(Minute, Tile);

public static class Shelters
{
    public static readonly IReadOnlyDictionary<ShelterKind, ShelterKindProps> Kinds = Content.Tables.ShelterKinds;

    // The camp is not a content site yet, so its shelter and bedding are code (see rebuild-deferred.md).
    public static readonly ShelterDef CampOverhang = new("Overhang", ShelterKind.NaturalShelter, 0.55, 0.3, 0.4, 0.70, 0.50, 0.55);

    public static readonly IReadOnlyDictionary<string, ShelterDef> Tents = Content.Tables.Tents;

    /// Natural shelters only; built ones are placed when their work is done, with who built them.
    public static ShelterState Place(World world, TileId tile, ShelterDef def) => Place(world, tile, def, null, NaturalCause);

    static readonly Cause NaturalCause = new(null, Cause.Natural);

    internal static ShelterState Place(World world, TileId tile, ShelterDef def, Gear? tent, Cause cause, int? fromFind = null)
    {
        if (world.Shelters.ContainsKey(tile)) throw new InvalidOperationException($"{tile} already has a shelter");
        var s = New(world, def, tent, cause, fromFind, world.NextShelterId++);
        world.Shelters[tile] = s;
        // A shelter here for good (a frame finished over a find): the find will never have the tile.
        if (!IsTemporary(s)) world.WaitingShelters.Remove(tile);
        return s;
    }

    /// A tent comes down when packed and snow melts; everything else stands for good.
    static bool IsTemporary(ShelterState s) => s.Tent != null || Kinds[s.Kind].IsSnow;

    /// id: the shelter's own (World.NextShelterId), or Unplaced for one shown before it stands.
    static ShelterState New(World world, ShelterDef def, Gear? tent, Cause cause, int? fromFind, int id)
    {
        var k = Kinds[def.Kind];
        var levels = tent?.TentLevels ?? new ShelterCover(def.Insulation ?? k.Insulation, def.Overhead ?? k.Overhead, def.Wind ?? k.Wind);
        var s = new ShelterState
        {
            Id = id,
            Name = def.Name,
            Kind = def.Kind,
            Tent = tent,
            FromFind = fromFind,
            Cause = cause,
            InsulationLevel = levels.InsulationLevel,
            OverheadLevel = levels.OverheadLevel,
            WindLevel = levels.WindLevel,
            InsulationCap = def.InsulationCap ?? k.InsulationCap,
            OverheadCap = def.OverheadCap ?? k.OverheadCap,
            WindCap = def.WindCap ?? k.WindCap,
            MeltedToHour = k.IsSnow ? (world.Minute + 59) / 60 : null,
        };
        foreach (var stat in Enum.GetValues<ShelterStat>())
            if (Level(s, stat) > Cap(s, stat) + 1e-9)
                throw new ArgumentException($"{def.Name}: {stat} {Level(s, stat)} is above its cap {Cap(s, stat)}", nameof(def));
        return s;
    }

    /// Settles any melting owed, so it writes to the world: reads that must not (refusals, reports)
    /// use Peek. A snow shelter that melted unseen is reported at the hour it went.
    public static ShelterState? At(World world, TileId tile, List<WorldEvent> events)
    {
        if (!world.Shelters.TryGetValue(tile, out var s)) return null;
        if (Melt(world, tile, s) is not long goneHour) return s;
        world.Shelters.Remove(tile);
        events.Add(new ShelterCollapsed(goneHour * 60, tile, s.Name));
        return StandWaiting(world, tile);
    }

    /// At without writing: a snow shelter is melted on a copy, and a find waiting for the tile is shown
    /// as it would stand. Callers must not change what it returns.
    public static ShelterState? Peek(World world, TileId tile)
    {
        if (!world.Shelters.TryGetValue(tile, out var s)) return null;
        if (s.MeltedToHour == null) return s;
        var copy = new ShelterState
        {
            Id = s.Id,
            Name = s.Name,
            Kind = s.Kind,
            Tent = s.Tent,
            MeltedToHour = s.MeltedToHour,
            FromFind = s.FromFind,
            Cause = s.Cause,
            InsulationLevel = s.InsulationLevel,
            OverheadLevel = s.OverheadLevel,
            WindLevel = s.WindLevel,
            InsulationCap = s.InsulationCap,
            OverheadCap = s.OverheadCap,
            WindCap = s.WindCap,
        };
        if (Melt(world, tile, copy) == null) return copy;
        return Waiting(world, tile) is var (def, index) ? New(world, def, null, NaturalCause, index, ShelterState.Unplaced) : null;
    }

    public static ShelterState? Remove(World world, TileId tile)
    {
        world.Shelters.Remove(tile, out var s);
        StandWaiting(world, tile);
        return s;
    }

    /// One shelter to a tile, and nothing is pushed off it. Behind a tent, a snow shelter or one being
    /// built, the find waits (World.WaitingShelters) and stands once the tile is free. A half-built
    /// frame is finished, never stranded; finished, it stands for good, and so does a natural shelter:
    /// behind one the find is only discovered. Repeat finds and repeat discoverers add nothing.
    public static void Found(World world, PoolId id, ShelterFind find, List<WorldEvent> events)
    {
        var standing = At(world, id.Tile, events);
        if (standing?.FromFind == id.Index) return;
        bool building = BuildingShelter(world, id.Tile);
        if (standing == null && !building) Place(world, id.Tile, find.Shelter, null, NaturalCause, id.Index);
        else if (building || IsTemporary(standing!)) world.WaitingShelters.TryAdd(id.Tile, id.Index);
    }

    static bool BuildingShelter(World world, TileId tile) =>
        world.Projects.TryGetValue(tile, out var p) && Recipes.Get(p.Recipe).Result.Shelter != null;

    static (ShelterDef Def, int Index)? Waiting(World world, TileId tile) =>
        world.WaitingShelters.TryGetValue(tile, out int index) && !BuildingShelter(world, tile)
            ? (((ShelterFind)Pools.At(world, tile)[index]).Shelter, index) : null;

    static ShelterState? StandWaiting(World world, TileId tile)
    {
        if (Waiting(world, tile) is not var (def, index)) return null;
        world.WaitingShelters.Remove(tile);
        return Place(world, tile, def, null, NaturalCause, index);
    }

    public static double Quality(ShelterState s) => (s.InsulationLevel + s.OverheadLevel + s.WindLevel) / 3.0;
    public static bool IsDestroyed(ShelterState s) => Quality(s) <= 0.05;

    public static ShelterCover CoverFor(World world, TileId tile, bool stationary, List<WorldEvent> events) =>
        stationary && At(world, tile, events) is { } s ? CoverOf(s) : default;

    /// CoverFor read through Peek: a snow shelter that has melted gives nothing.
    public static ShelterCover CoverNow(World world, TileId tile, bool stationary) =>
        stationary && Peek(world, tile) is { } s ? CoverOf(s) : default;

    static ShelterCover CoverOf(ShelterState s) => new(s.InsulationLevel, s.OverheadLevel, s.WindLevel);

    public static void Damage(ShelterState s, double amount)
    {
        s.InsulationLevel = Math.Max(0, s.InsulationLevel - amount);
        s.OverheadLevel = Math.Max(0, s.OverheadLevel - amount);
        s.WindLevel = Math.Max(0, s.WindLevel - amount);
    }

    public static void Repair(ShelterState s, double amount)
    {
        s.InsulationLevel = Math.Min(s.InsulationCap, s.InsulationLevel + amount);
        s.OverheadLevel = Math.Min(s.OverheadCap, s.OverheadLevel + amount);
        s.WindLevel = Math.Min(s.WindCap, s.WindLevel + amount);
    }

    public static double Level(ShelterState s, ShelterStat stat) => stat switch
    {
        ShelterStat.Insulation => s.InsulationLevel,
        ShelterStat.Overhead => s.OverheadLevel,
        _ => s.WindLevel,
    };

    public static double Cap(ShelterState s, ShelterStat stat) => stat switch
    {
        ShelterStat.Insulation => s.InsulationCap,
        ShelterStat.Overhead => s.OverheadCap,
        _ => s.WindCap,
    };

    /// Gain is 0.05 x effectiveness x room left under the cap, per piece.
    public static double Improve(ShelterState s, ShelterStat stat, Resource material, double pieces)
    {
        double cur = Level(s, stat), cap = Cap(s, stat);
        double gain = Math.Min(cap - cur, 0.05 * Effectiveness(material, stat) * Math.Max(0, 1 - cur / cap) * pieces);
        gain = Math.Max(0, gain);
        switch (stat)
        {
            case ShelterStat.Insulation: s.InsulationLevel += gain; break;
            case ShelterStat.Overhead: s.OverheadLevel += gain; break;
            default: s.WindLevel += gain; break;
        }
        return gain;
    }

    // Whole clock hours at the tile's open air at the top of each hour, so how often anyone looks
    // cannot change how fast it goes. The rate is invented: about a day at 35 °F, an afternoon at 50 °F. A fire inside does not melt it.
    const double MeltPerHourPerF = 0.01;

    /// Wind chill is felt by a body, it does not melt snow, hence Exposure.Surface. Returns the hour it
    /// was gone by (the caller removes it), null while it stands.
    static long? Melt(World world, TileId tile, ShelterState s)
    {
        if (s.MeltedToHour is not long hour) return null;
        long now = world.Minute / 60;
        for (; hour < now; hour++)
        {
            double airF = Exposure.Surface(world, tile, hour * 60).AirTemperatureF;
            if (airF > 32) Damage(s, MeltPerHourPerF * (airF - 32));
            if (IsDestroyed(s)) return hour + 1;
        }
        s.MeltedToHour = hour;
        return null;
    }

    public static readonly IReadOnlyDictionary<Resource, ShelterMaterial> Materials = Content.Tables.ShelterMaterials;

    static readonly Resource[] ShelterMaterials = [.. Materials.Where(m => m.Value.Shelter).Select(m => m.Key)];

    public static double Effectiveness(Resource r, ShelterStat stat)
    {
        var m = Materials.TryGetValue(r, out var v) ? v : throw new KeyNotFoundException($"{r} is not a shelter material");
        return stat switch { ShelterStat.Insulation => m.Insulating, ShelterStat.Overhead => m.Waterproof, _ => m.Structural };
    }

    public static readonly IReadOnlyDictionary<BeddingKind, BeddingProps> Beddings = Content.Tables.Beddings;
    // Bedding has no wind or ground flags: ground protection comes from quality alone.

    public static void PlaceBedding(World world, TileId tile, BeddingKind kind) => world.Beddings[tile] = kind;
    public static BeddingProps? BeddingAt(World world, TileId tile) =>
        world.Beddings.TryGetValue(tile, out var k) ? Beddings[k] : null;

    public static double GroundProtectionLevel(BeddingProps? bedding) => bedding == null ? 0 : Math.Clamp(0.5 + bedding.QualityPct * 0.4, 0, 0.95);

    public static bool Handles(Activity a) => a is ImproveShelter or PackTent;

    public const string BuiltReason = "built", Packed = "packed", AsGoodAsItGets = "as good as it gets";

    public static Refusal? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        var pack = Items.Pack(world, agent.Id);
        var tile = agent.Tile;
        switch (activity)
        {
            case ImproveShelter i:
                {
                    if (Work.TooDarkToWork(world, agent)) return Work.Dark;
                    if (Peek(world, tile) is not { } s || IsDestroyed(s)) return new("there's no shelter here to improve", new MissingState(Lack.Shelter));
                    if (Access.Refusal(world, agent.Id, LocationFeatures.ShelterAt(world, tile)!.Value) is { } barred) return barred;
                    if (!ShelterMaterials.Contains(i.Material)) return new($"{Names.Cap(Names.Of(i.Material))} is no use on a shelter");
                    if (Items.Have(pack, i.Material) <= 0) return new($"you have no {Names.Of(i.Material)}", new MissingResource(i.Material, Materials[i.Material].PieceKg));
                    if (Cap(s, i.Stat) - Level(s, i.Stat) < 1e-9) return new($"this {s.Name.ToLowerInvariant()}'s {Names.Of(i.Stat)} can't be improved further");
                    return null;
                }
            case PackTent:
                if (Peek(world, tile)?.Tent == null) return new("there's no tent to pack up here");
                return Access.Refusal(world, agent.Id, LocationFeatures.ShelterAt(world, tile)!.Value);
            default: throw new NotSupportedException(activity.GetType().Name);
        }
    }

    public static Gear? CarriedTent(Inventory pack) => pack.Gear.FirstOrDefault(g => Tents.ContainsKey(g.Kind));

    public static void Begin(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        var pack = Items.Pack(world, agent.Id);
        if (activity is PackTent)
        {
            var s = Remove(world, agent.Tile)!;
            s.Tent!.TentLevels = new ShelterCover(s.InsulationLevel, s.OverheadLevel, s.WindLevel);
            pack.Gear.Add(s.Tent);
        }
    }

    /// Improving: bad legs +20%, bad hands +25% (Making keeps the same rule for building).
    static double ImpairmentFactor(BodyState body)
    {
        var c = Body.GetCapacities(body);
        return (c.Moving < 0.5 ? 1.20 : 1.0) * (c.Manipulation < 0.5 ? 1.25 : 1.0);
    }

    /// Returns why the work stops, if it does.
    public static string? Minute(World world, Agent agent, Activity activity, List<WorldEvent> events)
    {
        var tile = agent.Tile;
        switch (activity)
        {
            case ImproveShelter i:
                {
                    if (Work.TooDarkToWork(world, agent)) return Work.TooDark;
                    if (At(world, tile, events) is not { } s) return "the shelter is gone";
                    var pack = Items.Pack(world, agent.Id);
                    double pieces = Items.Take(pack, i.Material, Materials[i.Material].PieceKg / 10 / ImpairmentFactor(agent.Body)) / Materials[i.Material].PieceKg;
                    Improve(s, i.Stat, i.Material, pieces);
                    if (Items.Have(pack, i.Material) <= 0) return $"out of {Names.Of(i.Material)}";
                    return Cap(s, i.Stat) - Level(s, i.Stat) < 1e-9 ? AsGoodAsItGets : null;
                }
            case PackTent: return Packed;
            default: throw new NotSupportedException(activity.GetType().Name);
        }
    }
}
