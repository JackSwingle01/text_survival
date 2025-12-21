namespace text_survival.Bodies;

/// <summary>
/// Blood as a systemic body component. Extends Tissue with ml-based math.
/// Blood multiplies BloodPumping (Heart pumps, Blood is what gets pumped).
/// Death occurs at 50% blood loss via circulation collapse.
/// </summary>
public class Blood : Tissue
{
    public const double TotalVolumeMl = 5000.0;
    public const double FatalThreshold = 0.50;  // Death at 50%

    public Blood() : base("Blood", toughness: TotalVolumeMl) { }

    public double VolumeMl => Condition * TotalVolumeMl;
    public double VolumeLostMl => (1.0 - Condition) * TotalVolumeMl;

    // Blood doesn't contribute base capacities - it's a multiplier for BloodPumping
    public override CapacityContainer GetBaseCapacities() => new();

    // Return neutral multipliers - Blood affects BloodPumping via direct multiplication in CapacityCalculator
    public override CapacityContainer GetConditionMultipliers()
        => CapacityContainer.GetBaseCapacityMultiplier();

    // No natural absorption - blood loss is blood loss
    public override double GetNaturalAbsorption(DamageType damageType) => 0;

    // Constant protection (not condition-scaled) so bleeding doesn't accelerate
    public override double GetProtection(DamageType damageType) => damageType switch
    {
        DamageType.Bleed => Toughness,
        DamageType.Internal => Toughness,
        _ => double.MaxValue  // Immune to physical damage types
    };
}
