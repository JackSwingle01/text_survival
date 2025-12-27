namespace text_survival.Actions.Variants;

/// <summary>
/// Bundles examination description for environmental details.
/// Used for info-only details like animal tracks, droppings, bent branches.
/// </summary>
public record ExaminationVariant(string Description);

/// <summary>
/// Variant pools for examining environmental details.
/// Provides variety in flavor text when examining info-only details.
/// </summary>
public static class ExaminationVariants
{
    /// <summary>
    /// Examining animal tracks in snow.
    /// </summary>
    public static readonly ExaminationVariant[] TrackExaminations =
    [
        new("Fresh prints, deep in the snow. Something passed recently."),
        new("Old tracks, half-filled with drift. Days old at least."),
        new("A clear trail heading east. The animal was moving quickly."),
        new("Multiple tracks crossing here. A well-traveled path."),
        new("The stride is long. Something was running."),
        new("Tracks circle back on themselves. Foraging, probably."),
    ];

    /// <summary>
    /// Examining animal droppings/scat.
    /// </summary>
    public static readonly ExaminationVariant[] DroppingExaminations =
    [
        new("Fresh scat, still steaming in the cold. Very recent."),
        new("Old droppings, frozen solid. They haven't been back in a while."),
        new("Marking territory. This area belongs to something."),
        new("Scattered wide. Whatever left this was moving through, not staying."),
        new("Bones and fur in the scat. A predator."),
    ];

    /// <summary>
    /// Examining bent/broken branches.
    /// </summary>
    public static readonly ExaminationVariant[] BranchExaminations =
    [
        new("Something large pushed through here. Branches snapped at shoulder height."),
        new("Low branches bent aside. Something small passed through."),
        new("Fur caught on the bark. Gray. Wolf, probably."),
        new("The breaks are fresh. Still oozing sap despite the cold."),
        new("Old damage, weathered and gray. Whatever did this is long gone."),
        new("A clear path through the underbrush. Something uses this route often."),
    ];

    /// <summary>
    /// Select a random variant from a pool.
    /// </summary>
    public static ExaminationVariant SelectRandom(ExaminationVariant[] pool)
    {
        return pool[Random.Shared.Next(pool.Length)];
    }
}
