using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>Retained history with deliberate following; never steals the reader's scroll position.</summary>
public sealed class EventLogPanel
{
    private readonly LogFollowState _reading = new();
    public bool Following => _reading.Following;
    public long Unread => _reading.Unread;
    private long _firstSequence;
    private readonly Dictionary<long, float> _heights = [];
    private NarrativeLog? _log;

    public void Render(GameContext ctx, HudRect rect, HudState state)
    {
        var log = ctx.Log;
        if (_log != log) { _log = log; _reading.Reset(log.Revision); _heights.Clear(); }
        bool incoming = _reading.Observe(log.Revision);
        HudWidgets.Begin("##Events", rect);
        bool focused = Input.UiInput.HudFocused && Input.UiInput.Context is Input.InputContext.Exploration or Input.InputContext.Combat && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (UiIcons.Button("journal", state.HistoryExpanded ? "Collapse [F2]" : "History [F2]", "history-toggle") || focused && UiNavigation.Key(ImGuiKey.F2)) state.HistoryExpanded = !state.HistoryExpanded;
        ImGui.SameLine();
        ImGui.BeginDisabled(_reading.Following);
        if (UiIcons.Button("continue", $"Latest ({_reading.Unread}) [F4]", "history-latest") || focused && UiNavigation.Key(ImGuiKey.F4)) _reading.JumpToLatest();
        ImGui.EndDisabled();
        // A permanent single-line feedback region cannot displace the history controls.
        UiText.Colored(state.Feedback == null ? UiStyle.Supporting : HudWidgets.Warning, state.Feedback ?? "F3: read · PgUp/PgDn: scroll");
        if (state.Feedback != null && ImGui.IsItemHovered()) UiText.Tooltip(state.Feedback);
        ImGui.Separator();
        ImGui.BeginChild("history-lines", new Vector2(0, 0));
        bool keyboardReading = focused && UiNavigation.Key(ImGuiKey.F3);
        keyboardReading |= UiNavigation.ScrollRegion(Raylib_cs.KeyboardKey.F3, focused);
        long first = log.Revision - log.Entries.Count;
        if (!_reading.Following && first > _firstSequence)
        {
            float removed = _heights.Where(p => p.Key < first).Sum(p => p.Value);
            if (removed > 0) ImGui.SetScrollY(Math.Max(0, ImGui.GetScrollY() - removed));
        }
        foreach (var key in _heights.Keys.Where(k => k < first).ToArray()) _heights.Remove(key);
        _firstSequence = first;
        bool userScrolling = keyboardReading || ImGui.IsWindowHovered() && (ImGui.GetIO().MouseWheel != 0 || ImGui.IsMouseDown(ImGuiMouseButton.Left));
        if (userScrolling) _reading.ReadOlder();
        for (int i = 0; i < log.Entries.Count; i++)
        {
            var entry = log.Entries[i];
            float start = ImGui.GetCursorPosY();
            ImGui.PushTextWrapPos(0);
            UiIcons.LabelColored(IconFor(entry.Level), ColorFor(entry.Level), $"{entry.Timestamp}  {entry.Text}");
            ImGui.PopTextWrapPos();
            _heights[first + i] = ImGui.GetCursorPosY() - start;
        }
        if (log.Entries.Count == 0) UiText.Disabled("No recent events.");
        if (_reading.Following) ImGui.SetScrollHereY(1);
        else _reading.UpdateViewport(ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 2, userScrolling, incoming);
        ImGui.EndChild();
        ImGui.End();
    }

    public static string IconFor(LogLevel level) => level switch
    { LogLevel.Success => "confirm", LogLevel.Warning => "warning", LogLevel.Danger => "blood", LogLevel.Discovery => "inspect", LogLevel.System => "help", _ => "journal" };

    private static Vector4 ColorFor(LogLevel level) => level switch
    {
        LogLevel.Success => new(.4f, .85f, .4f, 1),
        LogLevel.Warning => HudWidgets.Warning,
        LogLevel.Danger => UiStyle.Danger,
        LogLevel.Discovery => HudWidgets.Heading,
        LogLevel.System => UiStyle.Supporting,
        _ => new(.85f, .85f, .85f, 1)
    };
}
