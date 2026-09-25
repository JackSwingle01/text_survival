using ImGuiNET;
using Raylib_cs;
using text_survival.Desktop.Input;

namespace text_survival.Desktop.UI;

/// <summary>Focus requests only run for the active, released input owner.</summary>
public static class UiNavigation
{
    public static void Region(KeyboardKey key)
    {
        if (!Enum.TryParse<ImGuiKey>(key.ToString(), out var nativeKey) || !Key(nativeKey, whileEditing: true)) return;
        ImGui.SetWindowFocus();
        ImGui.SetKeyboardFocusHere();
    }

    public static void Initial(ref bool pending)
    {
        if (!pending || !UiInput.Ready || GameGui.Capture != null) return;
        ImGui.SetWindowFocus();
        ImGui.SetKeyboardFocusHere();
        pending = false;
    }

    /// <summary>Read-only regions remain scrollable without mouse hover or a selectable row.</summary>
    public static bool ScrollRegion(KeyboardKey key, bool enabled = true)
    {
        if (!enabled || GameGui.Capture != null) return false;
        if (Enum.TryParse<ImGuiKey>(key.ToString(), out var nativeKey) && Key(nativeKey)) ImGui.SetWindowFocus();
        if (!ImGui.IsWindowFocused()) return false;
        float delta = Key(ImGuiKey.PageDown) ? ImGui.GetWindowHeight() * .8f : Key(ImGuiKey.PageUp) ? -ImGui.GetWindowHeight() * .8f :
            Key(ImGuiKey.DownArrow) ? ImGui.GetTextLineHeightWithSpacing() : Key(ImGuiKey.UpArrow) ? -ImGui.GetTextLineHeightWithSpacing() : 0;
        if (Key(ImGuiKey.Home)) { ImGui.SetScrollY(0); return true; }
        if (Key(ImGuiKey.End)) { ImGui.SetScrollY(ImGui.GetScrollMaxY()); return true; }
        if (delta == 0) return false;
        ImGui.SetScrollY(Math.Clamp(ImGui.GetScrollY() + delta, 0, ImGui.GetScrollMaxY())); return true;
    }

    public static string Numbered(string label, int index) => index < 9 ? $"{index + 1}. {label}" : label;
    public static bool Number(int index) => index is >= 0 and < 9 && Key((ImGuiKey)((int)ImGuiKey._1 + index));

    public static bool Key(ImGuiKey key, bool whileEditing = false) => GameGui.Capture == null && UiInput.Ready &&
        (whileEditing || !ImGui.GetIO().WantTextInput) && !GameGui.AnyPopupOpen &&
        !ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeySuper && !ImGui.GetIO().KeyAlt && ImGui.IsKeyPressed(key, false);
}
