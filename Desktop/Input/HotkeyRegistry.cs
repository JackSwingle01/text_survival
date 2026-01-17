using Raylib_cs;

namespace text_survival.Desktop.Input;

/// <summary>
/// Actions that can be triggered by hotkeys.
/// Movement is handled separately (WASD/arrows).
/// </summary>
public enum HotkeyAction
{
    Inventory,
    Crafting,
    Fire,
    DiscoveryLog,
    NPCs,
    Wait,
    Cancel,
    Storage,
}

/// <summary>
/// Central registry for all hotkey bindings.
/// Single source of truth for key assignments.
/// </summary>
public static class HotkeyRegistry
{
    private static readonly Dictionary<HotkeyAction, KeyboardKey> _bindings = new()
    {
        { HotkeyAction.Inventory, KeyboardKey.I },
        { HotkeyAction.Crafting, KeyboardKey.C },
        { HotkeyAction.Fire, KeyboardKey.F },
        { HotkeyAction.DiscoveryLog, KeyboardKey.L },
        { HotkeyAction.NPCs, KeyboardKey.N },
        { HotkeyAction.Wait, KeyboardKey.Space },
        { HotkeyAction.Cancel, KeyboardKey.Escape },
        { HotkeyAction.Storage, KeyboardKey.T },
    };

    /// <summary>
    /// Get the key bound to an action.
    /// </summary>
    public static KeyboardKey GetKey(HotkeyAction action) => _bindings[action];

    /// <summary>
    /// Check if an action's key was pressed this frame.
    /// </summary>
    public static bool IsPressed(HotkeyAction action) =>
        Raylib.IsKeyPressed(_bindings[action]);

    /// <summary>
    /// Get display tip like "[C]" for button labels.
    /// </summary>
    public static string GetTip(HotkeyAction action)
    {
        var key = _bindings[action];
        var display = key switch
        {
            KeyboardKey.Space => "Space",
            KeyboardKey.Escape => "Esc",
            _ => key.ToString()
        };
        return $"[{display}]";
    }
}
