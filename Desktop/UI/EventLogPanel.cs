using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>Retained history with deliberate following; never steals the reader's scroll position.</summary>
public sealed class EventLogPanel
{
    private readonly LogFollowState _reading = new();
    private long _firstSequence;
    private readonly Dictionary<long, float> _heights = [];
    private NarrativeLog? _log;

    public void Render(GameContext ctx, HudRect rect, HudState state)
    {
        var log = ctx.Log;
        if (_log != log) { _log = log; _reading.Reset(log.Revision); _heights.Clear(); }
        bool incoming = _reading.Observe(log.Revision);
        HudWidgets.Begin("##Events", rect);
        UiText.Colored(HudWidgets.Heading, "RECENT EVENTS");
        ImGui.SameLine();
        if (ImGui.SmallButton(state.HistoryExpanded ? "Collapse" : "History")) state.HistoryExpanded = !state.HistoryExpanded;
        if (!_reading.Following)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"Latest ({_reading.Unread})")) _reading.JumpToLatest();
        }
        if (state.Feedback != null)
        {
            ImGui.SameLine();
            UiText.Colored(HudWidgets.Warning, state.Feedback);
            if (ImGui.IsItemHovered()) UiText.Tooltip(state.Feedback);
        }
        ImGui.Separator();
        ImGui.BeginChild("history-lines", new Vector2(0, 0));
        long first = log.Revision - log.Entries.Count;
        if (!_reading.Following && first > _firstSequence)
        {
            float removed = _heights.Where(p => p.Key < first).Sum(p => p.Value);
            if (removed > 0) ImGui.SetScrollY(Math.Max(0, ImGui.GetScrollY() - removed));
        }
        foreach (var key in _heights.Keys.Where(k => k < first).ToArray()) _heights.Remove(key);
        _firstSequence = first;
        bool userScrolling = ImGui.IsWindowHovered() && (ImGui.GetIO().MouseWheel != 0 || ImGui.IsMouseDown(ImGuiMouseButton.Left));
        if (userScrolling) _reading.ReadOlder();
        for (int i = 0; i < log.Entries.Count; i++)
        {
            var entry = log.Entries[i];
            float start = ImGui.GetCursorPosY();
            ImGui.PushTextWrapPos(0);
            UiText.Colored(ColorFor(entry.Level), $"{entry.Timestamp}  {entry.Text}");
            ImGui.PopTextWrapPos();
            _heights[first + i] = ImGui.GetCursorPosY() - start;
        }
        if (log.Entries.Count == 0) UiText.Disabled("No recent events.");
        if (_reading.Following) ImGui.SetScrollHereY(1);
        else _reading.UpdateViewport(ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 2, userScrolling, incoming);
        ImGui.EndChild();
        ImGui.End();
    }

    private static Vector4 ColorFor(LogLevel level) => level switch
    {
        LogLevel.Success => new(.4f, .85f, .4f, 1),
        LogLevel.Warning => HudWidgets.Warning,
        LogLevel.Danger => new(1, .4f, .35f, 1),
        LogLevel.Discovery => HudWidgets.Heading,
        LogLevel.System => new(.6f, .6f, .6f, 1),
        _ => new(.85f, .85f, .85f, 1)
    };
}
