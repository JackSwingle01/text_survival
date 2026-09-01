namespace text_survival.UI;

public enum LogLevel
{
    Normal,
    Success,
    Warning,
    Danger,
    System,
    Discovery  // New discoveries in the Discovery Log
}

public class NarrativeLog
{
    /// <summary>
    /// How much history is kept. Enough to reconstruct how a situation developed;
    /// bounded so a long run does not carry an ever-growing log through every save.
    /// </summary>
    private const int MaxEntries = 200;

    private List<(string Text, LogLevel Level, string Timestamp)> _entries = [];

    // For JSON serialization
    public List<(string Text, LogLevel Level, string Timestamp)> Entries
    {
        get => _entries;
        init => _entries = value;
    }

    public void Add(string text, LogLevel level = LogLevel.Normal, string timestamp = "")
    {
        // ignore duplicates
        if (_entries.Count > 0 && _entries[^1].Text.Trim() == text.Trim())
            return;
        _entries.Add((text, level, timestamp));

        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(0, _entries.Count - MaxEntries);
    }

    /// <summary>The most recent entries, oldest first. Reading does not consume them.</summary>
    public IReadOnlyList<(string Text, LogLevel Level, string Timestamp)> Recent(int count) =>
        _entries.TakeLast(count).ToList();

    public void AddRange(IEnumerable<string> texts, LogLevel level = LogLevel.Normal, string timestamp = "")
    {
        foreach (var text in texts)
            Add(text, level, timestamp);
    }

    public void Clear() => _entries.Clear();
}
