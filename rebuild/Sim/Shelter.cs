using System.Text.Json.Serialization;

namespace Survival;

/// Old ShelterType. Natural ones are found in the world; the rest are built or carried.
public enum ShelterKind { BranchFrame, LogFrame, RockOverhang, Cave, DenseThicket, FallenTree, SnowShelter, HideTent, NaturalShelter }

/// The three things a shelter does (old ShelterImprovementType).
public enum ShelterStat { Insulation, Overhead, Wind }

/// Old ShelterTypeConfig: a kind's starting levels and how far work can take them.
public sealed record ShelterKindProps(double Insulation, double Overhead, double Wind,
    double InsulationCap, double OverheadCap, double WindCap, bool IsNatural, bool IsSnow = false);

/// What to put on a tile. Levels and caps left null are the kind's (old ShelterFeature constructors).
/// Content (named locations) authors these; Shelters.Place puts one down.
public sealed record ShelterDef(string Name, ShelterKind Kind,
    double? Insulation = null, double? Overhead = null, double? Wind = null,
    double? InsulationCap = null, double? OverheadCap = null, double? WindCap = null);

/// A shelter standing on a tile. Levels are 0-1. Owned by Shelters.
public sealed class ShelterState
{
    public required string Name { get; init; }
    public required ShelterKind Kind { get; init; }
    public double InsulationLevel { get; set; }
    public double OverheadLevel { get; set; }
    public double WindLevel { get; set; }
    public required double InsulationCap { get; init; }
    public required double OverheadCap { get; init; }
    public required double WindCap { get; init; }
    /// A pitched tent: the item it packs back into. Saved with the shelter (old: [JsonIgnore], so a
    /// load stranded a pitched tent as a shelter that could never be packed - bug, fixed).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Gear? Tent { get; init; }
    /// Snow only: the clock hours before this one have been melted (Shelters.Melt).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MeltedToHour { get; set; }
    /// A find standing: the index of its pool on this tile (Pools.At), which is what it is, whatever it is called.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FromFind { get; init; }
}

/// What a shelter does for someone keeping still in it this minute. Default: nothing, which is
/// also what it does for someone walking about (old IsStationary).
public readonly record struct ShelterCover(double InsulationLevel, double OverheadLevel, double WindLevel);

/// A shelter under construction, or a tent being pitched. Owned by Shelters.
public sealed class Project
{
    public required Structure Building { get; init; }
    public double MinutesDone { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Gear? Tent { get; init; }
}

/// Things built on a tile with the Build activity (old camp-infrastructure recipes and MakeCamp).
public enum Structure { MakeshiftBedding, PaddedBedding, MossBedding, SleepingBag, BranchFrame, LogFrame, SnowShelter, Tent }

/// One recipe (Content/shelters.json). Materials are (any of these, kg); taken when the work starts.
/// The simplest camp recipes only; full crafting (and the hide tent recipe) is not ported.
public sealed record Recipe(int Minutes, (Resource[] AnyOf, double Kg)[] Materials, bool NeedsLight = true,
    bool NeedsShovel = false, bool ShovelHelps = false, bool NeedsFrost = false, ShelterKind? Shelter = null, BeddingKind? Bedding = null);

public enum BeddingKind { Camp, Makeshift, Padded, MossPadded, SleepingBag }

/// Old BeddingFeature. Quality is sleep efficiency and keeps you off wet ground; warmth is felt asleep.
public sealed record BeddingProps(string Name, double QualityPct, double WarmthBonusF);

/// Old MaterialProperties: a nominal piece's kilos, and how much one does for each shelter stat.
/// Shelter false is a material recipes use that cannot be worked into a shelter (sinew: sewing only).
public sealed record ShelterMaterial(double PieceKg, double Insulating, double Waterproof, double Structural, bool Shelter = true);

/// The Build activity finished something on this tile.
public sealed record Built(long Minute, AgentId Who, TileId Tile, Structure What) : WorldEvent(Minute, Tile);
/// A snow shelter melted away.
public sealed record ShelterCollapsed(long Minute, TileId Tile, string Name) : WorldEvent(Minute, Tile);

/// Shelters, bedding and the work that makes them. Port of old ShelterFeature, BeddingFeature, the
/// shelter and bedding parts of CampHandler and NeedCraftingSystem, and ShelterImprovementStrategy.
// Old CacheFeature (capacity, predator and weather protection, food preservation) is not ported: the
// tile stash is unlimited, unprotected storage. No rebuilding a branch frame into a log frame (old 60%
// salvage), no reclaim from an abandoned project.
public static class Shelters
{
    /// Every shelter kind's levels and caps (Content/shelters.json).
    public static readonly IReadOnlyDictionary<ShelterKind, ShelterKindProps> Kinds = Content.Tables.ShelterKinds;

