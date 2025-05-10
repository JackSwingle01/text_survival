namespace text_survival.Bodies;
public class Injury : PhysicalCondition
{
    public eInjuryType InjuryType { get; }
    
    public enum eInjuryType
    {
        Cut,
        Bruise,
        Break,
        Burn,
        Frostbite,
        Infection
    }
    
    public Injury(eInjuryType type, double severity)
    {
        Type = "injury";
        InjuryType = type;
        Severity = Math.Clamp(severity, 0, 1);
        
        // Configure based on type
        switch (type)
        {
            case eInjuryType.Cut:
                Name = "Cut";
                Description = "A bleeding wound";
                HealRate = 0.05; // Per day
                CapacityModifiers["Manipulation"] = 0.3;
                CapacityModifiers["Moving"] = 0.1;
                break;
                
            case eInjuryType.Bruise:
                Name = "Bruise";
                Description = "Painful bruising";
                HealRate = 0.1; // Per day
                CapacityModifiers["Manipulation"] = 0.1;
                CapacityModifiers["Moving"] = 0.1;
                break;
                
            case eInjuryType.Break:
                Name = "Broken";
                Description = "A broken bone";
                HealRate = 0.01; // Very slow healing
                CapacityModifiers["Manipulation"] = 0.8;
                CapacityModifiers["Moving"] = 0.8;
                break;
                
            case eInjuryType.Burn:
                Name = "Burn";
                Description = "A painful burn";
                HealRate = 0.03;
                CapacityModifiers["Manipulation"] = 0.4;
                break;
                
            case eInjuryType.Frostbite:
                Name = "Frostbite";
                Description = "Frozen tissue";
                HealRate = 0.02;
                CapacityModifiers["Manipulation"] = 0.5;
                CapacityModifiers["Moving"] = 0.5;
                break;
                
            case eInjuryType.Infection:
                Name = "Infection";
                Description = "Infected wound";
                HealRate = -0.02; // Gets worse over time
                CapacityModifiers["Manipulation"] = 0.2;
                CapacityModifiers["Vitality"] = 0.4;
                break;
        }
    }
    
    public override void Update(TimeSpan timePassed)
    {
        // Natural healing over time
        double daysElapsed = timePassed.TotalDays;
        Severity -= HealRate * daysElapsed;
        Severity = Math.Clamp(Severity, 0, 1);
    }
    
    public override void ApplyTreatment(TreatmentInfo treatment)
    {
        // Different treatments have different effectiveness
        double effectiveAmount = 0;
        
        switch (treatment.Type)
        {
            case "bandage":
                if (InjuryType == eInjuryType.Cut || InjuryType == eInjuryType.Burn)
                {
                    effectiveAmount = 0.2;
                }
                break;
                
            case "splint":
                if (InjuryType == eInjuryType.Break)
                {
                    effectiveAmount = 0.1;
                }
                break;
                
            case "antibiotics":
                if (InjuryType == eInjuryType.Infection)
                {
                    effectiveAmount = 0.5;
                }
                break;
                
            case "warmth":
                if (InjuryType == eInjuryType.Frostbite)
                {
                    effectiveAmount = 0.3;
                }
                break;
        }
        
        // Apply quality factor
        effectiveAmount *= treatment.Quality;
        
        // Reduce severity
        Severity -= effectiveAmount;
        Severity = Math.Clamp(Severity, 0, 1);
    }
}