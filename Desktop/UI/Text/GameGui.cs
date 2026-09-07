using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using Native = ImGuiNET.ImGui;

namespace text_survival.Desktop.UI;

/// <summary>Shared screen primitives. Without a text frame, calls go straight to ImGui.
/// The CLI records the same screens and activates one exposed control per frame.</summary>
public static class GameGui
{
    [ThreadStatic] public static TextFrame? Capture;
    public static int ScreenWidth => Capture != null ? 1600 : Raylib.GetScreenWidth();
    public static int ScreenHeight => Capture != null ? 1000 : Raylib.GetScreenHeight();
    public static bool IsKeyPressed(KeyboardKey key) => Capture == null && Raylib.IsKeyPressed(key);
    public static Vector4 StyleColor(ImGuiCol color) => Capture != null ? Vector4.One : Native.GetStyle().Colors[(int)color];
    public static Vector2 FramePadding => Capture != null ? new(4, 4) : Native.GetStyle().FramePadding;
    public static GameDrawList GetWindowDrawList() => new(Capture == null ? Native.GetWindowDrawList() : default);
    public static ImFontPtr GetFont() => Capture == null ? Native.GetFont() : default;
    public static bool Begin(string name, ImGuiWindowFlags flags = 0) { if (Capture is { } c) { c.Push(name); return true; } return Native.Begin(name, flags); }
    public static bool Begin(string name, ref bool open, ImGuiWindowFlags flags = 0) { if (Capture != null) return Begin(name, flags); return Native.Begin(name, ref open, flags); }
    public static void End() { if (Capture is { } c) c.Pop(); else Native.End(); }
    public static bool BeginChild(string id, Vector2 size = default, ImGuiChildFlags childFlags = 0, ImGuiWindowFlags flags = 0) { if (Capture is { } c) { c.Push(id); return true; } return Native.BeginChild(id, size, childFlags, flags); }
    public static void EndChild() { if (Capture is { } c) c.Pop(); else Native.EndChild(); }
    public static void PushID(string id) { if (Capture is { } c) c.Push(id); else Native.PushID(id); }
    public static void PushID(int id) { if (Capture is { } c) c.Push(id.ToString()); else Native.PushID(id); }
    public static void PopID() { if (Capture is { } c) c.Pop(); else Native.PopID(); }
    public static void BeginDisabled(bool disabled = true) { if (Capture is { } c) c.Disable(disabled); else Native.BeginDisabled(disabled); }
    public static void EndDisabled() { if (Capture is { } c) c.EndDisable(); else Native.EndDisabled(); }
    public static void TextUnformatted(string text) { if (Capture is { } c) c.Text(text); else Native.TextUnformatted(text); }
    public static bool Button(string label, Vector2 size = default, bool? selected = null) => Capture is { } c ? c.Control(label, "button", selected) : Native.Button(label, size);
    public static bool SmallButton(string label) => Capture is { } c ? c.Control(label, "button") : Native.SmallButton(label);
    public static bool Selectable(string label, bool selected = false, ImGuiSelectableFlags flags = 0, Vector2 size = default) => Capture is { } c ? c.Control(label, "select", selected) : Native.Selectable(label, selected, flags, size);
    public static bool Checkbox(string label, ref bool value) { if (Capture is not { } c) return Native.Checkbox(label, ref value); if (!c.Control(label, "checkbox", value)) return false; value = !value; return true; }
    public static bool RadioButton(string label, ref int value, int buttonValue) { if (Capture is not { } c) return Native.RadioButton(label, ref value, buttonValue); if (!c.Control(label, "radio", value == buttonValue)) return false; value = buttonValue; return true; }
    public static bool InputTextWithHint(string label, string hint, ref string value, uint maxLength) { if (Capture is not { } c) return Native.InputTextWithHint(label, hint, ref value, maxLength); if (value.Length == 0) c.Text(hint); if (!c.Control(label, "text", value)) return false; if (c.Input == null || c.Input.Length >= maxLength) throw new ArgumentException($"Input must be shorter than {maxLength} characters."); value = c.Input; return true; }
    public static bool IsItemHovered(ImGuiHoveredFlags flags = 0) => Capture != null || Native.IsItemHovered(flags);
    public static bool IsItemClicked(ImGuiMouseButton button = 0) => Capture is { } c ? c.ClickText() : Native.IsItemClicked(button);
    public static bool CollapsingHeader(string label, ImGuiTreeNodeFlags flags = 0) { if (Capture is { } c) { c.Text(label); return true; } return Native.CollapsingHeader(label, flags); }
    public static bool TreeNode(string label) { if (Capture is { } c) { c.Text(label); c.Push(label); return true; } return Native.TreeNode(label); }
    public static void TreePop() { if (Capture is { } c) c.Pop(); else Native.TreePop(); }
    public static bool BeginCombo(string label, string preview, ImGuiComboFlags flags = 0) { if (Capture is { } c) { c.Text(preview); c.Text(label); c.Push(label); return true; } return Native.BeginCombo(label, preview, flags); }
    public static void EndCombo() { if (Capture is { } c) c.Pop(); else Native.EndCombo(); }
    public static bool BeginTabBar(string id, ImGuiTabBarFlags flags = 0) { if (Capture is { } c) { c.Push(id); return true; } return Native.BeginTabBar(id, flags); }
    public static void EndTabBar() { if (Capture is { } c) c.Pop(); else Native.EndTabBar(); }
    public static bool BeginTabItem(string label) { if (Capture is { } c) { c.Text(label); c.Push(label); return true; } return Native.BeginTabItem(label); }
    public static void EndTabItem() { if (Capture is { } c) c.Pop(); else Native.EndTabItem(); }
    public static bool BeginTable(string id, int columns, ImGuiTableFlags flags = 0) { if (Capture is { } c) { c.Push(id); return true; } return Native.BeginTable(id, columns, flags); }
    public static void EndTable() { if (Capture is { } c) c.Pop(); else Native.EndTable(); }
    public static void BeginTooltip() { if (Capture is { } c) c.TooltipDepth++; else Native.BeginTooltip(); }
    public static void EndTooltip() { if (Capture is { } c) c.TooltipDepth--; else Native.EndTooltip(); }
    public static void ProgressBar(float fraction, Vector2 size, string overlay) { if (Capture is { } c) c.Bar(fraction, overlay); else Native.ProgressBar(fraction, size, overlay); }
    public static Vector2 CalcTextSize(string text, bool hideTextAfterDoubleHash = false, float wrapWidth = -1) => Capture != null ? new(text.Length * 7, 16) : Native.CalcTextSize(text, hideTextAfterDoubleHash, wrapWidth);
    public static float GetFontSize() => Capture != null ? 16f : Native.GetFontSize();
    public static float GetFrameHeight() => Capture != null ? 24f : Native.GetFrameHeight();
    public static float GetTextLineHeight() => Capture != null ? 16f : Native.GetTextLineHeight();
    public static float GetTextLineHeightWithSpacing() => Capture != null ? 20f : Native.GetTextLineHeightWithSpacing();
    public static Vector2 GetContentRegionAvail() => Capture != null ? new Vector2(1200, 1000) : Native.GetContentRegionAvail();
    public static Vector2 GetCursorScreenPos() => Capture != null ? Vector2.Zero : Native.GetCursorScreenPos();
    public static Vector2 GetItemRectMin() => Capture != null ? Vector2.Zero : Native.GetItemRectMin();
    public static Vector2 GetItemRectSize() => Capture != null ? new Vector2(1200, 24) : Native.GetItemRectSize();
    public static uint GetColorU32(ImGuiCol color) => Capture != null ? 0 : Native.GetColorU32(color);
    public static uint GetColorU32(Vector4 color) => Capture != null ? 0 : Native.GetColorU32(color);
    public static uint ColorConvertFloat4ToU32(Vector4 color) => Capture != null ? 0 : Native.ColorConvertFloat4ToU32(color);
    public static void Separator() { if (Capture == null) Native.Separator(); }
    public static void Spacing() { if (Capture == null) Native.Spacing(); }
    public static void NewLine() { if (Capture == null) Native.NewLine(); }
    public static void Bullet() { if (Capture == null) Native.Bullet(); }
    public static void SameLine(float offset = 0, float spacing = -1) { if (Capture is { } c) c.Join(); else Native.SameLine(offset, spacing); }
    public static void Dummy(Vector2 size) { if (Capture == null) Native.Dummy(size); }
    public static void Indent(float width = 0) { if (Capture == null) Native.Indent(width); }
    public static void Unindent(float width = 0) { if (Capture == null) Native.Unindent(width); }
    public static void PushTextWrapPos(float pos = 0) { if (Capture == null) Native.PushTextWrapPos(pos); }
    public static void PopTextWrapPos() { if (Capture == null) Native.PopTextWrapPos(); }
    public static void PushStyleColor(ImGuiCol color, Vector4 value) { if (Capture == null) Native.PushStyleColor(color, value); }
    public static void PopStyleColor(int count = 1) { if (Capture == null) Native.PopStyleColor(count); }
    public static void PushStyleVar(ImGuiStyleVar style, Vector2 value) { if (Capture == null) Native.PushStyleVar(style, value); }
    public static void PushStyleVar(ImGuiStyleVar style, float value) { if (Capture == null) Native.PushStyleVar(style, value); }
    public static void PopStyleVar(int count = 1) { if (Capture == null) Native.PopStyleVar(count); }
    public static void SetNextWindowPos(Vector2 pos, ImGuiCond cond = 0, Vector2 pivot = default) { if (Capture == null) Native.SetNextWindowPos(pos, cond, pivot); }
    public static void SetNextWindowSize(Vector2 size, ImGuiCond cond = 0) { if (Capture == null) Native.SetNextWindowSize(size, cond); }
    public static void SetNextItemWidth(float width) { if (Capture == null) Native.SetNextItemWidth(width); }
    public static void TableSetupColumn(string label, ImGuiTableColumnFlags flags = 0, float width = 0) { if (Capture == null) Native.TableSetupColumn(label, flags, width); }
    public static bool TableNextColumn() => Capture != null || Native.TableNextColumn();
}

public readonly struct GameDrawList(ImDrawListPtr native)
{
    public void AddText(Vector2 pos, uint color, string text) { if (GameGui.Capture is { } c) c.DrawText(text); else native.AddText(pos, color, text); }
    public void AddText(ImFontPtr font, float size, Vector2 pos, uint color, string text, float wrap = 0) { if (GameGui.Capture is { } c) c.DrawText(text); else native.AddText(font, size, pos, color, text, wrap); }
    public void AddRectFilled(Vector2 min, Vector2 max, uint color) { if (GameGui.Capture == null) native.AddRectFilled(min, max, color); }
    public void PushClipRect(Vector2 min, Vector2 max, bool intersect) { if (GameGui.Capture == null) native.PushClipRect(min, max, intersect); }
    public void PopClipRect() { if (GameGui.Capture == null) native.PopClipRect(); }
    public void AddImage(IntPtr texture, Vector2 min, Vector2 max, Vector2 uvMin, Vector2 uvMax) { if (GameGui.Capture == null) native.AddImage(texture, min, max, uvMin, uvMax); }
}
