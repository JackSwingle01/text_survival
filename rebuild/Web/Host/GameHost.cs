using System.Text;
using System.Text.Json;

namespace Survival.Web;

/// The whole boundary between a UI and the game: NewGame, Act, View, Save, Load. JSON in, JSON views
/// out, bytes for saves; no Sim type crosses it, so the same calls can sit behind JSExport in a Web
/// Worker (rebuild/Web/App) or behind HTTP on a server.
///
/// The action vocabulary is the game's own: every view lists verbs (Verbs.At, and Combat's round
/// actions in a fight) under ids, and the client posts an id back. The host keeps the exact activity
/// behind each id, for that view only: an id from an older view is refused, never guessed at.
///
/// Time is paced by the client. Posting a verb starts a job; posting {"advance":N} runs it for up to N
/// more game minutes, so a long action comes back as many views and the clock is seen to move. Waiting,
/// resting, sleeping, walking and making run in those chunks, because Sim continues each of them
/// across steps (a crossing resumes; a project in the pack is picked up where it was left). Other work
/// (foraging, butchering, cooking) runs as one Sim.Step on its first advance, since a step is its
/// session; the client still plays its minutes out on the clock.
public sealed class GameHost
{
    World? _world;
    Job? _job;
    int _gen;
    readonly Dictionary<string, Func<CellDto?, Job>> _verbs = [];
    readonly List<LogDto> _log = [];
    ReceiptDto? _receipt;

    public const int MaxLog = 80;

    World W => _world ?? throw new InvalidOperationException("No game: call NewGame or Load first");

    /// Read access for tests and dev scenarios; the page never sees it.
    internal World World => W;

    public string NewGame(int seed)
    {
        _world = World.New(seed);
        // A new world has looked at nothing yet (Beliefs.Explored is null until the first look): the
        // player takes in where they stand, as they will every quarter-hour awake.
        Knowledge.Glance(_world, _world.Agent(AgentId.Player), []);
        Reset();
        _log.Add(new LogDto(_world.Minute, "Separated from the tribe in the crossing. You are alone at the camp.", "plain", "shelter"));
        return View();
    }

