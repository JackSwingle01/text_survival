using Survival;
using Xunit.Abstractions;

/// What a set of reference bodies can do: strength, speed, dexterity and perception, each as a share of a fresh
/// body of the same species and sex (SpeciesTable.Average), and vitality. Each body is built through the sim's own
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
    public sealed record Faculties(double Strength, double Speed, double Dexterity, double Perception, double Vitality);

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
            Body.Strength(body) / Body.Strength(fresh),
            Body.Speed(body) / Body.Speed(fresh),
            Body.Dexterity(body) / Body.Dexterity(fresh),
            Body.Perception(body) / Body.Perception(fresh),
            Body.Vitality(body));
    }

    public static string Table()
    {
        int width = All.Max(b => b.Name.Length);
        var lines = new List<string> { $"{"body".PadRight(width)}  strength  speed  dexterity  perception  vitality" };
        foreach (var (name, species, build) in All)
        {
            var f = Measure(species, build());
            lines.Add($"{name.PadRight(width)}  {f.Strength,8:0.00}  {f.Speed,5:0.00}  {f.Dexterity,9:0.00}  {f.Perception,10:0.00}  {f.Vitality,8:0.00}");
        }
        return string.Join('\n', lines);
    }

    static BodyState Person() => SpeciesTable.Average(Species.Human);

    static BodyState Destroyed(BodyState body, params string[] regions)
    {
        foreach (var r in body.Regions.Where(r => regions.Contains(r.Name)))
            foreach (var layer in r.Layers) layer.Condition = 0;
        return body;
    }

    static BodyState HalfBlood()
    {
        var body = Person();
        Body.Damage(body, new DamageInfo(Body.BloodVolumeMl(body) / 2, DamageType.Bleed, BodyTarget.Blood), Rng());
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
        Step(body, hours * 60, water: true, food: true, rest: false);
        return body;
    }

    /// From full, with nothing to drink: fed and rested every hour.
    static BodyState WithoutWater(int hours)
    {
        var body = Rested(Person());
        Step(body, hours * 60, water: false, food: true, rest: true);
        return body;
    }

    /// No food until a minute burns neither fat nor muscle: both floors reached. Watered and rested every hour.
    static BodyState Starved()
    {
        var body = Rested(Person());
        var rng = Rng();
        for (int minute = 0; ; minute++)
        {
            if (minute % 60 == 0) TopUp(body, water: true, food: false, rest: true);
            double fat = body.FatKg, muscle = body.MuscleKg;
            Body.Update(body, Room, rng);
            if (body.CaloriesKcal == 0 && body.FatKg == fat && body.MuscleKg == muscle) return body;
            if (minute > 200 * 1440) throw new InvalidOperationException("never reached the floors");
        }
    }

    static readonly SurvivalContext Room = new() { AirTempF = 70 };

    static void Step(BodyState body, int minutes, bool water, bool food, bool rest)
    {
        var rng = Rng();
        for (int minute = 0; minute < minutes; minute++)
        {
            if (minute % 60 == 0) TopUp(body, water, food, rest);
            Body.Update(body, Room, rng);
        }
    }

    static void TopUp(BodyState body, bool water, bool food, bool rest)
    {
        if (water) Body.Drink(body, Body.MaxHydrationMlOf(body));
        if (food) Body.Eat(body, Body.CaloriesCapacityKcal(body) - body.CaloriesKcal);
        if (rest) body.EnergyMinutes = Body.MaxEnergyMinutes;
    }

    static BodyState Rested(BodyState body)
    {
        TopUp(body, water: true, food: true, rest: true);
        return body;
    }

    static Rng Rng() => Survival.Rng.ForSystem(1, "balance report");
}
