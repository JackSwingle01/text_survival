using ImGuiNET;
using Raylib_cs;
using System.Numerics;

namespace text_survival.Desktop.UI;

/// <summary>
/// Standard overlay sizes for consistent UI scaling.
/// </summary>
public static class OverlaySizes
{
    // Standard: 55% width × 70% height
    private const float StandardWidthPct = 0.55f;
    private const float StandardHeightPct = 0.70f;

    // Wide: 70% width × 70% height (for two-column layouts)
    private const float WideWidthPct = 0.70f;
    private const float WideHeightPct = 0.70f;

    // Compact bar sizes for StatsPanel effects and capacities
    // Uses -1 width to auto-fill remaining space (matches main stat bar alignment)
    public const float EffectBarStart = 110;
    public const float CompactBarHeight = 16;

    /// <summary>
    /// Set up a centered overlay with standard dimensions (55% × 70%).
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
    /// Set up a centered overlay with wide dimensions (70% × 70%).
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
}
