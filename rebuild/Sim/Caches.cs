using System.Text.Json.Serialization;

namespace Survival;

/// The kinds of cache (GLOSSARY "Cache"): its row is in Content/shelters.json caches.
[JsonConverter(typeof(JsonStringEnumConverter<CacheKind>))]
public enum CacheKind { BuriedInSnow, IceCrevasse, RockCleft }

public sealed record CacheRow(double CapacityKg, Ability LetsIn, int BreakInMinutes, double ScentMultiplier, ShelterCover Cover, double? BuryDepthM);

public sealed class CacheState
{
    public required TileId Tile { get; init; }
    public required CacheKind Kind { get; init; }
    public Inventory Contents { get; init; } = new();
    /// Broken into by an animal: it lets everyone in until a person repairs it. Damage, so stored.
    public bool TornOpen { get; set; }
    public int BreakInMinutes { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Cause? Cause { get; init; }
}

/// Caches on tiles: the found ones placed with the world, and food buried in snow.
public static class Caches
{
    public static CacheRow Row(CacheKind kind) => Content.Tables.Caches[kind];

    public static CacheId Place(World world, TileId tile, CacheKind kind, Cause? made = null)
    {
        var id = new CacheId(world.NextCacheId++);
        world.Caches[id] = new CacheState { Tile = tile, Kind = kind, Cause = made };
        return id;
    }

    public static void PlaceSites(World world)
    {
        foreach (var (tile, site) in Map.Of(world).Sites)
            foreach (var c in site.All.OfType<SiteCache>()) Place(world, tile, c.Cache);
    }

    public static IEnumerable<CacheId> At(World world, TileId tile) => world.Caches.Where(c => c.Value.Tile == tile).Select(c => c.Key);

    public static CacheId? BuriedAt(World world, TileId tile)
    {
        foreach (var id in At(world, tile))
            if (Row(world.Caches[id].Kind).BuryDepthM != null) return id;
        return null;
    }

    public static CacheState? Find(World world, CacheId id) => world.Caches.GetValueOrDefault(id);

    public static double RoomKg(CacheState c) => Math.Max(0, Row(c.Kind).CapacityKg - Items.WeightKg(c.Contents));

    /// Buried in snow that has thinned below its depth: it lies open, as a pile does.
    public static bool Uncovered(World world, CacheState c) =>
        Row(c.Kind).BuryDepthM is double depth && Ground.Underfoot(world, c.Tile).SnowDepthM < depth;

    public static bool Open(World world, CacheState c) => c.TornOpen || Uncovered(world, c);

    /// Minutes the agent must spend breaking in before it eats: none with hands or into an open cache.
    public static int BreakInLeft(World world, CacheState c, Agent agent) =>
        Open(world, c) || SpeciesTable.Of(agent.Species).Abilities.HasFlag(Ability.Hands) ? 0 : Math.Max(0, Row(c.Kind).BreakInMinutes - c.BreakInMinutes);

    public static void BreakIn(CacheState c)
    {
        c.BreakInMinutes++;
        if (c.BreakInMinutes >= Row(c.Kind).BreakInMinutes) c.TornOpen = true;
    }

    public static void Repair(CacheState c)
    {
        c.TornOpen = false;
        c.BreakInMinutes = 0;
    }

    /// An emptied snow cache is snow again; the found caches stay.
    public static void Tidy(World world, CacheId id)
    {
        if (Find(world, id) is { } c && Row(c.Kind).BuryDepthM != null && Items.IsEmpty(c.Contents)) world.Caches.Remove(id);
    }
}