    /// The old starting camp's Overhang (GridWorldGenerator.CreateCampLocation), placed by World.New.
    // The camp is not a content site yet, so its shelter and bedding are code (see rebuild-deferred.md).
    public static readonly ShelterDef CampOverhang = new("Overhang", ShelterKind.NaturalShelter, 0.55, 0.3, 0.4, 0.70, 0.50, 0.55);

    /// Pitched tents by gear kind (old Gear.HideTent, MammothHideTent shelter values; Content/shelters.json).
    public static readonly IReadOnlyDictionary<string, ShelterDef> Tents = Content.Tables.Tents;

    // ---- placing and reading ----

    /// Puts a shelter on a tile: natural ones from content, built ones when their work is done.
    public static ShelterState Place(World world, TileId tile, ShelterDef def) => Place(world, tile, def, null);

    static ShelterState Place(World world, TileId tile, ShelterDef def, Gear? tent, int? fromFind = null)
    {
        if (world.Shelters.ContainsKey(tile)) throw new InvalidOperationException($"{tile} already has a shelter");
        var s = New(world, def, tent, fromFind);
        world.Shelters[tile] = s;
        // A shelter here for good (a frame finished over a find): the find will never have the tile.
        if (!IsTemporary(s)) world.WaitingShelters.Remove(tile);
        return s;
    }

    /// A tent comes down when packed and snow melts; everything else stands for good.
    static bool IsTemporary(ShelterState s) => s.Tent != null || Kinds[s.Kind].IsSnow;

    static ShelterState New(World world, ShelterDef def, Gear? tent, int? fromFind = null)
    {
        var k = Kinds[def.Kind];
        // A tent pitched again stands as it was packed, improvements and all.
        var levels = tent?.TentLevels ?? new ShelterCover(def.Insulation ?? k.Insulation, def.Overhead ?? k.Overhead, def.Wind ?? k.Wind);
        var s = new ShelterState
        {
            Name = def.Name, Kind = def.Kind, Tent = tent, FromFind = fromFind,
            InsulationLevel = levels.InsulationLevel, OverheadLevel = levels.OverheadLevel, WindLevel = levels.WindLevel,
            InsulationCap = def.InsulationCap ?? k.InsulationCap, OverheadCap = def.OverheadCap ?? k.OverheadCap, WindCap = def.WindCap ?? k.WindCap,
            // Melting counts whole clock hours from the first one it stands through.
            MeltedToHour = k.IsSnow ? (world.Minute + 59) / 60 : null,
        };
        foreach (var stat in Enum.GetValues<ShelterStat>())
            if (Level(s, stat) > Cap(s, stat) + 1e-9)
                throw new ArgumentException($"{def.Name}: {stat} {Level(s, stat)} is above its cap {Cap(s, stat)}", nameof(def));
        return s;
    }

