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
            case "fire": Run(new StartFire(AgentId.Player, FireStarter.FireStriker, Fuel.BirchBark, 0.05, 1.0), 1); break;
            case "fuel": Run(new AddFuel(AgentId.Player, Enum.Parse<Fuel>(parts[1], ignoreCase: true), double.Parse(parts[2])), 1); break;
            case "torch": Run(new LightTorch(AgentId.Player), 1); break;
            case "save":
                File.WriteAllText(Path(parts), Save.ToJson(world));
                Console.WriteLine($"Saved to {Path(parts)}");
                break;
            case "load":
                world = Save.FromJson(File.ReadAllText(Path(parts)));
                Console.WriteLine($"Loaded {Path(parts)}");
                break;
            default: Console.WriteLine("Commands: wait N, rest N, sleep N, fire, fuel KIND KG, torch, status, save [file], load [file], quit"); continue;
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
static string Path(string[] parts) => parts.Length > 1 ? parts[1] : "rebuild-save.json";
static string Clock(long minute) => $"day {minute / 1440 + 1} {minute % 1440 / 60:00}:{minute % 60:00}";

void PrintStatus(World w)
{
    var p = w.Agent(AgentId.Player);
    var b = p.Body;
    string effects = string.Join(", ", b.Effects.Select(e => $"{e.Kind} {e.Severity:P0}"));
    Console.WriteLine($"{Clock(w.Minute)} | {w.Weather.TemperatureC:0}C wind {w.Weather.WindMps:0}m/s | " +
                      $"core {b.CoreTempF:0.0}F energy {b.EnergyPct:P0} food {b.CaloriesKcal:0}kcal water {b.HydrationMl:0}ml " +
                      $"vitality {Body.Vitality(b):P0} | tile {p.Tile.X},{p.Tile.Y}" + FireStatus(w, p.Tile) + (effects.Length > 0 ? $" | {effects}" : ""));
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
