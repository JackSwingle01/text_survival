using System.Text.Json.Serialization;

namespace Survival;

/// What a layer is made of. Mud is Ground that is wet and unfrozen; slush is Snow holding
/// liquid; ice is the frozen fraction of whatever holds the water.
public enum SurfaceMaterial { Ground, Snow, Water }

/// Walking is brief per step through everything in the way; kneeling (digging, tending a fire) is long.
public enum SurfaceContact
{
    Walking,
    WorkingOnGround,
    /// The longest contact there is, and the one bedding is for.
    RestingOnGround,
}

/// One layer of a tile's ground, stored bottom-to-top. Saturation counts liquid and
/// frozen pore water together, so freezing cannot conjure free space a thaw would un-conjure.
public sealed class SurfaceLayer
{
    public SurfaceMaterial Material { get; set; }
    public double ThicknessM { get; set; }
    public double CompactionPct { get; set; }
    public double WaterSaturationPct { get; set; }
    public double FrozenFractionPct { get; set; }
}

/// A tile's ground as the eye takes it in (Ground.SurfaceNow). What the map draws; the physics stays in Ground.
public readonly record struct SurfaceSummary(double SnowDepthM, bool Crusted, double MudLevel, double WaterDepthM, bool Iced);

/// A tile's ground, where it differs from generation. Depths are metres of a one-square-metre
/// column. Water is conserved: WaterAddedM = held + drained + evaporated + overflowed, always.
public sealed class GroundState
{
    public List<SurfaceLayer> Layers { get; init; } = [];
    public double WaterAddedM { get; set; }
    public double WaterDrainedM { get; set; }
    public double WaterEvaporatedM { get; set; }
    public double WaterOverflowedM { get; set; }
    /// The world minute this state describes. Always a multiple of Ground.StepMinutes.
    public long LastUpdatedMinute { get; set; }
}

/// Everything besides the sky that a never-observed tile's ground history depends on: tiles sharing these share ground.
internal readonly record struct GroundClass(SubstrateProfile Substrate, double CoverLevel, double TemperatureDeltaF, double WindFactor);

/// The ground under a tile before anything falls on it (Content/terrain.json substrates). Derived from terrain, never saved.
public sealed record SubstrateProfile(
    double ThicknessM,
    double PoreCapacityPct,
    double PermeabilityMPerMinute,
    double DrainagePerMinute,
    double RetentionPct,
    double RunoffPerMinute,
    double InitialSaturationPct,
    double SoftnessLevel,
    bool HoldsStandingWater)
{
    public double PoreVolumeM => ThicknessM * PoreCapacityPct;

    public static SubstrateProfile Loam => Named("loam");
    public static SubstrateProfile ForestFloor => Named("forestFloor");
    public static SubstrateProfile Slope => Named("slope");
    public static SubstrateProfile Rock => Named("rock");
    /// Holds nearly everything and lets go of none of it: wet in any weather, ponds the moment it rains.
    public static SubstrateProfile Peat => Named("peat");
    /// Lake ice. Inert here: standing water runs away. While the lake is open it is waded instead (Underfoot).
    public static SubstrateProfile IceSheet => Named("iceSheet");
    public static SubstrateProfile Impassable => Named("impassable");

    static SubstrateProfile Named(string id) =>
        Content.Tables.Substrates.TryGetValue(id, out var s) ? s : throw new KeyNotFoundException($"No substrate '{id}'");

    public static SubstrateProfile For(Terrain terrain) => Named(terrain.Row().Substrate);
}

/// Tiles catch up lazily, so when or how often one is looked at never changes what it holds.
public static class Ground
{
    /// Sub-step of the lazy catch-up, aligned to world minutes divisible by it.
    public const int StepMinutes = 15;

    const double IceExpansion = 1000.0 / 917.0;
    const double FreezingPointF = 32;
    /// Depths below this are rounding error, not state.
    const double NegligibleM = 1e-9;
    /// Fresh snow is about a tenth the density of ice; wind-packed old snow nearer 0.6.
    const double LooseSnowIceFraction = 0.109, DenseSnowIceFraction = 0.60;
    const double SnowRetentionPct = 0.05;
    const double SnowPermeabilityMPerMinute = 0.02;
    /// An e-fold of settling every two and a half days.
    const double SnowSettlingPerMinute = 0.00028;
    /// Degree-minute melt: about 3 mm water-equivalent per °C per day.
    const double PhaseChangeMPerFPerMinute = 1.16e-6;
    const double SunMeltMPerMinute = 3.3e-5;
    const double WindPhaseChangeFactor = 0.8;
    const double BaseEvaporationMPerMinute = 1.0e-7;
    const double SnowInsulationPerMetre = 12;
    /// Snow deeper than this is what a foot stands on, hiding the footing of whatever is under it.
    const double SnowFootingDepthM = 0.05;
    /// Pond ice this thick is as slick as ice gets; a thinner film is slick in proportion.
    const double FullSlickIceM = 0.004;
    const double SlickIceHazardDelta = 0.35;
    /// Deepest liquid water modelled; the excess leaves through the overflow sink.
    public const double MaxStandingWaterDepthM = 0.3048;
    const int MaxSnowLayers = 3;
    const double MergeCompactionTolerance = 0.08;
    /// Per-call step bound, also the percolation window for arriving water.
    const int MaxStepMinutes = 15;

