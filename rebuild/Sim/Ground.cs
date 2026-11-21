using System.Text.Json.Serialization;

namespace Survival;

/// What a layer is made of. Mud is Ground that is wet and unfrozen; slush is Snow holding
/// liquid; ice is the frozen fraction of whatever holds the water.
public enum SurfaceMaterial { Ground, Snow, Water }

/// How something is touching the ground. Different contact, different soaking.
public enum SurfaceContact
{
    /// Walking through it: brief per step, but you go through everything in the way.
    Walking,
    /// Kneeling, digging, tending a fire: hands and knees in it for a long time.
    WorkingOnGround,
    /// Lying on it: the longest contact there is, and the one bedding is for.
    RestingOnGround,
}

/// One horizontal layer of a tile's ground, stored bottom-to-top. Saturation counts liquid and
/// frozen pore water together, so freezing cannot conjure free space a thaw would un-conjure.
/// Operations work in metres of skeleton and water and recompute the fractions afterwards.
public sealed class SurfaceLayer
{
    public SurfaceMaterial Material { get; set; }
    public double ThicknessM { get; set; }
    public double CompactionPct { get; set; }
    public double WaterSaturationPct { get; set; }
    public double FrozenFractionPct { get; set; }
}

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

/// Everything a never-observed tile's ground history depends on besides the sky (Exposure.Surface
/// and Ground.Substrate): tiles that share these have the same ground until something observes one.
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

    // The profiles themselves are Content/terrain.json's substrates; these name the ones code and tests ask for.
    public static SubstrateProfile Loam => Named("loam");
    public static SubstrateProfile ForestFloor => Named("forestFloor");
    public static SubstrateProfile Slope => Named("slope");
    /// Takes almost nothing in, so rain ponds at once and then runs off the tile.
    public static SubstrateProfile Rock => Named("rock");
    /// Holds nearly everything and lets go of none of it: wet in any weather, ponds the moment it rains.
    public static SubstrateProfile Peat => Named("peat");
    /// Lake ice. Inert here: standing water runs away.
    public static SubstrateProfile IceSheet => Named("iceSheet");
    public static SubstrateProfile Impassable => Named("impassable");

    static SubstrateProfile Named(string id) =>
        Content.Tables.Substrates.TryGetValue(id, out var s) ? s : throw new KeyNotFoundException($"No substrate '{id}'");

    public static SubstrateProfile For(Terrain terrain) => Named(terrain.Row().Substrate);
}

/// Snow accumulates, packs, melts and refreezes; rain and meltwater soak in until the ground will
/// take no more and then stand on it; standing water freezes from the top down. Port of the old
/// GroundSurface. Tiles catch up lazily when observed (plan decision 3), in sub-steps aligned to
/// the world clock, so when or how often a tile is looked at never changes what it holds.
public static class Ground
{
    /// Sub-step of the lazy catch-up, aligned to world minutes divisible by it. The old game's
    /// own MaxStepMinutes, so a sub-step is one old background batch.
    public const int StepMinutes = 15;

    // ---- physics constants (old SurfacePhysics) ----

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
    /// Deepest liquid water modelled; the excess leaves through the overflow sink.
    public const double MaxStandingWaterDepthM = 0.3048;
    const int MaxSnowLayers = 3;
    const double MergeCompactionTolerance = 0.08;
    /// The old per-call step bound, also used as the percolation window for arriving water.
    const int MaxStepMinutes = 15;

    // ---- per-tile ground from the generated map ----

    /// What the ground is made of. Caves and the pass stages are rock, the camp is forest floor, as their old Locations were.
    public static SubstrateProfile Substrate(GeneratedMap m, TileId t) =>
        m.StageAt(t) != null || m.CaveAt(t) != Cave.None ? SubstrateProfile.Rock : SubstrateProfile.For(m.TerrainAt(t));

