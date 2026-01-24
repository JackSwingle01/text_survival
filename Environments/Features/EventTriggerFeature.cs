namespace text_survival.Environments.Features;

/// <summary>
/// A feature that triggers an event when discovered, then gets removed.
/// Used for one-time discovery events like finding an abandoned campsite.
///
/// When revealed from HiddenFeatures during foraging:
/// 1. ForageStrategy detects it's an EventTriggerFeature
/// 2. DiscoveryEventFactory creates and triggers the event
/// 3. Feature is removed from location (consumed)
/// </summary>
public class EventTriggerFeature : LocationFeature
{
    /// <summary>
    /// Maps to a factory in DiscoveryEventFactory.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    public EventTriggerFeature() : base("Discovery") { }

    public EventTriggerFeature(string eventId) : base("Discovery")
    {
        EventId = eventId;
    }

    /// <summary>
    /// No resources provided - this feature exists only to trigger an event.
    /// </summary>
    public override List<Resource> ProvidedResources() => [];
}