    /// One post from the client: {"verb":"12.3"} starts that verb; {"advance":15} runs the job under way
    /// for up to 15 game minutes; {"stop":true} drops it. Returns the view after.
    public string Act(string json)
    {
        var a = JsonSerializer.Deserialize(json, HostJson.Default.ActDto) ?? throw new JsonException("No action");
        if (a.Verb != null)
        {
            if (_job is { Finished: false }) throw new InvalidOperationException($"A job is under way ({_job.Label}): advance or stop it first");
            if (!_verbs.TryGetValue(a.Verb, out var start))
                throw new ArgumentException($"No verb '{a.Verb}' in the current view (generation {_gen})");
            _job = start(a.To);
        }
        else if (a.Advance is int minutes)
        {
            if (_job is not { Finished: false } job) throw new InvalidOperationException("No job under way to advance");
            if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(json), minutes, "Advance needs at least one minute");
            Run(job, minutes);
        }
        else if (a.Stop == true)
        {
            if (_job is { Finished: false } job) job.Finish("stopped");
        }
        else throw new ArgumentException("An act names a verb, an advance or a stop");
        return View();
    }

    public string View() => JsonSerializer.Serialize(BuildView(), HostJson.Default.ViewDto);

    public byte[] Save()
    {
        if (_job is { Finished: false }) throw new InvalidOperationException("Finish or stop the job under way before saving");
        return Encoding.UTF8.GetBytes(Survival.Save.ToJson(W));
    }

    public string Load(byte[] bytes)
    {
        _world = Survival.Save.FromJson(Encoding.UTF8.GetString(bytes));
        Reset();
        _log.Add(new LogDto(_world.Minute, "Loaded.", "plain", "ui/storage"));
        return View();
    }

    void Reset()
    {
        _job = null;
        _receipt = null;
        _log.Clear();
        _verbs.Clear();
    }

    // ---- jobs ----

    /// A verb under way. Budget: the minutes it runs for, or null for work that stops itself.
    internal sealed class Job(string label, string kind, Activity activity, int? budget, bool chunked, TileId from, Direction? dir)
    {
        public string Label { get; } = label;
        public string Kind { get; } = kind;
        public Activity Activity { get; } = activity;
        public int? Budget { get; } = budget;
        /// Whether Sim continues it across steps, so it can be run in the client's chunks.
        public bool Chunked { get; } = chunked;
        public TileId From { get; } = from;
        public Direction? Dir { get; } = dir;
        public int Done { get; set; }
        public bool Finished { get; private set; }
        public string? Reason { get; private set; }
        public void Finish(string? reason) { Finished = true; Reason ??= reason; }
    }

    Job JobFor(Verb v)
    {
        var p = W.Agent(AgentId.Player);
        bool chunked = v.Activity is Wait or Rest or Sleep or Travel or Make;
        int? budget = v.Minutes == Verbs.UntilDoneMinutes ? null : v.Minutes;
        var job = new Job(v.Label, ViewBuilder.KindOf(v.Activity), v.Activity, budget, chunked, p.Tile, (v.Activity as Travel)?.Dir);
        // A refused verb never starts: no time passes, and the refusal comes back as the job's reason.
        if (v.Refused != null) job.Finish(v.Refused);
        return job;
    }

    void Run(Job job, int chunk)
    {
        var w = W;
        if (!job.Chunked)
        {
            var whole = Sim.Step(w, job.Activity, job.Budget ?? Verbs.UntilDoneMinutes);
            Took(job, whole);
            job.Finish(whole.InterruptedReason);
            return;
        }
        int left = (job.Budget ?? Verbs.UntilDoneMinutes) - job.Done;
        int minutes = Math.Min(chunk, left);
        var r = Sim.Step(w, job.Activity, minutes);
        Took(job, r);
        if (r.InterruptedReason != null || r.MinutesElapsed < minutes || job.Done >= (job.Budget ?? Verbs.UntilDoneMinutes) || StoppedItself(job))
            job.Finish(r.InterruptedReason);
    }

    /// Work that ends on its own and finished exactly at the end of a chunk: the crossing is over,
    /// or the project is no longer in the pack. Run again, it would begin anew.
    bool StoppedItself(Job job)
    {
        var p = W.Agent(AgentId.Player);
        return job.Activity switch
        {
            Travel => p.Crossing == null,
            Make m => Items.PackOf(p) is not { } pack || Making.ProjectFor(pack, m.Recipe) == null,
            _ => false,
        };
    }

    void Took(Job job, StepResult r)
    {
        job.Done += r.MinutesElapsed;
        foreach (var e in r.Events)
        {
            if (e is Noticed n && n.Who == AgentId.Player) _receipt = ReceiptOf(n.Receipt, job.Activity is Sleep);
            else if (Words.Event(W, e) is { } line) _log.Add(line);
        }
        if (_log.Count > MaxLog) _log.RemoveRange(0, _log.Count - MaxLog);
    }

    ReceiptDto ReceiptOf(Receipt r, bool woke)
    {
        var lines = new List<LogDto>();
        foreach (var e in r.Happened)
            if (Words.Line(W, e) is { } l) lines.Add(l);
        foreach (var c in r.Changes) lines.Add(new LogDto(W.Minute, ChangeText(c), c.Now == null ? "bad" : "plain", ChangeIcon(c)));
        foreach (var s in r.Signs)
            lines.Add(new LogDto(W.Minute - s.AgeMinutes, $"{Words.Cap(Words.Of(s.Maker))} prints, {ViewBuilder.Reading(s)}", "bad", ViewBuilder.TrackIcon(s.Maker)));
        lines.Sort((a, b) => a.Minute.CompareTo(b.Minute));
        string title = woke ? "Morning" : r.Full ? "While you were away" : "Since you were last here";
        return new ReceiptDto(title, r.SinceMinute, W.Minute, r.Full, lines);
    }

    static string ChangeText(Change c) => c.Kind switch
    {
        ChangeKind.Fire => c.Now == null ? "The fire pit is gone" : c.Was == null ? $"A fire here: {Words.Of(c.Now)}" : $"The fire: {Words.Of(c.Was)} to {Words.Of(c.Now)}",
        ChangeKind.Stash => c.Now == null ? $"The {Words.Of(c.Subject)} in the stash is gone" : c.Was == null ? $"{c.Now} kg {Words.Of(c.Subject)} in the stash" : $"Stash {Words.Of(c.Subject)}: {c.Was} to {c.Now} kg",
        ChangeKind.Carcass => c.Now == null ? $"The {Words.Lower(c.Subject)} carcass is gone" : $"A {Words.Lower(c.Subject)} carcass",
        ChangeKind.Shelter => c.Now == null ? $"The {Words.Lower(c.Was ?? "shelter")} is gone" : $"A {Words.Lower(c.Now)} stands here",
        _ => throw new ArgumentOutOfRangeException(nameof(c), c.Kind, null),
    };

    static string ChangeIcon(Change c) => c.Kind switch
    {
        ChangeKind.Fire => c.Now is "Cold" or "Embers" or null ? "embers" : "fire",
        ChangeKind.Stash => "ui/storage",
        ChangeKind.Carcass => "carcass",
        ChangeKind.Shelter => "shelter",
        _ => throw new ArgumentOutOfRangeException(nameof(c), c.Kind, null),
    };

    // ---- the view ----

    ViewDto BuildView()
    {
        _gen++;
        _verbs.Clear();
        var b = new ViewBuilder(W, _gen);
        var view = b.Build(_job, _receipt, _log);
        foreach (var (id, verb) in b.Verbs) _verbs[id] = _ => JobFor(verb);
        foreach (var (id, fight) in b.Fights) _verbs[id] = to => FightJob(fight, to);
        _receipt = null; // shown once
        return view;
    }

    Job FightJob(ViewBuilder.FightVerb f, CellDto? to)
    {
        var p = W.Agent(AgentId.Player);
        var act = new Fight(AgentId.Player, f.Action, f.Target, f.Action == CombatAction.Move
            ? to is { } c ? new Cell(c.X, c.Y) : throw new ArgumentException("Move needs a cell (to)")
            : null);
        var job = new Job(Words.Cap(Words.Of(f.Action)), "fight", act, 1, false, p.Tile, null);
        if (Sim.Refusal(W, act) is string refused) job.Finish(refused);
        else if (Combat.EncounterOf(W, AgentId.Player) is { } enc &&
                 Combat.Refusal(W, enc, Combat.UnitOf(enc, AgentId.Player), f.Action, f.Target is int t ? enc.Units[t] : null, act.To) is string no)
            job.Finish(no);
        return job;
    }
}
