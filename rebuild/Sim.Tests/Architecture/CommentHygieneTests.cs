using System.Text.RegularExpressions;

namespace Survival.Tests;

/// Comments in the Sim say why the design is what it is (principles-code.md, "Self-documenting").
/// History - what the old game did, what an earlier version of this code did, which seed showed a
/// bug, which review round or build step changed it - goes stale the moment the code changes, so it
/// belongs in commits and decision records, not in the source.
public class CommentHygieneTests
{
    /// Files other work is rewriting right now: not yet swept. Empty this list as each is cleaned.
    static readonly Dictionary<string, string> NotYetSwept = new()
    {
        ["Npc.cs"] = "being edited by the NPC work",
        ["Combat.cs"] = "being edited by the combat work",
        ["CombatAI.cs"] = "being edited by the combat work",
        ["CombatAftermath.cs"] = "being edited by the combat work",
        ["CombatFlight.cs"] = "being edited by the combat work",
        ["CombatObserver.cs"] = "being edited by the combat work",
        ["Fire.cs"] = "being edited by the fire work",
        ["Knowledge.cs"] = "being edited by the knowledge work",
        ["Ground.cs"] = "being edited by the ground work",
        ["Save.cs"] = "being rewritten with the save format",
        ["Content.cs"] = "being rewritten with the content loader",
        ["Content.Tables.cs"] = "being rewritten with the content loader",
        ["Species.cs"] = "being rewritten with the species tables",
    };

    /// History, not design. Each pattern is matched against comment text only.
    static readonly (string Name, Regex Pattern)[] HistoryPatterns =
    [
        ("a seed number", new(@"\bseeds? \d", RegexOptions.IgnoreCase)),
        ("the old game", new(@"\bold game\b", RegexOptions.IgnoreCase)),
        ("an 'old:' note", new(@"\bold\s*[:(]", RegexOptions.IgnoreCase)),
        ("a name from the old code", new(@"\b[Oo]ld [A-Z][a-z]+[A-Z]\w*")),
        ("this rebuild", new(@"\bthis rebuild\b", RegexOptions.IgnoreCase)),
        ("a port note", new(@"\b(port of|not ported|kept as ported)\b", RegexOptions.IgnoreCase)),
        ("'was ... before'", new(@"\bwas\b[^.;]*\bbefore\b\s*[,(]|\bwas\b[^.;]*\bbefore (the|this) (rebuild|review|change|fix)\b", RegexOptions.IgnoreCase)),
        ("'before,'", new(@"\b[Bb]efore,")),
        ("a review, round, stage, phase or step number", new(@"\b(review|round|stage|phase|step|roadmap) [A-Z]?\d", RegexOptions.IgnoreCase)),
        ("a review", new(@"\b(body review|review round|species round)\b", RegexOptions.IgnoreCase)),
        ("a plan decision", new(@"\bplan decision\b", RegexOptions.IgnoreCase)),
        ("a date", new(@"\b20\d\d-\d\d-\d\d\b(?!-[a-z])")),
        ("'decided' and a date", new(@"\bdecided \d", RegexOptions.IgnoreCase)),
        ("a fixed bug", new(@"\(fixed\)|\bbug, fixed\b|\(bug\b", RegexOptions.IgnoreCase)),
        ("'used to'", new(@"(?<!\b(is|are|be|been|get|got) )\bused to\b", RegexOptions.IgnoreCase)),
        ("'previously'", new(@"\bpreviously\b", RegexOptions.IgnoreCase)),
        ("parity with the old code", new(@"\bparity\b", RegexOptions.IgnoreCase)),
    ];

    /// Above this share of non-blank lines in comments, a file is worth a look: a warning, not a failure.
    const double CommentShareWarningPct = 0.10;

