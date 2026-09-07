namespace text_survival.Environments.Surface;

/// <summary>How something is touching the ground. Different contact, different soaking.</summary>
public enum SurfaceContact
{
    /// <summary>Walking through it. Brief per step, but you go through everything in the way.</summary>
    Walking,

    /// <summary>Kneeling, digging, butchering. Hands and knees in it for a long time.</summary>
    WorkingOnGround,

    /// <summary>Lying on it. The longest contact there is, and the one bedding is for.</summary>
    RestingOnGround
}

/// <summary>
/// The ground of one tile, and everything the weather has done to it.
///
/// Callers ask what crossing costs, how slippery it is, how hard it is to search, how wet it
/// makes you, and whether a buried thing can be reached. They never see a layer, because
/// combining mud, snow, water and crust belongs in one place - every caller that combined
/// them itself would combine them slightly differently.
///
/// Depths are metres of a one-square-metre column, so a depth is also a volume. Water is
/// tracked in metres of water-equivalent and conserved: stored water equals water in minus
/// the named sinks, at all times.
/// </summary>
public sealed partial class GroundSurface
{
    private const int MaxStepMinutes = 15;
    private const int MaxSnowLayers = 3;

    /// <summary>Compaction difference below which two snow layers are the same snow.</summary>
    private const double MergeCompactionTolerance = 0.08;

    /// <summary>A tile is about a hundred metres across.</summary>
    public const double TileAreaM2 = 100 * 100;

    private List<SurfaceLayer> _layers = [];
    private Dictionary<string, double> _clearedDepthM = [];

    public SubstrateProfile Substrate { get; set; } = SubstrateProfile.Loam;

    public double WaterAddedM { get; set; }
    public double WaterDrainedM { get; set; }
    public double WaterEvaporatedM { get; set; }
    public double WaterOverflowedM { get; set; }

    public GroundSurface() { }

    public GroundSurface(SubstrateProfile substrate, double initialFrozenFractionPct = 1.0)
    {
        Substrate = substrate;

        var ground = new SurfaceLayer(SurfaceMaterial.Ground, substrate.ThicknessM);
        ground.SetStoredWaterM(substrate,
            substrate.PoreVolumeM * substrate.InitialSaturationPct,
            substrate.PoreVolumeM * substrate.InitialSaturationPct * initialFrozenFractionPct);
        _layers.Add(ground);

        WaterAddedM = StoredWaterM;
    }

    // ================= Queries =================

    /// <summary>
    /// Top of the modelled cover, from the stable substrate reference. Zero is bare ground
    /// as it was found.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double SurfaceHeightM
    {
        get
        {
            double height = Ground.ThicknessM - Substrate.ThicknessM;
            for (int i = 1; i < _layers.Count; i++)
                height += _layers[i].PhysicalThicknessM;
            return height;
        }
    }

