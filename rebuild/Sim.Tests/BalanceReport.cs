using Survival;
using Xunit.Abstractions;

/// What a set of reference bodies can do: strength, speed, dexterity, perception and vision, each as a share of a fresh
/// body of the same species and sex (SpeciesTable.Average), and vitality. Strength and speed are the shares the
/// world reads (SpeciesTable.StrengthPct, and Running's SpeedPct with its floor). Butcher and forage are the times their
/// whole-body time each takes (Work.TimeFactor: by dexterity, by speed); a body without hands butchers nothing: "-". Each body is built through the sim's own
/// code: wounds by tissue condition or Body.Damage, deprivation by stepping Body.Update a minute at a time with the
/// other needs kept topped up. It reports and never gates; no number in it is asserted. It runs only with
/// BALANCE_REPORT set: BALANCE_REPORT=1 dotnet test rebuild/Sim.Tests -c Release --filter BalanceReport
public class BalanceReport(ITestOutputHelper output)
{
    [ReportFact("BALANCE_REPORT")]
    public void Print()
    {
        output.WriteLine(ReferenceBodies.Table());
        output.WriteLine("");
        output.WriteLine(ReferenceBodies.Timelines());
        output.WriteLine("");
        output.WriteLine(ReferenceBodies.Starvation());
    }
}

public class BalanceReportTests
{
    [Fact]
    public void Table_HasARowForEveryReferenceBody()
    {
        var lines = ReferenceBodies.Table().Split('\n');
        foreach (var (name, _, _) in ReferenceBodies.All)
            Assert.Single(lines, l => l.StartsWith(name + "  ", StringComparison.Ordinal));
    }

    [Fact]
    public void Timelines_PrintEachTimeline()
    {
        var lines = ReferenceBodies.Timelines().Split('\n');
        foreach (var title in ReferenceBodies.TimelineTitles) Assert.Single(lines, l => l == title);
    }

    /// Two bodies starved to death a minute at a time: a long run.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void Starvation_PrintsEachBody_ToItsDeath()
    {
        var lines = ReferenceBodies.Starvation().Split('\n');
        foreach (var title in ReferenceBodies.StarvationTitles) Assert.Single(lines, l => l == title);
        Assert.Equal(ReferenceBodies.StarvationTitles.Length, lines.Count(l => l.StartsWith("dead on day ", StringComparison.Ordinal)));
    }
}

public static class ReferenceBodies
{
    public sealed record Faculties(double StrengthPct, double SpeedPct, double DexterityPct, double PerceptionPct, double VisionPct, double Vitality,
        double ButcherTimeFactor, double ForageTimeFactor);

    public static readonly IReadOnlyList<(string Name, Species Species, Func<BodyState> Build)> All =
    [
        ("whole", Species.Human, Person),
        ("one arm destroyed", Species.Human, () => Destroyed(Person(), "Left Arm")),
        ("both arms destroyed", Species.Human, () => Destroyed(Person(), "Left Arm", "Right Arm")),
        ("one leg destroyed", Species.Human, () => Destroyed(Person(), "Left Leg")),
        ("both legs destroyed", Species.Human, () => Destroyed(Person(), "Left Leg", "Right Leg")),
        ("half its blood lost", Species.Human, HalfBlood),
        ("a blow to the head", Species.Human, BlowToTheHead),
        ("sixteen hours awake", Species.Human, () => Awake(16)),
        ("forty hours awake", Species.Human, () => Awake(40)),
        ("a day and a half without water", Species.Human, () => WithoutWater(36)),
        ("starved to half its muscle", Species.Human, Starved),
        ("a whole wolf", Species.Wolf, () => SpeciesTable.Average(Species.Wolf)),
        ("a wolf, one leg destroyed", Species.Wolf, () => Destroyed(SpeciesTable.Average(Species.Wolf), "Front Left Leg")),
    ];

    public static Faculties Measure(Species species, BodyState body)
    {
        var fresh = SpeciesTable.Average(species, species == Species.Human ? body.Sex : Sex.Male);
        return new(
            SpeciesTable.StrengthPct(species, body),
            SpeciesTable.RunningIfAble(species, body)?.SpeedPct ?? 0,
            Body.Dexterity(body) / Body.Dexterity(fresh),
            Body.Perception(body) / Body.Perception(fresh),
            Body.Vision(body) / Body.Vision(fresh),
            Body.Vitality(body, species),
            Work.TimeFactor(Body.Share(body, species, Faculty.Dexterity)),
            Work.TimeFactor(Body.Share(body, species, Faculty.Speed)));
    }

