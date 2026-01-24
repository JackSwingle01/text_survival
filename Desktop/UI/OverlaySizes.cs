using ImGuiNET;
using Raylib_cs;
using System.Numerics;

namespace text_survival.Desktop.UI;

/// <summary>
/// Standard overlay sizes for consistent UI scaling.
/// </summary>
public static class OverlaySizes
{
    // Standard: 65% width × 75% height
    private const float StandardWidthPct = 0.65f;
    private const float StandardHeightPct = 0.75f;

    // Wide: 80% width × 75% height (for two-column layouts)
    private const float WideWidthPct = 0.80f;
    private const float WideHeightPct = 0.75f;

    // Dialog: 35% width for dialogs/popups (auto height)
    private const float DialogWidthPct = 0.35f;

    // Small Dialog: 25% width for compact dialogs (auto height)
    private const float SmallDialogWidthPct = 0.25f;

    // Compact bar sizes for StatsPanel effects and capacities
    // Uses -1 width to auto-fill remaining space (matches main stat bar alignment)
    public const float EffectBarStart = 110;
    public const float CompactBarHeight = 16;

    /// <summary>
    /// Set up a centered overlay with standard dimensions (65% × 75%).
    /// Call before ImGui.Begin().
    /// </summary>
    public static void SetupStandard()
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        ImGui.SetNextWindowPos(new Vector2(screenWidth * 0.5f, screenHeight * 0.5f),
            ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(screenWidth * StandardWidthPct, screenHeight * StandardHeightPct),
            ImGuiCond.Once);
    }

    /// <summary>
    /// Set up a centered overlay with wide dimensions (80% × 75%).
    /// Use for two-column layouts like Crafting and Transfer.
    /// Call before ImGui.Begin().
    /// </summary>
    public static void SetupWide()
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        ImGui.SetNextWindowPos(new Vector2(screenWidth * 0.5f, screenHeight * 0.5f),
            ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(screenWidth * WideWidthPct, screenHeight * WideHeightPct),
            ImGuiCond.Once);
    }

    /// <summary>
    /// Set up a centered dialog with standard dialog dimensions (35% width, auto height).
    /// Use for most dialogs, events, confirmations.
    /// Call before ImGui.Begin().
    /// </summary>
    public static void SetupDialog()
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        ImGui.SetNextWindowPos(new Vector2(screenWidth * 0.5f, screenHeight * 0.5f),
            ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(screenWidth * DialogWidthPct, 0), ImGuiCond.Always);
    }

    /// <summary>
    /// Set up a centered small dialog (25% width, auto height).
    /// Use for compact dialogs and short messages.
    /// Call before ImGui.Begin().
    /// </summary>
    public static void SetupSmallDialog()
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        ImGui.SetNextWindowPos(new Vector2(screenWidth * 0.5f, screenHeight * 0.5f),
            ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(screenWidth * SmallDialogWidthPct, 0), ImGuiCond.Always);
    }
}
