using System.Text.RegularExpressions;

namespace text_survival.Tests.Architecture;

/// <summary>
/// The dependency direction is one-way: UI depends on the simulation, never the reverse.
/// These scan the source because no compiler rule can express it - the whole solution is
/// one assembly.
/// </summary>
public class LayeringTests
{
    /// <summary>Every game source file, with its path relative to the repo root.</summary>
    private static IEnumerable<(string Path, string[] Lines)> SourceFiles()
    {
        string root = RepoRoot();

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');

            if (relative.StartsWith("bin/") || relative.Contains("/bin/")) continue;
            if (relative.StartsWith("obj/") || relative.Contains("/obj/")) continue;
            if (relative.StartsWith("tools/")) continue;
            if (relative.StartsWith("text_survival.Tests/")) continue;

            yield return (relative, File.ReadAllLines(file));
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "text_survival.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static bool IsUnder(string path, params string[] folders) =>
        folders.Any(f => path.StartsWith(f + "/", StringComparison.Ordinal));

    private static List<string> Offences(
        Func<string, bool> appliesTo, Regex forbidden, Func<string, bool>? lineExempt = null)
    {
        var offences = new List<string>();

        foreach (var (path, lines) in SourceFiles())
        {
            if (!appliesTo(path)) continue;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!forbidden.IsMatch(lines[i])) continue;
                if (lineExempt?.Invoke(lines[i]) == true) continue;

                offences.Add($"{path}:{i + 1}: {lines[i].Trim()}");
            }
        }

        return offences;
    }

    [Fact]
    public void GameLogic_DoesNotReferenceTheRenderer()
    {
        var offences = Offences(
            path => !IsUnder(path, "Desktop", "Core"),
            new Regex(@"\bRaylib\b|\brlImGui\b|\bImGuiNET\b|\bImGui\.|text_survival\.Desktop"));

        Assert.True(offences.Count == 0,
            "Only Desktop/ and Core/ may reference the renderer:\n" + string.Join("\n", offences));
    }

    [Fact]
    public void Simulation_DoesNotReferenceTheUi()
    {
        var offences = Offences(
            path => IsUnder(path, "Actors", "Environments", "Survival", "Bodies", "Items", "Effects"),
            new Regex(@"\.Ui\b|\bIGameUi\b"));

        Assert.True(offences.Count == 0,
            "The simulation must not talk to the player - queue a notice instead:\n" + string.Join("\n", offences));
    }

    [Fact]
    public void ExactlyOneFile_BeginsDrawing()
    {
        // Spelled in pieces so that grepping the tree for the call still finds exactly
        // one file - this one would otherwise be a second hit.
        string beginDrawing = "Raylib." + "Begin" + "Drawing";

        var files = SourceFiles()
            .Where(f => f.Lines.Any(l => l.Contains(beginDrawing)))
            .Select(f => f.Path)
            .ToList();

        Assert.True(files.Count == 1,
            "There must be exactly one frame composition:\n" + string.Join("\n", files));
    }

    [Fact]
    public void GameLogic_DoesNotBlockOrThread()
    {
        var offences = Offences(
            path => !IsUnder(path, "Desktop", "Core"),
            new Regex(@"Task\.Run\(|Task\.Delay\(|\.Result\b|\.Wait\(\)|ConfigureAwait\(|async void"));

        Assert.True(offences.Count == 0,
            "The scheduler is single-threaded; blocking or threading would deadlock it:\n" + string.Join("\n", offences));
    }
}