    /// Caves and the pass stages are rock.
    public static SubstrateProfile Substrate(GeneratedMap m, TileId t) =>
        m.StageAt(t) != null || m.CaveAt(t) != Cave.None ? SubstrateProfile.Rock : SubstrateProfile.For(m.TerrainAt(t));

    /// Frozen through by default, with the starting water entered in the ledger.
    public static GroundState Baseline(SubstrateProfile sub, double initialFrozenFractionPct = 1.0)
    {
        var ground = new SurfaceLayer { Material = SurfaceMaterial.Ground, ThicknessM = sub.ThicknessM };
        SetStoredWaterM(ground, sub, sub.PoreVolumeM * sub.InitialSaturationPct,
            sub.PoreVolumeM * sub.InitialSaturationPct * initialFrozenFractionPct);
        var state = new GroundState { Layers = [ground] };
        state.WaterAddedM = StoredWaterM(state, sub);
        return state;
    }

    /// A never-observed tile starts frozen through at HistoryStartMinute and lives every minute since,
    /// so day 1 has weather behind it; only observed tiles pay (about 2 ms each). Stored in
    /// World.Tiles once it has lived any time; until then it is the unsaved baseline.
    public static GroundState CatchUp(World world, TileId tile)
    {
        long target = world.Minute / StepMinutes * StepMinutes;
        var sub = Substrate(Map.Of(world), tile);
        world.Tiles.TryGetValue(tile, out var ts);
        if (ts?.Ground is { } state)
        {
            StepTo(world, tile, state, sub, target);
            return state;
        }
        if (target <= HistoryStartMinute(world)) return Unobserved(world, tile, sub, target);
        state = Unobserved(world, tile, sub, target);
        if (ts == null) world.Tiles[tile] = ts = new TileState();
        ts.Ground = state;
        return state;
    }

    static void StepTo(World world, TileId tile, GroundState state, SubstrateProfile sub, long target)
    {
        var column = new Column(state, sub);
        for (long start = state.LastUpdatedMinute; start < target; start += StepMinutes)
            // Midpoint sample: weather holds for whole front states (two hours or more); only
            // sunlight moves within a sub-step, and the midpoint is its average to first order.
            column.Step(StepMinutes, Exposure.Surface(world, tile, start + StepMinutes / 2 + 1));
        state.LastUpdatedMinute = Math.Max(state.LastUpdatedMinute, target);
    }

    /// CatchUp without writing: a copy is caught up, so looking changes nothing.
    public static GroundState Peek(World world, TileId tile)
    {
        long target = world.Minute / StepMinutes * StepMinutes;
        var sub = Substrate(Map.Of(world), tile);
        if (world.Tiles.GetValueOrDefault(tile)?.Ground is not { } stored) return Unobserved(world, tile, sub, target);
        var state = Copy(stored);
        StepTo(world, tile, state, sub, target);
        return state;
    }

    /// Every tile of a GroundClass shares ground, so the class's latest is cached (World.GroundCache,
    /// never saved) and carried on: a whole-map pass pays once per class, with the result the long way gives.
    static GroundState Unobserved(World world, TileId tile, SubstrateProfile sub, long target)
    {
        var m = Map.Of(world);
        var key = ClassOf(m, tile, sub);
        var cache = world.GroundCache ??= [];
        GroundState state;
        if (cache.TryGetValue(key, out var known) && known.LastUpdatedMinute <= target) state = Copy(known);
        else
        {
            state = Baseline(sub);
            state.LastUpdatedMinute = HistoryStartMinute(world);
        }
        StepTo(world, tile, state, sub, target);
        if (known == null || known.LastUpdatedMinute < state.LastUpdatedMinute) cache[key] = Copy(state);
        return state;
    }

    static GroundClass ClassOf(GeneratedMap m, TileId tile, SubstrateProfile sub) =>
        new(sub, Map.OverheadCover(m, tile), Map.TemperatureDeltaF(m, tile), Map.WindFactor(m, tile));

    static GroundState Copy(GroundState s) => new()
    {
        Layers = [.. s.Layers.Select(l => new SurfaceLayer
        {
            Material = l.Material, ThicknessM = l.ThicknessM, CompactionPct = l.CompactionPct,
            WaterSaturationPct = l.WaterSaturationPct, FrozenFractionPct = l.FrozenFractionPct,
        })],
        WaterAddedM = s.WaterAddedM,
        WaterDrainedM = s.WaterDrainedM,
        WaterEvaporatedM = s.WaterEvaporatedM,
        WaterOverflowedM = s.WaterOverflowedM,
        LastUpdatedMinute = s.LastUpdatedMinute,
    };

