using text_survival.Actions.Expeditions.WorkStrategies;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// A work option that can be displayed in a menu and executed.
/// Features create these to describe their available work.
/// </summary>
public record WorkOption(string Label, string Id, IWorkStrategy Strategy);
