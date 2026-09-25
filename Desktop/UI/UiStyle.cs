using ImGuiNET;
using System.Numerics;

namespace text_survival.Desktop.UI;

public enum UiTone { Normal, Caution, Danger, Success }

public static class UiStyle
{
    public static UiSettings Settings { get; private set; } = new();
    public static string? SaveError { get; private set; }
    public static float Scale => Settings.Factor;
    public static float IconSize => Scale > 1 ? 32 : 16;
    public static float BarHeight => MathF.Ceiling(20 * Scale);
    public static Vector4 Text => new(.92f, .93f, .92f, 1);
    public static Vector4 Supporting => new(.72f, .76f, .78f, 1);
    public static Vector4 Focus => new(.95f, .85f, .45f, 1);
    public static Vector4 Selection => new(.22f, .39f, .48f, 1);
    public static Vector4 Caution => new(1, .77f, .37f, 1);
    public static Vector4 Danger => new(1, .46f, .42f, 1);
    public static Vector4 Success => new(.56f, .85f, .60f, 1);
    public static Vector4 BarFill(Vector4 color) => new(color.X * .28f, color.Y * .28f, color.Z * .28f, 1);
    public static Vector4 Color(UiTone tone) => tone switch {
        UiTone.Caution => Caution, UiTone.Danger => Danger, UiTone.Success => Success, _ => Text };

    public static void Initialize(UiSettings? settings = null) => Apply(settings ?? UiSettings.Load(UiSettings.DefaultPath));
    public static void Apply(UiSettings settings)
    {
        Settings = settings;
        var io = ImGui.GetIO();
        io.ConfigNavCursorVisibleAlways = true;
        io.FontGlobalScale = 1.25f * Scale;
        var style = ImGui.GetStyle();
        // Assign from baseline every time: switching presets must not compound scaling.
        style.WindowPadding = new Vector2(10, 10) * Scale;
        style.FramePadding = new Vector2(7, 5) * Scale;
        style.ItemSpacing = new Vector2(8, 6) * Scale;
        style.ItemInnerSpacing = new Vector2(6, 4) * Scale;
        style.CellPadding = new Vector2(4, 3) * Scale;
        style.ScrollbarSize = 16 * Scale;
        style.FrameRounding = 3 * Scale;
        style.Colors[(int)ImGuiCol.Text] = Text;
        style.Colors[(int)ImGuiCol.TextDisabled] = Supporting;
        style.Colors[(int)ImGuiCol.WindowBg] = new(.075f, .095f, .11f, 1);
        style.Colors[(int)ImGuiCol.Button] = new(.13f, .20f, .25f, 1);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new(.20f, .31f, .37f, 1);
        style.Colors[(int)ImGuiCol.ButtonActive] = Selection;
        style.Colors[(int)ImGuiCol.Header] = Selection;
        style.Colors[(int)ImGuiCol.NavCursor] = Focus;
    }
    public static void ChooseScale(UiScale scale)
    {
        // Applying is deferred until the next frame, outside active ImGui windows.
        Pending = new UiSettings(scale);
    }
    private static UiSettings? Pending;
    public static void ApplyPending()
    {
        if (Pending is not { } next) return;
        Pending = null;
        Apply(next);
        try { next.Save(UiSettings.DefaultPath); SaveError = null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { SaveError = "UI size changed, but could not save the preference."; }
    }
}
