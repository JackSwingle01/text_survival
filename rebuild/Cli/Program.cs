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
            case "wait": Run(new Wait(AgentId.Player), Minutes(parts)); break;
            case "rest": Run(new Rest(AgentId.Player), Minutes(parts)); break;
            case "sleep": Run(new Sleep(AgentId.Player), Minutes(parts)); break;
            case "fire": Run(LightFire(world), 1); break;
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
                Console.WriteLine("Commands: wait N, rest N, sleep N, fire, fuel KIND KG, torch, charcoal, n/s/e/w [careful], go DIR [careful], " +
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
}
return 0;

void Run(Activity activity, int minutes)
{
    var result = Sim.Step(world, activity, minutes);
    foreach (var e in result.Events) Console.WriteLine($"  [{Clock(e.Minute)}] {e}");
    if (result.InterruptedReason != null)
        Console.WriteLine($"  Stopped after {result.MinutesElapsed} min: {result.InterruptedReason}");
}

static int Minutes(string[] parts) => int.Parse(parts[1]);
static int MinutesOr(string[] parts, int fallback) => parts.Length > 1 ? int.Parse(parts[1]) : fallback;
static double Kg(string[] parts) => parts.Length > 2 ? double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) : 99;

/// The best fire starter and tinder in the pack, and up to a kilo of kindling.
static Activity LightFire(World w)
{
    var pack = Items.Pack(w, AgentId.Player);
    var tool = new[] { ToolType.FireStriker, ToolType.BowDrill, ToolType.HandDrill }.FirstOrDefault(t => Items.Tool(pack, t) != null, ToolType.HandDrill);
    var tinder = new[] { Resource.BirchBark, Resource.Amadou, Resource.Usnea, Resource.Chaga, Resource.Tinder }
        .FirstOrDefault(r => Items.Have(pack, r) >= 0.05, Resource.Tinder);
    return new StartFire(AgentId.Player, tool, tinder, 0.05, Math.Min(1.0, Items.Have(pack, Resource.Kindling)));
}

static void Look(World w)
{
    var p = w.Agent(AgentId.Player);
    var pools = Pools.At(w, p.Tile);
    for (int i = 0; i < pools.Count; i++)
    {
        var id = new PoolId(p.Tile, i);
        if (!Pools.Knows(w, AgentId.Player, id)) continue; // what you have not found, you cannot see
        Console.WriteLine(pools[i] switch
        {
            ForagePool => $"  [{i}] forage, density {Pools.ForageDensity(w, id):0.00}",
            TreePool t => $"  [{i}] {t.Name} ({(Pools.HasTrees(w, id) ? $"{Pools.ChopProgressPct(w, id):P0} cut" : "cleared")})",
            StockPool s => $"  [{i}] {s.Name}: " + string.Join(", ", s.Items.Zip(Pools.UnitsNow(w, id)).Select(x => $"{x.First.Resource} {x.Second}/{x.First.Units}")),
            _ => $"  [{i}] {pools[i].Name}",
        });
    }
    var tile = w.Tiles.GetValueOrDefault(p.Tile);
    foreach (var c in tile?.Carcasses ?? [])
    {
        Carcasses.CatchUp(w, p.Tile, c);
        Console.WriteLine($"  carcass {c.Id}: {c.Animal}, decay {Carcasses.DecayLevel(c):P0}, {Carcasses.TotalRemainingKg(c):0.0} kg left");
    }
    if (tile?.Stash is { } stash && (stash.Kg.Count > 0 || stash.Gear.Count > 0)) Console.WriteLine($"  stash: {Items.Describe(stash)}");
    if (Work.HasOpenWater(w, p.Tile)) Console.WriteLine("  open water");
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
    Console.WriteLine($"{Clock(w.Minute)} | {sky.Condition} {sky.TemperatureC:0}C wind {sky.WindMph:0}mph {sky.WindDirection} | " +
                      $"core {b.CoreTempF:0.0}F energy {b.EnergyPct:P0} food {b.CaloriesKcal:0}kcal water {b.HydrationMl:0}ml " +
                      $"vitality {Body.Vitality(b):P0} | tile {p.Tile.X},{p.Tile.Y} {Map.Describe(Map.Of(w), p.Tile)}" + FireStatus(w, p.Tile) + (effects.Length > 0 ? $" | {effects}" : ""));
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
