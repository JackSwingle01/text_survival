using System.Runtime.InteropServices.JavaScript;
using Survival.Web;

/// The JSExport shim over GameHost, run inside the Web Worker (wwwroot/worker.js). Strings and bytes only.
public static partial class Game
{
    static readonly GameHost Host = new();

    [JSExport] internal static string NewGame(int seed) => Host.NewGame(seed);
    [JSExport] internal static string Act(string json) => Host.Act(json);
    [JSExport] internal static string View() => Host.View();
    [JSExport] internal static byte[] Save() => Host.Save();
    [JSExport] internal static string Load(byte[] bytes) => Host.Load(bytes);

    /// Development only (Scenarios): the page calls it for ?scenario= and the screenshot states.
    [JSExport] internal static string Scenario(string name) => Scenarios.Apply(Host, name);

    /// Development only: Observer mode (observe.html), a world with no player in it.
    static readonly Observer Watch = new();
    [JSExport] internal static string ObserveNew(int seed) => Watch.New(seed);
    [JSExport] internal static string ObserveView() => Watch.View();
    [JSExport] internal static string ObserveMap() => Watch.TruthMap();
    [JSExport] internal static string ObserveAdvance(int minutes) => Watch.Advance(minutes);
    [JSExport] internal static string ObserveSpawn(string kind, int x, int y) => Watch.Spawn(kind, x, y);
    [JSExport] internal static string ObserveInspect(int x, int y) => Watch.Inspect(x, y);

    static void Main() { }
}
