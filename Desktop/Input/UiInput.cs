using ImGuiNET;
using Raylib_cs;

namespace text_survival.Desktop.Input;

public enum InputContext { Exploration, Combat, Dialog, Popup, TextEntry }

/// <summary>New input owners must see keys released before accepting commands.</summary>
public sealed class InputGate
{
    private object? _owner;
    private bool _armed;
    public bool Enter(object owner, bool anyKeyDown)
    {
        if (!ReferenceEquals(owner, _owner)) { _owner = owner; _armed = false; return false; }
        if (!_armed && !anyKeyDown) _armed = true;
        return _armed;
    }
}

public static class UiInput
{
    public static bool Ready { get; set; } = true;
    public static InputContext Context { get; set; } = InputContext.Exploration;
    public static bool HudFocused { get; set; }
    private static readonly KeyboardKey[] Keys = Enum.GetValues<KeyboardKey>().Where(k => (int)k > 0).Distinct().ToArray();
    public static bool AnyKeyDown() => Keys.Any(k => Raylib.IsKeyDown(k));
    public static bool Pressed(KeyboardKey key) => Ready && !ImGui.GetIO().WantTextInput &&
        !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId) &&
        !ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeySuper && !ImGui.GetIO().KeyAlt && Raylib.IsKeyPressed(key);
    public static bool AllowsGameCommands(InputContext context, bool ready, bool uiFocused, bool textInput, bool popup, bool modifier = false)
        => ready && context == InputContext.Exploration && !uiFocused && !textInput && !popup && !modifier;
    public static bool GameCommandsAllowed => AllowsGameCommands(Context, Ready, HudFocused,
        ImGui.GetIO().WantTextInput, ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId),
        ImGui.GetIO().KeyCtrl || ImGui.GetIO().KeySuper || ImGui.GetIO().KeyAlt);
    public static bool SearchRequested => Ready && Context != InputContext.Popup &&
        (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl) ||
         Raylib.IsKeyDown(KeyboardKey.LeftSuper) || Raylib.IsKeyDown(KeyboardKey.RightSuper)) && Raylib.IsKeyPressed(KeyboardKey.F);
}
