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
    }

    public void AddRange(IEnumerable<string> texts, LogLevel level = LogLevel.Normal, string timestamp = "")
    {
        foreach (var text in texts)
            Add(text, level, timestamp);
    }

    public void Clear() => _entries.Clear();
}
