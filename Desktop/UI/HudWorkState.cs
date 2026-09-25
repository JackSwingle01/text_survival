using text_survival.Environments;

namespace text_survival.Desktop.UI;

/// <summary>Keep depleted work focusable until the player changes location.</summary>
public sealed class HudWorkState
{
    private Location? _location;
    private readonly Dictionary<string, HudAction> _seen = [];

    public IReadOnlyList<HudAction> Observe(Location location, IReadOnlyList<HudAction> actions)
    {
        if (!ReferenceEquals(location, _location)) { _location = location; _seen.Clear(); }
        var current = actions.Where(a => !a.IsCore && a.Group != HudActionGroup.Navigation).ToDictionary(a => a.Id);
        foreach (var id in _seen.Keys.ToArray())
            if (!current.ContainsKey(id)) _seen[id] = _seen[id] with { DisabledReason = "No longer available here." };
        foreach (var action in current.Values) _seen[action.Id] = action;
        return _seen.Values.OrderBy(a => a.Group).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray();
    }
}
