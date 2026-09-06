namespace text_survival.Actors;

public enum CompanionDecisionReason { CommittedWork, SelfCare, Pursuit, OptionalWork, RequestWait, AgreedWait, Combat, BlockedNeed }

/// <summary>Read-only measurements. Ground-truth separation must never feed actor decisions.</summary>
public sealed class CompanionDiagnostics
{
    public Dictionary<string, int> SeparatedMinutes { get; } = [];
    public List<SeparationEpisode> Episodes { get; } = [];
    public List<CompanionTransition> Transitions { get; } = [];
    private readonly Dictionary<Actor, int> _ids = [];
    private string Label(Actor actor)
    {
        if (!_ids.TryGetValue(actor, out int id)) _ids[actor] = id = _ids.Count + 1;
        return $"{actor.Name}#{id}";
    }
    private readonly Dictionary<NPC, SeparationEpisode> _open = [];
    private readonly Dictionary<NPC, string> _lastState = [];

    public void Sample(NPC npc, double minute, bool captureTransitions = false)
    {
        var intent = npc.Following;
        bool separated = npc.IsAlive && intent != null && npc.CurrentLocation != intent.Target.CurrentLocation;
        string reason = npc.IsAlive ? (npc.DecisionReason == CompanionDecisionReason.Pursuit ? $"Pursuit/{intent?.Status}" : npc.DecisionReason.ToString()) : "Dead";
        if (separated)
        {
            SeparatedMinutes[reason] = SeparatedMinutes.GetValueOrDefault(reason) + 1;
            if (!_open.ContainsKey(npc))
            {
                var episode = new SeparationEpisode(Label(npc), Label(intent!.Target), minute);
                _open[npc] = episode;
                Episodes.Add(episode);
            }
        }
        else if (_open.Remove(npc, out var ended))
        {
            ended.EndMinute = minute;
            ended.Outcome = !npc.IsAlive ? "Died" : intent == null ? npc.FollowEndReason ?? "AgreementEnded" : "Reunited";
        }
        if (!captureTransitions) return;
        string state = $"{reason}/{npc.CurrentAction?.Name}/{npc.CurrentNeed}/{npc.CurrentLocation.Name}/{intent?.LeadPosition}/{intent?.RouteFailures}/{intent == null}";
        if (_lastState.GetValueOrDefault(npc) == state) return;
        _lastState[npc] = state;
        Transitions.Add(new(minute, Label(npc), intent == null ? null : Label(intent.Target), reason, npc.CurrentAction?.Name,
            npc.CurrentAction?.MinutesSpent ?? 0, npc.CurrentNeed?.ToString(),
            intent == null ? null : minute - intent.LastEvidenceMinute, npc.CurrentLocation.Name,
            intent?.LeadPosition?.ToString(), intent?.Status.ToString(), npc.FollowEndReason));
    }
}

public sealed record CompanionTransition(double Minute, string Actor, string? Target, string Reason,
    string? Action, int Progress, string? Need, double? EvidenceAge, string Location, string? Lead, string? Pursuit, string? EndReason);

public sealed record SeparationEpisode(string Actor, string Target, double StartMinute)
{
    public double? EndMinute { get; set; }
    public string Outcome { get; set; } = "StillSeparated";
}
