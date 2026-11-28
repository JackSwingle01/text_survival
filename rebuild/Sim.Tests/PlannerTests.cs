using Survival;
using Xunit;

/// Animals find water and rest through the shared planner (spec 2026-11-22-animal-life, "The planner"):
/// driven through the world on a hand-built strip of plain, read from where animals went and what their bodies did.
public class PlannerTests
{
    /// A July day, mild and dry: lakes open, no snow.
    static readonly WeatherSample Mild = new(0, WeatherCondition.Clear, 15, 0.1, 0, 0.2, WindDirection.North);

    internal const int Width = 40;

    /// A plain Width tiles long and `rows` deep (a corridor between mountains when rows is 1), with the camp and the
    /// pass's exit in the top corners, these tiles open water, and nothing growing anywhere.
    internal static World Strip(WeatherSample? sky = null, int rows = 3, params TileId[] water)
    {
        int height = rows + 2;
        var terrain = new Terrain[Width, height];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < height; y++) terrain[x, y] = y == 0 || y == height - 1 ? Terrain.Mountain : Terrain.Plain;
        foreach (var t in water) terrain[t.X, t.Y] = Terrain.Water;
        var map = new GeneratedMap(Width, height, terrain, new Cave[Width, height], new int?[Width, height], [], [], new(0, 0),
            new() { [new(Width - 1, 0)] = 5 });
        var w = new World { Seed = 1, GeneratedMap = map, FixedWeather = sky ?? Mild };
        w.PoolCache = [];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < height; y++) w.PoolCache[new TileId(x, y)] = [];
        return w;
    }

    internal static Agent Animal(World w, Species species, TileId at, double hydrationPct = 1, double sleepDebtMinutes = 0)
    {
        var a = w.Agent(Agents.Spawn(w, species, at));
        a.Body.HydrationMl = hydrationPct * Body.MaxHydrationMlOf(a.Body);
        a.Body.SleepDebtMinutes = sleepDebtMinutes;
        return a;
    }

    static TileId T(int x, int y = 1) => new(x, y);

    static void Feed(Agent a) => a.Body.CaloriesKcal = Body.CaloriesCapacityKcal(a.Body);

    [Fact]
    public void AThirstyAnimal_GoesToTheNearerOfTwoWaters()
    {
        var w = Strip(water: [T(4, 2), T(30, 2)]);
        var caribou = Animal(w, Species.Caribou, T(12, 2), hydrationPct: 0.4);
        double before = caribou.Body.HydrationPct;
        var visited = new HashSet<TileId>();
        for (int i = 0; i < 8 * 60; i++)
        {
            Sim.Advance(w, 1);
            visited.Add(caribou.Tile);
        }
        Assert.Contains(visited, t => t.X <= 5);
        Assert.DoesNotContain(visited, t => t.X >= 20);
        Assert.True(caribou.Body.HydrationPct > before, $"hydration {before:F2} -> {caribou.Body.HydrationPct:F2}");
    }

    /// A corridor with water at its west end and a wolf lying beside it: the only way to drink is past the wolf.
    static (World World, Agent Caribou) WolfAtTheWater(double hydrationPct)
    {
        var w = Strip(rows: 1, water: [T(0)]);
        var wolf = Animal(w, Species.Wolf, T(1));
        Feed(wolf);
        return (w, Animal(w, Species.Caribou, T(10), hydrationPct));
    }

    [Fact]
    public void AMerelyThirstyAnimal_WontDrinkWhereAWolfIs()
    {
        var (w, caribou) = WolfAtTheWater(0.4);
        double before = caribou.Body.HydrationPct;
        for (int i = 0; i < 6 * 60; i++)
        {
            Sim.Advance(w, 1);
            Assert.True(caribou.Tile.X >= 3, $"minute {i}: at {caribou.Tile}, beside the wolf");
        }
        Assert.True(caribou.Body.HydrationPct < before);
    }

    [Fact]
    public void ACriticallyThirstyAnimal_DrinksPastTheWolf()
    {
        var (w, caribou) = WolfAtTheWater(0.1);
        double before = caribou.Body.HydrationPct;
        bool drank = false;
        for (int i = 0; i < 6 * 60 && !drank && w.Agents.ContainsKey(caribou.Id); i++)
        {
            Sim.Advance(w, 1);
            drank = caribou.Body.HydrationPct > before;
        }
        Assert.True(drank, "it never drank");
        Assert.True(caribou.Tile.X <= 1);
    }

    [Fact]
    public void AnAnimal_InADrySnowlessPlace_DiesOfThirst()
    {
        var w = Strip();
        var caribou = Animal(w, Species.Caribou, T(20), hydrationPct: 0.3);
        var events = new List<WorldEvent>();
        for (int day = 0; day < 6 && w.Agents.ContainsKey(caribou.Id); day++) events.AddRange(Sim.Advance(w, 1440));
        Assert.False(w.Agents.ContainsKey(caribou.Id), "still alive after six dry days");
        var carcass = Assert.Single(w.Tiles.Values.SelectMany(t => t.Carcasses ?? []));
        Assert.Equal("thirst", carcass.Cause?.What);
    }

    /// Snow is water at a price: about 117 kcal a litre to melt and warm (spec "Sources"). A wolf, which does not graze,
    /// so every calorie it loses is its burn or the snow's.
    [Fact]
    public void AnAnimal_OnSnow_StaysWatered_AndPaysForItInCalories()
    {
        var w = Strip(sky: new WeatherSample(0, WeatherCondition.LightSnow, -10, 0.1, 0.8, 0.8, WindDirection.North));
        Sim.Advance(w, 1440); // a day's snow on the ground
        var wolf = Animal(w, Species.Wolf, T(20), hydrationPct: 0.4);
        var body = wolf.Body;
        double KcalHeld() => body.CaloriesKcal + body.FatKg * Body.KcalPerKgFat + body.MuscleKg * Body.KcalPerKgMuscle;
        double drankMl = 0, burnKcal = 0, held = KcalHeld();
        for (int i = 0; i < 3 * 1440; i++)
        {
            double ml = body.HydrationMl, debt = body.SleepDebtMinutes;
            burnKcal += Fauna.BurnKcalPerMinute(body);
            Sim.Advance(w, 1);
            drankMl += Math.Max(0, body.HydrationMl - ml + Body.WaterLossMlPerMinute(body, Asleep(wolf, debt)));
            Assert.True(body.HydrationPct > Priorities.WaterCriticalPct, $"minute {i}: hydration {body.HydrationPct:F2}");
        }
        Assert.True(drankMl > 1000, $"drank {drankMl:F0} ml");
        double lost = held - KcalHeld();
        Assert.InRange(lost - burnKcal, 0.9 * drankMl * Work.SnowKcalPerMl, 1.1 * drankMl * Work.SnowKcalPerMl);
    }

    [Fact]
    public void AnAnimal_DrinksFromAnOpenPond_AfterRain()
    {
        var w = Strip(sky: new WeatherSample(0, WeatherCondition.Rainy, 5, 0.1, 0.8, 0.8, WindDirection.North));
        Sim.Advance(w, 2 * 1440); // two days' rain stand on the plain
        var caribou = Animal(w, Species.Caribou, T(20), hydrationPct: 0.4);
        Sim.Advance(w, 60);
        Assert.True(caribou.Body.HydrationPct > 0.8, $"hydration {caribou.Body.HydrationPct:F2}");
    }

    [Fact]
    public void AnAnimal_DoesNotDrinkFromAPond_OnceItFreezes()
    {
        var w = Strip(sky: new WeatherSample(0, WeatherCondition.FreezingRain, -12, 0.1, 0.8, 0.8, WindDirection.North));
        Sim.Advance(w, 4 * 1440); // freezing rain stands on the plain and ices over
        Assert.True(Ground.Underfoot(w, T(20)).Iced, "the pond is not iced over");
        var caribou = Animal(w, Species.Caribou, T(20), hydrationPct: 0.4);
        double before = caribou.Body.HydrationPct;
        Sim.Advance(w, 6 * 60);
        Assert.True(caribou.Body.HydrationPct < before, $"hydration {before:F2} -> {caribou.Body.HydrationPct:F2}");
    }

    /// Asleep this minute: its debt fell, or it lay with none owed (the body's view of a minute's Rest).
    static bool Asleep(Agent a, double debtBefore) => a.Body.SleepDebtMinutes < debtBefore || debtBefore == 0 && a.Body.SleepDebtMinutes == 0;

    /// The share of these animals' minutes spent asleep within the hours [from, to) of each day, over `days` days.
    /// Their bodies are kept fed and watered so only rest and the hour decide.
    static double RestShare(Species species, int fromHour, int toHour, int days = 3)
    {
        var w = Strip();
        var pack = Agents.SpawnGroup(w, species, T(20, 2), 4).Select(w.Agent).ToList();
        int resting = 0, counted = 0;
        for (int i = 0; i < days * 1440; i++)
        {
            foreach (var a in pack.Where(a => w.Agents.ContainsKey(a.Id)))
            {
                Feed(a);
                a.Body.HydrationMl = Body.MaxHydrationMlOf(a.Body);
            }
            var debt = pack.Select(a => a.Body.SleepDebtMinutes).ToList();
            Sim.Advance(w, 1);
            int hour = Weather.MinuteOfDay(w.Minute) / 60;
            if (hour < fromHour || hour >= toHour) continue;
            for (int k = 0; k < pack.Count; k++)
            {
                counted++;
                if (Asleep(pack[k], debt[k])) resting++;
            }
        }
        return (double)resting / counted;
    }

    [Fact]
    public void Wolves_RestInTheMiddleOfTheDay_AndAreUpAtDusk()
    {
        Assert.True(RestShare(Species.Wolf, 11, 15) > 0.8, "wolves are up at midday");
        Assert.True(RestShare(Species.Wolf, 19, 23) < 0.5, "wolves lie up at dusk");
    }

    [Fact]
    public void Hyenas_AreUpAtNight_AndRestByDay()
    {
        Assert.True(RestShare(Species.Hyena, 11, 15) > 0.8, "hyenas are up at midday");
        Assert.True(RestShare(Species.Hyena, 21, 24) < 0.5, "hyenas lie up at night");
    }

    /// At noon, a caribou's active hours, a sleep-starved one lies down while a rested one with the same hunger grazes.
    [Fact]
    public void ASleepDeprivedAnimal_RestsInItsActiveHours()
    {
        static bool RestsAtNoon(double debtHours)
        {
            var w = Strip();
            w.PoolCache = null; // the plain grows forage
            Sim.Advance(w, 3 * 60); // 12:00
            var caribou = Animal(w, Species.Caribou, T(20), sleepDebtMinutes: debtHours * 60);
            caribou.Body.CaloriesKcal = 0.4 * Body.CaloriesCapacityKcal(caribou.Body);
            int resting = 0;
            for (int i = 0; i < 60; i++)
            {
                double debt = caribou.Body.SleepDebtMinutes;
                Sim.Advance(w, 1);
                if (Asleep(caribou, debt)) resting++;
            }
            return resting > 50;
        }
        Assert.True(RestsAtNoon(30), "a sleep-deprived caribou stayed up"); // held at Need down to the 18 h onset for over the hour
        Assert.False(RestsAtNoon(0), "a rested, hungry caribou lay down");
    }
}
