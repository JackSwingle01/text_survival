using System.Globalization;

namespace Survival;

/// How a line should feel: good news, bad news, a rare find, or neither. The UI picks its colour.
public enum Tone { Plain, Good, Bad, Rare }

/// One line of what happened, in the player's words. Event is what it tells of (the UI picks an icon by it).
public sealed record Narrated(long Minute, string Text, Tone Tone, WorldEvent Event);

/// The player's words for what happens in the world, the one place they are written: the CLI and the
/// web host both read them. Second person, present tense, short (principles: narrative). Names come
/// from Names. Nothing here decides anything.
public static class Narration
{
    /// The line for an event the player should hear about, or null for the ones they should not:
    /// another's private business, a step's bookkeeping, a fire out of their reach.
    public static Narrated? ForPlayer(World world, WorldEvent e)
    {
        var me = AgentId.Player;
        var here = world.Agents.TryGetValue(me, out var p) ? p.Tile : (TileId?)null;
        bool mine = e switch
        {
            EffectStarted x => x.Who == me,
            EffectEnded x => x.Who == me,
            Died x => x.Who == me || x.Tile == here,
            Exhausted x => x.Who == me,
            FullyRested x => x.Who == me,
            FireLit x => x.By == me,
            FireStartFailed x => x.Who == me,
            FuelAdded x => x.By == me,
            Moved x => x.Who == me,
            Injured x => x.Who == me || Combat.EncounterOf(world, me) is { } fight && fight.Units.Any(u => u.Agent == x.Who), // a blow in your fight is seen
            CrossedThePass x => x.Who == me,
            Gathered x => x.Who == me,
            Dropped x => x.Who == me,
            PutOnPile x => x.Who == me,
            PickedUp x => x.Who == me,
            PoolDiscovered x => x.Who == me,
            Scavenged x => x.Who == me,
            TutorialShown x => x.Who == me,
            TreeFelled x => x.Who == me,
            Ate x => x.Who == me,
            Drank x => x.Who == me,
            Washed x => x.Who == me,
            Butchered x => x.Who == me && x.Finished,
            Killed x => x.By == me,
            EncounterEnded x => x.Agent == me,
            WoundedEscaped x => x.Tile == here,
            Built x => x.Who == me,
            SkillLeveledUp x => x.Who == me,
            Made x => x.Who == me,
            ProjectAbandoned x => x.Who == me,
            FireBurnedToEmbers or FireDied or ShelterCollapsed => e.Tile == here,
            _ => false,
        };
        return mine ? Of(world, e) : null;
    }

