using text_survival.Actions;
using text_survival.Actions.Expeditions;

namespace text_survival.Environments.Features;

/// <summary>
/// Implemented by features that provide work options at a location.
/// </summary>
public interface IWorkableFeature
{
    /// <summary>
    /// Returns work options available from this feature given current game state.
    /// May return empty if the feature has no current work (depleted, missing tools, etc.)
    /// </summary>
    IEnumerable<WorkOption> GetWorkOptions(GameContext ctx);
}
