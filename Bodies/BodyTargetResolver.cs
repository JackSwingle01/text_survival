namespace text_survival.Bodies;

/// <summary>
/// Resolves BodyTarget enum values to actual body part names.
/// Handles symmetric part selection (AnyLeg, AnyArm) and organ targeting.
/// </summary>
public static class BodyTargetResolver
{
    public static string? ResolveTargetName(BodyTarget target, Body body)
    {
        return target switch
        {
            BodyTarget.Random => null,
            BodyTarget.Blood => "Blood",

            // Symmetric selection - pick random side
            BodyTarget.AnyLeg => PickRandomLeg(body),
            BodyTarget.AnyArm => PickRandomArm(body),

            // Direct region mapping
            BodyTarget.Head => BodyRegionNames.Head,
            BodyTarget.Chest => BodyRegionNames.Chest,
            BodyTarget.Abdomen => BodyRegionNames.Abdomen,
            BodyTarget.LeftArm => BodyRegionNames.LeftArm,
            BodyTarget.RightArm => BodyRegionNames.RightArm,
            BodyTarget.LeftLeg => BodyRegionNames.LeftLeg,
            BodyTarget.RightLeg => BodyRegionNames.RightLeg,

            // Organ targeting
            BodyTarget.Heart => "Heart",
            BodyTarget.Brain => "Brain",
            BodyTarget.Lungs => "Lungs",
            BodyTarget.Liver => "Liver",
            BodyTarget.Stomach => "Stomach",
            BodyTarget.LeftKidney => "Left Kidney",
            BodyTarget.RightKidney => "Right Kidney",
            BodyTarget.LeftEye => "Left Eye",
            BodyTarget.RightEye => "Right Eye",
            BodyTarget.LeftEar => "Left Ear",
            BodyTarget.RightEar => "Right Ear",

            _ => null
        };
    }

    private static string? PickRandomLeg(Body body)
    {
        var legs = body.Parts.Where(p => p.Name.Contains("Leg")).ToList();
        return legs.Count > 0 ? legs[Random.Shared.Next(legs.Count)].Name : null;
    }

    private static string? PickRandomArm(Body body)
    {
        var arms = body.Parts.Where(p => p.Name.Contains("Arm")).ToList();
        return arms.Count > 0 ? arms[Random.Shared.Next(arms.Count)].Name : null;
    }

    /// <summary>
    /// Get a human-readable display name for a body target.
    /// Useful for event text interpolation: "A cut on your {partName}."
    /// </summary>
    public static string GetDisplayName(BodyTarget target)
    {
        return target switch
        {
            BodyTarget.AnyLeg => "leg",
            BodyTarget.AnyArm => "arm",
            BodyTarget.Head => "head",
            BodyTarget.Chest => "chest",
            BodyTarget.Abdomen => "side",
            BodyTarget.LeftArm => "left arm",
            BodyTarget.RightArm => "right arm",
            BodyTarget.LeftLeg => "left leg",
            BodyTarget.RightLeg => "right leg",
            BodyTarget.Blood => "blood",
            BodyTarget.Heart => "heart",
            BodyTarget.Brain => "head",
            BodyTarget.Lungs => "chest",
            BodyTarget.Stomach => "stomach",
            _ => "body"
        };
    }
}
