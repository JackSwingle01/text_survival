using ImGuiNET;
using Raylib_cs;
using System.Numerics;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for major discoveries during foraging.
/// Shows a modal popup with discovery text that the player must acknowledge.
/// </summary>
public class MajorDiscoveryOverlay
{
    public bool IsOpen { get; private set; }

    private string _message = "";
    private bool _shouldClose;

    /// <summary>
    /// Open the overlay with a discovery message.
    /// </summary>
    public void Open(string message)
    {
        IsOpen = true;
        _message = message;
        _shouldClose = false;
    }

    /// <summary>
    /// Render the major discovery overlay.
    /// Returns true when player dismisses the popup.
    /// </summary>
    public bool Render(float deltaTime)
    {
        if (!IsOpen) return false;

        // Set up centered window
        OverlaySizes.SetupDialog();

        ImGui.Begin("Discovery!", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

        ImGui.Spacing();

        // Discovery message with warm/golden color
        ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.5f, 1f), _message);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Continue button
        if (ImGui.Button("Continue [Enter]", new Vector2(-1, 30)) || Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            _shouldClose = true;
            IsOpen = false;
        }

        ImGui.End();

        return _shouldClose;
    }
}