    public static string Table()
    {
        int width = All.Max(b => b.Name.Length);
        var lines = new List<string> { $"{"body".PadRight(width)}  strength  speed  dexterity  perception  vision  vitality  butcher ×  forage ×" };
        foreach (var (name, species, build) in All)
        {
            var shares = Measure(species, build());
            lines.Add($"{name.PadRight(width)}  {shares.StrengthPct,8:0.00}  {shares.SpeedPct,5:0.00}  {Share(shares.DexterityPct),9}  {shares.PerceptionPct,10:0.00}  {shares.VisionPct,6:0.00}  {shares.Vitality,8:0.00}  {Share(double.IsNaN(shares.DexterityPct) ? double.NaN : shares.ButcherTimeFactor),9}  {shares.ForageTimeFactor,8:0.00}");
        }
        return string.Join('\n', lines);
    }

    public const string WithoutAnything = "no food, water or sleep, from full: each hour",
        NormalDay = "a normal day, 16 hours awake and 8 asleep, fed and watered: each hour",
        OneLegDeprived = "one leg destroyed, hungry, thirsty and tired at full";

    public static readonly string[] TimelineTitles = [WithoutAnything, NormalDay, OneLegDeprived];

    /// The deprivation timelines (spec 2026-11-27-deprivation-rethink, "The balance report"): a person's faculties,
    /// vitality and the three severities, hour by hour.
    public static string Timelines()
    {
        var lines = new List<string> { WithoutAnything, TimelineHeader };
        var rng = Rng.ForSystem(1, "balance report");
        var body = Rested(Person());
        for (int hour = 0; hour <= 72; hour++)
        {
            lines.Add(TimelineRow(hour.ToString(), body));
            if (!Body.IsAlive(body))
            {
                lines.Add($"dead at hour {hour}: {Body.CauseOfDeath(body)}");
                break;
            }
            Step(body, awake: true, 60, rng);
        }

        lines.AddRange(["", NormalDay, TimelineHeader]);
        body = Rested(Person());
        for (int hour = 0; hour <= 48; hour++)
        {
            lines.Add(TimelineRow(hour.ToString(), body));
            TopUp(body, new(Water: true, Food: true, Rest: false));
            Step(body, awake: hour % 24 < 16, 60, rng);
        }

        lines.AddRange(["", OneLegDeprived, TimelineHeader]);
        body = Destroyed(Person(), "Left Leg");
        (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (0, 0, 0);
        lines.Add(TimelineRow("-", body));
        lines.Add(Crippled.Refusal(body, Faculty.Speed, default) is { } refused ? $"walks: no, {refused.Reason}" : "walks: yes");
        return string.Join('\n', lines);
    }

    const string TimelineHeader = "hour  strength  speed  dexterity  perception  vision  vitality  hungry  thirsty  tired  alive";

    static string TimelineRow(string hour, BodyState body)
    {
        var s = Measure(Species.Human, body);
        double Felt(Deprivation d) => Deprivations.Severity(body, d);
        return $"{hour,4}  {s.StrengthPct,8:0.00}  {s.SpeedPct,5:0.00}  {s.DexterityPct,9:0.00}  {s.PerceptionPct,10:0.00}  {s.VisionPct,6:0.00}  {s.Vitality,8:0.00}  {Felt(Deprivation.Hungry),6:0.00}  {Felt(Deprivation.Thirsty),7:0.00}  {Felt(Deprivation.Tired),5:0.00}  {(Body.IsAlive(body) ? "yes" : "no"),5}";
    }

    public const string StarvingAverage = "starving, the species-average body from full, watered, 16 hours awake and 8 asleep: each day",
        StarvingGameStart = "starving, a game-start body, watered, 16 hours awake and 8 asleep: each day";

    public static readonly string[] StarvationTitles = [StarvingAverage, StarvingGameStart];

    /// Spec 2026-11-27-deprivation-rethink, "The balance report": fat, muscle, the hunger pang, faculties and
    /// vitality each day of starving, until death.
    public static string Starvation()
    {
        var lines = new List<string>();
        foreach (var (title, body) in new[] { (StarvingAverage, Rested(Person())), (StarvingGameStart, GameStart()) })
        {
            if (lines.Count > 0) lines.Add("");
            lines.AddRange([title, StarvationHeader]);
            var rng = Rng.ForSystem(1, "balance report");
            for (int day = 0; ; day++)
            {
                if (day > 200) throw new InvalidOperationException("the body never starved");
                lines.Add(StarvationRow(day, body));
                for (int hour = 0; hour < 24 && Body.IsAlive(body); hour++)
                {
                    TopUp(body, new(Water: true, Food: false, Rest: false));
                    Step(body, awake: hour < 16, 60, rng);
                }
                if (!Body.IsAlive(body))
                {
                    lines.Add(StarvationRow(day + 1, body));
                    lines.Add($"dead on day {day + 1}: {Body.CauseOfDeath(body)}");
                    break;
                }
            }
        }
        return string.Join('\n', lines);
    }

    const string StarvationHeader = " day  fat kg  muscle kg  strength  speed  dexterity  perception  vision  vitality  hungry  alive";

    static string StarvationRow(int day, BodyState body)
    {
        var s = Measure(Species.Human, body);
        return $"{day,4}  {body.FatKg,6:0.00}  {body.MuscleKg,9:0.00}  {s.StrengthPct,8:0.00}  {s.SpeedPct,5:0.00}  {s.DexterityPct,9:0.00}  {s.PerceptionPct,10:0.00}  {s.VisionPct,6:0.00}  {s.Vitality,8:0.00}  {Deprivations.Severity(body, Deprivation.Hungry),6:0.00}  {(Body.IsAlive(body) ? "yes" : "no"),5}";
    }

    /// The player's body as World.New makes it: autumn fat, the stores three quarters full.
    static BodyState GameStart() => SpeciesTable.NewBody(Species.Human, Rng.ForSystem(1, "balance report"));

    /// Body.Update a minute at a time in a still 70 °F room, awake or asleep; asleep at Sim's sleeping burn.
    static void Step(BodyState body, bool awake, int minutes, Rng rng)
    {
        var room = new SurvivalContext { AirTempF = 70, IsSleeping = !awake, ActivityFactor = awake ? 1.0 : 0.5 };
        for (int m = 0; m < minutes && Body.IsAlive(body); m++) Body.Update(body, room, rng);
    }

    /// A faculty a fresh body of the species lacks (a wolf's dexterity, 0 of 0) is left out of vitality: "-".
    static string Share(double pct) => double.IsNaN(pct) ? "-" : pct.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    static BodyState Person() => SpeciesTable.Average(Species.Human);

    static BodyState Destroyed(BodyState body, params string[] regions)
    {
        foreach (var name in regions)
            foreach (var layer in body.Regions.Single(r => r.Name == name).Layers) layer.Condition = 0;
        return body;
    }

    static BodyState HalfBlood()
    {
        var body = Person();
        Body.Damage(body, new DamageInfo(Body.BloodVolumeMl(body) / 2, DamageType.Bleed, BodyTarget.Blood), Rng.ForSystem(1, "balance report"));
        return body;
    }

    static BodyState BlowToTheHead()
    {
        var body = Person();
        body.Regions.SelectMany(r => r.Organs).Single(o => o.Name == "Brain").Condition = 0.5;
        return body;
    }

    /// Awake from a full night's rest, fed and watered every hour.
    static BodyState Awake(int hours)
    {
        var body = Rested(Person());
        Live(body, new(Water: true, Food: true, Rest: false), minute => minute == hours * 60);
        return body;
    }

    /// From full, with nothing to drink: fed and rested every hour.
    static BodyState WithoutWater(int hours)
    {
        var body = Rested(Person());
        Live(body, new(Water: false, Food: true, Rest: true), minute => minute == hours * 60);
        return body;
    }

    /// No food until hunger begins to harm the heart. Watered and rested every hour.
    static BodyState Starved()
    {
        var body = Rested(Person());
        Live(body, new(Water: true, Food: false, Rest: true), _ => Body.StarvingToDeath(body, Species.Human));
        return body;
    }

    /// Which needs are refilled every hour while the others run down.
    readonly record struct Kept(bool Water, bool Food, bool Rest);

    /// Steps Body.Update a minute at a time in a still 70 °F room until done(minutes lived) says so.
    static void Live(BodyState body, Kept kept, Func<int, bool> done)
    {
        var rng = Rng.ForSystem(1, "balance report");
        var stillRoom = new SurvivalContext { AirTempF = 70 };
        for (int minute = 0; !done(minute); minute++)
        {
            if (minute > 200 * 1440) throw new InvalidOperationException("the body never got there");
            if (minute % 60 == 0) TopUp(body, kept);
            Body.Update(body, stillRoom, rng);
        }
    }

    static void TopUp(BodyState body, Kept kept)
    {
        if (kept.Water) Body.Drink(body, Body.MaxHydrationMlOf(body));
        if (kept.Food) Body.Eat(body, Body.CaloriesCapacityKcal(body) - body.CaloriesKcal);
        if (kept.Rest) body.EnergyMinutes = Body.MaxEnergyMinutes;
    }

    static BodyState Rested(BodyState body)
    {
        TopUp(body, new(Water: true, Food: true, Rest: true));
        return body;
    }
}
