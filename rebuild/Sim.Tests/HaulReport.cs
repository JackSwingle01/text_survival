using Survival;
using Xunit.Abstractions;

/// The forage report (#152): a first hour and a day of foraging per material on each ground against the targets
/// (Report.HaulText), and herd numbers on a fixed seed with the forage tables from before #152 and with today's,
/// placed and then thirty days on, so the animals can be retuned from data. It reports and never gates; it runs
/// only with HAUL_REPORT set: HAUL_REPORT=1 dotnet test rebuild/Sim.Tests -c Release --filter HaulReport
public class HaulReport(ITestOutputHelper output)
{
    const int Seed = 17, Days = 30, ChunkDays = 10;

    [ReportFact("HAUL_REPORT")]
    public void Print()
    {
        output.WriteLine(Report.HaulText(Seed));
        output.WriteLine($"# herds, seed {Seed}: animals by species on the day, placed on day 0");
        output.WriteLine("before #152 (Fixtures/forage-before-152):");
        output.WriteLine(Report.CurveText(Herds(World.New(Seed, BeforeTables.Lay))));
        output.WriteLine("after:");
        output.WriteLine(Report.CurveText(Herds(World.New(Seed))));
    }

    static List<CurvePoint> Herds(World w) => [new CurvePoint(0, Report.Populations(w)), .. Report.Curve(w, Days, ChunkDays)];
}

public class HaulReportTests
{
    [Fact]
    public void Haul_HasARowForEveryItemOnEveryGround_AndEveryTargetOnItsGround()
    {
        var rows = Report.Haul(17);
        foreach (var (ground, pool) in Report.HaulGrounds())
            foreach (var r in pool.Items.Select(i => i.Resource).Distinct())
                Assert.Single(rows, x => x.Ground == ground && x.Resource == r);
        foreach (var t in Report.HaulTargets)
            Assert.Equal(t.Kg, rows.Single(x => x.Ground == t.Ground && x.Resource == t.Resource).TargetKg);
    }

    [Fact]
    public void Haul_SampledHourAgreesWithTheForecast_AndADayOnOneTileBringsInLessThanEightFreshHours()
    {
        foreach (var row in Report.Haul(17).Where(r => r.HourKg > 0.05))
        {
            Assert.InRange(row.SampledHourKg, row.HourKg * 0.7, row.HourKg * 1.3);
            Assert.InRange(row.DayKg, row.HourKg, row.HourKg * Report.HaulDayHours);
        }
    }

    [Fact]
    public void HaulNeeds_EachIsGatheredOnSomeGround()
    {
        foreach (var need in Report.HaulNeeds())
        {
            var row = Report.HaulHours(need);
            Assert.InRange(row.Hours, 1 / 60.0, 24);
        }
    }

    [Fact]
    public void BeforeTables_PutTheOldRowsUnderTheTilesForage()
    {
        var w = new World { Seed = 17, GeneratedMap = TestWorlds.Map(17) };
        BeforeTables.Lay(w);
        var camp = Map.Of(w).Camp;
        var forage = (ForagePool)Pools.At(w, camp)[0];
        Assert.Same(BeforeTables.Tables.Forage["MixedForest"], forage.Items);
    }
}

/// The forage tables and site rows as they were before #152 (Fixtures/forage-before-152, copied from main then).
/// Dropped once the animals are retuned to the new forage.
static class BeforeTables
{
    public static readonly ContentTables Tables = Load();

    static ContentTables Load()
    {
        var files = Content.EmbeddedFiles();
        foreach (var name in new[] { "forage.json", "locations.json" }) files[name] = File.ReadAllText(Path.Combine(Dir(), name));
        return Content.Parse(files);
    }

    static string Dir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Sim.Tests.csproj"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "Fixtures", "forage-before-152");
    }

    /// Every tile's pools generated now, each forage pool on its old rows: a terrain's table by name, a site's by its id.
    public static void Lay(World w)
    {
        var m = Map.Of(w);
        var byTable = new Dictionary<object, IReadOnlyList<ForageItem>>(ReferenceEqualityComparer.Instance);
        foreach (var (name, items) in Content.Tables.Forage) byTable[items] = Tables.Forage[name];
        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                var t = new TileId(x, y);
                var pools = Pools.At(w, t);
                if (pools.Count == 0 || pools[0] is not ForagePool f) continue;
                var old = byTable.TryGetValue(f.Items, out var table) ? table
                    : Tables.Locations.Single(l => l.Id == m.SiteAt(t)!.Id).All.OfType<SiteForage>().First().Items;
                w.PoolCache![t] = [f with { Items = old }, .. pools.Skip(1)];
            }
    }
}
