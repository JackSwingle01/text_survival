namespace Survival;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LuckTier>))]
public enum LuckTier { Lean, Normal, Good, Jackpot }

/// What a clue promises: resources a matching focus finds more of, game (waiting for small game), a poorer search, or remains.
public enum ClueKind { Resource, Game, Negative, Scavenge }

public enum ClueWhen { Always, Forest, Rocky, Water, Wind, Rain, Snow, DawnDusk, NearlyDepleted, Frozen, Grazed, Saturated, PredatorsNear, BearsNear }

/// A once-per-game teaching line (GLOSSARY: Tutorial), kept in World.Tutorials so it never repeats.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<Tutorial>))]
public enum Tutorial { FirstClue, Axe, Shovel, DayOneFuel }

/// One clue (Content/foraging.json clues). Remains: the words a scavenge find is told in; no Animals is bone scraps.
/// Frozen: the carcass it lays is frozen through.
public sealed record ClueDef(string Text, ClueKind Kind, ClueWhen When, double Weight, IReadOnlyList<Resource>? Resources = null,
    double Yield = 1, Species? Game = null, IReadOnlyList<AnimalWeight>? Animals = null, IReadOnlyList<double>? HarvestedPct = null,
    string? Remains = null, double PredatorBoost = 1, bool Frozen = false);

public sealed record QualityRow(string Word, double AtLeast);
public sealed record FailureRow(IReadOnlyList<string> Words, IReadOnlyList<string> Lines);
public sealed record SearchBand(string Word, double BelowPct);

public sealed record ForagingTables(IReadOnlyList<QualityRow> Quality, IReadOnlyList<FailureRow> Failures,
    IReadOnlyDictionary<LuckTier, string> Luck, IReadOnlyDictionary<Resource, string> Hints, IReadOnlyList<ClueDef> Clues,
    IReadOnlyDictionary<Tutorial, string> Tutorials, IReadOnlyList<SearchBand> SearchBands);

public readonly record struct Clue(int Index, ClueDef Def);

/// What a forager notices before searching, from what grows here, the ground, the sky, the hour and the animals near.
/// Seeded by the tile and its draw (ForageState.ClueDraw), so looking again shows the same clues until a search moves it on.
public static class Clues
{
    public static readonly ForagingTables Table = Content.Tables.Foraging;