    /// The shelter on this tile as it is now, or null. Settles any melting owed, so it writes to the
    /// world: reads that must not (refusals, reports) use Peek. A snow shelter that melted away while
    /// nobody looked is reported at the hour it went, not the minute it was found gone.
    public static ShelterState? At(World world, TileId tile, List<WorldEvent> events)
    {
        if (!world.Shelters.TryGetValue(tile, out var s)) return null;
        if (Melt(world, tile, s) is not long goneHour) return s;
        world.Shelters.Remove(tile);
        // Old game: snow shelters never melted, and the rebuild melted them without a word (bug, fixed).
        events.Add(new ShelterCollapsed(goneHour * 60, tile, s.Name));
        return StandWaiting(world, tile);
    }

    /// The shelter on this tile as it is now, or null, without writing anything: a snow shelter is
    /// melted on a copy, and a find waiting for the tile is shown as it would stand. Callers must not
    /// change what it returns.
    public static ShelterState? Peek(World world, TileId tile)
    {
        if (!world.Shelters.TryGetValue(tile, out var s)) return null;
        if (s.MeltedToHour == null) return s;
        var copy = new ShelterState
        {
            Name = s.Name, Kind = s.Kind, Tent = s.Tent, MeltedToHour = s.MeltedToHour, FromFind = s.FromFind,
            InsulationLevel = s.InsulationLevel, OverheadLevel = s.OverheadLevel, WindLevel = s.WindLevel,
            InsulationCap = s.InsulationCap, OverheadCap = s.OverheadCap, WindCap = s.WindCap,
        };
        if (Melt(world, tile, copy) == null) return copy;
        return Waiting(world, tile) is var (def, index) ? New(world, def, null, index) : null;
    }

    public static ShelterState? Remove(World world, TileId tile)
    {
        world.Shelters.Remove(tile, out var s);
        StandWaiting(world, tile);
        return s;
    }

    /// A shelter find, found: it stands on the tile for everyone. One shelter to a tile, and nothing
    /// is pushed off it. Behind a tent, a snow shelter or one being built here, the find waits
    /// (World.WaitingShelters) and stands once the tile is free - a tent packed, snow melted. A
    /// half-built frame is finished, never stranded; finished, it stands for good, and so does a
    /// natural shelter: behind one the find is only discovered, with nothing to wait for. A second
    /// find while one waits adds nothing, and a second discoverer of a find standing adds nothing.
    public static void Found(World world, PoolId id, ShelterFind find, List<WorldEvent> events)
    {
        var standing = At(world, id.Tile, events);
        if (standing?.FromFind == id.Index) return;
        bool building = BuildingShelter(world, id.Tile);
        if (standing == null && !building) Place(world, id.Tile, find.Shelter, null, id.Index);
        else if (building || IsTemporary(standing!)) world.WaitingShelters.TryAdd(id.Tile, id.Index);
    }

    static bool BuildingShelter(World world, TileId tile) =>
        world.Projects.TryGetValue(tile, out var p) && Recipes[p.Building].Shelter != null;

    /// The find waiting for this tile and its pool index, if the tile is free for it now.
    static (ShelterDef Def, int Index)? Waiting(World world, TileId tile) =>
        world.WaitingShelters.TryGetValue(tile, out int index) && !BuildingShelter(world, tile)
            ? (((ShelterFind)Pools.At(world, tile)[index]).Shelter, index) : null;

    /// The tile has just come free: a find waiting for it stands now.
    static ShelterState? StandWaiting(World world, TileId tile)
    {
        if (Waiting(world, tile) is not var (def, index)) return null;
        world.WaitingShelters.Remove(tile);
        return Place(world, tile, def, null, index);
    }

    public static double Quality(ShelterState s) => (s.InsulationLevel + s.OverheadLevel + s.WindLevel) / 3.0;
    public static bool IsDestroyed(ShelterState s) => Quality(s) <= 0.05;

    /// What the shelter here does for someone; nothing unless they keep still (old IsStationary).
    public static ShelterCover CoverFor(World world, TileId tile, bool stationary, List<WorldEvent> events) =>
        stationary && At(world, tile, events) is { } s ? new(s.InsulationLevel, s.OverheadLevel, s.WindLevel) : default;

