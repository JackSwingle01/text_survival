namespace Survival.Web;

/// The page's side of the game's words: which icon a line wears, and the few page-only helpers. The
/// words themselves are Sim's (Names for names, Narration for what happens), shared with the CLI.
static class Words
{
    /// An enum value as the page's renderer names it (terrain-core.js): a code, not a word for the player.
    public static string Code<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    /// The fight view's labels: "Throw stone". With Of, below, until its names join Names.
    public static string Cap(string s) => Names.Cap(s);

    /// "QuickStrip" as "quick strip". The fight view's names only (actions, awareness, range): the
    /// combat round is being reworked and its enums with it, so they join Names (names.json) once
    /// they settle. Everything else is named by Names.
    public static string Of(Enum e) =>
        string.Concat(e.ToString().Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));

    /// The species' icon basename under icons/animals (humans are drawn as people).
    public static string SpeciesIcon(Species s) => s == Species.Human ? "npc/male_0" : "animals/" + s.ToString().ToLowerInvariant();

    /// A narrated line as a journal entry: Sim's words and tone, the page's icon for the event.
    public static LogDto Log(Narrated n) => new(n.Minute, n.Text, n.Tone.ToString().ToLowerInvariant(), Icon(n.Event));

    static string Icon(WorldEvent e) => e switch
    {
        EffectStarted or EffectEnded => "ui/effects",
        Died => "body",
        Exhausted or FullyRested => "ui/energy",
        FireLit => "fire",
        FireStartFailed => "ui/fire",
        FuelAdded => "ui/fuel",
        FireBurnedToEmbers or FireDied => "embers",
        Moved => "ui/follow",
        Injured or WoundedEscaped => "ui/blood",
        CrossedThePass or SkillLeveledUp => "ui/confirm",
        Gathered => "ui/foraging",
        Dropped => "ui/drop",
        PutOnPile or PickedUp => "ui/storage",
        PoolDiscovered => "ui/inspect",
        Scavenged => "carcass",
        TutorialShown => "ui/help",
        TreeFelled => "forest",
        Ate => "ui/food",
        Drank or Washed => "ui/water",
        Butchered => "ui/knife",
        Killed => "carcass",
        EncounterEnded => "ui/target",
        Built => "construction",
        ShelterCollapsed => "shelter",
        Made m => m.Succeeded ? "ui/project" : "ui/cancel",
        ProjectAbandoned => "ui/cancel",
        TransformationFinished => "ui/meat",
        TransformationStopped => "embers",
        _ => throw new ArgumentOutOfRangeException(nameof(e), e.GetType().Name, "Narration tells of it, but it has no icon"),
    };
}
