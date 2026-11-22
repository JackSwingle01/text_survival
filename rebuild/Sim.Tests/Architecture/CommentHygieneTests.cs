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

    /// Comment lines over non-blank lines a file may reach.
    const double CommentShareLimitPct = 0.10;

    /// The ratchet: files over the limit today, each held at its current share (whole percent). A file
    /// may only go down; the ratchet task takes one off at a time and brings it under the limit.
    static readonly Dictionary<string, int> OverCommentLimit = new()
    {
        ["Access.cs"] = 31,
        ["Activity.cs"] = 33,
        ["Agent.cs"] = 25,
        ["Body.cs"] = 16,
        ["BodyMotion.cs"] = 33,
        ["BodySurvival.cs"] = 24,
        ["Carcass.cs"] = 27,
        ["Carrion.cs"] = 27,
        ["Combat.cs"] = 22,
        ["CombatAI.cs"] = 25,
        ["CombatAftermath.cs"] = 26,
        ["CombatFlight.cs"] = 25,
        ["CombatObserver.cs"] = 25,
        ["Content.cs"] = 15,
        ["Effects.cs"] = 16,
        ["Exposure.cs"] = 34,
        ["Fauna.cs"] = 21,
        ["Fire.cs"] = 19,
        ["Ground.cs"] = 18,
        ["Items.cs"] = 26,
        ["Knowledge.cs"] = 24,
        ["Make.cs"] = 18,
        ["Map.cs"] = 28,
        ["Names.cs"] = 23,
        ["Nearby.cs"] = 21,
        ["Npc.cs"] = 37,
        ["Odds.cs"] = 24,
        ["Pools.cs"] = 18,
        ["Populate.cs"] = 16,
        ["Remains.cs"] = 31,
        ["Renewal.cs"] = 34,
        ["Report.cs"] = 14,
        ["Rng.cs"] = 18,
        ["Save.cs"] = 20,
        ["Shelter.cs"] = 23,
        ["Signs.cs"] = 20,
        ["Sim.cs"] = 29,
        ["Skills.cs"] = 36,
        ["Species.cs"] = 33,
        ["Tags.cs"] = 26,
        ["Things.cs"] = 34,
        ["Tiers.cs"] = 35,
        ["Tracks.cs"] = 25,
        ["Travel.cs"] = 27,
        ["Trip.cs"] = 28,
        ["Verbs.cs"] = 22,
        ["Weather.cs"] = 18,
        ["Work.cs"] = 13,
        ["World.cs"] = 39,
        ["WorldEvent.cs"] = 27,
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
