namespace Survival.Tests;

/// Words the glossary retired (GLOSSARY.md, "Avoid") stay out of the rebuild's code, content and UI.
public class RetiredWordsTests
{
    /// What lies on a tile is a pile (#121).
    static readonly string[] MayNameTheStash = [Path.Combine("Sim.Tests", "Architecture", "RetiredWordsTests.cs")];

    static readonly string[] Extensions = [".cs", ".json", ".js", ".html", ".css", ".md"];

    [Fact]
    public void Stash_IsNamedPileEverywhere()
    {
        string rebuild = Path.GetDirectoryName(ArchitectureTests.SimSourceDir())!;
        char sep = Path.DirectorySeparatorChar;
        var found = Directory.EnumerateFiles(rebuild, "*", SearchOption.AllDirectories)
            .Where(f => Extensions.Contains(Path.GetExtension(f))
                     && !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}node_modules{sep}"))
            .Where(f => !MayNameTheStash.Contains(Path.GetRelativePath(rebuild, f)))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => x.line.Contains("stash", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{Path.GetRelativePath(rebuild, x.f)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();
        Assert.True(found.Count == 0, "Call it a pile (GLOSSARY.md):\n" + string.Join("\n", found));
    }
}