    /// On an open lake the water is waded at the deepest the ground holds, so the ground's own
    /// rules apply. Snow on the lake's ice waits, unread, for the freeze.
    static Column Underfoot(World world, TileId tile, Func<World, TileId, GroundState> ground)
    {
        var m = Map.Of(world);
        var sub = Substrate(m, tile);
        if (!Map.IsOpenWater(m, tile, world.Minute)) return new Column(ground(world, tile), sub);
        var water = Baseline(sub);
        water.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Water, ThicknessM = MaxStandingWaterDepthM, WaterSaturationPct = 1 });
        return new Column(water, sub);
    }

    public static SurfaceSummary SurfaceNow(World world, TileId tile) => Underfoot(world, tile, Peek).Summary;

    /// Kept ground is caught up in place; never-kept ground is read without writing. Either way the same
    /// sub-steps, so what is seen does not depend on when anyone looked before.
    public static SurfaceSummary Look(World world, TileId tile) =>
        world.Tiles.GetValueOrDefault(tile)?.Ground != null ? Underfoot(world, tile, CatchUp).Summary : SurfaceNow(world, tile);

    public static double SnowDepthNowM(World world, TileId tile) => Underfoot(world, tile, Peek).SnowDepthM;

    /// A pinned test sky has no past, so its ground starts at minute 0.
    public static long HistoryStartMinute(World world) => world.FixedWeather == null ? -Weather.PreRollMinutes : 0;

    // ---- world queries (each one is an observation: it catches the tile up) ----

    public static double TraversalFactor(World world, TileId tile) => Underfoot(world, tile, CatchUp).TraversalFactor;

    /// Zero beside a frozen river, whose own ice (Map.TravelHazardLevel) already owns the hazard.
    public static double HazardDelta(World world, TileId tile) => HazardDelta(world, tile, CatchUp);

    // ---- the weather-only estimate: the ground as the weather alone has made it, never this tile's own ----

    /// What animals read (Traversal.GroundReadOf): the tile's ground class carried on through the weather, as if
    /// nobody had ever trodden or dug it. Cheap (one cached state per class) and never written.
    public static double TraversalFactorEstimate(World world, TileId tile) => Underfoot(world, tile, Estimate).TraversalFactor;

    public static double HazardDeltaEstimate(World world, TileId tile) => HazardDelta(world, tile, Estimate);

    static GroundState Estimate(World world, TileId tile) =>
        Unobserved(world, tile, Substrate(Map.Of(world), tile), world.Minute / StepMinutes * StepMinutes);

    /// The estimate's snow and a pond's open liquid (none under ice): what an animal drinks. Read from the class's cached state without copying it.
    public static (double SnowDepthM, double PondLiquidM) WaterEstimate(World world, TileId tile)
    {
        var m = Map.Of(world);
        if (Map.IsOpenWater(m, tile, world.Minute)) return (0, MaxStandingWaterDepthM);
        var sub = Substrate(m, tile);
        long target = world.Minute / StepMinutes * StepMinutes;
        var key = ClassOf(m, tile, sub);
        if (world.WaterEstimateCache is not { } cache || cache.Minute != target) world.WaterEstimateCache = cache = (target, []);
        if (cache.ByClass.TryGetValue(key, out var kept)) return kept;
        var state = world.GroundCache?.GetValueOrDefault(key) is { } known && known.LastUpdatedMinute == target ? known : Unobserved(world, tile, sub, target);
        var column = new Column(state, sub);
        return cache.ByClass[key] = (column.SnowDepthM, column.Summary.Iced ? 0 : column.StandingLiquidDepthM);
    }

    // ---- the same, read without writing (Peek) ----

    public static double TraversalFactorNow(World world, TileId tile) => Underfoot(world, tile, Peek).TraversalFactor;

    public static double HazardDeltaNow(World world, TileId tile) => HazardDelta(world, tile, Peek);

    static double HazardDelta(World world, TileId tile, Func<World, TileId, GroundState> ground) =>
        Map.Of(world).BesideRiver(tile) && Map.IsFrozen(Map.Of(world), tile, world.Minute) ? 0 : Underfoot(world, tile, ground).HazardDelta;

    public static double ContactWettingRate(World world, TileId tile, SurfaceContact contact, double currentWetnessPct) =>
        Underfoot(world, tile, CatchUp).ContactWettingRate(contact, currentWetnessPct);

    public static double SearchFactor(World world, TileId tile, double baseElevationM, double verticalExtentM) =>
        Underfoot(world, tile, CatchUp).SearchFactor(baseElevationM, verticalExtentM);

    public static double SnowDepthM(World world, TileId tile) => Underfoot(world, tile, CatchUp).SnowDepthM;

    // Burial access and digging need a per-placement elevation, footprint and hole depth on TileState; not built.
    // No route packing: nothing calls Compact.

    public static void Advance(GroundState s, SubstrateProfile sub, int minutes, SurfaceWeather weather)
    {
        var column = new Column(s, sub);
        for (int remaining = minutes; remaining > 0; remaining -= MaxStepMinutes)
            column.Step(Math.Min(remaining, MaxStepMinutes), weather);
    }

    public static void AddWater(GroundState s, SubstrateProfile sub, double waterDepthM) => new Column(s, sub).AddWater(waterDepthM);
    public static void AddSnow(GroundState s, SubstrateProfile sub, double waterEquivalentM) => new Column(s, sub).AddSnow(waterEquivalentM);
    public static void Compact(GroundState s, SubstrateProfile sub, double pressureFactor) => new Column(s, sub).Compact(pressureFactor);
    public static double TraversalFactor(GroundState s, SubstrateProfile sub) => new Column(s, sub).TraversalFactor;
    public static double HazardDelta(GroundState s, SubstrateProfile sub) => new Column(s, sub).HazardDelta;
    public static double ContactWettingRate(GroundState s, SubstrateProfile sub, SurfaceContact contact, double currentWetnessPct = 0) =>
        new Column(s, sub).ContactWettingRate(contact, currentWetnessPct);
    public static double SearchFactor(GroundState s, SubstrateProfile sub, double baseElevationM, double verticalExtentM) =>
        new Column(s, sub).SearchFactor(baseElevationM, verticalExtentM);
    public static double SnowDepthM(GroundState s, SubstrateProfile sub) => new Column(s, sub).SnowDepthM;
    public static double SurfaceHeightM(GroundState s, SubstrateProfile sub) => new Column(s, sub).SurfaceHeightM;
    public static double StandingWaterDepthM(GroundState s, SubstrateProfile sub) => new Column(s, sub).StandingWaterDepthM;
    public static double StandingLiquidDepthM(GroundState s, SubstrateProfile sub) => new Column(s, sub).StandingLiquidDepthM;
    public static double WetnessPct(GroundState s) => s.Layers[0].WaterSaturationPct;

    public static double StoredWaterM(GroundState s, SubstrateProfile sub) => s.Layers.Sum(l => TotalWaterM(l, sub));

    // ---- layer geometry ----

    static double SnowIceFraction(double compactionPct) =>
        LooseSnowIceFraction + Math.Clamp(compactionPct, 0, 1) * (DenseSnowIceFraction - LooseSnowIceFraction);

    static double PoreCapacityPct(SurfaceLayer l, SubstrateProfile sub) => l.Material switch
    {
        SurfaceMaterial.Snow => 1 - SnowIceFraction(l.CompactionPct),
        SurfaceMaterial.Ground => sub.PoreCapacityPct * (1 - 0.35 * l.CompactionPct),
        SurfaceMaterial.Water => 1,
        _ => 0,
    };

    static double PoreVolumeM(SurfaceLayer l, SubstrateProfile sub) => l.ThicknessM * PoreCapacityPct(l, sub);
    static double StoredLayerWaterM(SurfaceLayer l, SubstrateProfile sub) => PoreVolumeM(l, sub) * l.WaterSaturationPct;
    static double LiquidWaterM(SurfaceLayer l, SubstrateProfile sub) => StoredLayerWaterM(l, sub) * (1 - l.FrozenFractionPct);
    static double FrozenWaterM(SurfaceLayer l, SubstrateProfile sub) => StoredLayerWaterM(l, sub) * l.FrozenFractionPct;

    /// Water a snow layer is made of rather than holding: melting it releases water and shrinks the layer.
    static double SkeletonWaterEquivalentM(SurfaceLayer l, SubstrateProfile sub) =>
        l.Material == SurfaceMaterial.Snow ? l.ThicknessM * (1 - PoreCapacityPct(l, sub)) / IceExpansion : 0;

    static double TotalWaterM(SurfaceLayer l, SubstrateProfile sub) =>
        l.Material == SurfaceMaterial.Water ? l.ThicknessM : StoredLayerWaterM(l, sub) + SkeletonWaterEquivalentM(l, sub);

    /// How tall the layer stands; ice takes more room than the water it froze from.
    static double PhysicalThicknessM(SurfaceLayer l) => l.Material == SurfaceMaterial.Water
        ? l.ThicknessM * (1 - l.FrozenFractionPct) + IceThicknessM(l)
        : l.ThicknessM;

    static double IceThicknessM(SurfaceLayer pond) => pond.ThicknessM * pond.FrozenFractionPct * IceExpansion;

    /// Callers pass at most the layer's pore volume, and a layer with no room holds nothing: the few
    /// that can pass more (snow being packed or shrunk) take back the difference themselves.
    static void SetStoredWaterM(SurfaceLayer l, SubstrateProfile sub, double waterM, double frozenM)
    {
        double capacity = PoreVolumeM(l, sub);
        double stored = Math.Max(0, waterM);
        if (capacity <= NegligibleM || stored <= 0)
        {
            l.WaterSaturationPct = 0;
            l.FrozenFractionPct = 0;
            return;
        }
        l.WaterSaturationPct = Math.Clamp(stored / capacity, 0, 1);
        l.FrozenFractionPct = Math.Clamp(frozenM / stored, 0, 1);
    }

    /// Working view of one tile's state; holds nothing beyond water in transit within one operation.
    sealed class Column(GroundState state, SubstrateProfile sub)
    {
        readonly List<SurfaceLayer> _layers = state.Layers;
        double _displacedWaterM;

        SurfaceLayer GroundLayer => _layers[0];
        SurfaceLayer? Pond => _layers.Count > 1 && _layers[1].Material == SurfaceMaterial.Water ? _layers[1] : null;
        int SnowLayerCount => _layers.Count(l => l.Material == SurfaceMaterial.Snow);

        SurfaceLayer? TopSnow
        {
            get
            {
                for (int i = _layers.Count - 1; i >= 1; i--)
                    if (_layers[i].Material == SurfaceMaterial.Snow) return _layers[i];
                return null;
            }
        }

        double TopSnowCompaction => TopSnow?.CompactionPct ?? 0;

        double TopSnowLiquidFraction => TopSnowFraction(LiquidWaterM);
        double TopSnowFrozenLiquid => TopSnowFraction(FrozenWaterM);

        double TopSnowFraction(Func<SurfaceLayer, SubstrateProfile, double> water)
        {
            var snow = TopSnow;
            if (snow == null) return 0;
            double capacity = PoreVolumeM(snow, sub) * SnowRetentionPct;
            if (capacity <= NegligibleM) return 0;
            return Math.Clamp(water(snow, sub) / capacity, 0, 1);
        }

        double MudLevel
        {
            get
            {
                if (SnowDepthM > 0.02) return 0;
                var g = GroundLayer;
                return Math.Clamp(g.WaterSaturationPct * (1 - g.FrozenFractionPct) * sub.SoftnessLevel, 0, 1);
            }
        }

        // ---- queries ----

        public double SurfaceHeightM
        {
            get
            {
                double height = GroundLayer.ThicknessM - sub.ThicknessM;
                for (int i = 1; i < _layers.Count; i++) height += PhysicalThicknessM(_layers[i]);
                return height;
            }
        }

        public double SnowDepthM => _layers.Where(l => l.Material == SurfaceMaterial.Snow).Sum(l => l.ThicknessM);
        public double StandingWaterDepthM => Pond is { } p ? PhysicalThicknessM(p) : 0;
        public double StandingLiquidDepthM => Pond is { } p ? p.ThicknessM * (1 - p.FrozenFractionPct) : 0;

        public SurfaceSummary Summary => new(SnowDepthM, SnowDepthM > SnowFootingDepthM && TopSnowFrozenLiquid > 0.25, MudLevel, StandingWaterDepthM,
            Pond is { } p && p.FrozenFractionPct > 0.5);

        public double TraversalFactor
        {
            get
            {
                double factor = 1;
                double snow = SnowDepthM;
                if (snow > 0.02)
                {
                    double looseness = 1 - 0.55 * TopSnowCompaction; // loose snow you wade; packed you walk on
                    factor *= 1 + Math.Pow(snow / 0.10, 1.3) * 0.25 * looseness;
                }
                double water = StandingLiquidDepthM;
                if (water > 0.01) factor *= 1 + water / MaxStandingWaterDepthM * 0.8;
                factor *= 1 + MudLevel * 0.25;
                return Math.Clamp(factor, 1, 6);
            }
        }

        public double HazardDelta
        {
            get
            {
                double hazard = 0;
                bool snowIsFooting = SnowDepthM > SnowFootingDepthM;
                if (!snowIsFooting && Pond is { } pond) hazard += SlickIceHazardDelta * Math.Min(1, IceThicknessM(pond) / FullSlickIceM);
                if (snowIsFooting)
                {
                    hazard += 0.05;
                    if (TopSnowFrozenLiquid > 0.25) hazard += 0.10; // refrozen crust
                }
                hazard += MudLevel * 0.05;
                return Math.Clamp(hazard, 0, 0.5);
            }
        }

        /// In proportion to how much of it is under the cover, floored: a discovery that can never be made should not have been placed.
        public double SearchFactor(double baseElevationM, double verticalExtentM)
        {
            const double Floor = 0.1;
            double extent = Math.Max(verticalExtentM, 0.05);
            double cover = SurfaceHeightM - baseElevationM;
            if (cover <= 0) return 1;
            return 1 - Math.Clamp(cover / extent, 0, 1) * (1 - Floor);
        }

        /// Per minute, before clothing or bedding. Each contact has a ceiling; frozen ground has no liquid to give.
        public double ContactWettingRate(SurfaceContact contact, double currentWetnessPct)
        {
            double pond = Math.Clamp(StandingLiquidDepthM / 0.15, 0, 1); // ankle-deep is as wet as walking gets
            double slush = TopSnowLiquidFraction * 0.3;
            double damp = GroundLayer.WaterSaturationPct * (1 - GroundLayer.FrozenFractionPct) * 0.05;
            double source = Math.Clamp(pond + slush + damp, 0, 1);
            if (source <= 0) return 0;

            double rate = contact switch
            {
                SurfaceContact.Walking => 0.12,
                SurfaceContact.RestingOnGround => 0.10,
                SurfaceContact.WorkingOnGround => 0.06,
                _ => throw new ArgumentOutOfRangeException(nameof(contact), contact, null),
            };
            return currentWetnessPct >= ContactCeiling(contact) ? 0 : source * rate;
        }

        /// Walking is geometry; lying in two centimetres of meltwater soaks your whole back.
        double ContactCeiling(SurfaceContact contact) => contact switch
        {
            SurfaceContact.RestingOnGround => 0.7,
            SurfaceContact.WorkingOnGround => 0.4,
            // Hip-deep (0.9 m) wets you all over; wet ground with nothing on it still gets into your boots.
            _ => Math.Clamp(Math.Max(StandingLiquidDepthM, SnowDepthM) / 0.9, 0.1, 1),
        };

        // ---- operations ----

        public void AddSnow(double waterEquivalentM)
        {
            if (waterEquivalentM <= NegligibleM) return;
            state.WaterAddedM += waterEquivalentM;
            double thickness = waterEquivalentM * IceExpansion / LooseSnowIceFraction;
            _layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = thickness });
            Consolidate();
        }

        public void AddWater(double waterDepthM)
        {
            if (waterDepthM <= NegligibleM) return;
            state.WaterAddedM += waterDepthM;
            Percolate(waterDepthM, MaxStepMinutes);
        }

        public void Compact(double pressureFactor)
        {
            double pressure = Math.Clamp(pressureFactor, 0, 1);
            if (pressure <= 0) return;
            for (int i = _layers.Count - 1; i >= 1; i--)
            {
                if (_layers[i].Material != SurfaceMaterial.Snow) continue;
                SetSnowCompaction(_layers[i], _layers[i].CompactionPct + (1 - _layers[i].CompactionPct) * pressure);
                break;
            }
            Consolidate();
        }

        public void Step(int minutes, SurfaceWeather weather)
        {
            if (weather.SnowfallWeMPerMinute > 0) AddSnow(weather.SnowfallWeMPerMinute * minutes);
            if (weather.RainfallMPerMinute > 0) AddWater(weather.RainfallMPerMinute * minutes);
            ChangePhase(minutes, weather);
            Settle(minutes);
            Infiltrate(minutes);
            Drain(minutes);
            RunOff(minutes);
            Evaporate(minutes, weather);
            CapPonding();
            Consolidate();
        }

        void ChangePhase(int minutes, SurfaceWeather weather)
        {
            double degrees = weather.AirTemperatureF - FreezingPointF;
            if (Math.Abs(degrees) < 0.01) return;
            double windGain = 1 + weather.WindLevel * WindPhaseChangeFactor;
            double budget = Math.Abs(degrees) * PhaseChangeMPerFPerMinute * windGain * minutes;
            if (degrees > 0) Melt(budget + weather.SunlightLevel * SunMeltMPerMinute * minutes);
            else Freeze(budget);
        }

        void Melt(double budgetM)
        {
            double released = 0;
            for (int i = _layers.Count - 1; i >= 0 && budgetM > NegligibleM; i--)
            {
                var layer = _layers[i];
                double exposure = ExposureAt(i);
                double available = budgetM * exposure;
                if (available <= NegligibleM) continue;

                // Frozen pore water goes before the skeleton holding it.
                double frozenPore = FrozenWaterM(layer, sub);
                if (frozenPore > NegligibleM)
                {
                    double thawed = Math.Min(frozenPore, available);
                    SetStoredWaterM(layer, sub, StoredLayerWaterM(layer, sub), frozenPore - thawed);
                    available -= thawed;
                    budgetM -= thawed / exposure;
                }

                if (layer.Material != SurfaceMaterial.Snow || available <= NegligibleM) continue;

                double skeletonWe = SkeletonWaterEquivalentM(layer, sub);
                double melted = Math.Min(skeletonWe, available);
                if (melted <= NegligibleM) continue;
                ResizeSnow(layer, skeletonWe - melted, StoredLayerWaterM(layer, sub), FrozenWaterM(layer, sub));
                released += melted;
                budgetM -= melted / exposure;
            }
            if (released > NegligibleM) Percolate(released, MaxStepMinutes);
            FlushDisplacedWater();
        }

        /// Freezing moves water between ledger columns, never out.
        void Freeze(double budgetM)
        {
            for (int i = _layers.Count - 1; i >= 0 && budgetM > NegligibleM; i--)
            {
                var layer = _layers[i];
                double exposure = ExposureAt(i);
                double available = budgetM * exposure;
                if (available <= NegligibleM) continue;
                double liquid = LiquidWaterM(layer, sub);
                if (liquid <= NegligibleM) continue;
                double frozenNow = Math.Min(liquid, available);
                SetStoredWaterM(layer, sub, StoredLayerWaterM(layer, sub), FrozenWaterM(layer, sub) + frozenNow);
                budgetM -= frozenNow / exposure;
            }
        }

        /// How much of the weather reaches layer index: snow is a blanket.
        double ExposureAt(int index)
        {
            double snowAbove = 0;
            for (int i = index + 1; i < _layers.Count; i++)
                if (_layers[i].Material == SurfaceMaterial.Snow) snowAbove += _layers[i].ThicknessM;
            return 1 / (1 + snowAbove * SnowInsulationPerMetre);
        }

        void Percolate(double liquidM, int minutes)
        {
            double carried = liquidM;
            for (int i = _layers.Count - 1; i >= 1 && carried > NegligibleM; i--)
            {
                var layer = _layers[i];
                if (layer.Material != SurfaceMaterial.Snow) continue;
                double capacity = PoreVolumeM(layer, sub) * SnowRetentionPct;
                double held = StoredLayerWaterM(layer, sub);
                double room = Math.Max(0, capacity - held);
                double absorbed = Math.Min(carried, Math.Min(room, SnowPermeabilityMPerMinute * minutes));
                if (absorbed > NegligibleM)
                {
                    SetStoredWaterM(layer, sub, held + absorbed, FrozenWaterM(layer, sub));
                    carried -= absorbed;
                }
            }
            if (carried > 0) AddToPond(carried);
        }

        /// On lake ice water runs away, and so does a film too thin to stand as a pond.
        void AddToPond(double liquidM)
        {
            var pond = Pond;
            if (!sub.HoldsStandingWater || (pond == null && liquidM <= NegligibleM))
            {
                state.WaterOverflowedM += liquidM;
                return;
            }
            if (pond == null)
            {
                _layers.Insert(1, new SurfaceLayer { Material = SurfaceMaterial.Water, ThicknessM = liquidM, WaterSaturationPct = 1 });
                return;
            }
            double frozen = pond.ThicknessM * pond.FrozenFractionPct;
            pond.ThicknessM += liquidM;
            pond.WaterSaturationPct = 1;
            pond.FrozenFractionPct = pond.ThicknessM > 0 ? Math.Clamp(frozen / pond.ThicknessM, 0, 1) : 0;
        }

        static void DrawPond(SurfaceLayer pond, double lostM)
        {
            double frozen = pond.ThicknessM * pond.FrozenFractionPct;
            pond.ThicknessM -= lostM;
            pond.FrozenFractionPct = pond.ThicknessM > NegligibleM ? Math.Clamp(frozen / pond.ThicknessM, 0, 1) : 0;
        }

        void Infiltrate(int minutes)
        {
            var pond = Pond;
            if (pond == null) return;
            double liquid = pond.ThicknessM * (1 - pond.FrozenFractionPct);
            if (liquid <= NegligibleM) return;
            var g = GroundLayer;
            double room = Math.Max(0, PoreVolumeM(g, sub) - StoredLayerWaterM(g, sub));
            double soaked = Math.Min(liquid, Math.Min(room, sub.PermeabilityMPerMinute * minutes));
            if (soaked <= NegligibleM) return;
            SetStoredWaterM(g, sub, StoredLayerWaterM(g, sub) + soaked, FrozenWaterM(g, sub));
            DrawPond(pond, soaked);
        }

        void Drain(int minutes)
        {
            var g = GroundLayer;
            double capacity = PoreVolumeM(g, sub);
            if (capacity <= NegligibleM) return;
            double held = StoredLayerWaterM(g, sub);
            double frozen = FrozenWaterM(g, sub);
            double free = Math.Max(0, held - frozen - capacity * sub.RetentionPct);
            if (free <= NegligibleM) return;
            double lost = Math.Min(free, free * sub.DrainagePerMinute * minutes);
            SetStoredWaterM(g, sub, held - lost, frozen);
            state.WaterDrainedM += lost;
        }

        /// Ponded water finds a way off the tile. Rock sheds it; peat does not.
        void RunOff(int minutes)
        {
            var pond = Pond;
            if (pond == null) return;
            double liquid = pond.ThicknessM * (1 - pond.FrozenFractionPct);
            if (liquid <= NegligibleM) return;
            double lost = Math.Min(liquid, liquid * sub.RunoffPerMinute * minutes);
            if (lost <= NegligibleM) return;
            DrawPond(pond, lost);
            state.WaterOverflowedM += lost;
        }

        void Evaporate(int minutes, SurfaceWeather weather)
        {
            double rate = BaseEvaporationMPerMinute * (1 + weather.WindLevel * 2) * (1 + weather.SunlightLevel * 3) * minutes;
            if (rate <= NegligibleM) return;

            var top = _layers[^1];
            double liquid = top.Material == SurfaceMaterial.Water
                ? top.ThicknessM * (1 - top.FrozenFractionPct)
                : LiquidWaterM(top, sub);
            double lost = Math.Min(rate, liquid);
            if (lost <= NegligibleM)
            {
                if (top.Material != SurfaceMaterial.Snow) return; // dry snow still sublimates
                double skeletonWe = SkeletonWaterEquivalentM(top, sub);
                double sublimated = Math.Min(rate, skeletonWe);
                if (sublimated <= NegligibleM) return;
                ResizeSnow(top, skeletonWe - sublimated, StoredLayerWaterM(top, sub), FrozenWaterM(top, sub));
                state.WaterEvaporatedM += sublimated;
                return;
            }
            if (top.Material == SurfaceMaterial.Water) DrawPond(top, lost);
            else SetStoredWaterM(top, sub, StoredLayerWaterM(top, sub) - lost, FrozenWaterM(top, sub));
            state.WaterEvaporatedM += lost;
        }

        /// A foot of liquid is as deep as this goes; the rest leaves through a named sink. Ice is never capped.
        void CapPonding()
        {
            var pond = Pond;
            if (pond == null) return;
            double excess = pond.ThicknessM * (1 - pond.FrozenFractionPct) - MaxStandingWaterDepthM;
            if (excess <= NegligibleM) return;
            DrawPond(pond, excess);
            state.WaterOverflowedM += excess;
        }

        void Settle(int minutes)
        {
            for (int i = 1; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.Material != SurfaceMaterial.Snow) continue;
                SetSnowCompaction(layer, layer.CompactionPct + (1 - layer.CompactionPct) * SnowSettlingPerMinute * minutes);
            }
            FlushDisplacedWater();
        }

        /// Compact a snow layer, conserving its ice. Water squeezed out is displaced downward, not deleted.
        void SetSnowCompaction(SurfaceLayer layer, double compaction)
        {
            double skeletonWe = SkeletonWaterEquivalentM(layer, sub);
            double water = StoredLayerWaterM(layer, sub);
            double frozen = FrozenWaterM(layer, sub);

            double skeletonM = skeletonWe * IceExpansion;
            double target = Math.Clamp(compaction, layer.CompactionPct, 1);
            // Frozen pore water is a solid: stop compacting where the ice fraction reaches
            // skeleton / (skeleton + frozen), or the skeleton grows out of nothing and breaks the water ledger.
            if (frozen > NegligibleM && skeletonM > NegligibleM)
            {
                double maxCompaction = (skeletonM / (skeletonM + frozen) - LooseSnowIceFraction) / (DenseSnowIceFraction - LooseSnowIceFraction);
                target = Math.Max(layer.CompactionPct, Math.Min(target, maxCompaction));
            }
            layer.CompactionPct = target;
            layer.ThicknessM = skeletonM / SnowIceFraction(target);
            SetStoredWaterM(layer, sub, Math.Min(water, PoreVolumeM(layer, sub)), frozen);
            Displace(water - StoredLayerWaterM(layer, sub));
        }

        /// Rebuild a snow layer around a new amount of skeleton ice, keeping its water.
        /// A crumb of skeleton too small to keep as a layer melts into the water it leaves behind.
        void ResizeSnow(SurfaceLayer layer, double skeletonWeM, double waterM, double frozenM)
        {
            bool crumb = skeletonWeM <= NegligibleM;
            layer.ThicknessM = crumb ? 0 : skeletonWeM * IceExpansion / SnowIceFraction(layer.CompactionPct);
            double kept = Math.Min(waterM, PoreVolumeM(layer, sub));
            SetStoredWaterM(layer, sub, kept, Math.Min(frozenM, kept));
            Displace(waterM - StoredLayerWaterM(layer, sub) + (crumb ? Math.Max(0, skeletonWeM) : 0));
        }

        /// Water a layer could no longer hold, in transit down. However little, it is never dropped:
        /// settling saturated snow squeezes out a sub-nanometre every sub-step, which would break the ledger.
        void Displace(double waterM)
        {
            if (waterM > 0) _displacedWaterM += waterM;
        }

        void FlushDisplacedWater()
        {
            if (_displacedWaterM <= 0) return;
            double displaced = _displacedWaterM;
            _displacedWaterM = 0;
            Percolate(displaced, MaxStepMinutes);
        }

        /// Coarsens an over-full stack, never by discarding material.
        void Consolidate()
        {
            FlushDisplacedWater();
            for (int i = _layers.Count - 1; i >= 1; i--)
            {
                var layer = _layers[i];
                bool empty = layer.Material == SurfaceMaterial.Water
                    ? layer.ThicknessM <= NegligibleM
                    : layer.ThicknessM <= NegligibleM && StoredLayerWaterM(layer, sub) <= NegligibleM;
                if (!empty) continue;
                double stranded = TotalWaterM(layer, sub);
                _layers.RemoveAt(i);
                if (stranded > NegligibleM) Percolate(stranded, MaxStepMinutes);
                else if (stranded > 0) state.WaterOverflowedM += stranded; // too little to stand anywhere
            }

            for (int i = _layers.Count - 1; i >= 2; i--)
            {
                if (_layers[i].Material != SurfaceMaterial.Snow || _layers[i - 1].Material != SurfaceMaterial.Snow) continue;
                if (Math.Abs(_layers[i].CompactionPct - _layers[i - 1].CompactionPct) > MergeCompactionTolerance) continue;
                MergeSnow(i - 1, i);
            }

            while (SnowLayerCount > MaxSnowLayers) MergeClosestSnow();
            FlushDisplacedWater();
        }

        void MergeClosestSnow()
        {
            int best = -1;
            double closest = double.MaxValue;
            for (int i = 1; i < _layers.Count - 1; i++)
            {
                if (_layers[i].Material != SurfaceMaterial.Snow || _layers[i + 1].Material != SurfaceMaterial.Snow) continue;
                double gap = Math.Abs(_layers[i].CompactionPct - _layers[i + 1].CompactionPct);
                if (gap >= closest) continue;
                closest = gap;
                best = i;
            }
            // Snow layers can be split by nothing (a pond sits only at index 1), so a pair always exists.
            if (best < 0) throw new InvalidOperationException("More than three snow layers but no adjacent pair to merge");
            MergeSnow(best, best + 1);
        }

        void MergeSnow(int lower, int upper)
        {
            var a = _layers[lower];
            var b = _layers[upper];
            double skeletonA = SkeletonWaterEquivalentM(a, sub);
            double skeletonB = SkeletonWaterEquivalentM(b, sub);
            double skeleton = skeletonA + skeletonB;
            double water = StoredLayerWaterM(a, sub) + StoredLayerWaterM(b, sub);
            double frozen = FrozenWaterM(a, sub) + FrozenWaterM(b, sub);
            if (skeleton > NegligibleM) a.CompactionPct = (a.CompactionPct * skeletonA + b.CompactionPct * skeletonB) / skeleton;
            ResizeSnow(a, skeleton, water, frozen);
            _layers.RemoveAt(upper);
        }
    }
}
