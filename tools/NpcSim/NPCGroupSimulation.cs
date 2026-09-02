using System.Text;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Survival;

using text_survival.Tests.Support;

namespace NpcSim;

/// <summary>One NPC's outcome within a group run.</summary>
public sealed record MemberOutcome(string Name, double Boldness, int SurvivedMinutes, bool Died, string? DeathCause);

public sealed class GroupSummary
{
    public List<MemberOutcome> Members = [];
    public int MembersAliveAtEnd => Members.Count(m => !m.Died);
    public double AverageSurvivedMinutes => Members.Count > 0 ? Members.Average(m => m.SurvivedMinutes) : 0;
    public int MinSurvivedMinutes => Members.Count > 0 ? Members.Min(m => m.SurvivedMinutes) : 0;
    public int MaxSurvivedMinutes => Members.Count > 0 ? Members.Max(m => m.SurvivedMinutes) : 0;

    public double CacheFuelKgFinal;
    public double CacheWaterLFinal;
    public double CacheFoodKgFinal;
    public double CacheFuelKgPeak;
    public double CacheWaterLPeak;

    // Group-level fuel flow. Attributed at the group level rather than per-member because
    // the cache and fire are shared - when several NPCs act in the same minute there is no
    // reliable way to say whose kilogram went where, and the question that matters ("does
    // gathered fuel ever reach the cache, or is it all burned straight out of inventory")
    // is answered the same either way.
    public double FuelGatheredKg;
    public double FuelStashedKg;
    public double FuelToFireKg;
    public int StashActionCount;

    public bool FuelEverStockpiled;
    public bool WaterEverStockpiled;
    public bool FoodEverStockpiled;

    /// <summary>Minutes each need was active, summed across all members (so a 2-member group over 100 minutes contributes 200 member-minutes).</summary>
    public Dictionary<string, int> NeedMemberMinutes = new();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Members: {Members.Count}   AliveAtEnd: {MembersAliveAtEnd}");
        sb.AppendLine($"Survived (min): avg={AverageSurvivedMinutes:F0}  min={MinSurvivedMinutes}  max={MaxSurvivedMinutes}");
        sb.AppendLine($"CacheFinal: fuel={CacheFuelKgFinal:F1}kg water={CacheWaterLFinal:F1}L food={CacheFoodKgFinal:F1}kg   (peak fuel={CacheFuelKgPeak:F1}kg water={CacheWaterLPeak:F1}L)");
        sb.AppendLine($"StockpiledEver: fuel={FuelEverStockpiled} water={WaterEverStockpiled} food={FoodEverStockpiled}");
        sb.AppendLine($"FuelFlow: gathered={FuelGatheredKg:F2}kg  stashed={FuelStashedKg:F2}kg  toFire={FuelToFireKg:F2}kg  stashActions={StashActionCount}");
        sb.AppendLine("NeedMemberMinutes:");
        foreach (var (need, minutes) in NeedMemberMinutes.OrderByDescending(kv => kv.Value))
            sb.AppendLine($"  {need}: {minutes}");
        foreach (var m in Members)
            sb.AppendLine($"  {m.Name} (boldness={m.Boldness:F2}): survived={m.SurvivedMinutes}min  {(m.Died ? $"died: {m.DeathCause}" : "alive at end")}");
        return sb.ToString();
    }
}

/// <summary>
/// Runs several NPCs sharing one camp - same fire, same cache - to test whether group
/// resource-sharing changes survival outcomes versus the solo runs in
/// <see cref="NPCSimulation"/>. The underlying simulation already updates every NPC in
/// <see cref="GameContext.NPCs"/> each tick; this only adds the extra spawns and tracks
/// each member's own outcome (not full per-minute snapshots - group runs are about
/// aggregate/comparative outcomes, not reading one NPC's decision trace).
/// </summary>
public sealed class NPCGroupSimulation
{
    public GameContext Ctx { get; }
    public List<NPC> Npcs { get; }

    private readonly Dictionary<NPC, int> _survivedMinutes = [];
    private readonly Dictionary<NPC, string?> _deathCause = [];
    private readonly Dictionary<string, int> _needMemberMinutes = [];

    private double _fuelGatheredKg, _fuelStashedKg, _fuelToFireKg;
    private double _cacheFuelPeak, _cacheWaterPeak;
    private int _stashActionCount;
    private bool _fuelEverStockpiled, _waterEverStockpiled, _foodEverStockpiled;

    private NPCGroupSimulation(GameContext ctx, List<NPC> npcs)
    {
        Ctx = ctx;
        Npcs = npcs;
        foreach (var npc in npcs)
        {
            _survivedMinutes[npc] = 0;
            _deathCause[npc] = null;
        }
    }

    /// <summary>
    /// <paramref name="personality"/>, if given, is applied to every member (e.g. "what if
    /// the whole group is Bold"); omit it to let each member's personality roll
    /// independently, which is the more realistic "band of survivors" scenario.
    /// </summary>
    public static NPCGroupSimulation Create(int npcCount, int? seed = null, Personality? personality = null)
    {
        if (npcCount < 1)
            throw new ArgumentOutOfRangeException(nameof(npcCount), "A group needs at least one NPC.");

        var ctx = GameContext.CreateNewGame(seed);
        ctx.Ui = new ScriptedUi();

        if (ctx.NPCs.Count == 0)
            throw new InvalidOperationException("CreateNewGame produced no starting NPC - nothing to simulate.");

        var npcs = new List<NPC>(ctx.NPCs);

        // Bring the whole group together at camp so they start out actually sharing the
        // fire and cache, rather than one NPC adjacent and the rest spawned on top of it.
        npcs[0].CurrentLocation = ctx.Camp;

        for (int i = 1; i < npcCount; i++)
        {
            var extra = NPCFactory.CreateTestNPC(ctx.Camp, ctx.Map!, ctx.Camp);
            ctx.NPCs.Add(extra);
            npcs.Add(extra);
        }

        if (personality != null)
            foreach (var npc in npcs)
                npc.Personality = personality;

        return new NPCGroupSimulation(ctx, npcs);
    }

