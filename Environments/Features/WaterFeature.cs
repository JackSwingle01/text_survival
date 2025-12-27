namespace text_survival.Environments.Features;

/// <summary>
/// Represents a body of water - frozen by default in Ice Age setting.
/// Water is primarily an obstacle and hazard, not a drinking source.
/// Enables ice-specific events (fall through, foot wet) and future fishing.
/// </summary>
public class WaterFeature : LocationFeature
{
    public override string? MapIcon => "water_drop";
    public override int IconPriority => 2;

    // Explicit public fields for serialization (System.Text.Json IncludeFields requires public)
    public string _displayName = string.Empty;
    public bool _isFrozen = true;
    public double _iceThicknessLevel = 0.7;
    public bool _hasIceHole = false;
    public double _iceHoleRefreezeProgress = 0;

    // Public properties backed by fields
    public string DisplayName => _displayName;
    public string Description { get; set; } = "";

    /// <summary>
    /// Whether the water is frozen. Default true (Ice Age setting).
    /// </summary>
    public bool IsFrozen => _isFrozen;

    /// <summary>
    /// Ice thickness on 0-1 scale:
    /// 0.0 = open water (no ice)
    /// 0.3 = thin ice (dangerous, high fall-through risk)
    /// 0.5 = moderate ice (crossable but risky)
    /// 0.7 = solid ice (safe for crossing)
    /// 1.0 = glacier-thick (extremely solid)
    /// </summary>
    public double IceThicknessLevel => _iceThicknessLevel;

    /// <summary>
    /// Whether an ice hole has been cut for fishing/water access.
    /// </summary>
    public bool HasIceHole => _hasIceHole;

    /// <summary>
    /// Progress toward ice hole refreezing (0-1). At 1.0, hole closes.
    /// </summary>
    public double IceHoleRefreezeProgress => _iceHoleRefreezeProgress;

    // Hazard constants
    private const double BaseIceHazard = 0.15;      // All ice is slippery
    private const double ThinIceHazard = 0.20;      // Thin ice adds instability
    private const double ThinIceThreshold = 0.4;    // Below this = thin ice

    // Refreeze rate: ~0.1 per hour = 10 hours to fully refreeze
    private const double RefreezeRatePerHour = 0.1;

    public WaterFeature(string name, string displayName) : base(name)
    {
        _displayName = displayName;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    public WaterFeature() : base("water") { }

    /// <summary>
    /// Calculates terrain hazard contribution from frozen water.
    /// Returns 0 if not frozen.
    /// </summary>
    public double GetTerrainHazardContribution()
    {
        if (!IsFrozen) return 0;

        double hazard = BaseIceHazard;

        // Thin ice is unstable - adds to hazard
        if (IceThicknessLevel < ThinIceThreshold)
        {
            hazard += ThinIceHazard;
        }

        return hazard;
    }

    /// <summary>
    /// Whether this water has thin ice that risks fall-through.
    /// </summary>
    public bool HasThinIce => _isFrozen && _iceThicknessLevel < ThinIceThreshold;

    /// <summary>
    /// Cut an ice hole for fishing/water access.
    /// Requires IsFrozen, no existing hole, and ice not glacier-thick.
    /// </summary>
    public bool CutIceHole()
    {
        if (!CanCutIceHole()) return false;

        _hasIceHole = true;
        _iceHoleRefreezeProgress = 0;
        return true;
    }

    /// <summary>
    /// Whether an ice hole can be cut here.
    /// </summary>
    public bool CanCutIceHole() => _isFrozen && !_hasIceHole && _iceThicknessLevel < 1.0;

    /// <summary>
    /// Estimated minutes to cut through the ice based on thickness.
    /// </summary>
    public int GetIceCuttingMinutes()
    {
        if (!CanCutIceHole()) return 0;

        // Thin ice: 15 min, Solid ice: 45 min, scaling linearly
        return (int)(15 + _iceThicknessLevel * 40);
    }

    /// <summary>
    /// Close an ice hole (for refreeze completion or manual reset).
    /// </summary>
    public void CloseIceHole()
    {
        _hasIceHole = false;
        _iceHoleRefreezeProgress = 0;
    }

    public override void Update(int minutes)
    {
        if (!_hasIceHole || !_isFrozen) return;

        // Ice holes slowly refreeze
        double hours = minutes / 60.0;
        _iceHoleRefreezeProgress += RefreezeRatePerHour * hours;

        if (_iceHoleRefreezeProgress >= 1.0)
        {
            CloseIceHole();
        }
    }

    /// <summary>
    /// Get a human-readable status description.
    /// </summary>
    public string GetStatusDescription()
    {
        if (!IsFrozen)
            return "open water";

        string thickness = IceThicknessLevel switch
        {
            < 0.2 => "barely frozen",
            < 0.4 => "thin ice",
            < 0.6 => "moderate ice",
            < 0.8 => "solid ice",
            _ => "thick ice"
        };

        if (HasIceHole)
            return $"{thickness}, ice hole cut";

        return thickness;
    }

    // Builder methods for fluent construction

    public WaterFeature WithDescription(string description)
    {
        Description = description;
        return this;
    }

    public WaterFeature WithIceThickness(double thickness)
    {
        _iceThicknessLevel = Math.Clamp(thickness, 0, 1);
        return this;
    }

    public WaterFeature AsOpenWater()
    {
        _isFrozen = false;
        _iceThicknessLevel = 0;
        return this;
    }

    public WaterFeature AsThinIce()
    {
        _isFrozen = true;
        _iceThicknessLevel = 0.3;
        return this;
    }

    public WaterFeature AsSolidIce()
    {
        _isFrozen = true;
        _iceThicknessLevel = 0.7;
        return this;
    }

    public WaterFeature WithExistingHole()
    {
        if (_isFrozen)
        {
            _hasIceHole = true;
            _iceHoleRefreezeProgress = 0;
        }
        return this;
    }
}
