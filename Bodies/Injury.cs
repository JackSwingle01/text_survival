using text_survival.Effects;

namespace text_survival.Bodies;
public class Injury : Effect
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

    public Injury(eInjuryType type, string source, BodyPart bodyPart, float severity) : base(type.ToString(), source, bodyPart, severity)
    {
        InjuryType = type;
        Severity = Math.Clamp(severity, 0, 1);

        // Configure based on type
        switch (type)
        {
            case eInjuryType.Cut:
                EffectKind = "Cut";
                SeverityChangeRate = -0.05; // Per day
                CapacityModifiers["Manipulation"] = 0.3;
                CapacityModifiers["Moving"] = 0.1;
                break;

            case eInjuryType.Bruise:
                EffectKind = "Bruise";
                SeverityChangeRate = -0.1; // Per day
                CapacityModifiers["Manipulation"] = 0.1;
                CapacityModifiers["Moving"] = 0.1;
                break;

            case eInjuryType.Break:
                EffectKind = "Broken";
                SeverityChangeRate = -0.01; // Very slow healing
                CapacityModifiers["Manipulation"] = 0.8;
                CapacityModifiers["Moving"] = 0.8;
                break;

            case eInjuryType.Burn:
                EffectKind = "Burn";
                SeverityChangeRate = -0.03;
                CapacityModifiers["Manipulation"] = 0.4;
                break;

            case eInjuryType.Frostbite:
                EffectKind = "Frostbite";
                SeverityChangeRate = -0.02;
                CapacityModifiers["Manipulation"] = 0.5;
                CapacityModifiers["Moving"] = 0.5;
                break;

            case eInjuryType.Infection:
                EffectKind = "Infection";
                SeverityChangeRate = 0.02; // Gets worse over time
                CapacityModifiers["Manipulation"] = 0.2;
                CapacityModifiers["Vitality"] = 0.4;
                break;
        }
    }

    // public override void ApplyTreatment(TreatmentInfo treatment)
    // {
    //     // Different treatments have different effectiveness
    //     double effectiveAmount = 0;

    //     switch (treatment.Type)
    //     {
    //         case "bandage":
    //             if (InjuryType == eInjuryType.Cut || InjuryType == eInjuryType.Burn)
    //             {
    //                 effectiveAmount = 0.2;
    //             }
    //             break;

    //         case "splint":
    //             if (InjuryType == eInjuryType.Break)
    //             {
    //                 effectiveAmount = 0.1;
    //             }
    //             break;

    //         case "antibiotics":
    //             if (InjuryType == eInjuryType.Infection)
    //             {
    //                 effectiveAmount = 0.5;
    //             }
    //             break;

    //         case "warmth":
    //             if (InjuryType == eInjuryType.Frostbite)
    //             {
    //                 effectiveAmount = 0.3;
    //             }
    //             break;
    //     }

    //     // Apply quality factor
    //     effectiveAmount *= treatment.Quality;

    //     // Reduce severity
    //     Severity -= effectiveAmount;
    //     Severity = Math.Clamp(Severity, 0, 1);
    // }
}