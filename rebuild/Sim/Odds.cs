namespace Survival;

/// How a term changes the running value: added to it (a percentage point), or multiplying it.
public enum OddsOp { Add, Scale }

/// One named factor in a bet: "Birch bark" +0.25, "Distance" x0.6.
public readonly record struct OddsTerm(string Factor, OddsOp Op, double Value)
{
    public static OddsTerm Add(string factor, double pct) => new(factor, OddsOp.Add, pct);
    public static OddsTerm Scale(string factor, double multiplier) => new(factor, OddsOp.Scale, multiplier);
}

/// A chance (or a yield multiplier) as its factors, for the odds sheet. The value is a fold: start
/// at BasePct and apply each term in order, adding or multiplying, then clamp to [MinPct, MaxPct].
/// TotalPct is computed from the terms by that one fold, so the terms always add up to the total
/// exactly; it is the formula itself, in its own order.
/// Terms that change nothing (adding 0, multiplying by 1) are left out by the builders.
public sealed record Odds(string Base, double BasePct, IReadOnlyList<OddsTerm> Terms, double MinPct = 0, double MaxPct = 1)
{
    /// Before the clamp.
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

    /// The clamp bound the total: the sheet shows "capped".
    public bool Capped => TotalPct != RawPct;

    /// Each term's signed contribution to the running value (what the segmented bar draws): exactly
    /// the term for an added one, the change it made for a multiplying one.
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

    /// The terms that move the total: those that change nothing are dropped.
    internal static List<OddsTerm> Moving(params OddsTerm[] terms) =>
        terms.Where(t => t.Op == OddsOp.Add ? t.Value != 0 : t.Value != 1).ToList();
}
