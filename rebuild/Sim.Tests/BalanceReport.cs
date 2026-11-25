using Survival;
using Xunit.Abstractions;

/// What a set of reference bodies can do: strength, speed, dexterity and perception, each as a share of a fresh
/// body of the same species and sex (SpeciesTable.Average), and vitality. Strength and speed are the shares the
/// world reads (SpeciesTable.StrengthPct, and Running's SpeedPct with its floor). Each body is built through the sim's own
/// code: wounds by tissue condition or Body.Damage, deprivation by stepping Body.Update a minute at a time with the
/// other needs kept topped up. It reports and never gates; no number in it is asserted. It runs only with
/// BALANCE_REPORT set: BALANCE_REPORT=1 dotnet test rebuild/Sim.Tests -c Release --filter BalanceReport
public class BalanceReport(ITestOutputHelper output)
{
    [ReportFact("BALANCE_REPORT")]
    public void Print() => output.WriteLine(ReferenceBodies.Table());
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
}

public static class ReferenceBodies
{
    public sealed record Faculties(double StrengthPct, double SpeedPct, double DexterityPct, double PerceptionPct, double Vitality);

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
        ("starved to the floors", Species.Human, Starved),
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
            Body.Vitality(body, species));
    }

    public static string Table()
    {
        int width = All.Max(b => b.Name.Length);
        var lines = new List<string> { $"{"body".PadRight(width)}  strength  speed  dexterity  perception  vitality" };
        foreach (var (name, species, build) in All)
        {
            var shares = Measure(species, build());
            lines.Add($"{name.PadRight(width)}  {shares.StrengthPct,8:0.00}  {shares.SpeedPct,5:0.00}  {Share(shares.DexterityPct),9}  {shares.PerceptionPct,10:0.00}  {shares.Vitality,8:0.00}");
        }
        return string.Join('\n', lines);
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

    /// No food until a minute burns neither fat nor muscle: both floors reached. Watered and rested every hour.
    static BodyState Starved()
    {
        var body = Rested(Person());
        double fat = body.FatKg, muscle = body.MuscleKg;
        Live(body, new(Water: true, Food: false, Rest: true), _ =>
        {
            bool floors = body.CaloriesKcal == 0 && body.FatKg == fat && body.MuscleKg == muscle;
            (fat, muscle) = (body.FatKg, body.MuscleKg);
            return floors;
        });
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
