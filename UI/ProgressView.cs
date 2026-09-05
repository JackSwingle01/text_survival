namespace text_survival.UI;

/// <summary>
/// Which progress presentation to use. The mechanics are the same; only the framing differs.
/// </summary>
public enum ProgressKind
{
    /// <summary>A plain bar: resting, working, sleeping, walking off an event's time cost.</summary>
    Activity,

    /// <summary>A bar with a "Found" list that grows as the player searches.</summary>
    Forage,

    /// <summary>A bar with materials being consumed and a result taking shape.</summary>
    Crafting,
}

/// <summary>The tone a progress line is drawn in. The UI owns the actual colours.</summary>
public enum ProgressTone
{
    Normal,
    Muted,
    Done,
    Discovery,
    Fuel,
    Food,
    Medicine,
    Material,
    Tinder,
}

/// <summary>
/// A progress display line. <paramref name="Icon"/> is a key into assets/icons/ui/
/// (see Resource.GetIconKey) — the platform-agnostic layer names the icon, the
/// desktop layer resolves and draws it.
/// </summary>
public record ProgressLine(string Text, ProgressTone Tone = ProgressTone.Normal, string? Icon = null);

/// <summary>A headed block of lines inside a progress view.</summary>
public class ProgressSection(string header)
{
    public string Header { get; set; } = header;
    public List<ProgressLine> Lines { get; } = [];
}

/// <summary>
/// A live progress display. Game logic creates one, mutates it between frames, and
/// disposes it when the activity ends. It is modal while it lives.
/// </summary>
public sealed class ProgressView(
    ProgressKind kind,
    string status,
    Func<Task> waitForContinue,
    Action close) : IDisposable
{
    private readonly Func<Task> _waitForContinue = waitForContinue;
    private readonly Action _close = close;
    private bool _closed;

    public ProgressKind Kind { get; } = kind;
    public string Status { get; set; } = status;

    /// <summary>0-1. Animation progress, not a count of minutes.</summary>
    public float Progress { get; set; }

    /// <summary>Minutes simulated so far and in total, shown on the bar.</summary>
    public int SimulatedMinutes { get; set; }
    public int TotalMinutes { get; set; }

    public List<ProgressSection> Sections { get; } = [];

    public ProgressSection Section(string header)
    {
        var existing = Sections.FirstOrDefault(s => s.Header == header);
        if (existing != null) return existing;

        var section = new ProgressSection(header);
        Sections.Add(section);
        return section;
    }

    /// <summary>Show a Continue button and complete when the player presses it.</summary>
    public Task WaitForContinue() => _waitForContinue();

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _close();
    }
}
