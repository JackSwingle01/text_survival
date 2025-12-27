namespace text_survival.Environments.Grid;

/// <summary>
/// Visibility state for fog of war rendering.
/// </summary>
public enum TileVisibility
{
    /// <summary>
    /// Never seen - not shown on map.
    /// </summary>
    Unexplored,

    /// <summary>
    /// Previously visited but not currently visible.
    /// Shows terrain but not dynamic features.
    /// </summary>
    Explored,

    /// <summary>
    /// Currently in view - shows all details.
    /// </summary>
    Visible
}
