using CsCheck;
using Survival.Tests.Verification;

namespace Survival.Tests;

/// Walking through the faculties (documentation/specs/2026-11-24-a-body-is-read-in-tiers.md, "Refusing and
/// slowing"): a body walks if it has any speed at all, is refused only at zero, and is told why. Nothing turns
/// speed into a time for a lame body: not the walk, its forecast, or the route planner.
public class LameTests
{
    static readonly AgentId P = AgentId.Player;

    static void SetLayers(BodyState b, string region, double condition)
    {
        foreach (var layer in b.Regions.Single(r => r.Name == region).Layers) layer.Condition = condition;
    }

    static BodyState Legless()
    {
        var b = BodyState.BaselineHuman();
        SetLayers(b, "Left Leg", 0);
        SetLayers(b, "Right Leg", 0);
        return b;
    }

    /// The first open way out of where the player stands.
    static Travel OpenWalk(World w) =>
        Enum.GetValues<Direction>().Select(d => new Travel(P, d))
            .First(t => Traversal.Blocked(w, w.Agent(P), w.Agent(P).Tile, Map.Step(w.Agent(P).Tile, t.Dir)) == null);

    static World With(BodyState body)
    {
        var w = TestWorlds.Bare(17);
        w.Agent(P).Members[0] = body;
        return w;
    }

    static void AssertRefusedFor(World w, CrippledBy why, string named)
    {
        var walk = OpenWalk(w);
        var refused = Sim.Refusal(w, walk);
        Assert.NotNull(refused);
        var missing = Assert.IsType<MissingFaculty>(refused.Missing);
        Assert.Equal(Faculty.Speed, missing.Faculty);
        Assert.Contains(why, missing.Because);
        Assert.Contains(named, refused.Reason);
        var r = Sim.Step(w, walk, 60);
        Assert.Equal((refused.Reason, 0), (r.InterruptedReason, r.MinutesElapsed));
    }

    [Fact]
    public void BothLegsDestroyed_IsRefusedWalking_AndTheRefusalNamesTheLegs() =>
        AssertRefusedFor(With(Legless()), CrippledBy.NoWorkingLimb, "leg");

    /// Circulation fails at half the blood, and with it the head: no living body is lame by lost blood alone
    /// (Sim.Refusal says "dead" first). Travel's own refusal still names it.
    [Fact]
    public void LostBlood_IsNamedInTheWalksRefusal()
    {
        var bled = BodyState.BaselineHuman();
        bled.BloodPct = 0.5;
        var w = With(bled);
        var refused = Traversal.Refusal(w, OpenWalk(w));
        Assert.NotNull(refused);
        Assert.Equal(new MissingFaculty(Faculty.Speed, [CrippledBy.LostBlood]).Because, ((MissingFaculty)refused.Missing!).Because);
        Assert.Contains("lost blood", refused.Reason);
    }

    /// Unconscious and alive: deep cold, shock and a daze take the whole of consciousness, but leave the legs some moving.
    [Fact]
    public void Unconscious_IsRefusedWalking_AndTheRefusalNamesIt()
    {
        var knockedOut = BodyState.BaselineHuman();
        foreach (var kind in new[] { EffectKind.Hypothermia, EffectKind.Shock, EffectKind.Dazed }) Effects.Set(knockedOut, kind, 1.0);
        Assert.True(Body.IsAlive(knockedOut));
        Assert.Equal([CrippledBy.Unconscious], Body.SpeedCrippledBy(knockedOut));
        AssertRefusedFor(With(knockedOut), CrippledBy.Unconscious, "unconscious");
    }

    /// Wasted to nothing and worn out, with the pack full: the load is what stops it, and it walks without it.
    [Fact]
    public void AFullPack_OnABodyTooWeakToCarryIt_IsRefusedWalking_AndTheRefusalNamesTheLoad()
    {
        var weak = BodyState.Create(BodyKind.Human, 60, fatPct: 0, musclePct: 0.01);
        Deprive.Tired(weak, 1.0);
        var w = With(weak);
        Assert.Null(Sim.Refusal(w, OpenWalk(w)));
        var me = w.Agent(P);
        var pack = Items.Pack(w, P);
        TestKit.Give(pack, Resource.Stone, Items.CapacityKg(pack) - Items.CarriedKg(me));
        Assert.InRange(Items.EncumbrancePct(me), 0.99, 1.01);
        AssertRefusedFor(w, CrippledBy.Load, "load");
    }

    /// One leg gone and the other at a hundredth: still offered the walk, and a crossing far longer than a whole body's,
    /// though no more than ten times it: the walk is held at a tenth of a fresh body's speed (#288).
    [Fact]
    public void OneLegAtAHundredth_IsOfferedTheWalk_AndItsCrossingIsFarLonger()
    {
        var weak = BodyState.BaselineHuman();
        SetLayers(weak, "Left Leg", 0);
        SetLayers(weak, "Right Leg", 0.01);
        Assert.True(Body.Speed(weak) > 0);
        var w = With(weak);
        var walk = OpenWalk(w);
        var verb = Verbs.At(w, P).Walk.First(v => v.Activity == walk);
        Assert.True(verb.Allowed, verb.Refused?.Reason);

        var whole = With(BodyState.BaselineHuman());
        var wholeVerb = Verbs.At(whole, P).Walk.First(v => v.Activity == walk);
        Assert.InRange(verb.Trip!.TotalMinutes, 2 * wholeVerb.Trip!.TotalMinutes + 1, 10 * wholeVerb.Trip.TotalMinutes); // a whole body may be on the five-minute floor
    }

    [Fact]
    public void TheRoutePlanner_GivesALameBodyNoRoute_AndPricesNoCrossing()
    {
        var w = With(Legless());
        var here = w.Agent(P).Tile;
        var routes = Trip.Routes(w, P);
        Assert.Equal([here], routes.Reached);
        Assert.Throws<ArgumentException>(() => Trip.StepMinutes(w, P, here, Map.Step(here, OpenWalk(w).Dir), Pace.Quick));

        var whole = With(BodyState.BaselineHuman());
        Assert.True(Trip.Routes(whole, P, maxMinutes: 120).Reached.Count() > 1);
    }

    static readonly WeatherSample Calm = new(0, WeatherCondition.Clear, -10, 0, 0, 0, WindDirection.North);

    /// Over any body and load: a crossing is refused, or it takes a positive whole number of minutes.
    [Fact]
    public void ACrossingTime_IsRefusedOrAPositiveWholeNumberOfMinutes_ForAnyBody()
    {
        var world = new World { Seed = 17, GeneratedMap = TestWorlds.Map(17), FixedWeather = Calm };
        world.Agents[P] = new Agent { Id = P, Tile = new TileId(2, 33) };
        TileId from = new(2, 33), to = new(2, 34);
        FacultiesTests.AnyBody.Check(t =>
        {
            var (b, context) = t;
            if (Body.SpeedCrippledBy(b, context).Count > 0)
                Assert.Throws<InvalidOperationException>(() => Trip.Terms(world, b, from, to, Pace.Careful, Calm, context.EncumbrancePct));
            else
                Assert.True(Trip.Terms(world, b, from, to, Pace.Careful, Calm, context.EncumbrancePct).TotalMinutes > 0);
        }, iter: 300);
    }
}
