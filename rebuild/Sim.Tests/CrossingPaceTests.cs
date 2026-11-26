using CsCheck;
using Survival.Tests.Verification;

namespace Survival.Tests;

/// A crossing goes at the walker's speed each minute, and no walker that can move is slower than a tenth of a fresh
/// one (#288). Each world is hand-built: a row of two open tiles between mountains, nothing else alive.
public class CrossingPaceTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Calm = new(0, WeatherCondition.Clear, -5, 0, 0, 0.2, WindDirection.North);
    static readonly TileId West = new(1, 1), East = new(2, 1);

    /// Four tiles by three, mountains round a row of the two given tiles, the weather held.
    static World Row(Terrain west, Terrain east, WeatherSample sky)
    {
        const int width = 4, height = 3;
        var terrain = new Terrain[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++) terrain[x, y] = Terrain.Mountain;
        terrain[West.X, West.Y] = west;
        terrain[East.X, East.Y] = east;
        var map = new GeneratedMap(width, height, terrain, new Cave[width, height], new int?[width, height], [], [], new(0, 0),
            new() { [new(width - 1, 0)] = 5 });
        var w = new World { Seed = 1, GeneratedMap = map, FixedWeather = sky, PoolCache = [] };
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++) w.PoolCache[new TileId(x, y)] = [];
        return w;
    }

    static World WithPerson(BodyState body, Terrain ground = Terrain.Hills)
    {
        var w = Row(ground, ground, Calm);
        w.Agents[P] = new Agent { Id = P, Tile = West, Members = [body] };
        return w;
    }

    static World Reload(World saved, World built) => TestWorlds.Load(Save.ToJson(saved), built.GeneratedMap);

    static void SetLayers(BodyState body, string region, double condition)
    {
        foreach (var layer in body.Regions.Single(r => r.Name == region).Layers) layer.Condition = condition;
    }

    /// Steps the walk east a minute at a time, calling during(minute walked) after each, until it arrives.
    static int Walk(World w, AgentId who, Action<int>? during = null)
    {
        for (int minute = 1; minute < 10_000; minute++)
        {
            var r = Sim.Step(w, new Travel(who, Direction.East), 1);
            if (r.InterruptedReason == Traversal.Arrived) return minute;
            Assert.True(r.InterruptedReason is null or "done", r.InterruptedReason);
            during?.Invoke(minute);
        }
        throw new InvalidOperationException("never arrived");
    }

    [Fact]
    public void ABodyAtSpeedZero_IsRefusedTravel_WithItsReasons()
    {
        var legless = BodyState.BaselineHuman();
        SetLayers(legless, "Left Leg", 0);
        SetLayers(legless, "Right Leg", 0);
        var w = WithPerson(legless);
        var r = Sim.Step(w, new Travel(P, Direction.East), 60);
        Assert.Equal((Traversal.CannotWalkPrefix + "no working leg", 0), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Null(w.Agent(P).Crossing);
    }

    /// One leg gone and the other at a hundredth: below the floor.
    static BodyState Weak()
    {
        var weak = BodyState.BaselineHuman();
        SetLayers(weak, "Left Leg", 0);
        SetLayers(weak, "Right Leg", 0.01);
        return weak;
    }

    static readonly Terrain[] Open = Enum.GetValues<Terrain>().Where(t => t.IsPassable()).ToArray();

    static readonly Gen<WeatherSample> AnySky =
        Gen.Select(Gen.Enum<WeatherCondition>(), Gen.Double[-35, 15], Gen.Double[0, 1], Gen.Double[0, 1], Gen.Double[0, 1])
            .Select((condition, c, wind, precipitation, cloud) => new WeatherSample(0, condition, c, wind, precipitation, cloud, WindDirection.West));

    static readonly Gen<double> MostlyWhole = Gen.Frequency((1, Gen.Const(1.0)), (2, Gen.Double[0, 1]), (1, Gen.Const(0.0)), (1, Gen.Double[0, 0.02]));

    /// Any species and sex, wounded anywhere, any blood and effects: built from a fresh body, never stronger.
    static readonly Gen<(Species Species, BodyState Body)> AnyWoundedBody =
        Gen.Select(Gen.Enum<Species>(), Gen.Enum<Sex>(), MostlyWhole.Array[48], Gen.Double[0.5, 1], Gen.Select(Gens.Kind, Gens.Severity).Array[0, 4])
            .Select((species, sex, conditions, blood, effects) =>
            {
                var b = SpeciesTable.Average(species, sex);
                int i = 0;
                foreach (var t in b.Regions.SelectMany(r => r.Layers.Concat(r.Organs))) t.Condition = conditions[i++ % conditions.Length];
                b.BloodPct = blood;
                foreach (var (kind, severity) in effects) Effects.Set(b, kind, severity);
                return (species, b);
            });

    /// Over any body that can walk, any load it can carry, pace, ground and weather: a crossing takes at least what a
    /// fresh body of its species and sex takes with the same share of its capacity, and no more than ten times that.
    [Fact]
    public void ACrossing_TakesFromOnceToTenTimesAFreshBodysCrossing_ForAnyBodyThatCanWalk()
    {
        Gen.Select(AnyWoundedBody, Gen.Double[0, 0.99], Gen.Enum<Pace>(), Gen.Int[0, Open.Length - 1].Array[2], AnySky).Check(t =>
        {
            var ((species, body), load, pace, ground, sky) = t;
            var w = Row(Open[ground[0]], Open[ground[1]], sky);
            if (Map.Blocked(Map.Of(w), West, East) != null || Body.SpeedCrippledBy(body, new AbilityContext(load)).Count > 0) return;
            var fresh = SpeciesTable.Average(species, body.Sex);
            int freshMinutes = Traversal.CrossingMinutes(w, fresh, West, East, pace, sky, load, species);
            int minutes = Traversal.CrossingMinutes(w, body, West, East, pace, sky, load, species);
            Assert.InRange(minutes, freshMinutes, 10 * freshMinutes);
        });
    }

    /// The crossing is not priced once at the start: a quadruped that sets off with legs at a hundredth of their
    /// condition and is mended to nine tenths after a minute walks the rest at nearly its fresh speed. One such leg
    /// (the issue's case) leaves three sound ones, and the caribou sets off barely slowed; all four set it off at the
    /// floor, ten times the fresh crossing, which it used to owe all the way across.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void AnAnimalSettingOffOnLegsAtAHundredth_AndMendedAfterAMinute_ArrivesWithinTwiceTheFreshTime(int legs)
    {
        var w = Row(Terrain.Hills, Terrain.Hills, Calm);
        var fresh = Agents.Spawn(w, Species.Caribou, West);
        int freshMinutes = Trip.StepMinutes(w, fresh, West, East, Pace.Quick);
        Agents.Remove(w, fresh);

        var id = Agents.Spawn(w, Species.Caribou, West);
        var body = w.Agent(id).Body;
        var bad = body.Regions.Where(r => r.Name.EndsWith("Leg")).Take(legs).Select(r => r.Name).ToList();
        Assert.Equal(legs, bad.Count);
        foreach (var leg in bad) SetLayers(body, leg, 0.01);
        int setOff = Trip.StepMinutes(w, id, West, East, Pace.Quick);
        Assert.True(setOff > freshMinutes, $"{setOff} min on the bad legs, {freshMinutes} fresh");
        if (legs == 4) Assert.True(setOff > 5 * freshMinutes, $"{setOff} min on the bad legs, {freshMinutes} fresh");

        int minutes = Walk(w, id, minute =>
        {
            if (minute == 1) foreach (var leg in bad) SetLayers(body, leg, 0.9);
        });
        Assert.True(minutes <= 2 * freshMinutes, $"{minutes} min, {freshMinutes} fresh");
    }

    [Fact]
    public void APersonWoundedHalfwayAcross_ArrivesLater_AndOneDroppingHalfTheLoad_Sooner()
    {
        int Crossing(Action<World, int, int> halfway)
        {
            var w = WithPerson(BodyState.BaselineHuman());
            var pack = Items.Pack(w, P);
            TestKit.Give(pack, Resource.Stone, 0.9 * Items.CapacityKg(pack));
            int forecast = Trip.StepMinutes(w, P, West, East, Pace.Quick);
            return Walk(w, P, minute => halfway(w, minute, forecast));
        }
        int unchanged = Crossing((_, _, _) => { });
        int wounded = Crossing((w, minute, forecast) =>
        {
            if (minute == forecast / 2) SetLayers(w.Agent(P).Body, "Left Leg", 0.2);
        });
        int unloaded = Crossing((w, minute, forecast) =>
        {
            if (minute != forecast / 2) return;
            TestKit.Remove(Items.Pack(w, P), Resource.Stone);
            TestKit.Give(Items.Pack(w, P), Resource.Stone, 0.45 * Items.CapacityKg(Items.Pack(w, P)));
        });
        Assert.True(wounded > unchanged, $"wounded {wounded}, unchanged {unchanged}");
        Assert.True(unloaded < unchanged, $"unloaded {unloaded}, unchanged {unchanged}");
    }

    /// A body, load and weather that do not change on the way: the forecast is the stepped time, however the walk is
    /// cut into steps, and across a save and load part way.
    [Fact]
    public void TheForecast_IsTheSteppedTime_ForABodyThatDoesNotChange_HoweverSteppedOrSaved()
    {
        foreach (var body in new Func<BodyState>[] { () => BodyState.BaselineHuman(), Weak })
            foreach (var ground in new[] { Terrain.Plain, Terrain.Hills, Terrain.Rock })
                foreach (var pace in new[] { Pace.Quick, Pace.Careful })
                {
                    var w = WithPerson(body(), ground);
                    int forecast = Trip.StepMinutes(w, P, West, East, pace);
                    Assert.Equal(forecast, Trip.Time(w, P, Direction.East, pace).TotalMinutes);
                    var whole = Reload(w, w);
                    var r = Sim.Step(whole, new Travel(P, Direction.East, pace), Verbs.UntilDoneMinutes);
                    Assert.Equal((Traversal.Arrived, forecast), (r.InterruptedReason, r.MinutesElapsed));

                    var cut = Reload(w, w);
                    int walked = 0;
                    string? reason = null;
                    while (reason != Traversal.Arrived)
                    {
                        var step = Sim.Step(cut, new Travel(P, Direction.East, pace), 3);
                        (walked, reason) = (walked + step.MinutesElapsed, step.InterruptedReason);
                        cut = Reload(cut, w);
                    }
                    Assert.Equal(forecast, walked);
                }
    }

    /// At the floor a crossing is at most ten times a fresh one, so the one leg at a hundredth that used to be quoted
    /// days is quoted hours.
    [Fact]
    public void OneLegGoneAndTheOtherAtAHundredth_WalksAtTheFloor()
    {
        var weak = Weak();
        Assert.True(Body.Speed(weak) < SpeciesTable.MinSpeedPct * Body.Speed(BodyState.BaselineHuman()));
        Assert.Equal(SpeciesTable.MinSpeedPct * Body.Speed(BodyState.BaselineHuman()), SpeciesTable.WalkingSpeed(Species.Human, weak), 12);
        int fresh = Trip.StepMinutes(WithPerson(BodyState.BaselineHuman()), P, West, East, Pace.Quick);
        int floored = Trip.StepMinutes(WithPerson(Weak()), P, West, East, Pace.Quick);
        Assert.InRange(floored, 5 * fresh, 10 * fresh);
    }
}
