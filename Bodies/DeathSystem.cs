namespace text_survival.Bodies;
public enum BodyState
{
    Healthy,
    Injured,
    Critical,
    Dying,
    Dead
}

/// <summary>
/// prototype - not sure if this is how I want to do things
/// </summary>
public class DeathSystem
{
    public BodyState CheckBodyState(Body body)
    {
        var capacities = body.GetCapacities();

        // Instant death conditions
        if (HasVitalOrganDestroyed(body)) return BodyState.Dead;

        // System failure death
        if (IsInSystemFailure(capacities)) return BodyState.Dead;

        // Dying state (death inevitable without intervention)
        if (IsInDyingState(capacities, body)) return BodyState.Dying;

        // // Critical state (one system failure away from death)
        // if (IsInCriticalState(capacities)) return BodyState.Critical;

        // // Injured but stable
        // if (HasSignificantInjuries(capacities)) return BodyState.Injured;

        return BodyState.Healthy;
    }

    private bool HasVitalOrganDestroyed(Body body)
    {
        // Instant death: brain destroyed
        if (GetOrganCondition(body, "Brain") <= 0) return true;

        // Very rapid death: heart destroyed (maybe give a few minutes?)
        if (GetOrganCondition(body, "Heart") <= 0) return true;

        // Suffocation: both lungs destroyed
        var leftLung = GetOrganCondition(body, "Left Lung");
        var rightLung = GetOrganCondition(body, "Right Lung");
        if (leftLung <= 0 && rightLung <= 0) return true;

        return false;
    }

    private bool IsInSystemFailure(CapacityContainer capacities)
    {
        // Death from system collapse
        if (capacities.BloodPumping <= 0.05) return true;  // Heart barely functioning
        if (capacities.Breathing <= 0.05) return true;    // Can't breathe
        if (capacities.Consciousness <= 0) return true;   // Brain dead

        return false;
    }

    private bool IsInDyingState(CapacityContainer capacities, Body body)
    {
        // Multiple critical systems failing
        int criticalSystems = 0;
        if (capacities.BloodPumping <= 0.15) criticalSystems++;
        if (capacities.Breathing <= 0.15) criticalSystems++;
        if (capacities.Consciousness <= 0.15) criticalSystems++;

        if (criticalSystems >= 2) return true;

        // Severe blood loss (modeled as very low blood pumping)
        if (capacities.BloodPumping <= 0.1) return true;

        // Extreme pain/shock could also be dying
        if (body.GetPainLevel() > 0.8 && capacities.BloodPumping <= 0.3) return true;

        return false;
    }
}