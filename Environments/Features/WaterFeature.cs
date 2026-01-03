namespace text_survival.Environments.Features;

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
    public bool IsFrozen => _isFrozen;
    public double IceThicknessLevel => _iceThicknessLevel;  // 0=open, 0.3=thin, 0.7=solid, 1.0=glacier
    public bool HasIceHole => _hasIceHole;
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

    public bool HasThinIce => _isFrozen && _iceThicknessLevel < ThinIceThreshold;

    public bool CutIceHole()
    {
        if (!CanCutIceHole()) return false;

        _hasIceHole = true;
        _iceHoleRefreezeProgress = 0;
        return true;
    }

    public bool CanCutIceHole() => _isFrozen && !_hasIceHole && _iceThicknessLevel < 1.0;

    public int GetIceCuttingMinutes()
    {
        if (!CanCutIceHole()) return 0;

        // Thin ice: 15 min, Solid ice: 45 min, scaling linearly
        return (int)(15 + _iceThicknessLevel * 40);
    }

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

    public override FeatureUIInfo? GetUIInfo()
    {
        return new FeatureUIInfo(
            "water",
            DisplayName,
            GetStatusDescription(),
            null);
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

    public override List<Resource> ProvidedResources() =>
        (!IsFrozen || HasIceHole) ? [Resource.Water] : [];
}
