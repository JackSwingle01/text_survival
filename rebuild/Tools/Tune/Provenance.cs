using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Survival.Tune;

/// Which code a run was fought with: the git commit, whether the fight's code had uncommitted
/// changes, and a hash of that code as it stood on disk. A replay is honest only when the code
/// that fights it now is the code that fought the run.
public sealed record Provenance(string? Commit, bool Dirty, string SourceHash, string Tool)
{
    /// The directories whose files decide a fight: the simulation and its content tables, and the
    /// harness that sets fights up. Run folders and band/parameter specs are not code (a run keeps
    /// its own copies of the specs).
    public static readonly string[] FightCode = ["rebuild/Sim", "rebuild/Tools/Tune"];
    static readonly string[] Skipped = ["bin", "obj", "runs", "specs"];
    static readonly string[] Extensions = [".cs", ".json", ".csproj"];

    public const string ToolVersion = "tune progress-log 1";

    /// The repository holding rebuild/Tools/Tune, found upward from a directory inside it.
    public static string RepoRoot(string from)
    {
        for (var dir = new DirectoryInfo(from); dir != null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "rebuild", "Sim")) && (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git"))))
                return dir.FullName;
        throw new DirectoryNotFoundException("No git repository with rebuild/Sim above " + from);
    }

    public static Provenance Current(string repoRoot)
    {
        string? commit = Git(repoRoot, "rev-parse", "HEAD")?.Trim();
        var status = Git(repoRoot, ["status", "--porcelain", "--", .. FightCode, ":(exclude)rebuild/Tools/Tune/runs"]);
        return new Provenance(commit, status == null || status.Trim().Length > 0, SourceHashOf(repoRoot), ToolVersion);
    }

    /// SHA-256 over every fight-code file's path and bytes, in ordinal path order.
    public static string SourceHashOf(string repoRoot)
    {
        using var sha = SHA256.Create();
        var files = FightCode.SelectMany(d => Files(Path.Combine(repoRoot, d)))
            .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/')).Order(StringComparer.Ordinal);
        foreach (var rel in files)
        {
            var name = Encoding.UTF8.GetBytes(rel + "\n");
            sha.TransformBlock(name, 0, name.Length, null, 0);
            var bytes = File.ReadAllBytes(Path.Combine(repoRoot, rel));
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    static IEnumerable<string> Files(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
            if (Extensions.Contains(Path.GetExtension(f))) yield return f;
        foreach (var d in Directory.EnumerateDirectories(dir))
            if (!Skipped.Contains(Path.GetFileName(d)) && !Path.GetFileName(d).EndsWith(".Tests"))
                foreach (var f in Files(d)) yield return f;
    }

    /// Why a replay of a run fought with `run` cannot be shown with the code here now, or null if it can.
    /// The commit must match and the fight code must hash the same: a dirty tree whose fight code is
    /// byte for byte what the run used is still the same fight.
    public static string? ReplayRefusal(Provenance run, Provenance now)
    {
        if (run.Commit == null) return "The run recorded no git commit, so there is no way to know which code fought it.";
        if (now.Commit != run.Commit) return $"The run was fought at commit {Short(run.Commit)}; the code here is at {Short(now.Commit)}. Check out {Short(run.Commit)} to replay it.";
        if (now.SourceHash != run.SourceHash)
            return $"The fight code ({string.Join(", ", FightCode)}) has changed since the run (uncommitted edits{(run.Dirty ? ", or the run's own uncommitted edits are gone" : "")}). "
                + "A replay now would show a fight that did not happen.";
        return null;
    }

    static string Short(string? c) => c == null ? "(none)" : c[..Math.Min(10, c.Length)];

    static string? Git(string repoRoot, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = repoRoot };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }
}
