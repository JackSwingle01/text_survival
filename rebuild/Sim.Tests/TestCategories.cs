/// Trait values for `[Trait("Category", ...)]`, the tiers a test runs in.
/// - Untagged: a quick test, under 5 s on CI's runner, which CI enforces.
/// - LongRun: a test that has to step a world for hours or days to see what it checks. Still on every PR,
///   in its own CI check, so the quick tests report without waiting on it.
/// - Slow: five seconds or more in Debug, and not worth every PR: nightly only.
/// `--filter "Category!=Slow"` is everything a PR runs; add `&Category!=LongRun` for the quick tests alone.
public static class TestCategories
{
    public const string Slow = "Slow";
    public const string LongRun = "LongRun";
}

/// A test that reports and never gates, so it runs only when its environment variable asks for it.
/// Everything else (CI, the nightly run, a bare `dotnet test`) lists it as skipped.
public sealed class ReportFactAttribute : FactAttribute
{
    public ReportFactAttribute(string variable)
    {
        if (Environment.GetEnvironmentVariable(variable) is not { Length: > 0 })
            Skip = $"Report: set {variable}=1 to run it";
    }
}
