using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;

namespace text_survival.Environments.Features;

/// <summary>
/// A multi-session construction project that tracks progress and stores invested materials.
/// Upon completion, removes itself and adds the resulting feature to the location.
/// </summary>
public class CraftingProjectFeature : LocationFeature, IWorkableFeature
{
    /// <summary>
    /// Display name of the project (e.g., "Cabin", "Sleeping Bag").
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// The feature that will be added to the location upon completion.
    /// </summary>
    public LocationFeature? ResultFeature { get; set; }

    /// <summary>
    /// Total time required to complete the project (in minutes).
    /// </summary>
    public double TimeRequiredMinutes { get; set; }

    /// <summary>
    /// Time invested so far (in minutes).
    /// </summary>
    public double TimeInvestedMinutes { get; set; } = 0;

    /// <summary>
    /// Materials consumed upfront and stored for potential reclaim if abandoned.
    /// </summary>
    public Dictionary<Resource, double> MaterialsInvested { get; set; } = new();

    /// <summary>
    /// Progress as a percentage (0-1).
    /// </summary>
    public double ProgressPct => TimeRequiredMinutes > 0 ? TimeInvestedMinutes / TimeRequiredMinutes : 0;

    /// <summary>
    /// Whether the project is complete.
    /// </summary>
    public bool IsComplete => TimeInvestedMinutes >= TimeRequiredMinutes;

    public CraftingProjectFeature() : base("Project") { }

    public CraftingProjectFeature(string projectName, LocationFeature resultFeature, double timeRequiredMinutes)
        : base($"{projectName} (In Progress)")
    {
        ProjectName = projectName;
        ResultFeature = resultFeature;
        TimeRequiredMinutes = timeRequiredMinutes;
    }

    /// <summary>
    /// Provides work option to continue working on this project.
    /// </summary>
    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (IsComplete)
            yield break;

        yield return new WorkOption(
            $"Work on {ProjectName} ({ProgressPct:P0})",
            $"project_{ProjectName.ToLower().Replace(" ", "_")}",
            new CraftingProjectStrategy()
        );
    }

    /// <summary>
    /// Add progress to the project. Called after work session completes.
    /// If project completes, removes itself and adds the result feature.
    /// </summary>
    public void AddProgress(double minutes, Location location)
    {
        TimeInvestedMinutes += minutes;

        if (IsComplete && ResultFeature != null)
        {
            location.RemoveFeature(this);
            location.AddFeature(ResultFeature);
        }
    }

    /// <summary>
    /// Abandon the project and reclaim 80% of invested materials (20% penalty).
    /// </summary>
    public Dictionary<Resource, double> Abandon()
    {
        var reclaimed = new Dictionary<Resource, double>();
        foreach (var (material, amount) in MaterialsInvested)
        {
            reclaimed[material] = amount * 0.8;  // 20% penalty for abandonment
        }
        return reclaimed;
    }
}
