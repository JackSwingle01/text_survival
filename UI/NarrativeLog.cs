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
    public const int MAX_VISIBLE_LINES = 30;
    private readonly List<(string Text, LogLevel Level)> _entries = [];

    public void Add(string text, LogLevel level = LogLevel.Normal)
    {
        _entries.Add((text, level));
    }

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
