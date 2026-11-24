namespace Survival.Tests;

/// Sim.Feels: the air the person feels doing this here, now, as its terms: the air, the wind and the
/// sky, the shelter (when the activity keeps them still in it), the fire, a torch, bedding asleep. The
/// same Exposure.At the body lives by, read without changing anything.
public class FeelsTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Cold = new(0, WeatherCondition.Cloudy, -15, 0.5, 0, 0.8, WindDirection.North);

    static World ColdCamp(out TileId camp)
    {
        var w = TestWorlds.Bare(17);
        w.FixedWeather = Cold;
        camp = Map.Of(w).Camp;
        return w;
    }

    static double BodyGets(World w, Activity a)
    {
        var s = Sim.Surroundings(w, w.Agent(P), a, []);
        return s.AirTempF + s.FireBonusF + s.TorchHeatF;
    }

    [Theory]
    [InlineData("wait")]
    [InlineData("sleep")]
    [InlineData("walk")]
    public void TheFeltAir_IsWhatTheBodyLivesBy_AndItsTermsAddUp(string what)
    {
        var w = ColdCamp(out _);
        TestKit.Supply(w);
        Sim.Step(w, new Wait(P), 1);
        Activity a = what switch { "wait" => new Wait(P), "sleep" => new Sleep(P), _ => new Travel(P, Direction.East) };
        string before = Save.ToJson(w);
        var f = Sim.Feels(w, P, a);
        Assert.Equal(before, Save.ToJson(w));
        Assert.Equal(BodyGets(w, a), f.FeltF, 9);
        Assert.Equal(f.FeltF, f.Terms.Sum(t => t.DeltaF), 9);
        Assert.Equal("Air", f.Terms[0].Factor);
    }

    /// On a world nothing has stepped yet, feeling the air observes nothing: the save is unchanged.
    [Fact]
    public void Feeling_OnAFreshWorld_WritesNothing()
    {
        var w = TestWorlds.Bare(17);
        string before = Save.ToJson(w);
        Sim.Feels(w, P, new Wait(P));
        Sim.Feels(w, P, new Sleep(P));
        Assert.Equal(before, Save.ToJson(w));
    }

    /// At camp the overhang keeps a still body out of the wind; walking out of it, it does not.
    [Fact]
    public void KeepingStillInTheShelter_FeelsWarmer_ThanWalkingOut()
    {
        var w = ColdCamp(out _);
        Sim.Step(w, new Wait(P), 1);
        var still = Sim.Feels(w, P, new Wait(P));
        var walking = Sim.Feels(w, P, new Travel(P, Direction.East));
        Assert.Contains(still.Terms, t => t.Factor == "Shelter" && t.DeltaF > 0);
        Assert.DoesNotContain(walking.Terms, t => t.Factor == "Shelter");
        Assert.True(still.FeltF > walking.FeltF);
    }

    [Fact]
    public void ABurningFire_IsATerm()
    {
        var w = ColdCamp(out var camp);
        TestKit.Supply(w);
        Sim.Step(w, new Wait(P), 1);
        var light = Verbs.At(w, P).Tile.Single(v => v.Activity is StartFire);
        for (int i = 0; i < 20 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++) Sim.Step(w, light.Activity, 1);
        Sim.Step(w, new AddFuel(P, Resource.Birch, 1), 1);
        Sim.Step(w, new Wait(P), 20);
        Assert.Contains(Sim.Feels(w, P, new Wait(P)).Terms, t => t.Factor == "Fire" && t.DeltaF > 1);
    }
}