    public static IReadOnlyList<Clue> At(World world, AgentId who)
    {
        var agent = world.Agent(who);
        if (Pools.ForageAt(world, agent.Tile) is not PoolId id) return [];
        int draw = (Pools.StateOf(world, id) as ForageState)?.ClueDraw ?? 0;
        var rng = Rng.ForSystem(world.Seed, $"clues {TileIdConverter.Format(agent.Tile)} {draw}");
        var pool = WeightedPool(world, agent, id);
        var clues = new List<Clue>();
        int count = Count(Body.Perception(agent.Body), rng);
        for (int n = 0; n < count && pool.Count > 0; n++)
        {
            double pick = rng.NextDouble() * pool.Sum(p => p.Weight), sum = 0;
            int chosen = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++) { sum += pool[i].Weight; if (pick <= sum) { chosen = i; break; } }
            clues.Add(pool[chosen].Clue);
            pool.RemoveAt(chosen);
        }
        // The old selector always showed something: the generic clue stands in for none.
        if (clues.Count == 0) clues.Add(Generic);
        return clues;
    }

    static Clue Generic => Table.Clues.Select((c, i) => new Clue(i, c)).First(c => c.Def.Kind == ClueKind.Resource && c.Def.When == ClueWhen.Always);

    static int Count(double perception, Rng rng)
    {
        double roll = rng.NextDouble();
        if (perception >= 0.9) return roll < 0.15 ? 0 : roll < 0.85 ? 1 : 2;
        if (perception >= 0.7) return roll < 0.15 ? 0 : roll < 0.95 ? 1 : 2;
        if (perception >= 0.5) return roll < 0.30 ? 0 : 1;
        return roll < 0.80 ? 0 : 1;
    }

    static List<(Clue Clue, double Weight)> WeightedPool(World world, Agent agent, PoolId id)
    {
        var def = (ForagePool)Pools.Def(world, id);
        var grows = def.Items.Select(i => i.Resource).ToHashSet();
        var place = new Place(world, agent.Tile, id);
        var pool = new List<(Clue, double)>();
        for (int i = 0; i < Table.Clues.Count; i++)
        {
            var c = Table.Clues[i];
            if (!place.Holds(c.When)) continue;
            double weight = c.Kind switch
            {
                ClueKind.Resource when c.When == ClueWhen.Always => c.Weight,
                ClueKind.Resource => c.Weight * c.Yield * c.Resources!.Count(grows.Contains) / c.Resources!.Count,
                ClueKind.Game => place.GameNear ? c.Weight : 0,
                ClueKind.Negative when c.When == ClueWhen.Grazed => c.Weight * place.GrazedLevel,
                ClueKind.Negative => c.Weight,
                ClueKind.Scavenge when c.Animals is { Count: > 0 } animals && !animals.Any(a => RangesNear(world, agent.Tile, a.Animal)) => 0,
                ClueKind.Scavenge => c.Weight * (place.PredatorsNear ? c.PredatorBoost : 1),
                _ => throw new ArgumentOutOfRangeException(nameof(c), c.Kind, null),
            };
            if (weight > 0) pool.Add((new Clue(i, c), weight));
        }
        return pool;
    }

    /// The place's facts the clues read, each from the real state of the tile, the sky and the herds near.
    sealed class Place
    {
        readonly World world;
        readonly TileId tile;
        readonly PoolId forage;
        readonly GeneratedMap m;
        readonly WeatherSample sky;

        public Place(World world, TileId tile, PoolId forage)
        {
            (this.world, this.tile, this.forage) = (world, tile, forage);
            m = Map.Of(world);
            sky = Weather.At(world, world.Minute);
        }

        string Tags => m.SiteAt(tile)?.Tags ?? "";
        bool Tagged(params string[] words) => words.Any(w => Tags.Contains(w, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<Agent>? near;
        IReadOnlyList<Agent> Near => near ??= [.. world.Agents.Values.Where(a => a.Mind is HerdMind h && Sim.IsAlive(a) && (a.Tile == tile || h.Territory.Contains(tile)))];

        public bool GameNear => Near.Count > 0;
        public bool PredatorsNear => Near.Any(a => Fauna.IsPredator(a.Species));
        public double GrazedLevel => ((ForagePool)Pools.Def(world, forage)).Items.Select(i => Pools.GrazedLevel(world, forage, i.Resource)).DefaultIfEmpty(0).Average();

        public bool Holds(ClueWhen when) => when switch
        {
            ClueWhen.Always => true,
            ClueWhen.Forest => Pools.At(world, tile).Any(p => p is TreePool) || Tagged("forest", "birch", "pine", "wood"),
            ClueWhen.Rocky => Map.TerrainHazardLevel(m, tile) > RockyHazardLevel || m.SiteAt(tile)?.Vantage == true
                || m.TerrainAt(tile) is Terrain.Rock or Terrain.Hills || Tagged("rock", "cliff", "overlook"),
            ClueWhen.Water => m.BesideRiver(tile) || m.TerrainAt(tile) == Terrain.Water || m.SiteAt(tile)?.Water != null || Tagged("water", "river", "creek", "lake"),
            ClueWhen.Wind => sky.WindLevel > WindyLevel,
            ClueWhen.Rain => sky.PrecipitationPct > RainyPct,
            ClueWhen.Saturated => sky.PrecipitationPct > SaturatedPct,
            ClueWhen.Snow => Ground.SnowDepthM(world, tile) >= SnowyM,
            ClueWhen.Frozen => sky.TemperatureF < FrozenBelowF,
            ClueWhen.DawnDusk => Weather.MinuteOfDay(world.Minute) / 60 is DawnHour or DuskHour,
            ClueWhen.NearlyDepleted => Pools.ForageDensity(world, forage) < NearlyDepletedDensity,
            ClueWhen.Grazed => GrazedLevel > 0,
            ClueWhen.PredatorsNear => PredatorsNear,
            ClueWhen.BearsNear => Near.Any(a => IsBear(a.Species)),
            _ => throw new ArgumentOutOfRangeException(nameof(when), when, null),
        };
    }

    /// Whether this kind of animal lives about here: a live herd of it holding the tile, or small game, which has no herds
    /// and is taken to be everywhere.
    public static bool RangesNear(World world, TileId tile, Species s) =>
        SpeciesTable.Of(s).IsSmall
        || world.Agents.Values.Any(a => a.Mind is HerdMind h && a.Species == s && Sim.IsAlive(a) && (a.Tile == tile || h.Territory.Contains(tile)));

    /// A bear, by what it is rather than its name: a lone hunter that eats anything.
    static bool IsBear(Species s) => Fauna.BehaviorOf(s) == HerdBehavior.Solitary && SpeciesTable.Of(s).Diet == Diet.Omnivore;

    const double RockyHazardLevel = 0.4, WindyLevel = 0.5, RainyPct = 0.3, SaturatedPct = 0.6, FrozenBelowF = 10, NearlyDepletedDensity = 1.2;
    /// Lying snow, rather than air below freezing, is what holds a track or shows scat.
    const double SnowyM = 0.01;
    const int DawnHour = 5, DuskHour = 20;

    /// What a search's clues make of one find: the first negative clue noticed cuts everything; every resource clue its
    /// focus follows multiplies its own.
    public static double YieldFactor(IReadOnlyList<int> clues, ForageFocus focus, Resource r)
    {
        double factor = 1;
        foreach (var i in clues) if (Table.Clues[i].Kind == ClueKind.Negative) { factor *= Table.Clues[i].Yield; break; }
        if (!Work.InFocus(r, focus)) return factor;
        foreach (var followed in Followed(clues, focus)) if (followed.Resources!.Contains(r)) factor *= followed.Yield;
        return factor;
    }

    /// The resource clues a focus follows: those noticed that name something in that focus. General follows none.
    public static IEnumerable<ClueDef> Followed(IReadOnlyList<int> clues, ForageFocus focus) =>
        focus == ForageFocus.General ? []
        : clues.Select(i => Table.Clues[i]).Where(c => c.Kind == ClueKind.Resource && c.Resources!.Any(r => Work.InFocus(r, focus)));

    public static ForageFocus? Suggests(ClueDef c) =>
        c.Kind != ClueKind.Resource ? null
        : new[] { ForageFocus.Fuel, ForageFocus.Food, ForageFocus.Medicine, ForageFocus.Materials }.Cast<ForageFocus?>()
            .FirstOrDefault(f => c.Resources!.Any(r => Work.InFocus(r, f!.Value)));

    public static void Redraw(World world, TileId tile)
    {
        if (Pools.ForageAt(world, tile) is PoolId id) Pools.ForageStateFor(world, id).ClueDraw++;
    }
}