    /// <summary>
    /// Cover you have to move to get at what is under it. Liquid water is excluded - you can
    /// reach through a puddle, you just get wet doing it.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double SolidCoverHeightM
    {
        get
        {
            double height = 0;
            foreach (var layer in _layers)
            {
                if (layer.Material == SurfaceMaterial.Snow)
                    height += layer.PhysicalThicknessM;
                else if (layer.Material == SurfaceMaterial.Water)
                    height += layer.ThicknessM * layer.FrozenFractionPct * SurfacePhysics.IceExpansion;
            }
            return height;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public double SnowDepthM
    {
        get
        {
            double depth = 0;
            foreach (var layer in _layers)
                if (layer.Material == SurfaceMaterial.Snow) depth += layer.ThicknessM;
            return depth;
        }
    }

    /// <summary>Ponded water, ice included, as you would wade it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double StandingWaterDepthM => Pond?.PhysicalThicknessM ?? 0;

    /// <summary>The liquid part of that pond.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double StandingLiquidDepthM =>
        Pond is { } pond ? pond.ThicknessM * (1 - pond.FrozenFractionPct) : 0;

    /// <summary>How wet the ground itself is, ignoring anything standing on it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double WetnessPct => Ground.WaterSaturationPct;

    /// <summary>Every drop the tile is holding, however it is held.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double StoredWaterM
    {
        get
        {
            double total = 0;
            foreach (var layer in _layers) total += layer.TotalWaterM(Substrate);
            return total;
        }
    }

    /// <summary>
    /// How much slower the ground is to cross than bare dry earth; 1 is the dry baseline.
    /// Ground only - the walker's speed, load and route are somebody else's business.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double TraversalFactor
    {
        get
        {
            double factor = 1;

            double snow = SnowDepthM;
            if (snow > 0.02)
            {
                // Loose snow you wade; packed snow you walk on.
                double looseness = 1 - 0.55 * TopSnowCompaction;
                factor *= 1 + Math.Pow(snow / 0.10, 1.3) * 0.25 * looseness;
            }

            double water = StandingLiquidDepthM;
            if (water > 0.01)
                factor *= 1 + (water / SurfacePhysics.MaxStandingWaterDepthM) * 0.8;

            factor *= 1 + MudLevel * 0.25;

            return Math.Clamp(factor, 1, 6);
        }
    }

    /// <summary>
    /// Footing hazard the surface adds on top of the terrain's own: slick ice, a crust that
    /// gives way, mud. Not cliffs, and not lake ice - <see cref="Features.WaterFeature"/>
    /// owns that, and <see cref="Location.GetSurfaceHazardDelta"/> yields to it.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double HazardDelta
    {
        get
        {
            double hazard = 0;

            if (Pond is { } pond && pond.FrozenFractionPct > 0.02)
                hazard += 0.35 * Math.Min(1, pond.FrozenFractionPct * 3);

            double snow = SnowDepthM;
            if (snow > 0.05)
            {
                hazard += 0.05;
                if (TopSnowFrozenLiquid > 0.25) hazard += 0.10;   // refrozen crust
            }

            hazard += MudLevel * 0.05;

            return Math.Clamp(hazard, 0, 0.5);
        }
    }

    /// <summary>
    /// How well the ground here can be searched, against bare ground. Cover hides a thing in
    /// proportion to how much of it is under the cover, down to a floor: a discovery that can
    /// never be made is a discovery that should not have been placed.
    /// </summary>
    /// <param name="baseElevationM">Where the object sits, against <see cref="SurfaceHeightM"/>.</param>
    /// <param name="verticalExtentM">How tall it stands.</param>
    public double GetSearchFactor(double baseElevationM, double verticalExtentM)
    {
        const double Floor = 0.1;

        double extent = Math.Max(verticalExtentM, 0.05);
        double cover = SurfaceHeightM - baseElevationM;
        if (cover <= 0) return 1;

        double buried = Math.Clamp(cover / extent, 0, 1);
        return 1 - buried * (1 - Floor);
    }

    /// <summary>
    /// Wetness (0-1 of a full soak) picked up per minute by whoever is in contact with the
    /// ground, before clothing, waterproofing or bedding get a say. Frozen ground has no
    /// liquid to give, however hazardous it is to stand on.
    ///
    /// The ground wets what touches it and no more: wading soaks your legs, not your hood,
    /// so each contact has a ceiling on how wet it can get you and the rate falls to zero as
    /// you approach it. Without that ceiling, crossing damp ground below freezing - where
    /// nothing dries but a fire - drove everyone to a full soak, because even a trickle
    /// outran the fastest drying in the game.
    /// </summary>
    public double GetContactWettingRate(SurfaceContact contact, double currentWetnessPct = 0)
    {
        // Ankle-deep is already as wet as walking through it gets.
        double pond = Math.Clamp(StandingLiquidDepthM / 0.15, 0, 1);
        double slush = TopSnowLiquidFraction * 0.3;
        double damp = Ground.WaterSaturationPct * (1 - Ground.FrozenFractionPct) * 0.05;

        double source = Math.Clamp(pond + slush + damp, 0, 1);
        if (source <= 0) return 0;

        double rate = contact switch
        {
            SurfaceContact.Walking => 0.12,
            SurfaceContact.RestingOnGround => 0.10,
            SurfaceContact.WorkingOnGround => 0.06,
            _ => 0
        };
        if (rate <= 0) return 0;

        double ceiling = ContactCeiling(contact);
        if (currentWetnessPct >= ceiling) return 0;

        return source * rate;
    }

    /// <summary>
    /// How wet this contact can leave you: how far up you the wet stuff reaches.
    ///
    /// Walking, that is geometry - ankle-deep water wets you to the ankles, and only
    /// hip-deep wets all of you. Lying in it, geometry is no help at all: two centimetres
    /// of meltwater soaks your whole back, which is what bedding is for. Kneeling sits
    /// between the two.
    ///
    /// Depth is what this reads; whether the depth is wet at all is the caller's source
    /// term, so dry powder has a high ceiling and no rate to reach it with.
    /// </summary>
    private double ContactCeiling(SurfaceContact contact)
    {
        const double SoakedDepthM = 0.9;  // hip-deep - above this you are wet all over

        if (contact != SurfaceContact.Walking) return contact switch
        {
            SurfaceContact.RestingOnGround => 0.7,
            SurfaceContact.WorkingOnGround => 0.4,
            _ => 0
        };

        double depth = Math.Max(StandingLiquidDepthM, SnowDepthM);
        // Wet ground with nothing standing on it still gets into your boots.
        return Math.Clamp(depth / SoakedDepthM, 0.1, 1);
    }

    /// <summary>What the ground is like right now, or null if it is unremarkable.</summary>
    public string? ConditionText()
    {
        double snow = SnowDepthM;
        double water = StandingLiquidDepthM;

        if (snow >= 0.45) return $"Deep snow, {Math.Round(snow * 100)} cm.";
        if (water >= 0.12) return "Standing water, ankle deep.";
        if (Pond is { FrozenFractionPct: > 0.2 }) return "The surface is glazed with ice.";
        if (snow >= 0.15) return $"Snow lies {Math.Round(snow * 100)} cm deep.";
        if (water > 0.02) return "Meltwater pools underfoot.";
        if (MudLevel > 0.6) return "The ground is soft and wet.";
        if (snow > 0.02) return "A thin cover of snow.";
        return null;
    }

    // ================= Operations =================

    /// <summary>Snow falls, in metres of water-equivalent.</summary>
    public void AddSnow(double waterEquivalentM)
    {
        if (waterEquivalentM <= SurfacePhysics.NegligibleM) return;

        WaterAddedM += waterEquivalentM;

        double skeleton = waterEquivalentM * SurfacePhysics.IceExpansion;
        double thickness = skeleton / SurfacePhysics.LooseSnowIceFraction;
        _layers.Add(new SurfaceLayer(SurfaceMaterial.Snow, thickness));
        Consolidate();
    }

    /// <summary>Liquid water arrives at the top of the stack.</summary>
    public void AddWater(double waterDepthM)
    {
        if (waterDepthM <= SurfacePhysics.NegligibleM) return;

        WaterAddedM += waterDepthM;
        Percolate(waterDepthM, MaxStepMinutes);
    }

    /// <summary>
    /// Press the snow down. <paramref name="pressureFactor"/> is 0 for nothing and 1 for
    /// packing it as hard as snow packs.
    /// </summary>
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

    /// <summary>
    /// Move the ground on under the given weather, in bounded steps so a long interval
    /// cannot outrun a rate.
    /// </summary>
    public void Advance(int minutes, SurfaceWeather weather)
    {
        if (minutes <= 0) return;

        int remaining = minutes;
        while (remaining > 0)
        {
            int step = Math.Min(remaining, MaxStepMinutes);
            Step(step, weather);
            remaining -= step;
        }
    }

    private void Step(int minutes, SurfaceWeather weather)
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

    // ---- Phase change -------------------------------------------------------

    private void ChangePhase(int minutes, SurfaceWeather weather)
    {
        double degrees = weather.AirTemperatureF - SurfacePhysics.FreezingPointF;
        if (Math.Abs(degrees) < 0.01) return;

        double windGain = 1 + weather.WindLevel * SurfacePhysics.WindPhaseChangeFactor;
        double budget = Math.Abs(degrees) * SurfacePhysics.PhaseChangeMPerFPerMinute * windGain * minutes;

        if (degrees > 0)
        {
            budget += weather.SunlightLevel * SurfacePhysics.SunMeltMPerMinute * minutes;
            Melt(budget);
        }
        else
        {
            Freeze(budget);
        }
    }

    /// <summary>Warmth works down from the top, spending itself as it goes.</summary>
    private void Melt(double budgetM)
    {
        double released = 0;

        for (int i = _layers.Count - 1; i >= 0 && budgetM > SurfacePhysics.NegligibleM; i--)
        {
            var layer = _layers[i];
            double exposure = ExposureAt(i);
            double available = budgetM * exposure;
            if (available <= SurfacePhysics.NegligibleM) continue;

            // Frozen pore water goes before the skeleton holding it.
            double frozenPore = layer.FrozenWaterM(Substrate);
            if (frozenPore > SurfacePhysics.NegligibleM)
            {
                double thawed = Math.Min(frozenPore, available);
                double stored = layer.StoredWaterM(Substrate);
                layer.SetStoredWaterM(Substrate, stored, frozenPore - thawed);
                available -= thawed;
                budgetM -= thawed / exposure;
            }

            if (layer.Material != SurfaceMaterial.Snow || available <= SurfacePhysics.NegligibleM) continue;

            double skeletonWe = layer.SkeletonWaterEquivalentM(Substrate);
            double melted = Math.Min(skeletonWe, available);
            if (melted <= SurfacePhysics.NegligibleM) continue;

            double water = layer.StoredWaterM(Substrate);
            double frozen = layer.FrozenWaterM(Substrate);
            ResizeSnow(layer, skeletonWe - melted, water, frozen);
            released += melted;
            budgetM -= melted / exposure;
        }

        if (released > SurfacePhysics.NegligibleM)
            Percolate(released, MaxStepMinutes);

        FlushDisplacedWater();
    }

    /// <summary>
    /// Cold works down from the top too. Freezing never removes water, it only changes which
    /// column of the ledger it sits in.
    /// </summary>
    private void Freeze(double budgetM)
    {
        for (int i = _layers.Count - 1; i >= 0 && budgetM > SurfacePhysics.NegligibleM; i--)
        {
            var layer = _layers[i];
            double exposure = ExposureAt(i);
            double available = budgetM * exposure;
            if (available <= SurfacePhysics.NegligibleM) continue;

            double liquid = layer.LiquidWaterM(Substrate);
            if (liquid <= SurfacePhysics.NegligibleM) continue;

            double frozenNow = Math.Min(liquid, available);
            double stored = layer.StoredWaterM(Substrate);
            layer.SetStoredWaterM(Substrate, stored, layer.FrozenWaterM(Substrate) + frozenNow);
            budgetM -= frozenNow / exposure;
        }
    }

    /// <summary>How much of the weather reaches layer <paramref name="index"/>, 0-1. Snow is
    /// a blanket, so a pond under a foot of it barely notices the night.</summary>
    private double ExposureAt(int index)
    {
        double snowAbove = 0;
        for (int i = index + 1; i < _layers.Count; i++)
            if (_layers[i].Material == SurfaceMaterial.Snow) snowAbove += _layers[i].ThicknessM;

        return 1 / (1 + snowAbove * SurfacePhysics.SnowInsulationPerMetre);
    }

    // ---- Water movement -----------------------------------------------------

    /// <summary>
    /// Liquid arriving at the top works down. Snow holds a little against gravity and passes
    /// the rest on; whatever reaches the bottom ponds, and <see cref="Infiltrate"/> takes it
    /// from there.
    /// </summary>
    private void Percolate(double liquidM, int minutes)
    {
        double carried = liquidM;

        for (int i = _layers.Count - 1; i >= 1 && carried > SurfacePhysics.NegligibleM; i--)
        {
            var layer = _layers[i];
            if (layer.Material != SurfaceMaterial.Snow) continue;

            double capacity = layer.PoreVolumeM(Substrate) * SurfacePhysics.SnowRetentionPct;
            double held = layer.StoredWaterM(Substrate);
            double room = Math.Max(0, capacity - held);
            double throughput = SurfacePhysics.SnowPermeabilityMPerMinute * minutes;

            double absorbed = Math.Min(carried, Math.Min(room, throughput));
            if (absorbed > SurfacePhysics.NegligibleM)
            {
                layer.SetStoredWaterM(Substrate, held + absorbed, layer.FrozenWaterM(Substrate));
                carried -= absorbed;
            }
        }

        if (carried > SurfacePhysics.NegligibleM)
            AddToPond(carried);
    }

    /// <summary>Water that reached the ground. On a lake it runs away - that ice is not ours.</summary>
    private void AddToPond(double liquidM)
    {
        if (!Substrate.HoldsStandingWater)
        {
            WaterOverflowedM += liquidM;
            return;
        }

        var pond = Pond;
        if (pond == null)
        {
            pond = new SurfaceLayer(SurfaceMaterial.Water, liquidM) { WaterSaturationPct = 1 };
            _layers.Insert(1, pond);
            return;
        }

        double frozen = pond.ThicknessM * pond.FrozenFractionPct;
        pond.ThicknessM += liquidM;
        pond.WaterSaturationPct = 1;
        pond.FrozenFractionPct = pond.ThicknessM > 0 ? Math.Clamp(frozen / pond.ThicknessM, 0, 1) : 0;
    }

    /// <summary>Ponded liquid soaks in, as fast as the ground will take it.</summary>
    private void Infiltrate(int minutes)
    {
        var pond = Pond;
        if (pond == null) return;

        double liquid = pond.ThicknessM * (1 - pond.FrozenFractionPct);
        if (liquid <= SurfacePhysics.NegligibleM) return;

        var ground = Ground;
        double room = Math.Max(0, ground.PoreVolumeM(Substrate) - ground.StoredWaterM(Substrate));
        double throughput = Substrate.PermeabilityMPerMinute * minutes;

        double soaked = Math.Min(liquid, Math.Min(room, throughput));
        if (soaked <= SurfacePhysics.NegligibleM) return;

        ground.SetStoredWaterM(Substrate, ground.StoredWaterM(Substrate) + soaked, ground.FrozenWaterM(Substrate));

        double frozen = pond.ThicknessM * pond.FrozenFractionPct;
        pond.ThicknessM -= soaked;
        pond.FrozenFractionPct = pond.ThicknessM > SurfacePhysics.NegligibleM
            ? Math.Clamp(frozen / pond.ThicknessM, 0, 1)
            : 0;
    }

    /// <summary>Free water keeps going down, out of the modelled layer.</summary>
    private void Drain(int minutes)
    {
        var ground = Ground;
        double capacity = ground.PoreVolumeM(Substrate);
        if (capacity <= SurfacePhysics.NegligibleM) return;

        double held = ground.StoredWaterM(Substrate);
        double frozen = ground.FrozenWaterM(Substrate);
        double retained = capacity * Substrate.RetentionPct;
        double free = Math.Max(0, held - frozen - retained);
        if (free <= SurfacePhysics.NegligibleM) return;

        double lost = Math.Min(free, free * Substrate.DrainagePerMinute * minutes);
        ground.SetStoredWaterM(Substrate, held - lost, frozen);
        WaterDrainedM += lost;
    }

    /// <summary>Ponded water finds a way off the tile. Rock sheds it; peat does not.</summary>
    private void RunOff(int minutes)
    {
        var pond = Pond;
        if (pond == null) return;

        double liquid = pond.ThicknessM * (1 - pond.FrozenFractionPct);
        if (liquid <= SurfacePhysics.NegligibleM) return;

        double lost = Math.Min(liquid, liquid * Substrate.RunoffPerMinute * minutes);
        if (lost <= SurfacePhysics.NegligibleM) return;

        double frozen = pond.ThicknessM * pond.FrozenFractionPct;
        pond.ThicknessM -= lost;
        pond.FrozenFractionPct = pond.ThicknessM > SurfacePhysics.NegligibleM
            ? Math.Clamp(frozen / pond.ThicknessM, 0, 1)
            : 0;
        WaterOverflowedM += lost;
    }

    private void Evaporate(int minutes, SurfaceWeather weather)
    {
        double rate = SurfacePhysics.BaseEvaporationMPerMinute
            * (1 + weather.WindLevel * 2)
            * (1 + weather.SunlightLevel * 3)
            * minutes;
        if (rate <= SurfacePhysics.NegligibleM) return;

        var top = _layers[^1];
        double liquid = top.LiquidWaterM(Substrate);

        if (top.Material == SurfaceMaterial.Water)
            liquid = top.ThicknessM * (1 - top.FrozenFractionPct);

        double lost = Math.Min(rate, liquid);
        if (lost <= SurfacePhysics.NegligibleM)
        {
            if (top.Material != SurfaceMaterial.Snow) return;   // dry snow still sublimates
            double skeletonWe = top.SkeletonWaterEquivalentM(Substrate);
            double sublimated = Math.Min(rate, skeletonWe);
            if (sublimated <= SurfacePhysics.NegligibleM) return;
            ResizeSnow(top, skeletonWe - sublimated, top.StoredWaterM(Substrate), top.FrozenWaterM(Substrate));
            WaterEvaporatedM += sublimated;
            return;
        }

        if (top.Material == SurfaceMaterial.Water)
        {
            double frozen = top.ThicknessM * top.FrozenFractionPct;
            top.ThicknessM -= lost;
            top.FrozenFractionPct = top.ThicknessM > SurfacePhysics.NegligibleM
                ? Math.Clamp(frozen / top.ThicknessM, 0, 1)
                : 0;
        }
        else
        {
            top.SetStoredWaterM(Substrate, top.StoredWaterM(Substrate) - lost, top.FrozenWaterM(Substrate));
        }

        WaterEvaporatedM += lost;
    }

    /// <summary>
    /// A foot of water is as deep as this pass goes. Beyond it water leaves through a named
    /// sink rather than being clamped away, because routing it to the next tile is the
    /// follow-up and the ledger has to survive until then. Ice is never capped.
    /// </summary>
    private void CapPonding()
    {
        var pond = Pond;
        if (pond == null) return;

        double liquid = pond.ThicknessM * (1 - pond.FrozenFractionPct);
        double excess = liquid - SurfacePhysics.MaxStandingWaterDepthM;
        if (excess <= SurfacePhysics.NegligibleM) return;

        double frozen = pond.ThicknessM * pond.FrozenFractionPct;
        pond.ThicknessM -= excess;
        pond.FrozenFractionPct = pond.ThicknessM > SurfacePhysics.NegligibleM
            ? Math.Clamp(frozen / pond.ThicknessM, 0, 1)
            : 0;
        WaterOverflowedM += excess;
    }

    // ---- Settling, merging, tidying ----------------------------------------

    private void Settle(int minutes)
    {
        for (int i = 1; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            if (layer.Material != SurfaceMaterial.Snow) continue;

            double settled = layer.CompactionPct
                + (1 - layer.CompactionPct) * SurfacePhysics.SnowSettlingPerMinute * minutes;
            SetSnowCompaction(layer, settled);
        }
        FlushDisplacedWater();
    }

    /// <summary>
    /// Compact a snow layer, conserving its ice. Squeezing the pores shut can leave more
    /// water in them than they hold; that water is displaced downward, not deleted.
    /// </summary>
    private void SetSnowCompaction(SurfaceLayer layer, double compaction)
    {
        double skeletonWe = layer.SkeletonWaterEquivalentM(Substrate);
        double water = layer.StoredWaterM(Substrate);
        double frozen = layer.FrozenWaterM(Substrate);

        double target = Math.Clamp(compaction, layer.CompactionPct, 1);
        layer.CompactionPct = target;
        double newPore = 1 - SurfaceLayer.SnowIceFraction(target);
        double newThickness = newPore >= 1 ? layer.ThicknessM : (skeletonWe * SurfacePhysics.IceExpansion) / (1 - newPore);

        // Frozen pore water is a solid: stop compacting where it still fits.
        if (frozen > newThickness * newPore && frozen > SurfacePhysics.NegligibleM)
            newThickness = frozen / newPore;

        layer.ThicknessM = newThickness;
        layer.SetStoredWaterM(Substrate, Math.Min(water, layer.PoreVolumeM(Substrate)), frozen);

        double displaced = water - layer.StoredWaterM(Substrate);
        if (displaced > SurfacePhysics.NegligibleM)
            _displacedWaterM += displaced;
    }

    /// <summary>Rebuild a snow layer around a new amount of skeleton ice, keeping its water.</summary>
    private void ResizeSnow(SurfaceLayer layer, double skeletonWeM, double waterM, double frozenM)
    {
        double iceFraction = SurfaceLayer.SnowIceFraction(layer.CompactionPct);
        layer.ThicknessM = skeletonWeM <= SurfacePhysics.NegligibleM
            ? 0
            : (skeletonWeM * SurfacePhysics.IceExpansion) / iceFraction;

        double capacity = layer.PoreVolumeM(Substrate);
        double kept = Math.Min(waterM, capacity);
        layer.SetStoredWaterM(Substrate, kept, Math.Min(frozenM, kept));

        double displaced = waterM - kept;
        if (displaced > SurfacePhysics.NegligibleM)
            _displacedWaterM += displaced;
    }

    private double _displacedWaterM;

    /// <summary>Send water squeezed or shrunk out of a layer on its way down.</summary>
    private void FlushDisplacedWater()
    {
        if (_displacedWaterM <= SurfacePhysics.NegligibleM) return;
        double displaced = _displacedWaterM;
        _displacedWaterM = 0;
        Percolate(displaced, MaxStepMinutes);
    }

    /// <summary>
    /// Merge deposits that are the same thing, drop the ones that are gone, and coarsen if
    /// the stack still will not fit - never by discarding material.
    /// </summary>
    private void Consolidate()
    {
        FlushDisplacedWater();

        for (int i = _layers.Count - 1; i >= 1; i--)
        {
            var layer = _layers[i];
            bool empty = layer.Material == SurfaceMaterial.Water
                ? layer.ThicknessM <= SurfacePhysics.NegligibleM
                : layer.ThicknessM <= SurfacePhysics.NegligibleM
                  && layer.StoredWaterM(Substrate) <= SurfacePhysics.NegligibleM;

            if (!empty) continue;

            double stranded = layer.TotalWaterM(Substrate);
            _layers.RemoveAt(i);
            if (stranded > SurfacePhysics.NegligibleM) Percolate(stranded, MaxStepMinutes);
        }

        MergeAlikeSnow();

        while (SnowLayerCount > MaxSnowLayers)
            MergeClosestSnow();

        FlushDisplacedWater();
    }

    private void MergeAlikeSnow()
    {
        for (int i = _layers.Count - 1; i >= 2; i--)
        {
            if (_layers[i].Material != SurfaceMaterial.Snow) continue;
            if (_layers[i - 1].Material != SurfaceMaterial.Snow) continue;
            if (Math.Abs(_layers[i].CompactionPct - _layers[i - 1].CompactionPct) > MergeCompactionTolerance) continue;

            MergeSnow(i - 1, i);
        }
    }

    private void MergeClosestSnow()
    {
        int best = -1;
        double closest = double.MaxValue;

        for (int i = 1; i < _layers.Count - 1; i++)
        {
            if (_layers[i].Material != SurfaceMaterial.Snow) continue;
            if (_layers[i + 1].Material != SurfaceMaterial.Snow) continue;

            double gap = Math.Abs(_layers[i].CompactionPct - _layers[i + 1].CompactionPct);
            if (gap >= closest) continue;
            closest = gap;
            best = i;
        }

        if (best < 0) return;
        MergeSnow(best, best + 1);
    }

    private void MergeSnow(int lower, int upper)
    {
        var a = _layers[lower];
        var b = _layers[upper];

        double skeletonA = a.SkeletonWaterEquivalentM(Substrate);
        double skeletonB = b.SkeletonWaterEquivalentM(Substrate);
        double skeleton = skeletonA + skeletonB;
        double water = a.StoredWaterM(Substrate) + b.StoredWaterM(Substrate);
        double frozen = a.FrozenWaterM(Substrate) + b.FrozenWaterM(Substrate);

        a.CompactionPct = skeleton > SurfacePhysics.NegligibleM
            ? (a.CompactionPct * skeletonA + b.CompactionPct * skeletonB) / skeleton
            : a.CompactionPct;

        ResizeSnow(a, skeleton, water, frozen);
        _layers.RemoveAt(upper);
    }

    // ---- Internal accessors -------------------------------------------------

    private SurfaceLayer Ground => _layers[0];

    private SurfaceLayer? Pond =>
        _layers.Count > 1 && _layers[1].Material == SurfaceMaterial.Water ? _layers[1] : null;

    private int SnowLayerCount
    {
        get
        {
            int count = 0;
            foreach (var layer in _layers)
                if (layer.Material == SurfaceMaterial.Snow) count++;
            return count;
        }
    }

    private SurfaceLayer? TopSnow
    {
        get
        {
            for (int i = _layers.Count - 1; i >= 1; i--)
                if (_layers[i].Material == SurfaceMaterial.Snow) return _layers[i];
            return null;
        }
    }

    private double TopSnowCompaction => TopSnow?.CompactionPct ?? 0;

    private double TopSnowLiquidFraction
    {
        get
        {
            var snow = TopSnow;
            if (snow == null) return 0;
            double capacity = snow.PoreVolumeM(Substrate) * SurfacePhysics.SnowRetentionPct;
            if (capacity <= SurfacePhysics.NegligibleM) return 0;
            return Math.Clamp(snow.LiquidWaterM(Substrate) / capacity, 0, 1);
        }
    }

    private double TopSnowFrozenLiquid
    {
        get
        {
            var snow = TopSnow;
            if (snow == null) return 0;
            double capacity = snow.PoreVolumeM(Substrate) * SurfacePhysics.SnowRetentionPct;
            if (capacity <= SurfacePhysics.NegligibleM) return 0;
            return Math.Clamp(snow.FrozenWaterM(Substrate) / capacity, 0, 1);
        }
    }

    /// <summary>Mud: wet, soft, unfrozen ground with nothing on top of it.</summary>
    private double MudLevel
    {
        get
        {
            if (SnowDepthM > 0.02) return 0;
            var ground = Ground;
            double wet = ground.WaterSaturationPct * (1 - ground.FrozenFractionPct);
            return Math.Clamp(wet * Substrate.SoftnessLevel, 0, 1);
        }
    }

    // ================= Serialization =================

    /// <summary>Primary state. Everything above is derived from it.</summary>
    public List<SurfaceLayer> Layers
    {
        get => _layers;
        set => _layers = value is { Count: > 0 } ? value : _layers;
    }

    /// <summary>Holes dug down to a buried thing, keyed by its placement id.</summary>
    public Dictionary<string, double> ClearedDepths
    {
        get => _clearedDepthM;
        set => _clearedDepthM = value ?? [];
    }
}