    [Fact]
    public void SimComments_RecordNoHistory()
    {
        var violations = SweptFiles()
            .SelectMany(f => HistoryIn(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();
        Assert.True(violations.Count == 0,
            $"{violations.Count} comments record history rather than design; say why, or delete:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Guard_CatchesHistory()
    {
        string src = """
            // old game: the pack fed on nothing (bug, fixed).
            /// Old WoodedAreaFeature.Update regrew it. Seed 123's packs ate it out.
            var x = "seed 7 old game"; // a string is not a comment
            /* body review A5, decided 2026-11-21 */
            """;
        var found = HistoryIn("Sample.cs", src).ToList();
        Assert.Contains(found, v => v.Contains("old game"));
        Assert.Contains(found, v => v.Contains("old code"));
        Assert.Contains(found, v => v.Contains("seed number"));
        Assert.Contains(found, v => v.Contains("date"));
        Assert.DoesNotContain(found, v => v.Contains(":3:"));
    }

    [Fact]
    public void Guard_LeavesDesignAlone()
    {
        string src = """
            /// Adds the tile; true if it was not there before.
            /// Old bones carry the cause their owner died of.
            /// Need with the fat short of the target it is used to.
            /// See documentation/decisions/2026-11-21-no-effect-needs-a-missing-treatment.md.
            // Hash the seed before mixing in k, or one seed's front 0 is the next seed's front 1.
            """;
        Assert.Empty(HistoryIn("Sample.cs", src));
    }

    /// A warning, not a failure: a heavily commented file is a refactor candidate, not a defect. The
    /// list is the skipped test's display name and its skip reason (CommentShareWarningAttribute), so
    /// a plain `dotnet test` prints it on the skip line; with no file over the line the test runs and passes.
    [CommentShareWarning]
    public void SimComments_WarnWhereHeavy() => Assert.Empty(HeavyFiles());

    /// Every swept file whose comment lines pass CommentShareWarningPct of its non-blank lines, heaviest first.
    internal static List<(string File, double Pct)> HeavyFiles() => SweptFiles()
        .Select(f => (File: Path.GetFileName(f), Pct: CommentShare(File.ReadAllText(f))))
        .Where(x => x.Pct > CommentShareWarningPct)
        .OrderByDescending(x => x.Pct).ThenBy(x => x.File, StringComparer.Ordinal)
        .ToList();

    /// Skips its test with the heavy files as its name and reason: in xunit 2 a passing run's console
    /// output at the default verbosity shows neither ITestOutputHelper lines nor skip reasons, only the
    /// names of skipped tests.
    sealed class CommentShareWarningAttribute : FactAttribute
    {
        public CommentShareWarningAttribute()
        {
            var heavy = HeavyFiles();
            if (heavy.Count == 0) return;
            Skip = $"warning: {heavy.Count} files are over {CommentShareWarningPct:P0} comments (comment lines / non-blank lines): "
                 + string.Join(", ", heavy.Select(h => $"{h.File} {h.Pct:P0}"));
            DisplayName = "Comment share " + Skip;
        }
    }

    static IEnumerable<string> SweptFiles() =>
        Directory.EnumerateFiles(ArchitectureTests.SimSourceDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !NotYetSwept.ContainsKey(Path.GetFileName(f)))
            .Order(StringComparer.Ordinal);

    static IEnumerable<string> HistoryIn(string file, string src)
    {
        foreach (var (start, end) in Comments(src))
        {
            string text = src[start..end];
            foreach (var (name, pattern) in HistoryPatterns)
                foreach (Match m in pattern.Matches(text))
                {
                    int line = LineOf(src, start + m.Index);
                    yield return $"{file}:{line}: {name}: {src.Split('\n')[line - 1].Trim()}";
                }
        }
    }

    /// Comment lines over non-blank lines; a line holding any comment text counts as a comment line.
    static double CommentShare(string src)
    {
        var lines = src.Split('\n');
        var commentLines = new HashSet<int>();
        foreach (var (start, end) in Comments(src))
            for (int l = LineOf(src, start); l <= LineOf(src, Math.Max(start, end - 1)); l++) commentLines.Add(l);
        int nonBlank = lines.Count(l => l.Trim().Length > 0);
        int comments = commentLines.Count(l => lines[l - 1].Trim().Length > 0);
        return nonBlank == 0 ? 0 : (double)comments / nonBlank;
    }

    static int LineOf(string src, int index)
    {
        int line = 1;
        for (int i = 0; i < index; i++) if (src[i] == '\n') line++;
        return line;
    }

    /// The comments in C# source, as [start, end) spans: line comments and block comments, skipping
    /// string and character literals (plain, verbatim, interpolated and raw).
    internal static List<(int Start, int End)> Comments(string src)
    {
        var spans = new List<(int, int)>();
        int i = 0, n = src.Length;
        while (i < n)
        {
            if (At(src, i, "//"))
            {
                int j = src.IndexOf('\n', i);
                j = j < 0 ? n : j;
                spans.Add((i, j));
                i = j;
            }
            else if (At(src, i, "/*"))
            {
                int j = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                j = j < 0 ? n : j + 2;
                spans.Add((i, j));
                i = j;
            }
            else if (src[i] is '"' or '$' or '@') i = SkipString(src, i);
            else if (src[i] == '\'') i = SkipChar(src, i);
            else i++;
        }
        return spans;
    }

    static bool At(string s, int i, string what) => string.CompareOrdinal(s, i, what, 0, what.Length) == 0;

    static int SkipString(string src, int i)
    {
        int n = src.Length, p = i;
        bool interpolated = false, verbatim = false;
        while (p < n && src[p] is '$' or '@')
        {
            if (src[p] == '$') interpolated = true; else verbatim = true;
            p++;
        }
        if (p >= n || src[p] != '"') return i + 1;
        int quotes = 0;
        while (p + quotes < n && src[p + quotes] == '"') quotes++;
        if (quotes >= 3)
        {
            string fence = new('"', quotes);
            int j = src.IndexOf(fence, p + quotes, StringComparison.Ordinal);
            return j < 0 ? n : j + quotes;
        }
        if (quotes == 2 && !verbatim) return p + 2; // ""
        int k = p + 1, depth = 0;
        while (k < n)
        {
            char c = src[k];
            if (depth > 0)
            {
                if (c == '"' || (c is '$' or '@' && k + 1 < n && src[k + 1] == '"')) { k = SkipString(src, k); continue; }
                if (c == '{') depth++;
                else if (c == '}') depth--;
                k++;
                continue;
            }
            if (verbatim && c == '"' && k + 1 < n && src[k + 1] == '"') { k += 2; continue; }
            if (!verbatim && c == '\\') { k += 2; continue; }
            if (c == '"') return k + 1;
            if (interpolated && c == '{')
            {
                if (k + 1 < n && src[k + 1] == '{') { k += 2; continue; }
                depth = 1;
            }
            k++;
        }
        return n;
    }

    static int SkipChar(string src, int i)
    {
        var m = Regex.Match(src.Substring(i, Math.Min(12, src.Length - i)), @"^'(\\[^']*|[^'\\])'");
        return m.Success ? i + m.Length : i + 1;
    }
}
