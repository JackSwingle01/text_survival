using Survival;

// Commands come from args (each arg one command, e.g. "wait 60") or, failing that, stdin lines.
// `--seed N` first picks the world seed. A bug report is a seed plus this command list.
int seed = 1;
var commandArgs = args.ToList();
if (commandArgs.Count >= 2 && commandArgs[0] == "--seed")
{
    seed = int.Parse(commandArgs[1]);
    commandArgs.RemoveRange(0, 2);
}

var world = World.New(seed);
bool interactive = commandArgs.Count == 0 && !Console.IsInputRedirected;
IEnumerable<string> commands = commandArgs.Count > 0 ? commandArgs : ReadStdin();

PrintStatus(world);
foreach (string line in commands)
{
    if (!interactive) Console.WriteLine($"> {line}");
    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0) continue;
    try
    {
        switch (parts[0])
        {
            case "quit": return 0;
            case "status": break;
            case "report": ReportCommand.Run(world, parts); break;
            case "wait": Run(new Wait(AgentId.Player), Minutes(parts)); break;
            case "rest": Run(new Rest(AgentId.Player), Minutes(parts)); break;
            case "sleep": Run(new Sleep(AgentId.Player), Minutes(parts)); break;
            case "fire":
            {
                // Lighting a fire is the verb Verbs offers: the tile's, or the pit's own once there is one.
                var light = Verbs.At(world, AgentId.Player).All.FirstOrDefault(v => v.Activity is StartFire);
                if (light == null) { Console.WriteLine("  Nothing to light: the fire here is burning."); continue; }
                if (light.Odds != null) PrintOdds(light.Odds);
                Run(light.Activity, light.Minutes);
                break;
            }
            case "verbs": PrintVerbs(Verbs.At(world, AgentId.Player)); continue;
            case "do":
            {
                var verb = Numbered(Verbs.At(world, AgentId.Player))[int.Parse(parts[1]) - 1];
                if (verb.Odds != null) PrintOdds(verb.Odds);
                foreach (var o in verb.Outcomes?.Outcomes ?? []) PrintOdds(o.Odds);
                Run(verb.Activity, verb.Minutes);
                break;
            }
            case "charcoal": Run(new GatherCharcoal(AgentId.Player), 1); break;
            case "look": Look(world); continue;
            case "inv" or "inventory": Inv(world); continue;
            case "forage": Run(new Gather(AgentId.Player, 0, parts.Length > 2 ? Enum.Parse<ForageFocus>(parts[2], ignoreCase: true) : ForageFocus.General), MinutesOr(parts, 60)); break;
            case "gather" or "harvest" or "chop" or "salvage": Run(new Gather(AgentId.Player, int.Parse(parts[1])), parts.Length > 2 ? int.Parse(parts[2]) : 240); break;
            case "drink": Run(new Drink(AgentId.Player), Work.DrinkMinutes); break;
            case "eat": Run(new Eat(AgentId.Player, Enum.Parse<Resource>(parts[1], ignoreCase: true), Kg(parts)), Work.EatMinutes); break;
            case "cook": Run(new Cook(AgentId.Player, Enum.Parse<Resource>(parts[1], ignoreCase: true), Kg(parts)), 600); break;
            case "melt": Run(new MeltSnow(AgentId.Player), Work.MeltSnowMinutes); break;
            case "butcher":
                Run(new Butcher(AgentId.Player, int.Parse(parts[1]), parts.Length > 2 ? Enum.Parse<ButcherMode>(parts[2], ignoreCase: true) : null), 24 * 60); break;
            case "stash": Run(new Stash(AgentId.Player, Enum.Parse<Resource>(parts[1], ignoreCase: true), Kg(parts)), 1); break;
            case "take": Run(new Unstash(AgentId.Player, Enum.Parse<Resource>(parts[1], ignoreCase: true), Kg(parts)), 1); break;
            case "wear": Run(new Wear(AgentId.Player, parts[1]), 1); break;
            case "fuel": Run(new AddFuel(AgentId.Player, Enum.Parse<Resource>(parts[1], ignoreCase: true), double.Parse(parts[2])), 1); break;
            case "torch": Run(new LightTorch(AgentId.Player), 1); break;
            case "build": Run(new Build(AgentId.Player, Enum.Parse<Structure>(parts[1], ignoreCase: true)), 24 * 60); break; // stops itself when built
            case "improve": Run(new ImproveShelter(AgentId.Player, Enum.Parse<ShelterStat>(parts[1], ignoreCase: true), Enum.Parse<Resource>(parts[2], ignoreCase: true)), parts.Length > 3 ? int.Parse(parts[3]) : 10); break;
            case "pack": Run(new PackTent(AgentId.Player), 1); break;
            case "n" or "s" or "e" or "w" or "north" or "south" or "east" or "west":
                Run(new Travel(AgentId.Player, Heading(parts[0]), Pace(parts)), 24 * 60); break; // a crossing stops itself on arrival
            case "go": Run(new Travel(AgentId.Player, Heading(parts[1]), Pace(parts)), 24 * 60); break; // a crossing stops itself on arrival
            case "save":
                File.WriteAllText(Path(parts), Save.ToJson(world));
                Console.WriteLine($"Saved to {Path(parts)}");
                break;
            case "load":
                world = Save.FromJson(File.ReadAllText(Path(parts)));
                Console.WriteLine($"Loaded {Path(parts)}");
                break;
            default:
                Console.WriteLine("Commands: verbs, do N, wait N, rest N, sleep N, fire, fuel KIND KG, torch, charcoal, n/s/e/w [careful], go DIR [careful], " +
                                  "look, inv, forage [MIN] [FOCUS], gather POOL [MIN], drink, eat KIND [KG], cook KIND [KG], melt, " +
                                  "butcher ID [MODE], stash KIND [KG], take KIND [KG], wear GEAR, status, save [file], load [file], quit");
                continue;
        }
    }
    catch (Exception e) when (e is FormatException or IndexOutOfRangeException or IOException or ArgumentException)
    {
        if (!interactive) throw; // a broken script should fail, not limp on
        Console.WriteLine($"Error: {e.Message}");
        continue;
    }
    PrintStatus(world);
    if (Map.Outcome(world) is not RunOutcome.Surviving and var outcome)
    {
        Console.WriteLine(outcome == RunOutcome.Crossed ? $"You crossed the pass on {Clock(world.Minute)}. The run is won." : "You are dead. The run is over.");
        break;
    }
}
return 0;

