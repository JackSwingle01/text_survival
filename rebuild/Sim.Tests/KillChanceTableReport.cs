using Survival;
using Xunit;
using Xunit.Abstractions;

/// Measures species.json's killChances again (KillOdds.Measure, 200 hunts a cell, about two minutes) and prints the
/// section to paste over it. It runs only with KILL_CHANCES set:
/// KILL_CHANCES=1 dotnet test rebuild/Sim.Tests -c Release --filter KillChanceTableReport
public class KillChanceTableReport(ITestOutputHelper output)
{
    [Trait("Category", TestCategories.Slow)]
    [ReportFact("KILL_CHANCES")]
    public void Measure()
    {
        foreach (var (predator, quarries) in Content.Tables.KillChances)
        {
            output.WriteLine($"  \"{predator}\": {{");
            foreach (var quarry in quarries.Keys)
            {
                var rows = Enumerable.Range(1, Fauna.MaxHunters).Select(n => "[" + string.Join(", ", Enumerable.Range(1, Combat.KillChanceHerds)
                    .Select(k => $"{KillOdds.Measure(predator, n, quarry, k, 200, 1):0.00}")) + "]");
                output.WriteLine($"   \"{quarry}\": [{string.Join(", ", rows)}],");
            }
            output.WriteLine("  },");
        }
    }
}
