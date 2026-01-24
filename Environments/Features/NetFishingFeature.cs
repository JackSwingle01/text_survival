using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;

namespace text_survival.Environments.Features;

/// <summary>
/// Manages placed fishing nets at a water location.
/// Requires WaterFeature for valid placement.
/// </summary>
public class NetFishingFeature : LocationFeature, IWorkableFeature
{
    public override string? MapIcon => NetCount > 0 ? (HasCatchWaiting ? "net_caught" : "net") : null;
    public override int IconPriority => HasCatchWaiting ? 8 : 2;  // Catches are urgent

    public readonly List<PlacedNet> _nets = [];
    public readonly WaterFeature _water;

    [System.Text.Json.Serialization.JsonConstructor]
    public NetFishingFeature() : base("net_fishing")
    {
        _water = new WaterFeature();
    }

    public NetFishingFeature(WaterFeature water) : base("net_fishing")
    {
        _water = water;
    }

    /// <summary>
    /// Number of active nets at this location.
    /// </summary>
    public int NetCount => _nets.Count(n => n.IsUsable);

    /// <summary>
    /// Number of nets with catches ready.
    /// </summary>
    public int CatchCount => _nets.Count(n => n.HasCatch);

    /// <summary>
    /// Check if any nets have catches ready.
    /// </summary>
    public bool HasCatchWaiting => _nets.Any(n => n.State == NetState.CatchReady);

    /// <summary>
    /// Check if any nets need attention (catch, stolen, lost).
    /// </summary>
    public bool HasAnythingToCheck => _nets.Any(n =>
        n.State == NetState.CatchReady ||
        n.State == NetState.Stolen ||
        n.State == NetState.Lost);

    public bool CanBeChecked => NetCount > 0;

    public IEnumerable<WorkOption> GetWorkOptions(GameContext ctx)
    {
        if (!CanBeChecked) yield break;

        string status = HasCatchWaiting
            ? $"{CatchCount} with fish!"
            : $"{NetCount} set";

        yield return new WorkOption(
            $"Check nets ({status})",
            "check_nets",
            new CheckNetStrategy()
        );
    }

    /// <summary>
    /// Place a net in the water.
    /// </summary>
    public void PlaceNet(int durability)
    {
        _nets.Add(new PlacedNet(durability));
    }

    /// <summary>
    /// Remove a net from this location.
    /// Returns the net if found.
    /// </summary>
    public PlacedNet? RemoveNet(int index)
    {
        if (index < 0 || index >= _nets.Count)
            return null;

        var net = _nets[index];
        _nets.RemoveAt(index);
        return net;
    }

    public override void Update(int minutes)
    {
        // Check if ice hole has refrozen (nets get frozen in!)
        if (_water.IsFrozen && !_water.HasIceHole)
        {
            foreach (var net in _nets.Where(n => n.IsUsable))
            {
                net.MarkLost();
            }
        }

        // Update each net
        foreach (var net in _nets.Where(n => n.IsUsable))
        {
            // Determine if this is flowing water (streams, rivers)
            // For now, use a simple heuristic based on name
            bool isFlowingWater = _water.DisplayName.Contains("Stream", StringComparison.OrdinalIgnoreCase) ||
                                  _water.DisplayName.Contains("River", StringComparison.OrdinalIgnoreCase);

            // TODO: Check for stalked tension from GameContext
            // For now, we'll handle this in the strategy when checking
            net.Update(minutes, _water.FishAbundance, isFlowingWater, false);
        }

        // Remove lost nets
        _nets.RemoveAll(n => n.State == NetState.Lost);
    }

    /// <summary>
    /// Update nets with stalked tension awareness.
    /// Called from GameContext update loop.
    /// </summary>
    public void UpdateWithTension(int minutes, bool isStalked)
    {
        // Check if ice hole has refrozen
        if (_water.IsFrozen && !_water.HasIceHole)
        {
            foreach (var net in _nets.Where(n => n.IsUsable))
            {
                net.MarkLost();
            }
        }

        foreach (var net in _nets.Where(n => n.IsUsable))
        {
            bool isFlowingWater = _water.DisplayName.Contains("Stream", StringComparison.OrdinalIgnoreCase) ||
                                  _water.DisplayName.Contains("River", StringComparison.OrdinalIgnoreCase);
            net.Update(minutes, _water.FishAbundance, isFlowingWater, isStalked);
        }

        _nets.RemoveAll(n => n.State == NetState.Lost);
    }

    /// <summary>
    /// Check all nets and collect results.
    /// Returns list of results for each net checked.
    /// </summary>
    public List<NetCheckResult> CheckAllNets()
    {
        var results = new List<NetCheckResult>();

        foreach (var net in _nets.ToList())
        {
            if (net.State == NetState.CatchReady)
            {
                var fish = net.CollectCatch();
                if (fish.Count > 0)
                {
                    results.Add(new NetCheckResult(fish, false, false));
                }
            }
            else if (net.State == NetState.Stolen)
            {
                results.Add(new NetCheckResult([], true, false));
                // Reset stolen net to empty
                net.CollectCatch();
            }
            else if (net.State == NetState.Lost)
            {
                results.Add(new NetCheckResult([], false, true));
                _nets.Remove(net);
            }
        }

        // Remove broken nets after checking
        _nets.RemoveAll(n => !n.IsUsable);

        return results;
    }

    public string GetDescription()
    {
        var usable = _nets.Count(n => n.IsUsable);
        var catches = _nets.Count(n => n.HasCatch);
        var stolen = _nets.Count(n => n.State == NetState.Stolen);

        if (catches > 0)
            return $"{usable} nets ({catches} with catches!)";
        if (stolen > 0)
            return $"{usable} nets ({stolen} plundered)";
        return $"{usable} nets set";
    }

    public override FeatureUIInfo? GetUIInfo()
    {
        if (NetCount == 0) return null;
        return new FeatureUIInfo(
            "nets",
            "Fishing Nets",
            GetDescription(),
            null);
    }

    public override List<Resource> ProvidedResources() =>
        HasCatchWaiting ? [Resource.RawMeat] : [];
}

/// <summary>
/// Result of checking a single net.
/// </summary>
public record NetCheckResult(List<double> FishWeights, bool WasStolen, bool WasLost)
{
    public double TotalWeight => FishWeights.Sum();
    public int FishCount => FishWeights.Count;
}