    /// Ground as generated: the substrate at its starting saturation, frozen through (initial
    /// frozen fraction 1, the old default), with that water entered in the ledger.
    public static GroundState Baseline(SubstrateProfile sub, double initialFrozenFractionPct = 1.0)
    {
        var ground = new SurfaceLayer { Material = SurfaceMaterial.Ground, ThicknessM = sub.ThicknessM };
        SetStoredWaterM(ground, sub, sub.PoreVolumeM * sub.InitialSaturationPct,
            sub.PoreVolumeM * sub.InitialSaturationPct * initialFrozenFractionPct);
        var state = new GroundState { Layers = [ground] };
        state.WaterAddedM = StoredWaterM(state, sub);
        return state;
    }

    // ---- lazy catch-up ----

    /// The tile's ground as of the latest StepMinutes boundary at or before world.Minute,
    /// simulated forward from wherever it was last left. A tile never observed starts frozen
    /// through at HistoryStartMinute and lives every minute since, so in the game it has the month
    /// of weather before minute 0 behind it, as the old Settle pass would have if ground had been
    /// cheap enough to run on every tile: here only observed tiles pay, about 2 ms each. Stored in
    /// World.Tiles once it has lived any time; until then it is the unsaved baseline.
    /// old game: day 1 opened on ground frozen through in July, whatever the weather (bug, fixed).
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

    /// The tile's ground as CatchUp would leave it now, without writing anything: a copy is caught
    /// up, so looking (Tags) changes nothing.
    public static GroundState Peek(World world, TileId tile)
    {
        long target = world.Minute / StepMinutes * StepMinutes;
        var sub = Substrate(Map.Of(world), tile);
        if (world.Tiles.GetValueOrDefault(tile)?.Ground is not { } stored) return Unobserved(world, tile, sub, target);
        var state = Copy(stored);
        StepTo(world, tile, state, sub, target);
        return state;
    }

