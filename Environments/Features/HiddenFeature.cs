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

    [JsonConstructor]
    public HiddenFeature() { }

    public HiddenFeature(LocationFeature feature, double revealAtHours, DiscoveryCategory category)
    {
        Feature = feature;
        RevealAtHours = revealAtHours;
        Category = category;
    }
}
