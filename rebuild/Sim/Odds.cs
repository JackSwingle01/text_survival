namespace Survival;

public enum OddsOp { Add, Scale }

public readonly record struct OddsTerm(string Factor, OddsOp Op, double Value)
{
    public static OddsTerm Add(string factor, double pct) => new(factor, OddsOp.Add, pct);
    public static OddsTerm Scale(string factor, double multiplier) => new(factor, OddsOp.Scale, multiplier);
}

/// A chance (or yield multiplier) as its factors, for the odds sheet. TotalPct is one fold over the
/// terms in order, so the terms always add up to the total exactly: it is the formula itself.
public sealed record Odds(string Base, double BasePct, IReadOnlyList<OddsTerm> Terms, double MinPct = 0, double MaxPct = 1)
{
    public double RawPct
    {
        get
        {
            double v = BasePct;
            foreach (var t in Terms) v = t.Op == OddsOp.Add ? v + t.Value : v * t.Value;
            return v;
        }
    }

    public double TotalPct => Math.Clamp(RawPct, MinPct, MaxPct);

    public bool Capped => TotalPct != RawPct;

    /// Signed change each term made to the running value, for the segmented bar.
    public IReadOnlyList<(string Factor, double DeltaPct)> Contributions
    {
        get
        {
            var list = new List<(string, double)>(Terms.Count);
            double v = BasePct;
            foreach (var t in Terms)
            {
                double next = t.Op == OddsOp.Add ? v + t.Value : v * t.Value;
                list.Add((t.Factor, t.Op == OddsOp.Add ? t.Value : next - v));
                v = next;
            }
            return list;
        }
    }

    /// Drops terms that change nothing (adding 0, multiplying by 1) so the sheet lists only real factors.
    internal static List<OddsTerm> Moving(params OddsTerm[] terms) =>
        terms.Where(t => t.Op == OddsOp.Add ? t.Value != 0 : t.Value != 1).ToList();
}
