using Raylib_cs;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Interface for rendering icons on the map grid.
/// Allows swapping between procedural shapes and texture-based rendering.
/// </summary>
public interface IIconRenderer
{
    /// <summary>
    /// Draw an icon at the specified position.
    /// </summary>
    /// <param name="iconName">Semantic name of the icon (e.g., "fire", "water", "shelter")</param>
    /// <param name="x">X position (top-left of icon area)</param>
    /// <param name="y">Y position (top-left of icon area)</param>
    /// <param name="size">Size of the icon area</param>
    /// <param name="tint">Color tint to apply</param>
    /// <param name="hasGlow">Whether to draw a glow effect around the icon</param>
    void DrawIcon(string iconName, float x, float y, float size, Color tint, bool hasGlow = false);

    /// <summary>
    /// Get the default color for an icon type.
    /// </summary>
    /// <param name="iconName">Semantic name of the icon</param>
    /// <returns>The color associated with this icon type</returns>
    Color GetIconColor(string iconName);
}
