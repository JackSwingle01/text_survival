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

    static void Main() { }
}