    /// The ground of a tile nobody has observed, as of target: frozen through at HistoryStartMinute
    /// and lived every sub-step since. That history depends on nothing but the sky and the four
    /// things the tile's weather and substrate are made of (GroundClass), so every tile of a class
    /// has the same ground, and the class's latest is kept (World.GroundCache, never saved) to be
    /// carried on from: a whole-map pass pays for each class once, not for each tile from the start
    /// of history, and the result is the one the long way gives, sub-step for sub-step.
    static GroundState Unobserved(World world, TileId tile, SubstrateProfile sub, long target)
    {
        var m = Map.Of(world);
        var key = new GroundClass(sub, Map.OverheadCover(m, tile), Map.TemperatureDeltaF(m, tile), Map.WindFactor(m, tile));
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

    static GroundState Copy(GroundState s) => new()
    {
        Layers = [.. s.Layers.Select(l => new SurfaceLayer
        {
            Material = l.Material, ThicknessM = l.ThicknessM, CompactionPct = l.CompactionPct,
            WaterSaturationPct = l.WaterSaturationPct, FrozenFractionPct = l.FrozenFractionPct,
        })],
        WaterAddedM = s.WaterAddedM, WaterDrainedM = s.WaterDrainedM, WaterEvaporatedM = s.WaterEvaporatedM,
        WaterOverflowedM = s.WaterOverflowedM, LastUpdatedMinute = s.LastUpdatedMinute,
    };

    /// Snow on the tile now, read without writing (Peek).
    public static double SnowDepthNowM(World world, TileId tile) => SnowDepthM(Peek(world, tile), Substrate(Map.Of(world), tile));

    /// When the ground's history begins: the weather pre-roll for a seeded world. A pinned test sky
    /// has no past, so its ground starts at minute 0 as generated.
    public static long HistoryStartMinute(World world) => world.FixedWeather == null ? -Weather.PreRollMinutes : 0;

    // ---- world queries (each one is an observation: it catches the tile up) ----

    /// How much slower the ground is to cross than bare dry earth; 1 is the baseline.
    public static double TraversalFactor(World world, TileId tile) =>
        new Column(CatchUp(world, tile), Substrate(Map.Of(world), tile)).TraversalFactor;

    /// Footing hazard the surface adds: slick ice, a crust, mud. Zero beside a river, whose own
    /// ice (Map.TravelHazardLevel) already owns the hazard, as the old WaterFeature did.
    public static double HazardDelta(World world, TileId tile) => HazardDelta(world, tile, CatchUp);

    // ---- the same, read without writing (Peek): looking at a tile from a list or a preview ----

    public static double TraversalFactorNow(World world, TileId tile) =>
        new Column(Peek(world, tile), Substrate(Map.Of(world), tile)).TraversalFactor;

    public static double HazardDeltaNow(World world, TileId tile) => HazardDelta(world, tile, Peek);

    static double HazardDelta(World world, TileId tile, Func<World, TileId, GroundState> ground) =>
        Map.Of(world).BesideRiver(tile) ? 0 : new Column(ground(world, tile), Substrate(Map.Of(world), tile)).HazardDelta;

    /// Wetness picked up per minute by whoever is in this contact with the ground.
    public static double ContactWettingRate(World world, TileId tile, SurfaceContact contact, double currentWetnessPct) =>
        new Column(CatchUp(world, tile), Substrate(Map.Of(world), tile)).ContactWettingRate(contact, currentWetnessPct);

    /// How well a thing here can be searched against bare ground (Pools.Search).
    public static double SearchFactor(World world, TileId tile, double baseElevationM, double verticalExtentM) =>
        new Column(CatchUp(world, tile), Substrate(Map.Of(world), tile)).SearchFactor(baseElevationM, verticalExtentM);

    public static double SnowDepthM(World world, TileId tile) => new Column(CatchUp(world, tile), Substrate(Map.Of(world), tile)).SnowDepthM;

    // Burial access and digging (old GroundSurface.Excavation, DigUpStrategy) are not ported: they need a
    // per-placement elevation, footprint and hole depth on TileState.
    // No route packing (Compact is ported but nothing calls it); the old game deferred it too.

    // ---- state-level API: the old GroundSurface methods, for tests and the catch-up ----

    /// Old GroundSurface.Advance: bounded steps of at most 15 minutes under one weather.
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
    /// How wet the ground itself is, ignoring anything standing on it.
    public static double WetnessPct(GroundState s) => s.Layers[0].WaterSaturationPct;

    /// Every drop the tile is holding, however it is held.
    public static double StoredWaterM(GroundState s, SubstrateProfile sub) => s.Layers.Sum(l => TotalWaterM(l, sub));

    // ---- layer geometry (old SurfaceLayer) ----

    static double SnowIceFraction(double compactionPct) =>
        LooseSnowIceFraction + Math.Clamp(compactionPct, 0, 1) * (DenseSnowIceFraction - LooseSnowIceFraction);

    /// Fraction of the layer's volume that is pore space; the material decides what compaction means.
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
        ? l.ThicknessM * (1 - l.FrozenFractionPct) + l.ThicknessM * l.FrozenFractionPct * IceExpansion
        : l.ThicknessM;

    static void SetStoredWaterM(SurfaceLayer l, SubstrateProfile sub, double waterM, double frozenM)
    {
        double capacity = PoreVolumeM(l, sub);
        double stored = Math.Max(0, waterM);
        if (capacity <= NegligibleM || stored <= NegligibleM)
        {
            l.WaterSaturationPct = 0;
            l.FrozenFractionPct = 0;
            return;
        }
        l.WaterSaturationPct = Math.Clamp(stored / capacity, 0, 1);
        l.FrozenFractionPct = Math.Clamp(frozenM / stored, 0, 1);
    }

    /// The working view of one tile's state: the old GroundSurface instance methods, nearly
    /// verbatim. Holds no state of its own beyond water in transit within one operation.
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

        /// Mud: wet, soft, unfrozen ground with nothing on top of it.
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

        /// Top of the modelled cover above bare ground as it was found.
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
                if (Pond is { } pond && pond.FrozenFractionPct > 0.02) hazard += 0.35 * Math.Min(1, pond.FrozenFractionPct * 3);
                if (SnowDepthM > 0.05)
                {
                    hazard += 0.05;
                    if (TopSnowFrozenLiquid > 0.25) hazard += 0.10; // refrozen crust
                }
                hazard += MudLevel * 0.05;
                return Math.Clamp(hazard, 0, 0.5);
            }
        }

        /// Cover hides a thing in proportion to how much of it is under the cover, down to a
        /// floor: a discovery that can never be made should not have been placed.
        public double SearchFactor(double baseElevationM, double verticalExtentM)
        {
            const double Floor = 0.1;
            double extent = Math.Max(verticalExtentM, 0.05);
            double cover = SurfaceHeightM - baseElevationM;
            if (cover <= 0) return 1;
            return 1 - Math.Clamp(cover / extent, 0, 1) * (1 - Floor);
        }

        /// Wetness per minute, before clothing or bedding. Each contact has a ceiling on how wet
        /// it can get you; frozen ground has no liquid to give.
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

        /// How far up you the wet stuff reaches. Walking it is geometry; lying in two
        /// centimetres of meltwater soaks your whole back.
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

        /// Press the top snow down: 0 is nothing, 1 packs it as hard as snow packs.
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

        /// Warmth works down from the top, spending itself as it goes.
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

        /// Cold works down from the top too. Freezing moves water between ledger columns, never out.
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

        /// Liquid arriving at the top works down; snow holds a little and passes the rest on.
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
            if (carried > NegligibleM) AddToPond(carried);
        }

        /// Water that reached the ground. On lake ice it runs away.
        void AddToPond(double liquidM)
        {
            if (!sub.HoldsStandingWater)
            {
                state.WaterOverflowedM += liquidM;
                return;
            }
            var pond = Pond;
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

        /// Take liquid off the pond, keeping its ice.
        static void DrawPond(SurfaceLayer pond, double lostM)
        {
            double frozen = pond.ThicknessM * pond.FrozenFractionPct;
            pond.ThicknessM -= lostM;
            pond.FrozenFractionPct = pond.ThicknessM > NegligibleM ? Math.Clamp(frozen / pond.ThicknessM, 0, 1) : 0;
        }

        /// Ponded liquid soaks in, as fast as the ground will take it.
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

        /// Free water keeps going down, out of the modelled layer.
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
            // Frozen pore water is a solid: stop compacting where it still fits, which is where the
            // ice fraction reaches skeleton / (skeleton + frozen).
            // Old game: it compacted anyway and then thickened the layer to frozen / pore, which
            // grew the skeleton out of nothing and broke the water ledger (bug, fixed).
            if (frozen > NegligibleM && skeletonM > NegligibleM)
            {
                double maxCompaction = (skeletonM / (skeletonM + frozen) - LooseSnowIceFraction) / (DenseSnowIceFraction - LooseSnowIceFraction);
                target = Math.Max(layer.CompactionPct, Math.Min(target, maxCompaction));
            }
            layer.CompactionPct = target;
            layer.ThicknessM = skeletonM / SnowIceFraction(target);
            SetStoredWaterM(layer, sub, Math.Min(water, PoreVolumeM(layer, sub)), frozen);
            double displaced = water - StoredLayerWaterM(layer, sub);
            if (displaced > NegligibleM) _displacedWaterM += displaced;
        }

        /// Rebuild a snow layer around a new amount of skeleton ice, keeping its water.
        void ResizeSnow(SurfaceLayer layer, double skeletonWeM, double waterM, double frozenM)
        {
            layer.ThicknessM = skeletonWeM <= NegligibleM ? 0 : skeletonWeM * IceExpansion / SnowIceFraction(layer.CompactionPct);
            double kept = Math.Min(waterM, PoreVolumeM(layer, sub));
            SetStoredWaterM(layer, sub, kept, Math.Min(frozenM, kept));
            double displaced = waterM - kept;
            if (displaced > NegligibleM) _displacedWaterM += displaced;
        }

        void FlushDisplacedWater()
        {
            if (_displacedWaterM <= NegligibleM) return;
            double displaced = _displacedWaterM;
            _displacedWaterM = 0;
            Percolate(displaced, MaxStepMinutes);
        }

        /// Merge deposits that are the same snow, drop the ones that are gone, and coarsen if the
        /// stack still will not fit - never by discarding material.
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
