using System.Collections.Concurrent;

namespace text_survival.Web;

/// <summary>
/// Thread-safe registry mapping session IDs to WebGameSession instances.
/// Allows GameContext to store just a string SessionId while WebIO can look up the actual session.
/// </summary>
public static class SessionRegistry
{
    private static readonly ConcurrentDictionary<string, WebGameSession> _sessions = new();

    public static void Register(string id, WebGameSession session)
    {
        _sessions[id] = session;
    }

    public static WebGameSession? Get(string? id)
    {
        if (id == null) return null;
        return _sessions.TryGetValue(id, out var session) ? session : null;
    }

    public static void Remove(string id)
    {
        _sessions.TryRemove(id, out _);
    }

    public static int ActiveSessionCount => _sessions.Count;
}
