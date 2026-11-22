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

    /// Comment lines over non-blank lines a file may reach.
    const double CommentShareLimitPct = 0.10;

    /// The ratchet: files over the limit today, each held at its current share (whole percent). A file
    /// may only go down; the ratchet task takes one off at a time and brings it under the limit.
    static readonly Dictionary<string, int> OverCommentLimit = new()
    {
        ["Access.cs"] = 22,
        ["Activity.cs"] = 25,
        ["Agent.cs"] = 24,
        ["Body.cs"] = 11,
        ["BodyMotion.cs"] = 22,
        ["BodySurvival.cs"] = 15,
        ["Carcass.cs"] = 12,
        ["Combat.cs"] = 22,
        ["CombatAI.cs"] = 25,
        ["CombatAftermath.cs"] = 26,
        ["CombatFlight.cs"] = 23,
        ["CombatObserver.cs"] = 25,
        ["Content.cs"] = 11,
        ["Effects.cs"] = 12,
        ["Exposure.cs"] = 24,
        ["Fauna.cs"] = 21,
        ["Fire.cs"] = 12,
        ["Ground.cs"] = 11,
        ["Items.cs"] = 15,
        ["LocationFeatures.cs"] = 22,
        ["Make.cs"] = 11,
        ["Map.cs"] = 17,
        ["Memory.cs"] = 24,
        ["Names.cs"] = 11,
        ["Nearby.cs"] = 13,
        ["Pools.cs"] = 11,
        ["Populate.cs"] = 11,
        ["Remains.cs"] = 14,
        ["Renewal.cs"] = 16,
        ["Report.cs"] = 11,
        ["Rng.cs"] = 15,
        ["Save.cs"] = 14,
        ["Shelter.cs"] = 12,
        ["Signs.cs"] = 11,
        ["Sim.cs"] = 27,
        ["Skills.cs"] = 15,
        ["Species.cs"] = 21,
        ["Tags.cs"] = 13,
        ["Priorities.cs"] = 14,
        ["Tracks.cs"] = 18,
        ["Travel.cs"] = 12,
        ["Trip.cs"] = 20,
        ["Verbs.cs"] = 11,
        ["Weather.cs"] = 11,
        ["World.cs"] = 25,
        ["WorldEvent.cs"] = 20,
    };

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

    /// Every Sim file is at or under the limit, or under its ratchet ceiling; a listed file that has come
    /// under the limit must leave the list, so the list only shrinks.
    [Fact]
    public void SimComments_StayUnderTheRatchet()
    {
        var problems = new List<string>();
        var shares = SimFiles().ToDictionary(f => Path.GetFileName(f), f => CommentShare(File.ReadAllText(f)));
        foreach (var (file, share) in shares.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            int pct = (int)Math.Ceiling(share * 100 - 1e-9);
            if (OverCommentLimit.TryGetValue(file, out int ceiling))
            {
                if (share <= CommentShareLimitPct) problems.Add($"{file} is at {pct}%, under the limit: take it off OverCommentLimit");
                else if (pct > ceiling) problems.Add($"{file} is at {pct}%, above its ratchet ceiling of {ceiling}%");
            }
            else if (share > CommentShareLimitPct)
                problems.Add($"{file} is at {pct}%, over the {CommentShareLimitPct:P0} limit (comment lines / non-blank lines)");
        }
        foreach (var gone in OverCommentLimit.Keys.Where(k => !shares.ContainsKey(k)))
            problems.Add($"{gone} no longer exists: take it off OverCommentLimit");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    internal static IEnumerable<string> SimFiles() =>
        Directory.EnumerateFiles(ArchitectureTests.SimSourceDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Order(StringComparer.Ordinal);

    static IEnumerable<string> SweptFiles() => SimFiles().Where(f => !NotYetSwept.ContainsKey(Path.GetFileName(f)));

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
    internal static List<(int Start, int End)> Comments(string src) => Spans(src, withLiterals: false);

    /// The source with every comment and string or character literal blanked to spaces, line breaks kept,
    /// so a scan of what the code says matches neither prose nor player-facing words, and line numbers hold.
    internal static string CodeOnly(string src)
    {
        var code = src.ToCharArray();
        foreach (var (start, end) in Spans(src, withLiterals: true))
            for (int k = start; k < end; k++) if (code[k] != '\n') code[k] = ' ';
        return new string(code);
    }

    static List<(int Start, int End)> Spans(string src, bool withLiterals)
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
            else if (src[i] is '"' or '$' or '@' or '\'')
            {
                int j = src[i] == '\'' ? SkipChar(src, i) : SkipString(src, i);
                if (withLiterals && j > i + 1) spans.Add((i, j));
                i = j;
            }
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
