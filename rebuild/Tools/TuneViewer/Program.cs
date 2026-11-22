using System.Globalization;
using System.Text;
using System.Text.Json;
using Survival.Tune;

// tune-viewer RUN_DIR [--port P]
// Serves the viewer page and one run folder. The page polls /api/progress for new lines of
// progress.jsonl, so a run in progress is watched live. /api/replay fights one fight of the run
// again, only when the code here is the code the run was fought with (Provenance.ReplayRefusal).

if (args.Length < 1 || args[0].StartsWith("--"))
{
    Console.Error.WriteLine("usage: tune-viewer RUN_DIR [--port P]");
    return 2;
}
string project = ProjectDir();
string tuneDir = Path.GetFullPath(Path.Combine(project, "..", "Tune"));
string runDir = new[] { Path.GetFullPath(args[0]), Path.Combine(tuneDir, args[0]) }.FirstOrDefault(Directory.Exists)
    ?? throw new DirectoryNotFoundException($"No run folder {args[0]} (looked here and under {tuneDir})");
int port = args.Length >= 3 && args[1] == "--port" ? int.Parse(args[2], CultureInfo.InvariantCulture) : 5177;
string repo = Provenance.RepoRoot(project);
string icons = Path.Combine(repo, "assets", "icons");
string progressPath = Path.Combine(runDir, ProgressLog.FileName);
string scratch = Path.Combine(Path.GetTempPath(), $"tune-viewer-{Environment.ProcessId}");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = project, WebRootPath = Path.Combine(project, "wwwroot") });
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-store" });

var json = new JsonSerializerOptions(ProgressLog.Json);

app.MapGet("/api/run", () => Results.Json(new
{
    name = Path.GetFileName(runDir),
    path = runDir,
    hasProgress = File.Exists(progressPath),
    files = Directory.EnumerateFiles(runDir).Select(Path.GetFileName).Order().ToList(),
    now = Provenance.Current(repo),
}, json));

// The log from a byte offset to its last whole line; the next offset is in X-Next-Offset.
app.MapGet("/api/progress", (long? from, HttpResponse res) =>
{
    if (!File.Exists(progressPath)) return Results.NotFound("This run has no progress.jsonl: it predates the run log.");
    using var f = new FileStream(progressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    long start = Math.Clamp(from ?? 0, 0, f.Length);
    f.Seek(start, SeekOrigin.Begin);
    var buf = new byte[f.Length - start];
    f.ReadExactly(buf);
    int end = Array.LastIndexOf(buf, (byte)'\n') + 1;
    res.Headers["X-Next-Offset"] = (start + end).ToString(CultureInfo.InvariantCulture);
    return Results.Text(Encoding.UTF8.GetString(buf, 0, end), "application/x-ndjson");
});

// The run's other files (convergence.tsv, RESULT.md, the specs), by bare name.
app.MapGet("/api/file/{name}", (string name) =>
{
    if (name.Contains('/') || name.Contains('\\') || name.StartsWith('.')) return Results.BadRequest();
    string p = Path.Combine(runDir, name);
    return File.Exists(p) ? Results.File(p, name.EndsWith(".json") ? "application/json" : "text/plain") : Results.NotFound();
});

app.MapGet("/icons/{**path}", (string path) =>
{
    string p = Path.GetFullPath(Path.Combine(icons, path));
    return p.StartsWith(icons + Path.DirectorySeparatorChar) && p.EndsWith(".png") && File.Exists(p) ? Results.File(p, "image/png") : Results.NotFound();
});

// One fight again. candidate is an index, or "mean". Answers NDJSON: one replay line, then one
// line per round when the fight could be watched.
app.MapGet("/api/replay", async (int gen, string candidate, string scenario, int fight, HttpResponse res) =>
{
    if (!File.Exists(progressPath)) return Results.Problem("This run has no progress.jsonl, so its fights cannot be found again.", statusCode: 404);
    var (header, gens) = ProgressLog.Read(progressPath);
    var refusal = Provenance.ReplayRefusal(header.Provenance, Provenance.Current(repo));
    if (refusal != null) return Results.Problem(refusal, statusCode: 409, title: "Replay refused");
    var g = gens.FirstOrDefault(x => x.Generation == gen);
    if (g == null) return Results.Problem($"No generation {gen} in the log yet.", statusCode: 404);
    int? cand = candidate == "mean" ? null : int.Parse(candidate, CultureInfo.InvariantCulture);
    ReplayJob job;
    try { job = Replays.Resolve(header, g, cand, scenario, fight); }
    catch (ArgumentException e) { return Results.Problem(e.Message, statusCode: 400); }
    var r = await Task.Run(() => Workers.Replay(job, scratch));
    string? recorded = Replays.Recorded(g, cand, scenario, fight);
    res.ContentType = "application/x-ndjson";
    await res.WriteAsync(JsonSerializer.Serialize(new
    {
        type = "replay",
        gen,
        candidate,
        scenario,
        fight,
        seed = job.Seed,
        outcome = r.Outcome,
        recorded,
        matchesRecord = recorded == null ? (bool?)null : recorded == r.Outcome,
        gridSize = r.GridSize,
        traceNote = r.TraceNote,
        mean = cand == null,
    }, json) + "\n");
    foreach (var frame in r.Frames ?? []) await res.WriteAsync(JsonSerializer.Serialize(frame, json) + "\n");
    return Results.Empty;
});

Console.WriteLine($"Tune viewer: http://127.0.0.1:{port}/  ({runDir})");
app.Run();
return 0;

// rebuild/Tools/TuneViewer, found from wherever the build put this executable.
static string ProjectDir()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "TuneViewer.csproj"))) return dir.FullName;
    throw new DirectoryNotFoundException("Cannot find rebuild/Tools/TuneViewer above " + AppContext.BaseDirectory);
}