    /// <summary>Advance minute by minute until every member has died or <paramref name="minutes"/> elapses.</summary>
    public void Run(int minutes)
    {
        var player = Ctx.player;
        var cache = Ctx.Camp.GetFeature<CacheFeature>()?.Storage;
        var campFire = Ctx.Camp.GetFeature<HeatSourceFeature>();

        double prevInvFuel = Npcs.Sum(n => n.Inventory.GetWeight(ResourceCategory.Fuel));
        double prevCacheFuel = cache?.GetWeight(ResourceCategory.Fuel) ?? 0;
        double prevFireMass = (campFire?.BurningMassKg ?? 0) + (campFire?.UnburnedMassKg ?? 0);

        for (int i = 0; i < minutes; i++)
        {
            if (Npcs.All(n => !n.IsAlive))
                break;

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

            foreach (var line in sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
                if (line.Contains("] Completed: Storing ", StringComparison.Ordinal))
                    _stashActionCount++;

            // Need occupancy, summed across living members
            foreach (var npc in Npcs)
            {
                if (!npc.IsAlive) continue;
                var needName = npc.CurrentNeed?.ToString() ?? "(none)";
                _needMemberMinutes[needName] = _needMemberMinutes.GetValueOrDefault(needName) + 1;
            }

            // Group fuel flow
            double invFuel = Npcs.Sum(n => n.Inventory.GetWeight(ResourceCategory.Fuel));
            double cacheFuel = cache?.GetWeight(ResourceCategory.Fuel) ?? 0;
            double fireMass = (campFire?.BurningMassKg ?? 0) + (campFire?.UnburnedMassKg ?? 0);

            double invDelta = invFuel - prevInvFuel;
            double cacheDelta = cacheFuel - prevCacheFuel;
            double fireDelta = fireMass - prevFireMass;

            if (invDelta > 0) _fuelGatheredKg += invDelta;
            else if (invDelta < 0)
            {
                double left = -invDelta;
                if (cacheDelta > 0) _fuelStashedKg += Math.Min(left, cacheDelta);
                if (fireDelta > 0) _fuelToFireKg += Math.Min(left, fireDelta);
            }
            prevInvFuel = invFuel;
            prevCacheFuel = cacheFuel;
            prevFireMass = fireMass;

            _cacheFuelPeak = Math.Max(_cacheFuelPeak, cacheFuel);
            _cacheWaterPeak = Math.Max(_cacheWaterPeak, cache?.Weight(Resource.Water) ?? 0);

            // Stockpile targets are read off any living member (they all share one camp cache)
            var probe = Npcs.FirstOrDefault(n => n.IsAlive);
            if (probe != null)
            {
                if (probe.IsEnoughStockpiled(ResourceCategory.Fuel)) _fuelEverStockpiled = true;
                if (probe.IsEnoughStockpiled(ResourceCategory.Water)) _waterEverStockpiled = true;
                if (probe.IsEnoughStockpiled(ResourceCategory.Food)) _foodEverStockpiled = true;
            }

            foreach (var npc in Npcs)
            {
                if (_deathCause[npc] != null) continue; // already recorded its death minute
                _survivedMinutes[npc] = i + 1;
                if (!npc.IsAlive)
                    _deathCause[npc] = NPCBodyFeature.DetermineDeathCause(npc);
            }
        }
    }

    public GroupSummary Summarize()
    {
        var s = new GroupSummary();
        foreach (var npc in Npcs)
        {
            s.Members.Add(new MemberOutcome(
                npc.Name, npc.Personality.Boldness,
                _survivedMinutes[npc],
                _deathCause[npc] != null,
                _deathCause[npc]));
        }

        var cache = Ctx.Camp.GetFeature<CacheFeature>()?.Storage;
        s.CacheFuelKgFinal = cache?.GetWeight(ResourceCategory.Fuel) ?? 0;
        s.CacheWaterLFinal = cache?.Weight(Resource.Water) ?? 0;
        s.CacheFoodKgFinal = cache?.GetWeight(ResourceCategory.Food) ?? 0;
        s.CacheFuelKgPeak = _cacheFuelPeak;
        s.CacheWaterLPeak = _cacheWaterPeak;

        s.FuelGatheredKg = _fuelGatheredKg;
        s.FuelStashedKg = _fuelStashedKg;
        s.FuelToFireKg = _fuelToFireKg;
        s.StashActionCount = _stashActionCount;

        s.FuelEverStockpiled = _fuelEverStockpiled;
        s.WaterEverStockpiled = _waterEverStockpiled;
        s.FoodEverStockpiled = _foodEverStockpiled;
        s.NeedMemberMinutes = new Dictionary<string, int>(_needMemberMinutes);

        return s;
    }
}
