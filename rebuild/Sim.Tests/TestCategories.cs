/// Trait values for `[Trait("Category", ...)]`. A test that takes five seconds or more in Debug is Slow,
/// so `dotnet test --filter "Category!=Slow"` runs the quick suite and a bare `dotnet test` runs everything.
public static class TestCategories
{
    public const string Slow = "Slow";
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
