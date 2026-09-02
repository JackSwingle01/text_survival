using System.Text;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Tests.Support;

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

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Members: {Members.Count}   AliveAtEnd: {MembersAliveAtEnd}");
        sb.AppendLine($"Survived (min): avg={AverageSurvivedMinutes:F0}  min={MinSurvivedMinutes}  max={MaxSurvivedMinutes}");
        sb.AppendLine($"CacheFinal: fuel={CacheFuelKgFinal:F1}kg water={CacheWaterLFinal:F1}L food={CacheFoodKgFinal:F1}kg");
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

        for (int i = 0; i < minutes; i++)
        {
            if (Npcs.All(n => !n.IsAlive))
                break;

            player.Body.BodyTemperature = Body.BASE_BODY_TEMP;
            player.Body.Energy = SurvivalProcessor.MAX_ENERGY_MINUTES;
            player.Body.Hydration = SurvivalProcessor.MAX_HYDRATION;
            player.Body.CalorieStore = SurvivalProcessor.MAX_CALORIES;

            var originalOut = Console.Out;
            Console.SetOut(TextWriter.Null); // group runs measure outcomes, not per-minute decision traces
            try
            {
                Ctx.UpdateWithoutEvents(1, ActivityType.Resting);
            }
            finally
            {
                Console.SetOut(originalOut);
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

        return s;
    }
}
