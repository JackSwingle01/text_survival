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

    public IReadOnlyList<ActiveTension> All => _tensions;

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
    /// Get all tensions of a given type (for location-specific tensions).
    /// </summary>
    public IEnumerable<ActiveTension> GetTensions(string type)
    {
        return _tensions.Where(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Add a new tension. If a tension of the same type already exists,
    /// it takes the higher severity.
    /// </summary>
    public void AddTension(ActiveTension tension)
    {
        var existing = GetTension(tension.Type);
        if (existing != null)
        {
            // Take the higher severity
            if (tension.Severity > existing.Severity)
            {
                existing.Severity = tension.Severity;
            }
        }
        else
        {
            _tensions.Add(tension);
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
    /// </summary>
    public void EscalateTension(string type, double amount)
    {
        var tension = GetTension(type);
        if (tension != null)
        {
            tension.Severity = Math.Clamp(tension.Severity + amount, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Reduce a tension's severity by a given amount.
    /// Removes the tension if severity drops to 0.
    /// </summary>
    public void ReduceTension(string type, double amount)
    {
        var tension = GetTension(type);
        if (tension != null)
        {
            tension.Severity = Math.Max(0, tension.Severity - amount);
            if (tension.Severity <= 0)
            {
                _tensions.Remove(tension);
            }
        }
    }

    /// <summary>
    /// Update all tensions based on elapsed time.
    /// Passive decay only - no passive escalation.
    /// </summary>
    /// <param name="minutes">Elapsed game time in minutes</param>
    /// <param name="atCamp">Whether the player is currently at camp</param>
    public void Update(int minutes, bool atCamp)
    {
        var toRemove = new List<ActiveTension>();

        foreach (var tension in _tensions)
        {
            // Only decay if: tension decays at camp, OR player is not at camp
            if (tension.DecaysAtCamp || !atCamp)
            {
                double decay = tension.DecayPerHour * (minutes / 60.0);
                tension.Severity = Math.Max(0, tension.Severity - decay);

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
    }

    /// <summary>
    /// Get tension details for display or event text substitution.
    /// Returns the animal type, direction, or description if available.
    /// </summary>
    public string? GetTensionDetails(string type)
    {
        var tension = GetTension(type);
        if (tension == null) return null;

        return tension.AnimalType ?? tension.Direction ?? tension.Description;
    }

    /// <summary>
    /// Clear all tensions.
    /// </summary>
    public void Clear()
    {
        _tensions.Clear();
    }
}