void Run(Activity activity, int minutes)
{
    var result = Sim.Step(world, activity, minutes);
    foreach (var e in result.Events)
    {
        if (e is Noticed n) PrintReceipt(n.Receipt);
        else Console.WriteLine($"  [{Clock(e.Minute)}] {e}");
    }
    if (result.InterruptedReason != null)
        Console.WriteLine($"  Stopped after {result.MinutesElapsed} min: {result.InterruptedReason}");
}

static int Minutes(string[] parts) => int.Parse(parts[1]);
static int MinutesOr(string[] parts, int fallback) => parts.Length > 1 ? int.Parse(parts[1]) : fallback;
static double Kg(string[] parts) => parts.Length > 2 ? double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) : 99;

/// Every verb Verbs.At lists, in the order `verbs` numbers them: each thing's menu, the walks, the tile's, making.
static List<Verb> Numbered(Here here) => [.. here.All];

/// What can be done here, numbered for `do N`: each thing with its conditions and the action shown
/// for it (or Interact, with its menu beneath), then the walks and the tile's own verbs.
static void PrintVerbs(Here here)
{
    int n = 0;
    string Line(Verb v) => $"{++n,3}. {v.Label}{(v.Refused != null ? $" (refused: {v.Refused})" : "")}{(v.Odds != null ? $" [{v.Odds.TotalPct:P0}]" : "")}{(v.Trip != null ? $" [{v.Trip.TotalMinutes} min]" : "")}";
    foreach (var t in here.Things)
    {
        string conditions = t.Conditions.Count > 0 ? $" ({string.Join(", ", t.Conditions.Select(c => c.Name))})" : "";
        string shown = t.Action?.Label ?? (t.Interact ? "Interact" : "nothing to do");
        Console.WriteLine($"  [{t.Name}{conditions}] - {shown}");
        foreach (var v in t.Menu) Console.WriteLine($"     {Line(v)}");
    }
    foreach (var v in here.Walk.Concat(here.Tile)) Console.WriteLine($"  {Line(v)}");
    Console.WriteLine("  [make]");
    foreach (var v in here.Make)
        Console.WriteLine($"     {Line(v)}{(v.Outcomes != null && v.Refused == null ? $" [{v.Outcomes.PlainPct:P0} as planned]" : "")}");
}

