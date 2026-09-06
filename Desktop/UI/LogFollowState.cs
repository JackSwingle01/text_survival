namespace text_survival.Desktop.UI;

/// <summary>Reader intent is independent of ImGui's delayed scroll positioning.</summary>
public sealed class LogFollowState
{
    public bool Following { get; private set; } = true;
    public long Unread { get; private set; }
    private long _revision;
    public void Reset(long revision) { _revision = revision; JumpToLatest(); }
    public bool Observe(long revision)
    {
        long incoming = Math.Max(0, revision - _revision);
        _revision = revision;
        if (!Following) Unread += incoming;
        return incoming > 0;
    }
    public void ReadOlder() => Following = false;
    public void JumpToLatest() { Following = true; Unread = 0; }
    public void UpdateViewport(bool atBottom, bool userScrolling, bool incoming)
    {
        if (userScrolling) ReadOlder();
        else if (atBottom && !incoming) JumpToLatest();
    }
}
