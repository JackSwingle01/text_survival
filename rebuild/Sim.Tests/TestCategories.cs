/// Trait values for `[Trait("Category", ...)]`. A test that takes five seconds or more in Debug is Slow,
/// so `dotnet test --filter "Category!=Slow"` runs the quick suite and a bare `dotnet test` runs everything.
public static class TestCategories
{
    public const string Slow = "Slow";
}
