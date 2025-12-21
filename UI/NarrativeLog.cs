namespace text_survival.UI;

public enum LogLevel
{
    Normal,
    Success,
    Warning,
    Danger,
    System
}

public class NarrativeLog
{
    public const int MAX_VISIBLE_LINES = 16;
    private const string SEPARATOR = "· · ·";
    private readonly List<(string Text, LogLevel Level)> _entries = [];

    public void Add(string text, LogLevel level = LogLevel.Normal)
    {
        // ignore duplicates
        if (_entries.Count > 0 && _entries[^1].Text.Trim() == text.Trim())
            return;
        _entries.Add((text, level));
    }

    public void AddSeparator()
    {
        if (_entries.Count > 0 && !LastEntryIsSeparator())
            _entries.Add((SEPARATOR, LogLevel.System));
    }

    public bool LastEntryIsSeparator() =>
        _entries.Count > 0 && _entries[^1].Text == SEPARATOR;

    public void AddRange(IEnumerable<string> texts, LogLevel level = LogLevel.Normal)
    {
        foreach (var text in texts)
            _entries.Add((text, level));
    }

    public IReadOnlyList<(string Text, LogLevel Level)> GetVisible() =>
        _entries.TakeLast(MAX_VISIBLE_LINES).ToList();

    public int TotalCount => _entries.Count;

    public void Clear() => _entries.Clear();
}
