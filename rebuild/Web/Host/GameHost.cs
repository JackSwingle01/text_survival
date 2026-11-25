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
/// across steps (a crossing resumes; a project in the pack is picked up where it was left). Work (foraging,
/// butchering, cooking) runs in those chunks too, held between them (Sim.Step with more), so it is one
/// session however it is cut, and a stop ends it at the minute it was pressed (Sim.Finish).
public sealed class GameHost
{
    World? _world;
    Job? _job;
    int _gen;
    readonly Dictionary<string, Func<CellDto?, int?, Job>> _verbs = [];
    readonly List<LogDto> _log = [];
    // What the rounds of the fight under way told: the fight screen's own lines, apart from the walk before it.
    readonly List<LogDto> _encounterLog = [];
    ReceiptDto? _receipt;
    // Sim looks around on the first minute awake, which falls in whatever the player does next, so
    // the minute sleep ended is what tells that look's news is the morning's.
    long? _sleptUntil;

    public const int MaxLog = 80;

    World W => _world ?? throw new InvalidOperationException("No game: call NewGame or Load first");

    /// Read access for tests and dev scenarios; the page never sees it.
    internal World World => W;

    public string NewGame(int seed)
    {
        _world = World.New(seed); // it has already looked around once: the map opens on what is in view
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
            _job = start(a.To, a.Minutes);
        }
        else if (a.Advance is int minutes)
        {
            if (_job is not { Finished: false } job) throw new InvalidOperationException("No job under way to advance");
            if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(json), minutes, "Advance needs at least one minute");
            Run(job, minutes);
        }
        else if (a.Stop == true)
        {
            if (_job is { Finished: false } job)
            {
                if (job.Held) Took(job, new StepResult(Sim.Finish(W, AgentId.Player), 0, StepOutcome.Finished, null));
                job.Finish("stopped", null);
            }
        }
        else throw new ArgumentException("An act names a verb, an advance or a stop");
        if (_job is not { Finished: false } && BodyTakesOver() is { } asleep) _job = asleep;
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
        _sleptUntil = null;
        _log.Clear();
        _encounterLog.Clear();
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
        /// Work held in Sim between this job's chunks (Sim.Step with more): a stop must finish it.
        public bool Held { get; set; }
        /// The job's work session, ended or held: what the forage screen draws its finds and mood from.
        public WorkSession? Session { get; set; }
        public bool Finished { get; private set; }
        /// How it ended (Sim's StepOutcome, or stopped by the client), once it has.
        public string? Outcome { get; private set; }
        public string? Reason { get; private set; }
        /// How the fight ended, when a step of this job ended one: said in place of Sim's bare "fight over".
        public string? EncounterEnding { get; set; }
        public void Finish(string outcome, string? reason)
        {
            if (Finished) return;
            (Finished, Outcome, Reason) = (true, outcome, reason);
        }
        public void Finish(StepResult r) => Finish(r.Outcome.ToString().ToLowerInvariant(), EncounterEnding ?? r.Reason);
    }

    /// minutes: how long to run it, one of the verb's durations, where it offers a choice.
    Job JobFor(Verb v, int? minutes)
    {
        var p = W.Agent(AgentId.Player);
        bool chunked = v.Activity is Wait or Rest or Sleep or Travel or Make or ImproveShelter || HeldWork(v.Activity);
        if (minutes is int m && v.Durations?.Contains(m) != true)
            throw new ArgumentException($"'{v.Label}' does not run for {m} minutes");
        int? budget = minutes ?? (v.Minutes == Verbs.UntilDoneMinutes ? null : v.Minutes);
        var job = new Job(v.Label, ViewBuilder.KindOf(v.Activity), v.Activity, budget, chunked, p.Tile, (v.Activity as Travel)?.Dir);
        // A refused verb never starts: no time passes, and the refusal comes back as the job's reason.
        if (v.Refused != null) job.Finish("refused", v.Refused.Reason);
        return job;
    }

    /// Dropped asleep from exhaustion (Body.IsAsleep), the player has nothing to choose until they wake:
    /// the job under way is the body's own sleep, run in the client's chunks like any other, and no stop
    /// ends it (a stop ends this job, and the next is the sleep again). Only while that sleep can run: a
    /// fight holds a sleeper to its rounds, and its view offers them.
    Job? BodyTakesOver()
    {
        var p = W.Agent(AgentId.Player);
        if (!Sim.IsAlive(p) || !Body.IsAsleep(p.Body)) return null;
        var sleep = new Sleep(AgentId.Player);
        if (Sim.Refusal(W, sleep) != null) return null;
        return new Job("Asleep", ViewBuilder.KindOf(sleep), sleep, Body.AsleepMinutesLeft(p.Body), chunked: true, p.Tile, null);
    }

    /// Work that takes time, run in chunks and held between them: a search, a butchering, cooking, melting snow.
    static bool HeldWork(Activity a) => a is Gather or Butcher or WalkAbout;

    void Run(Job job, int chunk)
    {
        var w = W;
        if (!job.Chunked)
        {
            var whole = Sim.Step(w, job.Activity, job.Budget ?? Verbs.UntilDoneMinutes);
            Took(job, whole);
            job.Finish(whole);
            return;
        }
        int left = (job.Budget ?? Verbs.UntilDoneMinutes) - job.Done;
        int minutes = Math.Min(chunk, left);
        bool more = HeldWork(job.Activity) && minutes < left;
        var r = Sim.Step(w, job.Activity, minutes, more);
        job.Session = r.Work ?? job.Session;
        job.Held = more && Sim.WorkUnderWay(w, AgentId.Player) != null;
        Took(job, r);
        // A chunk that ran all its minutes with no reason has only reached the client's chunk boundary;
        // anything else (a stop with a reason: arrived, made, interrupted, refused) is the job's end.
        if (r.Reason != null || r.MinutesElapsed < minutes || job.Done >= (job.Budget ?? Verbs.UntilDoneMinutes)) job.Finish(r);
    }

    void Took(Job job, StepResult r)
    {
        job.Done += r.MinutesElapsed;
        foreach (var e in r.Events)
        {
            if (e is Noticed n && n.Who == AgentId.Player)
                _receipt = ReceiptOf(n.Receipt, job.Activity is Sleep || n.Minute <= _sleptUntil + 1);
            else if (Narration.ForPlayer(W, e) is { } line)
            {
                _log.Add(Words.Log(line));
                if (job.Activity is Fight) _encounterLog.Add(Words.Log(line));
                if (e is EncounterEnded) job.EncounterEnding = line.Text;
            }
        }
        if (_log.Count > MaxLog) _log.RemoveRange(0, _log.Count - MaxLog);
        if (job.Activity is Sleep) _sleptUntil = W.Minute;
    }

    ReceiptDto ReceiptOf(Receipt r, bool woke)
    {
        var lines = new List<LogDto>();
        foreach (var e in r.Happened)
            if (Narration.Of(W, e) is { } l) lines.Add(Words.Log(l));
        foreach (var c in r.Changes) lines.Add(new LogDto(W.Minute, Narration.Change(c), c.Now == null ? "bad" : "plain", ChangeIcon(c)));
        foreach (var s in r.Signs)
            lines.Add(new LogDto(W.Minute - s.AgeMinutes, Narration.Sign(s), "bad", ViewBuilder.TrackIcon(s.Maker)));
        lines = [.. lines.OrderBy(l => l.Minute)]; // stable: lines of one minute keep the order they happened in
        string title = woke ? "Morning" : r.Full ? "While you were away" : "Since you were last here";
        return new ReceiptDto(title, r.SinceMinute, W.Minute, r.Full, lines);
    }

    static string ChangeIcon(Change c) => c.Kind switch
    {
        ChangeKind.Fire => c.Now is "Cold" or "Embers" or null ? "embers" : "fire",
        ChangeKind.Pile => "ui/storage",
        ChangeKind.Carcass => "carcass",
        ChangeKind.Shelter => "shelter",
        _ => throw new ArgumentOutOfRangeException(nameof(c), c.Kind, null),
    };

    // ---- the view ----

    ViewDto BuildView()
    {
        _gen++;
        _verbs.Clear();
        if (Combat.EncounterOf(W, AgentId.Player) == null) _encounterLog.Clear();
        var b = new ViewBuilder(W, _gen);
        var view = b.Build(_job, _receipt, _log, _encounterLog);
        foreach (var (id, verb) in b.Verbs) _verbs[id] = (_, minutes) => JobFor(verb, minutes);
        foreach (var (id, fight) in b.CombatVerbs) _verbs[id] = (to, _) => CombatJob(fight, to);
        _receipt = null; // shown once
        return view;
    }

    Job CombatJob(ViewBuilder.CombatVerb f, CellDto? to)
    {
        var p = W.Agent(AgentId.Player);
        var act = new Fight(AgentId.Player, f.Action, f.Target, f.Action == CombatAction.Move
            ? to is { } c ? new Cell(c.X, c.Y) : throw new ArgumentException("Move needs a cell (to)")
            : null);
        var job = new Job(Words.Cap(Words.Of(f.Action)), "fight", act, 1, false, p.Tile, null);
        if (Sim.Refusal(W, act) is { } refused) job.Finish("refused", refused.Reason);
        else if (Combat.EncounterOf(W, AgentId.Player) is { } enc &&
                 Combat.Refusal(W, enc, Combat.UnitOf(enc, AgentId.Player), f.Action, f.Target is int t ? enc.Units[t] : null, act.To) is { } no)
            job.Finish("refused", no.Reason);
        return job;
    }
}
