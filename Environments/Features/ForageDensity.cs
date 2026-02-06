namespace text_survival.Environments.Features;

/// <summary>
/// Named constants for forage density values.
/// Tiles feel rich early but deplete fast (4x density, 4x depletion rate).
/// </summary>
public static class ForageDensity
{
    // --- Barren tier: harsh environments, barely anything ---
    public const double Minimal = 0.12;    // Ice crevasse, thermal vent, snowfield basin
    public const double Barren = 0.20;     // Ice shelf, wind gap, ice shove ridge
    public const double Scarce = 0.32;     // Caves, rock overhang, hot spring, meltwater pool

    // --- Sparse tier: limited finds ---
    public const double Thin = 0.40;       // Snowfield hollow, cliff face, cairn marker
    public const double Sparse = 0.52;     // Plains, standing stones, young growth, eagle's crag
    public const double Light = 0.60;      // Frozen creek, game trail, wolf den, split rock, deer meadow

    // --- Moderate tier: reasonable finds ---
    public const double Modest = 0.72;     // Fox earth, creek falls, rabbit warren
    public const double Moderate = 0.80;   // Bear cave, lookout, granite outcrop, scree chute, tangled roots
    public const double Fair = 0.92;       // Raven's perch, tall grass, open pines
    public const double Standard = 1.0;    // Ancient grove, mossy hollow, peat bog, mammoth wallow

    // --- Productive tier: good foraging ---
    public const double Decent = 1.2;      // Riverbank, dense thicket, fallen giant, talus slope, pyrite outcrop
    public const double Good = 1.4;        // Clearing, birch stand, reed bed
    public const double Rich = 1.52;       // Fungal grove, flint seam, moraine field, kill site
    public const double Plentiful = 1.6;   // Sheltered valley

    // --- High-value tier: exceptional foraging ---
    public const double Lush = 1.8;        // Marsh
    public const double Premium = 2.0;     // Deep woods, burnt stand, krummholz, flint knapping site
    public const double Abundant = 2.52;   // Beaver dam
    public const double Exceptional = 3.0; // Deadwood grove, deadfall maze

    // --- Terrain tile defaults (used by FeatureFactory.CreateTerrainForage) ---
    public const double TerrainForest = 1.0;
    public const double TerrainClearing = 0.72;
    public const double TerrainMarsh = 0.52;
    public const double TerrainWater = 0.52;
    public const double TerrainPlain = 0.52;
    public const double TerrainHills = 0.40;
    public const double TerrainRock = 0.52;
    public const double TerrainDefault = 0.32;
}
