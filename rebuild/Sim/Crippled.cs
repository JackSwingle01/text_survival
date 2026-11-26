namespace Survival;

/// A verb refused by the body (GLOSSARY: Crippled): the first faculty it needs at zero, and why. Each owning system asks it.
public static class Crippled
{
    /// Speed and dexterity are read with no load or surroundings (the pack set down for the work), perception
    /// in the light there is: what a body cannot see by, it cannot search by.
    public static Refusal? Refusal(World world, Agent agent, params ReadOnlySpan<Faculty> needs)
    {
        foreach (var faculty in needs)
        {
            var context = faculty == Faculty.Perception ? Fire.AbilityContextFor(world, agent) with { EncumbrancePct = 0 } : default;
            if (Refusal(agent.Body, faculty, context) is { } refused) return refused;
        }
        return null;
    }

    public static Refusal? Refusal(BodyState body, Faculty faculty, AbilityContext context)
    {
        var why = Body.CrippledBy(body, faculty, context);
        return why.Count == 0 ? null
            : new(Prefix(faculty) + string.Join(", ", why.Select(w => Phrase(faculty, w))), new MissingFaculty(faculty, why));
    }

    /// With the hands ability a body eats and drinks with them; without, it reaches its food with its mouth, on its legs.
    public static Faculty ToEat(Species species) => SpeciesTable.Of(species).Hands ? Faculty.Dexterity : Faculty.Speed;

    static string Prefix(Faculty faculty) => faculty switch
    {
        Faculty.Speed => Traversal.CannotWalkPrefix,
        Faculty.Dexterity => "you cannot use your hands: ",
        Faculty.Perception => "you cannot make anything out: ",
        Faculty.Vision => "you cannot see: ",
        _ => throw new ArgumentOutOfRangeException(nameof(faculty), faculty, null),
    };

    static string Phrase(Faculty faculty, CrippledBy why) => why switch
    {
        CrippledBy.NoWorkingLimb => faculty == Faculty.Dexterity ? "no working arm" : "no working leg",
        CrippledBy.LostBlood => "lost blood",
        CrippledBy.Unconscious => "unconscious",
        CrippledBy.Load => "the load",
        CrippledBy.Build => "too frail a build",
        CrippledBy.BlindAndDeaf => "blind and deaf",
        CrippledBy.NoWorkingEye => "no working eye",
        _ => throw new ArgumentOutOfRangeException(nameof(why), why, null),
    };
}
