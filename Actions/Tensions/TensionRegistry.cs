using text_survival.Environments;

namespace text_survival.Actions.Tensions;

/// <summary>
/// Manages active tensions in the game. Follows the EffectRegistry pattern.
/// Tensions are building threats or opportunities that decay over time
/// and escalate through event outcomes.
/// </summary>
public class TensionRegistry
{
    private readonly List<ActiveTension> _tensions = [];

    /// <summary>
    /// Get all active tensions (read-only).
    /// </summary>
    public IReadOnlyList<ActiveTension> GetAllTensions() => _tensions.AsReadOnly();

    /// <summary>
    /// Check if a tension of the given type exists.
    /// </summary>
    public bool HasTension(string type)
    {
        return _tensions.Any(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if a tension exists at a specific location.
    /// </summary>
    public bool HasTension(string type, Location location)
    {
        return _tensions.Any(t =>
            t.Type.Equals(type, StringComparison.OrdinalIgnoreCase) &&
            (t.RelevantLocation == null || t.RelevantLocation == location));
    }

    /// <summary>
    /// Check if a tension exists with severity above a threshold.
    /// </summary>
    public bool HasTensionAbove(string type, double severityThreshold)
    {
        var tension = GetTension(type);
        return tension != null && tension.Severity > severityThreshold;
    }

    /// <summary>
    /// Get a tension by type. Returns null if not found.
    /// </summary>
    public ActiveTension? GetTension(string type)
    {
        return _tensions.FirstOrDefault(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Add a new tension. If a tension of the same type already exists,
    /// it takes the higher severity.
    /// Returns a stage change if the tension was created or escalated to a new stage.
    /// </summary>
    public TensionStageChange? AddTension(ActiveTension tension)
    {
        var existing = GetTension(tension.Type);
        if (existing != null)
        {
            // Take the higher severity
            if (tension.Severity > existing.Severity)
            {
                return existing.UpdateSeverity(tension.Severity);
            }
            return null;
        }
        else
        {
            _tensions.Add(tension);
            // Return creation change (null previous stage indicates new tension)
            return new TensionStageChange(tension.Type, null, tension.Stage, tension);
        }
    }

    /// <summary>
    /// Resolve (remove) a tension by type.
    /// </summary>
    public bool ResolveTension(string type)
    {
        var tension = GetTension(type);
        if (tension != null)
        {
            _tensions.Remove(tension);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Escalate a tension's severity by a given amount.
    /// Creates the tension if it doesn't exist (using default factory).
    /// Returns a stage change if a threshold was crossed.
    /// </summary>
    public TensionStageChange? EscalateTension(string type, double amount)
    {
        var tension = GetTension(type);
        if (tension != null)
        {
            return tension.UpdateSeverity(tension.Severity + amount);
        }
        else if (amount > 0)
        {
            // Create new tension with the escalation amount as initial severity
            // Use Custom factory with minimal decay (0.01/hr) and camp decay enabled
            return AddTension(ActiveTension.Custom(type, amount, decayPerHour: 0.01, decaysAtCamp: true));
        }
        return null;
    }

    /// <summary>
    /// Update all tensions based on elapsed time.
    /// Passive decay only - no passive escalation.
    /// Yields stage changes for any tensions that crossed thresholds.
    /// </summary>
    /// <param name="minutes">Elapsed game time in minutes</param>
    /// <param name="atCamp">Whether the player is currently at camp</param>
    public IEnumerable<TensionStageChange> Update(int minutes, bool atCamp)
    {
        var toRemove = new List<ActiveTension>();
        var changes = new List<TensionStageChange>();

        foreach (var tension in _tensions)
        {
            // Only decay if: tension decays at camp, OR player is not at camp
            if (tension.DecaysAtCamp || !atCamp)
            {
                double decayRate = tension.DecayPerHour;

                // FeverRising decays 3x faster at camp (rest accelerates recovery)
                if (tension.Type == "FeverRising" && atCamp)
                {
                    decayRate *= 3.0;
                }

                double decay = decayRate * (minutes / 60.0);
                double newSeverity = Math.Max(0, tension.Severity - decay);

                var change = tension.UpdateSeverity(newSeverity);
                if (change != null)
                {
                    changes.Add(change);
                }

                if (tension.Severity <= 0)
                {
                    toRemove.Add(tension);
                }
            }
        }

        foreach (var tension in toRemove)
        {
            _tensions.Remove(tension);
        }

        return changes;
    }

}
