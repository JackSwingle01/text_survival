using System.Diagnostics;
using Survival;

/// `report [days]`: runs the world on (if days given), writes the find dump, the distribution report and the
/// forage haul (Report.HaulText) to the working directory, and prints the summary.
static class ReportCommand
{
    public static void Run(World world, string[] parts)
    {
        var clock = Stopwatch.StartNew();
        if (parts.Length > 1)
        {
            int days = int.Parse(parts[1]);
            int chunk = days % 10 == 0 ? 10 : days;
            Console.Write(Report.CurveText(Report.Curve(world, days, chunk)));
            Console.WriteLine($"  advanced {days} days in {clock.Elapsed.TotalSeconds:0.0} s");
        }
        var finds = Report.Finds(world);
        var dist = Report.Distribution(world, finds);
        string text = Report.DistributionText(world, dist);
        File.WriteAllText("report-finds.txt", Report.DumpText(world, finds));
        File.WriteAllText("report-distribution.txt", text);
        File.WriteAllText("report-haul.txt", Report.HaulText(world.Seed));
        // The summary: everything above the per-region table.
        Console.Write(text[..text.IndexOf("\nregion", StringComparison.Ordinal)]);
        Console.WriteLine();
        Console.WriteLine($"  wrote report-finds.txt ({finds.Count} rows), report-distribution.txt and report-haul.txt");
    }
}
