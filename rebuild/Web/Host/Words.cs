using System.Globalization;

namespace Survival.Web;

/// The game's names and events in the page's words: "BirchBark" as "birch bark", a FireDied as
/// "The fire went out". Presentation only; nothing here decides anything.
static class Words
{
    /// "QuickStrip" as "quick strip".
    public static string Of(string name) =>
        string.Concat(name.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));

    public static string Of(Enum e) => Of(e.ToString());

    public static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public static string Kg(double kg) => kg.ToString(kg < 1 ? "0.##" : "0.#", CultureInfo.InvariantCulture) + " kg";

    /// The species' icon basename under icons/animals (humans are drawn as people).
    public static string SpeciesIcon(Species s) => s == Species.Human ? "npc/male_0" : "animals/" + s.ToString().ToLowerInvariant();

    /// One journal line for an event the player should hear about, or null for the ones they should not
    /// (another agent's private business, a step's bookkeeping). Seen: whether it happened where the
    /// player could know of it (their own tile, or their own doing).
    public static LogDto? Event(World world, WorldEvent e)
    {
        var me = AgentId.Player;
        bool mine = e switch
        {
            EffectStarted x => x.Who == me, EffectEnded x => x.Who == me, Died x => x.Who == me || x.Tile == world.Agent(me).Tile,
            Exhausted x => x.Who == me, FullyRested x => x.Who == me, FireLit x => x.By == me, FireStartFailed x => x.Who == me,
            FuelAdded x => x.By == me, Moved x => x.Who == me, Injured x => x.Who == me, CrossedThePass x => x.Who == me,
            Gathered x => x.Who == me, Dropped x => x.Who == me, Stashed x => x.Who == me, Unstashed x => x.Who == me,
            PoolDiscovered x => x.Who == me, TreeFelled x => x.Who == me, Ate x => x.Who == me, Drank x => x.Who == me,
            Butchered x => x.Who == me && x.Finished, Killed x => x.By == me, FightEnded x => x.Agent == me, WoundedEscaped x => x.Tile == world.Agent(me).Tile,
            Built x => x.Who == me, SkillLeveledUp x => x.Who == me, Made x => x.Who == me, ProjectAbandoned x => x.Who == me,
            FireBurnedToEmbers or FireDied or ShelterCollapsed => e.Tile == world.Agent(me).Tile,
            _ => false,
        };
        return mine ? Line(world, e) : null;
    }

    /// The line for an event, whoever it concerns (a receipt tells what others did at camp too).
    public static LogDto? Line(World world, WorldEvent e)
    {
        string who(AgentId id) => id == AgentId.Player ? "You" : Cap(Cause.Describe(world, id));
        (string Text, string Kind, string Icon)? line = e switch
        {
            EffectStarted x => ($"{(x.Who == AgentId.Player ? "You are" : who(x.Who) + " is")} {Of(x.Kind)}", Bad(x.Kind) ? "bad" : "plain", "ui/effects"),
            EffectEnded x => ($"No longer {Of(x.Kind)}", "good", "ui/effects"),
            Died x => (x.Who == AgentId.Player ? "You died" : $"{who(x.Who)} died", "bad", "body"),
            Exhausted => ("You are exhausted", "bad", "ui/energy"),
            FullyRested => ("Fully rested", "good", "ui/energy"),
            FireLit x => ($"{who(x.By)} lit a fire", "good", "fire"),
            FireStartFailed x => ($"The fire would not catch ({x.ChancePct:P0} chance)", "bad", "ui/fire"),
            FuelAdded x => ($"{who(x.By)} added {Kg(x.Kg)} {Of(x.Fuel)} to the fire", "plain", "ui/fuel"),
            FireBurnedToEmbers => ("The fire burned down to embers", "bad", "embers"),
            FireDied => ("The fire went out", "bad", "embers"),
            Moved x => ($"Walked to {Map.Describe(Map.Of(world), x.To)}", "plain", "ui/follow"),
            Injured x => ($"{(x.Who == AgentId.Player ? "You were" : who(x.Who) + " was")} hurt: {Of(x.Region)}", "bad", "ui/blood"),
            CrossedThePass => ("You crossed the pass", "rare", "ui/confirm"),
            Gathered x => ($"Gathered {Pretty(x.What)}", "good", "ui/foraging"),
            Dropped x => ($"No room: left {Pretty(x.What)} on the ground", "bad", "ui/drop"),
            Stashed x => ($"Stashed {Kg(x.Kg)} {Of(x.Resource)}", "plain", "ui/storage"),
            Unstashed x => (x.Taken ? $"{who(x.Who)} took {Kg(x.Kg)} {Of(x.Resource)} from the stash" : $"{who(x.Who)} took {Kg(x.Kg)} {Of(x.Resource)}",
                x.Taken ? "bad" : "plain", "ui/storage"),
            PoolDiscovered x => ($"Found {Lower(x.Name)}", "rare", "ui/inspect"),
            TreeFelled => ("The tree came down", "good", "forest"),
            Ate x => ($"Ate {Kg(x.Kg)} {Of(x.Food)}", "plain", "ui/food"),
            Drank x => ($"Drank {x.Liters.ToString("0.##", CultureInfo.InvariantCulture)} L", "plain", "ui/water"),
            Butchered => ("Finished butchering", "good", "ui/knife"),
            Killed x => ($"{(x.By == AgentId.Player ? "You killed" : "Killed:")} a {Of(x.Species)}", "rare", "carcass"),
            FightEnded x => ($"The fight ended: {FightResult(x.Result)}", x.Result is CombatResult.Victory or CombatResult.AnimalFled ? "good" : "bad", "ui/target"),
            WoundedEscaped x => ($"A wounded {Of(x.Species)} got away", "bad", "ui/blood"),
            Built x => ($"{who(x.Who)} built the {Of(x.What)}", "good", "construction"),
            ShelterCollapsed x => ($"The {Lower(x.Name)} came down", "bad", "shelter"),
            SkillLeveledUp x => ($"{Cap(Of(x.Skill))} rose to {x.Level}", "rare", "ui/confirm"),
            Made x => MadeLine(x),
            ProjectAbandoned x => ($"Gave up on the {Lower(Recipes.Get(x.Recipe).Name)}", "plain", "ui/cancel"),
            _ => null,
        };
        return line is { } l ? new LogDto(e.Minute, l.Text, l.Kind, l.Icon) : null;
    }

    static (string, string, string) MadeLine(Made m)
    {
        string name = Lower(Recipes.Get(m.Recipe).Name);
        if (!m.Succeeded) return ($"The {name} failed", "bad", "ui/cancel");
        return (m.ExtraGear.Count > 0 ? $"Made a {name}, and offcuts for more" : $"Made a {name}", "good", "ui/project");
    }

    static string FightResult(CombatResult r) => r switch
    {
        CombatResult.Victory => "you won",
        CombatResult.Defeat => "you lost",
        CombatResult.Fled => "you got away",
        CombatResult.AnimalFled => "it ran",
        CombatResult.AnimalDisengaged => "it backed off",
        _ => throw new ArgumentOutOfRangeException(nameof(r), r, null),
    };

    static bool Bad(EffectKind k) => k is not EffectKind.Sweating;

    public static string Lower(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    /// Items.Describe's "Kindling 0.3kg, Tinder 0.05kg, Hand drill" as "0.3 kg kindling, 0.05 kg tinder, hand drill".
    public static string Pretty(string what) =>
        string.Join(", ", what.Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(part =>
        {
            int sp = part.LastIndexOf(' ');
            if (sp > 0 && part.EndsWith("kg", StringComparison.Ordinal) &&
                double.TryParse(part[(sp + 1)..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out double kg))
                return $"{Kg(kg)} {Of(part[..sp])}";
            return Lower(part);
        }));

}
