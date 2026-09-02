using System.Text;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Tests.Support;

/// <summary>
/// Which starting condition to run the NPC simulation from. All three reuse
/// <see cref="GameContext.CreateNewGame"/> so the world (map, camp, forage densities) is
/// exactly what a real playthrough gets - only the NPC's starting position or the camp
/// fire's ignition state is changed.
/// </summary>
public enum SimulationScenario
{
    /// <summary>Exactly what a new game gives you: fire unlit, NPC adjacent to camp.</summary>
    Baseline,

    /// <summary>Baseline, but the camp fire is already lit at minute 0.</summary>
    FireLit,

    /// <summary>Baseline, but the NPC starts standing on the camp tile itself.</summary>
    NpcAtCamp,
}

/// <summary>
/// One minute of an <see cref="NPCSimulation"/> run: the NPC's full observable state, the
/// camp's fire/cache state, and the console lines the NPC's decision logic printed this
/// minute (so a specific minute's reasoning can be found by reading the log).
/// </summary>
public sealed record NPCSnapshot(
    int Minute, DateTime GameTime,
    GridPosition Pos, string LocationName, TerrainType Terrain,
    string? Action, int ActionMinutesSpent, int ActionDuration, NeedType? Need,
    double WarmPct, double BodyTempF, double HydratedPct, double EnergyPct, double FullPct,
    double AmbientTempF,
    int Sticks, int Tinder, int Logs, double WaterL, int FoodItems, double CarryKg, double CarryMaxKg,
    bool CampFireActive, double CampFireBurningKg, double CampFireUnburnedKg, double CampFireHoursLeft, double CampFireTempF,
    bool HereFireActive,
    double CacheFuelKg, double CacheWaterL, double CacheFoodKg,
    bool IsAlive, string? DeathCause,
    IReadOnlyList<string> Lines,
    IReadOnlyList<string>? TileView);

/// <summary>
/// Aggregate metrics computed from a run's snapshots. This is what hypotheses about NPC AI
/// changes are scored on - see documentation/npc-simulation-plan.md.
/// </summary>
public sealed class SimulationSummary
{
    public int SurvivedMinutes;
    public bool Died;
    public string? DeathCause;

    public double MinWarmPct = 1.0;
    public int MinutesBelowWarm25;

    public int CampFireActiveMinutes;
    public int MinutesAtActiveFire;

    public int FireStartAttempts;
    public int FireStartSuccesses;
    public int TendFireCount;

    public Dictionary<string, int> ActionMinutes = new();
    public int ActionStarts;
    public int Reversals;
    public int ColdIdleMinutes;

    public int ForageMinutes;
    public double ForageYieldKg;
    public int SticksGathered;
    public int TinderGathered;

    public double CacheFuelKgFinal;
    public double CacheWaterLFinal;
    public double CacheFoodKgFinal;

    public int StuckStreakMax;

    // Behavioral "fingerprint" metrics - these exist to tell NPCs apart from each other,
    // not just to tell whether one survived. A timid and a bold NPC with the same survival
    // time should still look different on these.
    public int TotalTilesMoved;       // path length, including backtracking - overall restlessness
    public int UniqueTilesVisited;    // distinct ground covered - actual range, not just motion
    public int MaxDistanceFromCamp;   // how far this NPC ever ranged from home
    public double WaterGatheredL;
    public int ItemsCrafted;
    public int ShelterImprovements;
    public int CombatEngagements;
    public int CombatVictories;
    public int FleeCount;
    public int HarvestMinutes;
    public int ChopMinutes;

    public int IdleMinutes;              // Resting or no action at all
    public double IdleRatio => SurvivedMinutes > 0 ? (double)IdleMinutes / SurvivedMinutes : 0;
    public int DistinctActionTypes;      // a robotic AI repeats the same 2-3 forever

    public int NightMinutesTotal;
    public int NightRestMinutes;
    public double NightRestPct => NightMinutesTotal > 0 ? (double)NightRestMinutes / NightMinutesTotal : 0;
    public int DayMinutesTotal;
    public int DayRestMinutes;
    public double DayRestPct => DayMinutesTotal > 0 ? (double)DayRestMinutes / DayMinutesTotal : 0;

