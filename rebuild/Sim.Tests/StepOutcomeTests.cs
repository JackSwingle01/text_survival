namespace Survival.Tests;

/// Sim.Step says how a step ended: the activity finished, was refused before any time passed, or was
/// interrupted. The reason text stays, for display, whichever it was.
public class StepOutcomeTests
{
    static readonly AgentId P = AgentId.Player;

    static World Camp(out TileId camp)
    {
        var w = World.New(17);
        camp = Map.Of(w).Camp;
        return w;
    }

    [Fact]
    public void AWaitThatRunsItsMinutes_Finished_WithNoReason()
    {
        var w = Camp(out _);
        var r = Sim.Step(w, new Wait(P), 30);
        Assert.Equal((StepOutcome.Finished, 30, (string?)null), (r.Outcome, r.MinutesElapsed, r.Reason));
    }

    [Fact]
    public void AWalkThatArrives_Finished_SayingSo()
    {
        var w = Camp(out var camp);
        var dir = new[] { Direction.North, Direction.East, Direction.South, Direction.West }
            .First(d => Sim.Refusal(w, new Travel(P, d)) == null);
        var r = Sim.Step(w, new Travel(P, dir), Verbs.UntilDoneMinutes);
        Assert.Equal(Map.Step(camp, dir), w.Agent(P).Tile);
        Assert.Equal((StepOutcome.Finished, Traversal.Arrived), (r.Outcome, r.Reason));
    }

    [Fact]
    public void EatingAndMeltingSnow_Finished()
    {
        var w = Camp(out _);
        TestKit.Supply(w);
        Items.Add(Items.Pack(w, P), Resource.Berries, 1);
        var ate = Sim.Step(w, new Eat(P, Resource.Berries, 0.5), Verbs.UntilDoneMinutes);
        Assert.Equal((StepOutcome.Finished, Work.Ate), (ate.Outcome, ate.Reason));

        var light = Verbs.At(w, P).Tile.Single(v => v.Activity is StartFire);
        for (int i = 0; i < 20 && !(Fire.PitAt(w, w.Agent(P).Tile) is FireId f && Fire.IsActive(w.Fires[f])); i++) Sim.Step(w, light.Activity, 1);
        var melted = Sim.Step(w, new MeltSnow(P), Verbs.UntilDoneMinutes);
        Assert.Equal((StepOutcome.Finished, Work.Melted), (melted.Outcome, melted.Reason));
    }

    [Fact]
    public void SleepingUntilFullyRested_Finished()
    {
        var w = Camp(out _);
        w.Agent(P).Body.EnergyMinutes -= 30;
        var r = Sim.Step(w, new Sleep(P), Verbs.SleepMinutes);
        Assert.Equal((StepOutcome.Finished, Sim.FullyRestedReason), (r.Outcome, r.Reason)); // Body.Tick's literal: this test pins it
        Assert.True(r.MinutesElapsed < Verbs.SleepMinutes);
    }

    /// A fight the player wins ends its last round Finished: Combat.EndRound's "fight over" is Sim.EncounterOver.
    [Fact]
    public void AFightThatEnds_Finished()
    {
        var w = Camp(out var camp);
        var wolf = Agents.Spawn(w, Species.Wolf, camp);
        Combat.Create(w, [new Fighter(P)], [new Fighter(wolf)], camp, 10, Awareness.Engaged, Awareness.Engaged);
        w.Agent(wolf).Body.BloodPct = 0;
        var r = Sim.Step(w, new Fight(P, CombatAction.Wait), 1);
        Assert.Equal((StepOutcome.Finished, Sim.EncounterOver), (r.Outcome, r.Reason));
    }

    [Fact]
    public void ARefusedActivity_Refused_NoTimePasses_WithStepsReason()
    {
        var w = Camp(out _);
        var eat = new Eat(P, Resource.Honey, 1);
        var r = Sim.Step(w, eat, 10);
        Assert.Equal((StepOutcome.Refused, 0, Sim.Refusal(w, eat)?.Reason), (r.Outcome, r.MinutesElapsed, r.Reason));
        Assert.NotNull(r.Reason);
    }

    [Fact]
    public void InAFight_AnythingElse_Refused()
    {
        var w = Camp(out var camp);
        var wolf = Agents.Spawn(w, Species.Wolf, camp);
        Combat.Create(w, [new Fighter(P)], [new Fighter(wolf)], camp, 10, Awareness.Engaged, Awareness.Engaged);
        var r = Sim.Step(w, new Wait(P), 5);
        Assert.Equal((StepOutcome.Refused, Sim.InAnEncounter), (r.Outcome, r.Reason));
    }

    [Fact]
    public void CookingWhenTheFireGoesOut_Interrupted_WithTheReason()
    {
        var w = Camp(out var camp);
        TestKit.Supply(w);
        Items.Add(Items.Pack(w, P), Resource.RawMeat, 5);
        var light = Verbs.At(w, P).Tile.Single(v => v.Activity is StartFire);
        for (int i = 0; i < 20 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++) Sim.Step(w, light.Activity, 1);
        var pit = w.Fires[Fire.PitAt(w, camp)!.Value];
        // Nothing left to burn: the fire dies under the meat.
        pit.UnburnedKg.Clear();
        var r = Sim.Step(w, new Cook(P, Resource.RawMeat, 5), Verbs.UntilDoneMinutes);
        Assert.Equal((StepOutcome.Interrupted, "the fire went out"), (r.Outcome, r.Reason));
    }
}
