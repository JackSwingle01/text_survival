using System.Text;

namespace Survival.Tests;

/// Formats the seeds of a food chain bench: for each row, the mean over seeds of a day's value, then the spread of each seed's own mean.
internal static class FoodBenchReport
{
    const int DaysPerWeek = 7;

    internal static string Format(IReadOnlyList<FoodBenchSeed> seeds, int days)
    {
        var text = new StringBuilder();
        text.AppendLine($"seeds {seeds.Count}, days {days}. A cell is the mean over seeds (min-max of a seed's own mean); week 1 is shown by day. Burn is net: intake less the change in held store, so mending and wasting count in it.");
        text.AppendLine("Hunted: carcasses killed by its own species (not told apart from another pack's); Scavenged: any other kill, or none.");

        text.AppendLine().AppendLine("== Kills per day, killer -> victim ==");
        foreach (var pair in seeds.SelectMany(s => s.Kills.Keys).Select(k => (k.Killer, k.Victim)).Distinct().Order())
            Row(text, $"{pair.Killer} -> {pair.Victim}", seeds, days, (s, d) => s.Kills.GetValueOrDefault((pair.Killer, pair.Victim, d)));

        var species = seeds.SelectMany(s => s.AnimalDays.Keys).Select(k => k.Species).Distinct().Order().ToList();
        text.AppendLine().AppendLine("== Animals at the start of a day ==");
        foreach (var sp in species) Row(text, sp, seeds, days, (s, d) => s.AnimalDays.GetValueOrDefault((sp, d)));

        text.AppendLine().AppendLine("== Encounters ended per day, per animal ==");
        foreach (var sp in species)
            Row(text, sp, seeds, days, (s, d) => PerAnimal(s.Encounters.GetValueOrDefault((sp, d)), s.AnimalDays.GetValueOrDefault((sp, d))));

        text.AppendLine().AppendLine("== Energy: kcal per animal-day ==");
        foreach (var sp in species)
        {
            foreach (var source in Enum.GetValues<FoodSource>())
                if (seeds.Any(s => s.KcalIn.Keys.Any(k => k.Species == sp && k.Source == source)))
                    Row(text, $"{sp} {source.ToString().ToLowerInvariant()}", seeds, days, (s, d) => PerAnimal(s.KcalIn.GetValueOrDefault((sp, source, d)), s.AnimalDays.GetValueOrDefault((sp, d))));
            Row(text, $"{sp} burned (net)", seeds, days, (s, d) => PerAnimal(s.NetBurn.GetValueOrDefault((sp, d)), s.AnimalDays.GetValueOrDefault((sp, d))));
        }
        return text.ToString();
    }

    static double? PerAnimal(double total, double animals) => animals > 0 ? total / animals : null;

    static void Row(StringBuilder text, string label, IReadOnlyList<FoodBenchSeed> seeds, int days, Func<FoodBenchSeed, int, double?> value)
    {
        var perSeed = seeds.Select(s => Enumerable.Range(0, days).Select(d => value(s, d)).ToArray()).ToList();
        var own = perSeed.Select(v => v.Where(x => x != null).Select(x => x!.Value).DefaultIfEmpty(double.NaN).Average()).Where(x => !double.IsNaN(x)).ToList();
        if (own.Count == 0) return;
        double[] byDay = Enumerable.Range(0, days).Select(d => perSeed.Select(v => v[d]).Where(x => x != null).Select(x => x!.Value).DefaultIfEmpty(0).Average()).ToArray();
        text.Append($"{label,-34} {own.Average():0.00} ({own.Min():0.00}-{own.Max():0.00})");
        for (int week = 0; week * DaysPerWeek < days; week++)
        {
            var span = byDay.Skip(week * DaysPerWeek).Take(DaysPerWeek).ToArray();
            text.Append(week == 0 ? $" | week 1: {string.Join(" ", span.Select(x => x.ToString("0.0")))}" : $" | week {week + 1}: {span.Average():0.0}");
        }
        text.AppendLine();
    }
}
