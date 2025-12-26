namespace text_survival.Bodies;

/// <summary>
/// Type-safe body part targeting for damage application.
/// Replaces error-prone string-based targeting with compile-time checked enum values.
/// </summary>
public enum BodyTarget
{
    /// <summary>Random selection based on coverage percentage (default)</summary>
    Random,

    // Human body regions
    Head,
    Chest,
    Abdomen,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,

    // Symmetric helpers - randomly picks left or right
    /// <summary>Randomly selects either left or right leg</summary>
    AnyLeg,
    /// <summary>Randomly selects either left or right arm</summary>
    AnyArm,

    // Special targeting
    /// <summary>Direct blood loss (bypasses body parts)</summary>
    Blood,

    // Common organs (for direct organ targeting)
    Heart,
    Brain,
    Lungs,
    Liver,
    Stomach,
    LeftKidney,
    RightKidney,
    LeftEye,
    RightEye,
    LeftEar,
    RightEar,
}