    // ---- wear and work ----

    /// Old Damage: every level down by amount.
    public static void Damage(ShelterState s, double amount)
    {
        s.InsulationLevel = Math.Max(0, s.InsulationLevel - amount);
        s.OverheadLevel = Math.Max(0, s.OverheadLevel - amount);
        s.WindLevel = Math.Max(0, s.WindLevel - amount);
    }

    /// Old Repair: every level up by amount, to its cap.
    public static void Repair(ShelterState s, double amount)
    {
        s.InsulationLevel = Math.Min(s.InsulationCap, s.InsulationLevel + amount);
        s.OverheadLevel = Math.Min(s.OverheadCap, s.OverheadLevel + amount);
        s.WindLevel = Math.Min(s.WindCap, s.WindLevel + amount);
    }

    public static double Level(ShelterState s, ShelterStat stat) => stat switch
    {
        ShelterStat.Insulation => s.InsulationLevel, ShelterStat.Overhead => s.OverheadLevel, _ => s.WindLevel,
    };

    public static double Cap(ShelterState s, ShelterStat stat) => stat switch
    {
        ShelterStat.Insulation => s.InsulationCap, ShelterStat.Overhead => s.OverheadCap, _ => s.WindCap,
    };

    /// Old Improve, for a fraction of a piece of material: 0.05 x effectiveness x the room left
    /// under the cap. Returns the gain.
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

    /// Snow melts on its own clock: each whole clock hour, at the tile's open air at the top of
    /// that hour, so how often anyone looks cannot change how fast it goes. Gone when destroyed.
    // Old snow shelters never melted (the hook was a stub). 0.01 of every level per hour per °F above
    // freezing is invented: about a day at 35 °F, an afternoon at 50 °F. A fire inside does not melt it.
    const double MeltPerHourPerF = 0.01;

    /// Melts s to the current hour. The air is what reaches the surface (Ground.WeatherAt): wind chill
    /// is felt by a body, it does not melt snow. When it melted away, the clock hour it was gone by;
    /// the caller removes it. Null while it stands.
    static long? Melt(World world, TileId tile, ShelterState s)
    {
        if (s.MeltedToHour is not long hour) return null;
        long now = world.Minute / 60;
        for (; hour < now; hour++)
        {
            double airF = Ground.WeatherAt(world, tile, hour * 60).AirTemperatureF;
            if (airF > 32) Damage(s, MeltPerHourPerF * (airF - 32));
            if (IsDestroyed(s)) return hour + 1;
        }
        s.MeltedToHour = hour;
        return null;
    }

    // ---- materials (old MaterialProperties) ----

    /// Old item counts become kilos through a nominal piece: a stick, a log, a hide (Content/shelters.json).
    public static readonly IReadOnlyDictionary<Resource, ShelterMaterial> Materials = Content.Tables.ShelterMaterials;

    static readonly Resource[] ShelterMaterials = [.. Materials.Where(m => m.Value.Shelter).Select(m => m.Key)];

    public static double Effectiveness(Resource r, ShelterStat stat)
    {
        var m = Materials.TryGetValue(r, out var v) ? v : throw new KeyNotFoundException($"{r} is not a shelter material");
        return stat switch { ShelterStat.Insulation => m.Insulating, ShelterStat.Overhead => m.Waterproof, _ => m.Structural };
    }

    /// Old NeedCraftingSystem camp recipes and CampHandler.MakeCamp (Content/shelters.json). A project's
    /// setup time is folded into its minutes, and the shovel speeds all of it.
    public static readonly IReadOnlyDictionary<Structure, Recipe> Recipes = Content.Tables.Recipes;

    public static readonly IReadOnlyDictionary<BeddingKind, BeddingProps> Beddings = Content.Tables.Beddings;
    // Old HasWindProtection and HasGroundInsulation were flags nothing read (ground
    // protection came from quality alone); dropped until a sleeper's wind has a number to give them.

