using System.Text.Json.Serialization;

namespace text_survival.Environments.Features;

/// <summary>
/// How a discovery should be presented to the player.
/// Minor = inline in forage results, Major = triggers a mini-event.
/// </summary>
public enum DiscoveryCategory
{
    Minor,
    Major
}

public class HiddenFeature
{
    public LocationFeature Feature { get; set; } = null!;
    public double RevealAtHours { get; set; }
    public DiscoveryCategory Category { get; set; }

    /// <summary>
    /// Search effort spent on *this* find, weighted by perception and by how searchable the
    /// ground was at the time. Per-find rather than per-tile, because a foot of snow all but
    /// ends the search for a bone and barely troubles the search for a berry bush.
    ///
    /// Snow makes new searching slower; it never revalues hours already put in, and a find
    /// created today inherits none of them.
    /// </summary>
    public double EffectiveSearchHours { get; set; }

    [JsonConstructor]
    public HiddenFeature() { }

    public HiddenFeature(LocationFeature feature, double revealAtHours, DiscoveryCategory category)
    {
        Feature = feature;
        RevealAtHours = revealAtHours;
        Category = category;
    }
}