    /// <summary>Times WarmPct dropped below .10 and later recovered to .30+ without dying - resilience, not just eventual failure.</summary>
    public int NearMissCount;

    /// <summary>
    /// Final cache stock against IsEnoughStockpiled's own targets (Fuel 40kg, Water 6L,
    /// Food 2kg for DAYS_RESERVE=2 at PEOPLE_AT_CAMP=1 - duplicated here since those
    /// constants are private to NPC.cs), averaged and capped at 1.0 per category. Directly
    /// measures whether stockpiling behavior ever gets anywhere.
    /// </summary>
    public double CacheUtilizationPct;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Survived: {SurvivedMinutes} min ({SurvivedMinutes / 60.0:F1} h){(Died ? $" - DIED: {DeathCause}" : "")}");
        sb.AppendLine($"MinWarmPct: {MinWarmPct:P0}   MinutesBelowWarm25: {MinutesBelowWarm25}");
        sb.AppendLine($"CampFireActiveMinutes: {CampFireActiveMinutes}   MinutesAtActiveFire: {MinutesAtActiveFire}");
        sb.AppendLine($"FireStarts: {FireStartSuccesses}/{FireStartAttempts}   TendFireCount: {TendFireCount}");
        sb.AppendLine($"ActionStarts: {ActionStarts}   Reversals: {Reversals}   ColdIdleMinutes: {ColdIdleMinutes}   StuckStreakMax: {StuckStreakMax}");
        sb.AppendLine($"ForageMinutes: {ForageMinutes}   ForageYieldKg: {ForageYieldKg:F2}   SticksGathered: {SticksGathered}   TinderGathered: {TinderGathered}   WaterGatheredL: {WaterGatheredL:F1}");
        sb.AppendLine($"HarvestMinutes: {HarvestMinutes}   ChopMinutes: {ChopMinutes}   ItemsCrafted: {ItemsCrafted}   ShelterImprovements: {ShelterImprovements}");
        sb.AppendLine($"TotalTilesMoved: {TotalTilesMoved}   UniqueTilesVisited: {UniqueTilesVisited}   MaxDistanceFromCamp: {MaxDistanceFromCamp}");
        sb.AppendLine($"CombatEngagements: {CombatEngagements}   CombatVictories: {CombatVictories}   FleeCount: {FleeCount}");
        sb.AppendLine($"IdleRatio: {IdleRatio:P0}   DistinctActionTypes: {DistinctActionTypes}   NearMissCount: {NearMissCount}");
        sb.AppendLine($"NightRestPct: {NightRestPct:P0}   DayRestPct: {DayRestPct:P0}   CacheUtilizationPct: {CacheUtilizationPct:P0}");
        sb.AppendLine($"CacheFinal: fuel={CacheFuelKgFinal:F1}kg water={CacheWaterLFinal:F1}L food={CacheFoodKgFinal:F1}kg");
        sb.AppendLine("ActionMinutes:");
        foreach (var (name, minutes) in ActionMinutes.OrderByDescending(kv => kv.Value))
            sb.AppendLine($"  {name}: {minutes}");
        return sb.ToString();
    }
}

/// <summary>
/// Runs a real <see cref="GameContext"/> with no UI and no scheduler, minute by minute,
/// through <see cref="GameContext.UpdateWithoutEvents"/>, and records what the starting NPC
/// does. The player is reset to full health every minute so they never interfere (die,
/// trigger player-only events, etc.) - this is purely an NPC observation tool.
///
/// See documentation/npc-simulation-plan.md for why this harness exists and what it is
/// used to test.
/// </summary>
public sealed class NPCSimulation
{
    public GameContext Ctx { get; }
    public NPC Npc { get; }
    public List<NPCSnapshot> Snapshots { get; } = [];
    public DateTime StartGameTime { get; }

    private GridPosition? _lastPos;
    private GridPosition? _prevPos;
    private string? _lastActionName;

    private NPCSimulation(GameContext ctx, NPC npc)
    {
        Ctx = ctx;
        Npc = npc;
        StartGameTime = ctx.GameTime;
    }

