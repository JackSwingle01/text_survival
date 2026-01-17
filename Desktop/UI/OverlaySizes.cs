using ImGuiNET;
using System.Numerics;

namespace text_survival.Desktop.UI;

/// <summary>
/// Standard overlay sizes for consistent UI scaling.
/// </summary>
public static class OverlaySizes
{
    // Standard: 40% width × 55% height
    private const float StandardWidthPct = 0.40f;
    private const float StandardHeightPct = 0.55f;

    // Wide: 55% width × 55% height (for two-column layouts)
    private const float WideWidthPct = 0.55f;
    private const float WideHeightPct = 0.55f;

    // Compact bar sizes for StatsPanel effects and capacities
    // Uses -1 width to auto-fill remaining space (matches main stat bar alignment)
    public const float EffectBarStart = 110;
    public const float CompactBarHeight = 16;

    /// <summary>
    /// Set up a centered overlay with standard dimensions (40% × 55%).
    /// Call before ImGui.Begin().
    /// </summary>
    public static void SetupStandard()
    {
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
            ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(io.DisplaySize.X * StandardWidthPct, io.DisplaySize.Y * StandardHeightPct),
            ImGuiCond.FirstUseEver);
    }

    /// <summary>
    /// Set up a centered overlay with wide dimensions (55% × 55%).
    /// Use for two-column layouts like Crafting and Transfer.
    /// Call before ImGui.Begin().
    /// </summary>
    public static void SetupWide()
    {
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
            ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(io.DisplaySize.X * WideWidthPct, io.DisplaySize.Y * WideHeightPct),
            ImGuiCond.FirstUseEver);
    }
}