    /// Bedding here replaces what was there (old: a second BeddingFeature was added behind the first,
    /// and GetFeature found the first, so the new bed did nothing - bug, fixed).
    public static void PlaceBedding(World world, TileId tile, BeddingKind kind) => world.Beddings[tile] = kind;
    public static BeddingProps? BeddingAt(World world, TileId tile) =>
        world.Beddings.TryGetValue(tile, out var k) ? Beddings[k] : null;

    /// Old SurvivalContext: bedding under someone resting on the ground keeps most of the wet off.
    public static double GroundProtectionLevel(BeddingProps? bedding) => bedding == null ? 0 : Math.Clamp(0.5 + bedding.QualityPct * 0.4, 0, 0.95);

    // ---- the activities: Build, ImproveShelter, PackTent ----

    public static bool Handles(Activity a) => a is Build or ImproveShelter or PackTent;

    public static string? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        var pack = Items.Pack(world, agent.Id);
        var tile = agent.Tile;
        switch (activity)
        {
            case Build b:
            {
                var r = Recipes[b.What];
                if (r.NeedsLight && Work.TooDarkToWork(world, agent)) return Work.TooDark;
                world.Projects.TryGetValue(tile, out var p);
                if (p != null && p.Building != b.What) return $"a {p.Building} is half-built here";
                if (r.Shelter != null && Peek(world, tile) is { } s) return $"there is already a shelter here ({s.Name})";
                // Old MakeCamp was offered only where there was no bedding; here any better bed may replace one.
                if (r.Bedding is BeddingKind bed && BeddingAt(world, tile) is { } there
                    && there.QualityPct >= Beddings[bed].QualityPct && there.WarmthBonusF >= Beddings[bed].WarmthBonusF)
                    return $"the {there.Name} here is as good as a {Beddings[bed].Name}";
                // Resuming checks tools and weather again; materials and the tent went in when it started.
                if (Requirement(world, tile, r, pack) is string missing) return missing;
                if (p != null) return null;
                if (b.What == Structure.Tent && Tent(pack) == null) return "you have no tent";
                foreach (var (anyOf, kg) in r.Materials)
                    if (anyOf.Sum(x => Items.Have(pack, x)) < kg - 1e-9) return $"you need {kg:0.##} kg of {string.Join(" or ", anyOf)}";
                return null;
            }
            case ImproveShelter i:
            {
                if (Work.TooDarkToWork(world, agent)) return Work.TooDark;
                if (Peek(world, tile) is not { } s || IsDestroyed(s)) return "there's no shelter here to improve";
                if (!ShelterMaterials.Contains(i.Material)) return $"{i.Material} is no use on a shelter";
                if (Items.Have(pack, i.Material) <= 0) return $"you have no {i.Material}";
                if (Cap(s, i.Stat) - Level(s, i.Stat) < 1e-9) return $"this {s.Name}'s {i.Stat} can't be improved further";
                return null;
            }
            case PackTent:
                return Peek(world, tile)?.Tent == null ? "there's no tent to pack up here" : null;
            default: throw new NotSupportedException(activity.GetType().Name);
        }
    }

    /// The tool and weather a recipe needs, checked when work starts or resumes and every minute of it.
    /// Frost is the tile's surface air, the same air that melts a snow shelter (Melt).
    static string? Requirement(World world, TileId tile, Recipe r, Inventory pack)
    {
        if (r.NeedsShovel && Items.Tool(pack, ToolType.Shovel) == null) return "you need a shovel";
        if (r.NeedsFrost && Ground.WeatherAt(world, tile, world.Minute).AirTemperatureF > 32) return "too warm to build in snow";
        return null;
    }

    static Gear? Tent(Inventory pack) => pack.Gear.FirstOrDefault(g => Tents.ContainsKey(g.Kind));