    /// <summary>
    /// <paramref name="personality"/> overrides the randomly-rolled starting personality
    /// (still drawn from the seeded RNG first, so it doesn't shift anything else's draw
    /// order) - use it to hold every other random factor fixed while varying only
    /// Boldness/Selfishness/Sociability, e.g. to test whether a bold NPC actually ranges
    /// further than a timid one under otherwise identical conditions.
    /// </summary>
    public static NPCSimulation Create(SimulationScenario scenario, int? seed = null, Personality? personality = null)
    {
        var ctx = GameContext.CreateNewGame(seed);
        ctx.Ui = new ScriptedUi();

        if (ctx.NPCs.Count == 0)
            throw new InvalidOperationException("CreateNewGame produced no starting NPC - nothing to simulate.");
        var npc = ctx.NPCs[0];

        if (personality != null)
            npc.Personality = personality;

        switch (scenario)
        {
            case SimulationScenario.Baseline:
                break;

            case SimulationScenario.FireLit:
                {
                    var fire = ctx.Camp.GetFeature<HeatSourceFeature>()
                        ?? throw new InvalidOperationException("Camp has no HeatSourceFeature to light.");
                    fire.IgniteAll();
                    break;
                }

            case SimulationScenario.NpcAtCamp:
                npc.CurrentLocation = ctx.Camp;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        return new NPCSimulation(ctx, npc);
    }

    /// <summary>Advance the simulation minute by minute, up to <paramref name="minutes"/>, stopping early if the NPC dies.</summary>
    public void Run(int minutes, bool captureTileView = false)
    {
        var player = Ctx.player;

        for (int i = 0; i < minutes; i++)
        {
            // Ghost the player: full stats every tick so they never die, trigger their own
            // threshold events, or otherwise perturb the world this harness isn't about.
            player.Body.BodyTemperature = Body.BASE_BODY_TEMP;
            player.Body.Energy = SurvivalProcessor.MAX_ENERGY_MINUTES;
            player.Body.Hydration = SurvivalProcessor.MAX_HYDRATION;
            player.Body.CalorieStore = SurvivalProcessor.MAX_CALORIES;

            var sw = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(sw);
            try
            {
                Ctx.UpdateWithoutEvents(1, ActivityType.Resting);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            var lines = sw.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.TrimEnd('\r'))
                .ToList();

            bool alive = Npc.IsAlive;
            string? deathCause = alive ? null : NPCBodyFeature.DetermineDeathCause(Npc);

            var pos = Ctx.Map!.GetPosition(Npc.CurrentLocation);
            bool positionChanged = _lastPos != pos;
            bool actionChanged = Npc.CurrentAction?.Name != _lastActionName;

            List<string>? tileView = null;
            if (captureTileView && (positionChanged || actionChanged || i == 0))
                tileView = BuildTileView();

            var campFire = Ctx.Camp.GetFeature<HeatSourceFeature>();
            var hereFire = Npc.CurrentLocation.GetFeature<HeatSourceFeature>();
            var cache = Ctx.Camp.GetFeature<CacheFeature>()?.Storage;

            var snapshot = new NPCSnapshot(
                Minute: i,
                GameTime: Ctx.GameTime,
                Pos: pos,
                LocationName: Npc.CurrentLocation.Name,
                Terrain: Npc.CurrentLocation.Terrain,
                Action: Npc.CurrentAction?.Name,
                ActionMinutesSpent: Npc.CurrentAction?.MinutesSpent ?? 0,
                ActionDuration: Npc.CurrentAction?.DurationMinutes ?? 0,
                Need: Npc.CurrentNeed,
                WarmPct: Npc.Body.WarmPct,
                BodyTempF: Npc.Body.BodyTemperature,
                HydratedPct: Npc.Body.HydratedPct,
                EnergyPct: Npc.Body.EnergyPct,
                FullPct: Npc.Body.FullPct,
                AmbientTempF: Npc.CurrentLocation.GetTemperature(),
                Sticks: Npc.Inventory.Count(Resource.Stick),
                Tinder: Npc.Inventory.Count(Resource.Tinder),
                Logs: Npc.Inventory.GetCount(ResourceCategory.Log),
                WaterL: Npc.Inventory.Weight(Resource.Water),
                FoodItems: Npc.Inventory.GetCount(ResourceCategory.Food),
                CarryKg: Npc.Inventory.CurrentWeightKg,
                CarryMaxKg: Npc.Inventory.MaxWeightKg,
                CampFireActive: campFire?.IsActive ?? false,
                CampFireBurningKg: campFire?.BurningMassKg ?? 0,
                CampFireUnburnedKg: campFire?.UnburnedMassKg ?? 0,
                CampFireHoursLeft: campFire?.TotalHoursRemaining ?? 0,
                CampFireTempF: campFire?.GetCurrentFireTemperature() ?? 0,
                HereFireActive: hereFire?.IsActive ?? false,
                CacheFuelKg: cache?.GetWeight(ResourceCategory.Fuel) ?? 0,
                CacheWaterL: cache?.Weight(Resource.Water) ?? 0,
                CacheFoodKg: cache?.GetWeight(ResourceCategory.Food) ?? 0,
                IsAlive: alive,
                DeathCause: deathCause,
                Lines: lines,
                TileView: tileView);

            Snapshots.Add(snapshot);

            _prevPos = _lastPos;
            _lastPos = pos;
            _lastActionName = Npc.CurrentAction?.Name;

            if (!alive)
                break;
        }
    }

    private List<string> BuildTileView()
    {
        var view = new List<string>();
        var here = Npc.CurrentLocation;
        view.Add(FormatTileLine("here", here));
        foreach (var neighbor in Ctx.Map!.GetTravelOptionsFrom(here))
            view.Add(FormatTileLine(DirectionOf(here, neighbor), neighbor));
        return view;
    }

    private string DirectionOf(Location from, Location to)
    {
        var a = Ctx.Map!.GetPosition(from);
        var b = Ctx.Map!.GetPosition(to);
        if (b.Y < a.Y) return "N";
        if (b.Y > a.Y) return "S";
        if (b.X > a.X) return "E";
        if (b.X < a.X) return "W";
        return "?";
    }

    private string FormatTileLine(string label, Location loc)
    {
        var forage = loc.GetFeature<ForageFeature>();
        var wooded = loc.GetFeature<WoodedAreaFeature>();
        var harvestable = loc.Features.OfType<HarvestableFeature>().FirstOrDefault();
        var water = loc.GetFeature<WaterFeature>();
        var fire = loc.GetFeature<HeatSourceFeature>();

        string forageStr = forage != null
            ? $"density={forage.CurrentDensity:F2} nearlyDepleted={forage.IsNearlyDepleted()} canForage={forage.CanForage()} [{string.Join(",", forage.ProvidedResources())}]"
            : "none";

        return $"{label,-5} {loc.Name,-16} {loc.Terrain,-8} forage: {forageStr}" +
               (wooded is { HasTrees: true } ? " wooded" : "") +
               (harvestable != null ? " harvestable" : "") +
               (water != null ? " water" : "") +
               (fire != null ? $" fire({(fire.IsActive ? "lit" : "unlit")})" : "");
    }

    public SimulationSummary Summarize()
    {
        var s = new SimulationSummary();
        if (Snapshots.Count == 0) return s;

        s.SurvivedMinutes = Snapshots.Count;
        var last = Snapshots[^1];
        s.Died = !last.IsAlive;
        s.DeathCause = last.DeathCause;

        var positionHistory = new List<GridPosition>();
        int stuckStreak = 0;

        double prevSticks = Snapshots[0].Sticks;
        double prevTinder = Snapshots[0].Tinder;
        double prevCarry = Snapshots[0].CarryKg;
        double prevWater = Snapshots[0].WaterL;
        bool prevCampFireActive = Snapshots[0].CampFireActive;

        var campPos = Ctx.Map!.GetPosition(Ctx.Camp);
        var visitedTiles = new HashSet<GridPosition>();
        GridPosition? prevPos = null;
        bool inCriticalDip = false;

        foreach (var snap in Snapshots)
        {
            s.MinWarmPct = Math.Min(s.MinWarmPct, snap.WarmPct);
            if (snap.WarmPct < 0.25) s.MinutesBelowWarm25++;

            if (snap.CampFireActive) s.CampFireActiveMinutes++;
            if (snap.HereFireActive) s.MinutesAtActiveFire++;

            if (snap.CampFireActive && !prevCampFireActive) s.FireStartSuccesses++;
            prevCampFireActive = snap.CampFireActive;

            var actionName = snap.Action ?? "(none)";
            s.ActionMinutes[actionName] = s.ActionMinutes.GetValueOrDefault(actionName) + 1;

            // A 1-minute action (e.g. NPCTendFire) starts and completes within the same
            // Update() call, so it never appears as snap.Action - the decision log lines
            // printed during that minute are the only record it happened. Scan those
            // instead of diffing snap.Action, which would silently drop it.
            foreach (var line in snap.Lines)
            {
                if (line.Contains("] Picked: ", StringComparison.Ordinal))
                {
                    s.ActionStarts++;
                    if (line.Contains("Picked: Starting Fire ", StringComparison.Ordinal))
                        s.FireStartAttempts++;
                }
                if (line.Contains("] Completed: Tending Fire ", StringComparison.Ordinal))
                    s.TendFireCount++;
                if (line.Contains("] Completed: Crafting ", StringComparison.Ordinal))
                    s.ItemsCrafted++;
                if (line.Contains("] Completed: Improving shelter", StringComparison.Ordinal))
                    s.ShelterImprovements++;
                if (line.Contains("Picked: Fighting ", StringComparison.Ordinal))
                    s.CombatEngagements++;
                if (line.Contains("Picked: Fleeing from ", StringComparison.Ordinal))
                    s.FleeCount++;
                if (line.Contains(": Victory", StringComparison.Ordinal))
                    s.CombatVictories++;
            }

            if (actionName.StartsWith("Foraging", StringComparison.Ordinal)) s.ForageMinutes++;
            if (actionName.StartsWith("Harvesting", StringComparison.Ordinal)) s.HarvestMinutes++;
            if (actionName.StartsWith("Chopping wood", StringComparison.Ordinal)) s.ChopMinutes++;

            if (actionName == "Resting" && snap.WarmPct < 0.5 && !snap.HereFireActive)
            {
                s.ColdIdleMinutes++;
                stuckStreak++;
            }
            else
            {
                stuckStreak = 0;
            }
            s.StuckStreakMax = Math.Max(s.StuckStreakMax, stuckStreak);

            bool resting = actionName is "Resting" or "(none)";
            if (resting) s.IdleMinutes++;

            bool isNight = snap.GameTime.Hour is < 5 or >= 21;
            if (isNight)
            {
                s.NightMinutesTotal++;
                if (resting) s.NightRestMinutes++;
            }
            else
            {
                s.DayMinutesTotal++;
                if (resting) s.DayRestMinutes++;
            }

            if (!inCriticalDip && snap.WarmPct < 0.10)
                inCriticalDip = true;
            else if (inCriticalDip && snap.WarmPct >= 0.30)
            {
                s.NearMissCount++;
                inCriticalDip = false;
            }

            if (snap.Sticks > prevSticks) s.SticksGathered += (int)(snap.Sticks - prevSticks);
            if (snap.Tinder > prevTinder) s.TinderGathered += (int)(snap.Tinder - prevTinder);
            if (snap.CarryKg > prevCarry) s.ForageYieldKg += snap.CarryKg - prevCarry;
            if (snap.WaterL > prevWater) s.WaterGatheredL += snap.WaterL - prevWater;
            prevSticks = snap.Sticks;
            prevTinder = snap.Tinder;
            prevCarry = snap.CarryKg;
            prevWater = snap.WaterL;

            visitedTiles.Add(snap.Pos);
            s.MaxDistanceFromCamp = Math.Max(s.MaxDistanceFromCamp, snap.Pos.ManhattanDistance(campPos));
            if (prevPos.HasValue && prevPos.Value != snap.Pos)
                s.TotalTilesMoved += prevPos.Value.ManhattanDistance(snap.Pos);
            prevPos = snap.Pos;

            positionHistory.Add(snap.Pos);
            if (positionHistory.Count >= 3)
            {
                var c = positionHistory[^1];
                var b = positionHistory[^2];
                var a = positionHistory[^3];
                if (a == c && a != b) s.Reversals++;
            }
        }

        s.CacheFuelKgFinal = last.CacheFuelKg;
        s.CacheWaterLFinal = last.CacheWaterL;
        s.CacheFoodKgFinal = last.CacheFoodKg;
        s.UniqueTilesVisited = visitedTiles.Count;

        s.DistinctActionTypes = s.ActionMinutes.Keys
            .Select(NormalizeActionType)
            .Distinct()
            .Count();

        const double fuelTargetKg = 40, waterTargetL = 6, foodTargetKg = 2; // mirrors NPC.IsEnoughStockpiled's DAYS_RESERVE=2, PEOPLE_AT_CAMP=1
        s.CacheUtilizationPct = (
            Math.Min(1.0, s.CacheFuelKgFinal / fuelTargetKg) +
            Math.Min(1.0, s.CacheWaterLFinal / waterTargetL) +
            Math.Min(1.0, s.CacheFoodKgFinal / foodTargetKg)
        ) / 3.0;

        return s;
    }

    public string WriteLog(string name)
    {
        string dir = Environment.GetEnvironmentVariable("NPC_SIM_LOG_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "npc-sim");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{name}.log");

        using var writer = new StreamWriter(path, append: false);
        writer.WriteLine($"=== NPC Simulation: {name} ===");
        writer.WriteLine($"npc: {Npc.Name}   camp: {Ctx.Map!.GetPosition(Ctx.Camp)}   start: {StartGameTime:MMM d HH:mm}");
        writer.WriteLine();

        foreach (var snap in Snapshots)
        {
            writer.WriteLine(
                $"[{snap.Minute:D4} {snap.GameTime:MMM d HH:mm}] {FormatPos(snap.Pos)} {snap.LocationName,-14} {snap.Terrain,-8} " +
                $"act={(snap.Action ?? "-"),-16}({snap.ActionMinutesSpent}/{snap.ActionDuration}) need={(snap.Need?.ToString() ?? "-"),-7} " +
                $"warm={snap.WarmPct:F2} hyd={snap.HydratedPct:F2} en={snap.EnergyPct:F2} full={snap.FullPct:F2} T={snap.AmbientTempF:F0}F " +
                $"inv: st={snap.Sticks} ti={snap.Tinder} lg={snap.Logs} w={snap.WaterL:F1} kg={snap.CarryKg:F1}/{snap.CarryMaxKg:F0} " +
                $"campfire: {(snap.CampFireActive ? "ON" : "off")} {snap.CampFireBurningKg:F1}/{snap.CampFireUnburnedKg:F1}kg {snap.CampFireHoursLeft:F1}h " +
                $"cache: fuel={snap.CacheFuelKg:F1} water={snap.CacheWaterL:F1} food={snap.CacheFoodKg:F1}" +
                (snap.IsAlive ? "" : $"  *** DIED: {snap.DeathCause} ***"));

            if (snap.TileView != null)
            {
                writer.WriteLine("  --- tile view ---");
                foreach (var line in snap.TileView)
                    writer.WriteLine($"  {line}");
            }

            foreach (var line in snap.Lines)
                writer.WriteLine($"  > {line}");
        }

        writer.WriteLine();
        writer.WriteLine("=== Summary ===");
        writer.WriteLine(Summarize().ToString());

        return path;
    }

    private static string FormatPos(GridPosition p) => $"({p.X,3},{p.Y,3})";

    /// <summary>
    /// Collapses destination-specific action names ("Traveling to Dense Forest", "Traveling
    /// to Snowy Plain") into one category, so DistinctActionTypes counts actual behavioral
    /// variety (forage vs. travel vs. craft vs. ...) rather than being inflated by how many
    /// different places an NPC happened to walk to.
    /// </summary>
    private static string NormalizeActionType(string actionName)
    {
        int toIdx = actionName.IndexOf(" to ", StringComparison.Ordinal);
        return toIdx >= 0 ? actionName[..toIdx] : actionName;
    }
}
