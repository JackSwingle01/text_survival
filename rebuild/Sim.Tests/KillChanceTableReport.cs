using Survival;
using Xunit;
using Xunit.Abstractions;

/// Measures species.json's killChances and injuryChances again (KillOdds.Measure, 200 hunts a cell, one run for both) and
/// prints the two sections to paste over them. It runs only with KILL_CHANCES set:
/// KILL_CHANCES=1 dotnet test rebuild/Sim.Tests -c Release --filter KillChanceTableReport
public class KillChanceTableReport(ITestOutputHelper output)
{
    [Trait("Category", TestCategories.Slow)]
    [ReportFact("KILL_CHANCES")]
    public void Measure()
    {
        var kill = new List<string>();
        var injury = new List<string>();
        foreach (var (predator, quarries) in Content.Tables.KillChances)
        {
            kill.Add($"  \"{predator}\": {{");
            injury.Add($"  \"{predator}\": {{");
            foreach (var quarry in quarries.Keys)
            {
                var cells = Enumerable.Range(1, Fauna.MaxHunters).Select(n => Enumerable.Range(1, Combat.KillChanceHerds)
                    .Select(k => KillOdds.Measure(predator, n, quarry, k, 200, 1)).ToList()).ToList();
                kill.Add($"   \"{quarry}\": [{Rows(cells, c => c.Kill)}],");
                injury.Add($"   \"{quarry}\": [{Rows(cells, c => c.Injury)}],");
            }
            kill.Add("  },");
            injury.Add("  },");
        }
        output.WriteLine(" \"killChances\": {");
        foreach (var line in kill) output.WriteLine(line);
        output.WriteLine(" \"injuryChances\": {");
        foreach (var line in injury) output.WriteLine(line);
    }

    static string Rows(List<List<(double Kill, double Injury)>> cells, Func<(double Kill, double Injury), double> pick) =>
        string.Join(", ", cells.Select(row => "[" + string.Join(", ", row.Select(c => $"{pick(c):0.00}")) + "]"));
}