    /// The line for an event, whoever it concerns (a receipt tells what others did at camp too), or
    /// null for an event that is never told (a step's refused plan, a receipt itself).
    public static Narrated? Of(World world, WorldEvent e)
    {
        bool Me(AgentId id) => id == AgentId.Player;
        string Who(AgentId id) => Me(id) ? "You" : Names.Cap(Cause.Describe(world, id));
        // "You light", "A person lights": the verb agrees with who did it.
        string Does(AgentId id, string verb) => $"{Who(id)} {(Me(id) ? verb : Third(verb))}";
        string Is(AgentId id) => Me(id) ? "You are" : $"{Who(id)} is";
        (string Text, Tone Tone)? line = e switch
        {
            // Grime builds from the first minute and comes back the minute after a wash: the wash is told instead.
            EffectStarted { Kind: EffectKind.Dirty } or EffectEnded { Kind: EffectKind.Dirty } => null,
            EffectStarted x => ($"{Is(x.Who)} {Names.Of(x.Kind)}.", x.Kind switch { EffectKind.Sweating => Tone.Plain, EffectKind.WellFed => Tone.Good, _ => Tone.Bad }),
            EffectEnded x => ($"{Is(x.Who)} no longer {Names.Of(x.Kind)}.", x.Kind == EffectKind.WellFed ? Tone.Plain : Tone.Good),
            Died x => (Me(x.Who) ? "You die." : $"{Who(x.Who)} dies.", Tone.Bad),
            Exhausted x => ($"{Is(x.Who)} exhausted.", Tone.Bad),
            FullyRested x => ($"{Is(x.Who)} fully rested.", Tone.Good),
            FireLit x => ($"{Does(x.By, "light")} a fire.", Tone.Good),
            FireStartFailed x => ($"The fire will not catch ({Pct(x.ChancePct)} chance).", Tone.Bad),
            FuelAdded x => ($"{Does(x.By, "add")} {Names.Amount(x.Fuel, x.Kg, x.Pieces)} to the fire.", Tone.Plain),
            FireBurnedToEmbers => ("The fire burns down to embers.", Tone.Bad),
            FireDied => ("The fire goes out.", Tone.Bad),
            Moved x => ($"{Does(x.Who, "reach")} {Place(world, x.To)}.", Tone.Plain),
            Injured x => (Me(x.Who) ? $"You hurt your {x.Region.ToLowerInvariant()}."
                : world.Agents.TryGetValue(x.Who, out var hurt) && hurt.Mind is HerdMind ? $"A {Names.Of(hurt.Species)} is hurt." // one of the herd, not the herd
                : $"{Who(x.Who)} is hurt.", Tone.Bad),
            CrossedThePass x => ($"{Does(x.Who, "cross")} the pass.", Tone.Rare),
            Gathered x => x.What.Length > 0 ? ($"{Does(x.Who, "gather")} {Haul(x.What)}.", Tone.Good)
                : Me(x.Who) && x.Density is double d ? (FailureLine(d, x.Minute), Tone.Bad) : ($"{Does(x.Who, "find")} nothing.", Tone.Bad),
            Dropped x => ($"No room in the pack: {Haul(x.What)} left on the ground.", Tone.Bad),
            PutOnPile x => ($"{Does(x.Who, "put")} down {Names.Amount(x.Resource, x.Kg, x.Pieces)}.", Tone.Plain),
            PickedUp x => (x.Taken ? $"{Does(x.Who, "take")} {Names.Amount(x.Resource, x.Kg, x.Pieces)} from the pile." : $"{Does(x.Who, "pick")} up {Names.Amount(x.Resource, x.Kg, x.Pieces)}.",
                x.Taken ? Tone.Bad : Tone.Plain),
            PoolDiscovered x => (Me(x.Who) && Pools.FoundLine(world, x.Pool) is string found ? found : $"{Does(x.Who, "find")} {Names.Lower(x.Name)}.", Tone.Rare),
            Scavenged x => (x.Animal is Species a ? $"{Does(x.Who, "find")} a {x.Remains} - {Names.Of(a)}. It could be butchered for resources."
                : $"{Does(x.Who, "find")} some bone fragments.", Tone.Rare),
            TutorialShown x => (Tutorials.Text(x.Tutorial, x.Kg), Tone.Plain),
            TreeFelled => ("The tree comes down.", Tone.Good),
            Ate x => ($"{Does(x.Who, "eat")} {Names.Amount(x.Food, x.Kg, x.Pieces)}.", Tone.Plain),
            Washed x => ($"{Does(x.Who, "wash")} {x.In switch { WashedIn.Snow => "with snow", WashedIn.CarriedWater => "with carried water", WashedIn.Water => "in the water", _ => throw new NotSupportedException($"No words for washing in {x.In}") }}, and {(Me(x.Who) ? "are" : "is")} left damp.", Tone.Plain),
            Drank x => ($"{Does(x.Who, "drink")} {x.Liters.ToString("0.##", CultureInfo.InvariantCulture)} L.", Tone.Plain),
            Butchered x => ($"{Does(x.Who, "finish")} butchering.", Tone.Good),
            Killed x => (x.By is AgentId by ? $"{Does(by, "kill")} a {Names.Of(x.Species)}." : $"A {Names.Of(x.Species)} dies.", Tone.Rare),
            EncounterEnded x => ($"The fight is over: {EncounterResult(x.Result)}.", x.Result is CombatResult.Victory or CombatResult.AnimalFled ? Tone.Good : Tone.Bad),
            WoundedEscaped x => ($"A wounded {Names.Of(x.Species)} gets away.", Tone.Bad),
            Built x => ($"{Does(x.Who, Recipes.Get(x.Recipe).Requires.Contains(Requirement.Tent) ? "pitch" : "build")} the {Names.Lower(Recipes.Get(x.Recipe).Name)}.", Tone.Good),
            ShelterCollapsed x => ($"The {Names.Lower(x.Name)} comes down.", Tone.Bad),
            SkillLeveledUp x => ($"{Names.Title(x.Skill)} rises to {x.Level}.", Tone.Rare),
            Made x => MadeLine(world, x),
            ProjectAbandoned x => (x.Target is { } kind ? $"{Does(x.Who, "give")} up {Making.Doing(Recipes.Get(x.Recipe))} the {Names.GearWords(kind)}."
                : $"{Does(x.Who, "give")} up on the {Names.Lower(Recipes.Get(x.Recipe).Name)}.", Tone.Plain),
            _ => null,
        };
        return line is { } l ? new Narrated(e.Minute, l.Text, l.Tone, e) : null;
    }

    /// What an empty search says, by the band of the ground's word, picked by the minute.
    public static string FailureLine(double density, long minute)
    {
        string word = Pools.QualityWord(density);
        var lines = Clues.Table.Failures.First(b => b.Words.Contains(word)).Lines;
        return lines[(int)((ulong)minute * 2654435761UL % (ulong)lines.Count)];
    }

    public static string Change(Change c) => c.Kind switch
    {
        ChangeKind.Fire => c.Now == null ? "The fire pit is gone."
            : c.Was == null ? $"A fire here: {Phase(c.Now)}."
            : $"The fire was {Phase(c.Was)}. Now it is {Phase(c.Now)}.",
        ChangeKind.Pile => c.Now == null ? $"The {Stuff(c.Subject)} in the pile is gone."
            : c.Was == null ? $"{c.Now} kg {Stuff(c.Subject)} in the pile."
            : $"The pile's {Stuff(c.Subject)}: {c.Was} kg, now {c.Now} kg.",
        ChangeKind.Carcass => c.Now == null ? $"The {c.Subject.ToLowerInvariant()} carcass is gone." : $"A {c.Subject.ToLowerInvariant()} carcass lies here.",
        ChangeKind.Shelter => c.Now == null ? $"The {(c.Was ?? "shelter").ToLowerInvariant()} is gone." : $"A {c.Now.ToLowerInvariant()} stands here.",
        _ => throw new ArgumentOutOfRangeException(nameof(c), c.Kind, null),
    };

    public static string Sign(TrackSign s) => $"{Names.Title(s.Maker)} prints, {Reading(s)}.";

    /// What prints say, without the maker: "heading north, fresh".
    public static string Reading(TrackSign s) =>
        (s.Pattern switch
        {
            TrafficPattern.Heading => $"heading {Names.Of(s.Heading)}",
            TrafficPattern.NorthSouth => "north-south traffic",
            TrafficPattern.EastWest => "east-west traffic",
            TrafficPattern.Mixed => "mixed traffic",
            _ => throw new ArgumentOutOfRangeException(nameof(s), s.Pattern, null),
        }) + (s.FreshnessPct > 0.8 ? ", fresh" : s.FreshnessPct < 0.3 ? ", old" : "");

    /// A place as a sentence names it: "Forest Camp", "the plain", "the forest by the river".
    public static string Place(World world, TileId t)
    {
        var m = Map.Of(world);
        if (m.SiteAt(t) != null || t == m.Camp || m.CaveAt(t) != Cave.None) return Map.Describe(m, t);
        return $"the {Names.Of(m.TerrainAt(t))}{(m.BesideRiver(t) ? " by the river" : "")}";
    }

    static (string, Tone) MadeLine(World world, Made m)
    {
        var r = Recipes.Get(m.Recipe);
        string name = Names.Lower(r.Name);
        string you = m.Who == AgentId.Player ? "You" : Names.Cap(Cause.Describe(world, m.Who));
        if (m.Target is { } kind)
            return m.Succeeded ? ($"{you} {Making.Verb(r)}{(m.Who == AgentId.Player ? "" : "s")} the {Names.GearWords(kind)}.", Tone.Good)
                : ($"{Names.Cap(Making.Doing(r))} the {Names.GearWords(kind)} goes wrong.", Tone.Bad);
        if (!m.Succeeded) return ($"The {name} fails.", Tone.Bad);
        string make = m.Who == AgentId.Player ? "make" : "makes";
        return (m.ExtraGear.Count > 0 ? $"{you} {make} a {name}, and offcuts for more." : $"{you} {make} a {name}.", Tone.Good);
    }

    static string EncounterResult(CombatResult r) => r switch
    {
        CombatResult.Victory => "you won",
        CombatResult.Defeat => "you lost",
        CombatResult.Fled => "you got away",
        CombatResult.AnimalFled => "it ran",
        CombatResult.AnimalDisengaged => "it backed off",
        _ => throw new ArgumentOutOfRangeException(nameof(r), r, null),
    };

    static string Third(string verb) => verb switch
    {
        "reach" or "finish" or "cross" => verb + "es",
        _ => verb + "s",
    };

    static string Phase(string phase) => Names.Of(Enum.Parse<FirePhase>(phase));

    static string Stuff(string resource) => Enum.TryParse<Resource>(resource, out var r) ? Names.Of(r)
        : throw new ArgumentException($"A pile change names no resource: '{resource}'", nameof(resource));

    static string Pct(double p) => (p * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// Items.Describe's "3 sticks of kindling, 50 g birch bark, Hand Drill" as "3 sticks of kindling, 50 g birch bark, hand drill".
    static string Haul(string what) =>
        string.Join(", ", what.Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(part =>
            Items.GearKinds.Values.Any(g => g.Name == part) ? part.ToLowerInvariant() : part));
}
