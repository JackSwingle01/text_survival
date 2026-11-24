namespace Survival.Tests;

/// The mountain crossing: the six stages along the eastern corridor and the only win there is.
public class CrossingTests
{
    static readonly AgentId P = AgentId.Player;

    // Old LocationFactory pass factories: hazard, wind, overhead cover, visibility, temperature delta.
    public static readonly TheoryData<int, string, double, double, double, double, double> OldStages = new()
    {
        { 0, "Pass Approach", 0.6, 1.3, 0.05, 1.2, -5 },
        { 1, "Lower Pass", 0.8, 1.6, 0, 1.3, -10 },
        { 2, "The Pass Proper", 1.0, 2.0, 0, 0.3, -15 },
        { 3, "Upper Descent", 0.7, 1.5, 0, 1.4, -10 },
        { 4, "Lower Descent", 0.5, 1.0, 0.1, 1.0, -5 },
        { 5, "Far Side", 0.3, 0.6, 0.3, 1.2, 0 },
    };

    [Theory, MemberData(nameof(OldStages))]
    public void Stages_StandWhereTheOldOnesDid_WithTheirOldGround(int index, string name, double hazard, double wind, double cover, double visibility, double tempF)
    {
        var m = TestWorlds.Map(17);
        var t = m.PassStageIndex.Single(s => s.Value == index).Key;
        Assert.Equal(name, Map.Describe(m, t));
        Assert.Equal((hazard, wind, cover, visibility, tempF),
            (Map.TerrainHazardLevel(m, t), Map.WindFactor(m, t), Map.OverheadCover(m, t), Map.Visibility(m, t), Map.TemperatureDeltaF(m, t)));
        Assert.Equal(Terrain.Rock, m.TerrainAt(t));
        Assert.Empty(Pools.At(TestWorlds.New(17), t)); // the old stages had nothing to forage or find
    }

    [Fact]
    public void Outcome_IsDerived_SurvivingDeadOrAcross()
    {
        var w = TestWorlds.New(17);
        Assert.Equal(RunOutcome.Surviving, Map.Outcome(w));
        var m = Map.Of(w);
        w.Agent(P).Tile = Map.Step(m.Exit, Direction.West);
        Assert.Equal(RunOutcome.Surviving, Map.Outcome(w));

        var r = Sim.Step(w, new Travel(P, Direction.East, Pace.Careful), 600);
        Assert.Equal("crossed the pass", r.InterruptedReason);
        Assert.Equal(RunOutcome.Crossed, Map.Outcome(w));

        // Nothing is stored: dying on the far side is still dying.
        w.Agent(P).Body.BloodPct = 0.5; // circulation collapses (BodyTests)
        Assert.Equal(RunOutcome.Died, Map.Outcome(w));
        Assert.Equal(RunOutcome.Died, Map.Outcome(TestWorlds.Load(Save.ToJson(w))));
    }

    /// Standing on the far side ends the run: the player can do nothing more, so cannot walk back
    /// and turn a win into Surviving, and the valley's clock runs on without them.
    [Fact]
    public void Crossed_TheRunIsOver()
    {
        var w = TestWorlds.New(17);
        var m = Map.Of(w);
        w.Agent(P).Tile = Map.Step(m.Exit, Direction.West);
        Assert.Equal("crossed the pass", Sim.Step(w, new Travel(P, Direction.East, Pace.Careful), 600).InterruptedReason);
        var body = System.Text.Json.JsonSerializer.Serialize(w.Agent(P));
        foreach (Activity a in new Activity[] { new Travel(P, Direction.West), new Wait(P), new Sleep(P), new Gather(P, 0) })
        {
            var r = Sim.Step(w, a, 60);
            Assert.Equal((0, Sim.RunOver), (r.MinutesElapsed, r.InterruptedReason));
        }
        long minute = w.Minute;
        Sim.Advance(w, 3 * 1440);
        Assert.Equal(minute + 3 * 1440, w.Minute);
        Assert.Equal(body, System.Text.Json.JsonSerializer.Serialize(w.Agent(P))); // not a minute lived: no hunger, no cold
        Assert.Equal(RunOutcome.Crossed, Map.Outcome(w));
        Assert.Equal(RunOutcome.Crossed, Map.Outcome(TestWorlds.Load(Save.ToJson(w))));
    }
}