    /// Starts the work at this instant: a new project takes its materials now. Packing a tent is instant.
    public static void Begin(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        var pack = Items.Pack(world, agent.Id);
        if (activity is Build b && !world.Projects.ContainsKey(agent.Tile))
        {
            foreach (var (anyOf, kg) in Recipes[b.What].Materials)
            {
                double left = kg;
                foreach (var r in anyOf) left -= Items.Take(pack, r, left);
            }
            Gear? tent = b.What == Structure.Tent ? Tent(pack) : null;
            if (tent != null) pack.Gear.Remove(tent);
            world.Projects[agent.Tile] = new Project { Building = b.What, Tent = tent };
        }
        else if (activity is PackTent)
        {
            // old game: packing kept the tent item as it was pitched, so improvements were lost (bug, fixed).
            var s = Remove(world, agent.Tile)!;
            s.Tent!.TentLevels = new ShelterCover(s.InsulationLevel, s.OverheadLevel, s.WindLevel);
            pack.Gear.Add(s.Tent);
        }
    }

    /// Old GetWorkImpairments for building and improving: bad legs +20%, bad hands +25%.
    static double ImpairmentFactor(BodyState body)
    {
        var c = Body.GetCapacities(body);
        return (c.Moving < 0.5 ? 1.20 : 1.0) * (c.Manipulation < 0.5 ? 1.25 : 1.0);
    }

    /// One minute of the work. Returns why it stops, if it does.
    public static string? Minute(World world, Agent agent, Activity activity, List<WorldEvent> events)
    {
        var tile = agent.Tile;
        switch (activity)
        {
            case Build b:
            {
                var r = Recipes[b.What];
                if (r.NeedsLight && Work.TooDarkToWork(world, agent)) return Work.TooDark;
                if (Requirement(world, tile, r, Items.Pack(world, agent.Id)) is string missing) return missing;
                var p = world.Projects[tile];
                bool shovel = r.ShovelHelps && Items.Tool(Items.Pack(world, agent.Id), ToolType.Shovel) != null;
                p.MinutesDone += (shovel ? 2.0 : 1.0) / ImpairmentFactor(agent.Body);
                // Crafting has no failures yet for practice to spare; it is learned, and waits for crafting luck to pay.
                Skills.Award(world, agent, Skill.Crafting, Skills.CraftXpPerHour / 60, events);
                if (p.MinutesDone < r.Minutes - 1e-9) return null;
                world.Projects.Remove(tile);
                if (r.Bedding is BeddingKind bed) PlaceBedding(world, tile, bed);
                else if (p.Tent is Gear tent) Place(world, tile, Tents[tent.Kind], tent);
                else Place(world, tile, new ShelterDef(r.Shelter!.Value switch
                {
                    ShelterKind.BranchFrame => "Branch Frame Shelter",
                    ShelterKind.LogFrame => "Log Frame Shelter",
                    _ => "Snow shelter",
                }, r.Shelter.Value));
                events.Add(new Built(world.Minute, agent.Id, tile, b.What));
                return "built";
            }
            case ImproveShelter i:
            {
                if (Work.TooDarkToWork(world, agent)) return Work.TooDark;
                if (At(world, tile, events) is not { } s) return "the shelter is gone";
                // Old: ten minutes a piece, longer when impaired; here a tenth of a piece a minute.
                var pack = Items.Pack(world, agent.Id);
                double pieces = Items.Take(pack, i.Material, Materials[i.Material].PieceKg / 10 / ImpairmentFactor(agent.Body)) / Materials[i.Material].PieceKg;
                Improve(s, i.Stat, i.Material, pieces);
                if (Items.Have(pack, i.Material) <= 0) return $"out of {i.Material}";
                return Cap(s, i.Stat) - Level(s, i.Stat) < 1e-9 ? "as good as it gets" : null;
            }
            case PackTent: return "packed";
            default: throw new NotSupportedException(activity.GetType().Name);
        }
    }
}
