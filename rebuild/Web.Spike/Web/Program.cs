using System.Runtime.InteropServices.JavaScript;
using WebSpike;

Console.WriteLine("Sim host ready");

/// JSExport shim over GameHost: the only thing the page can call. Strings and bytes only.
public static partial class Game
{
    static readonly GameHost Host = new();
    static Bench? _bench;

    [JSExport] internal static string NewGame(int seed) => Host.NewGame(seed);
    [JSExport] internal static string Act(string actionJson) => Host.Act(actionJson);
    [JSExport] internal static string View() => Host.View();
    [JSExport] internal static byte[] Save() => Host.Save();
    [JSExport] internal static string Load(byte[] bytes) => Host.Load(bytes);

    // ---- the spike's measurements (not part of the boundary) ----
    [JSExport] internal static void BenchNewWorld(int seed) { _bench = new Bench(); _bench.NewWorld(seed); }
    [JSExport] internal static void BenchStepsAtStart() => _bench!.StepsAtStart();
    [JSExport] internal static double BenchAdvanceDay() => _bench!.AdvanceDay();
    [JSExport] internal static void BenchStepsAfter() => _bench!.StepsAfter();
    [JSExport] internal static void BenchSave() => _bench!.SaveRoundTrip();
    [JSExport] internal static string BenchResult() => _bench!.ResultJson();
    [JSExport] internal static string BenchWorldJson() => _bench!.WorldJson();
}