/// What changed at a remembered tile while you were away or asleep: a camp tells you everything
/// that happened, the field only what you can see for yourself.
static void PrintReceipt(Receipt r)
{
    string since = r.SinceMinute is long s ? Clock(s) : "never";
    Console.WriteLine($"  -- {(r.Full ? "at camp" : "here")}, since you last saw it ({since}) --");
    foreach (var e in r.Happened) Console.WriteLine($"     [{Clock(e.Minute)}] {e}");
    foreach (var c in r.Changes) Console.WriteLine($"     {c.Kind} {c.Subject}: {c.Was ?? "none"} -> {c.Now ?? "gone"}");
    foreach (var t in r.Signs)
        Console.WriteLine($"     {t.Maker} prints, {(t.Pattern == TrafficPattern.Heading ? $"heading {t.Heading}" : t.Pattern.ToString())}, {t.AgeMinutes / 60} h old");
}

/// An odds sheet: the base, each factor's signed share, the total.
static void PrintOdds(Odds o)
{
    var terms = string.Join(" ", o.Contributions.Select(c => $"{(c.DeltaPct >= 0 ? "+" : "")}{c.DeltaPct:P0} {c.Factor}"));
    Console.WriteLine($"  odds: {o.Base} {o.BasePct:P0} {terms} = {o.TotalPct:P0}{(o.Capped ? " (capped)" : "")}");
}

static void Look(World w)
{
    foreach (var line in Report.Look(w, AgentId.Player)) Console.WriteLine($"  {line}");
}

static void Inv(World w)
{
    var p = w.Agent(AgentId.Player);
    var pack = Items.Pack(w, AgentId.Player);
    Console.WriteLine($"  pack: {Items.Describe(pack)}");
    Console.WriteLine($"  worn: {string.Join(", ", p.Body.Worn.Select(g => Items.Def(g).Name))} ({p.Body.ClothingClo:0.00} clo)");
    Console.WriteLine($"  carrying {Items.CarriedKg(p):0.0}/{Items.CapacityKg(pack):0} kg");
}
static Direction Heading(string word) => word.ToLowerInvariant() switch
{
    "n" or "north" => Direction.North, "e" or "east" => Direction.East,
    "s" or "south" => Direction.South, "w" or "west" => Direction.West,
    _ => throw new ArgumentException($"No direction '{word}'"),
};
static Pace Pace(string[] parts) => parts.Contains("careful") ? Survival.Pace.Careful : Survival.Pace.Quick;
static string Path(string[] parts) => parts.Length > 1 ? parts[1] : "rebuild-save.json";
static string Clock(long minute) => $"day {Weather.DayNumber(minute)} {Weather.MinuteOfDay(minute) / 60:00}:{minute % 60:00}";

void PrintStatus(World w)
{
    var p = w.Agent(AgentId.Player);
    var b = p.Body;
    string effects = string.Join(", ", b.Effects.Select(e => $"{e.Kind} {e.Severity:P0}"));
    var sky = Weather.At(w, w.Minute);
    string skills = p.Mind is PersonMind mind ? string.Join(", ", mind.Skills.Where(k => k.Value.Level > 0).Select(k => $"{k.Key} {k.Value.Level}")) : "";
    Console.WriteLine($"{Clock(w.Minute)} | {sky.Condition} {sky.TemperatureC:0}C wind {sky.WindMph:0}mph {sky.WindDirection} | " +
                      $"core {b.CoreTempF:0.0}F energy {b.EnergyPct:P0} food {b.CaloriesKcal:0}kcal water {b.HydrationMl:0}ml " +
                      $"vitality {Body.Vitality(b):P0} | tile {p.Tile.X},{p.Tile.Y} {Map.Describe(Map.Of(w), p.Tile)}" + FireStatus(w, p.Tile)
                      + (effects.Length > 0 ? $" | {effects}" : "") + (skills.Length > 0 ? $" | {skills}" : ""));
}

static string FireStatus(World w, TileId tile) =>
    Fire.PitAt(w, tile) is FireId id ? $" | fire {Fire.Phase(w.Fires[id])} {Fire.TemperatureF(w.Fires[id]):0}F" : "";

IEnumerable<string> ReadStdin()
{
    while (true)
    {
        if (interactive) Console.Write("> ");
        string? line = Console.ReadLine();
        if (line == null) yield break;
        yield return line;
    }
}
