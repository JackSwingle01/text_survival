namespace Survival.Tests;

/// Regressions from the phase-1 review: what a Step may and may not do, and how fire reaches a body.
public class StepRulesTests
{
    static readonly AgentId P = AgentId.Player;

    static World LitCamp(int seed = 1)
    {
        var world = World.New(seed);
        for (int i = 0; i < 20 && Fire.PitAt(world, Map.Of(world).Camp) == null; i++)
            Sim.Step(world, new StartFire(P, FireStarter.FireStriker, Fuel.BirchBark, 0.05, 1.0), 1);
        Assert.NotNull(Fire.PitAt(world, Map.Of(world).Camp));
        return world;
    }

    static FireId PlacePit(World world, FireState f)
    {
        var id = new FireId(world.NextFireId++);
        world.Fires[id] = f;
        return id;
    }

    // ---- 1. refusals cost nothing; a failed ignition is real effort ----

    [Fact]
    public void RefusedFireAction_CostsNoMinutes_AndChangesNothing()
    {
        var world = LitCamp();
        string before = Save.ToJson(world);
        var r = Sim.Step(world, new AddFuel(P, Fuel.Bone, 1), 5);
        Assert.Equal("the fire is not hot enough for Bone", r.InterruptedReason);
        Assert.Equal(0, r.MinutesElapsed);
        Assert.Empty(r.Events);
        Assert.Equal(before, Save.ToJson(world));

        var empty = World.New(1);
        before = Save.ToJson(empty);
        r = Sim.Step(empty, new AddFuel(P, Fuel.Kindling, 1), 1);
        Assert.Equal(("no fire here", 0), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(before, Save.ToJson(empty));
    }

    [Fact]
    public void FailedIgnition_CostsItsMinute_AndTheBodyLivesIt()
    {
        var world = World.New(3);
        for (int i = 0; i < 50; i++)
        {
            double kcal = world.Agent(P).Body.CaloriesKcal;
            long minute = world.Minute;
            var r = Sim.Step(world, new StartFire(P, FireStarter.HandDrill, Fuel.Tinder, 0.05, 0), 1);
            if (r.InterruptedReason != "fire did not catch") continue;
            Assert.Equal(1, r.MinutesElapsed);
            Assert.Equal(minute + 1, world.Minute);
            Assert.True(world.Agent(P).Body.CaloriesKcal < kcal, "the minute spent drilling burned no calories");
            return;
        }
        Assert.Fail("no failed ignition in 50 tries at 50% odds");
    }

    // ---- 2. the fire warms the air, embers included ----

    [Fact]
    public void Embers_WarmTheAirAroundThem()
    {
        var cold = World.New(1);
        var embers = World.New(1);
        PlacePit(embers, new FireState
        {
            Tile = Map.Of(embers).Camp, LastUpdatedMinute = 0,
            HasEmbers = true, EmberDurationHours = 2, EmberHoursRemaining = 2, EmberStartTempF = 800,
        });
        Sim.Step(cold, new Rest(P), 60);
        Sim.Step(embers, new Rest(P), 60);
        var c = cold.Agent(P).Body;
        var e = embers.Agent(P).Body;
        Assert.True(e.CoreTempF + e.ClothingHeatBufferPct > c.CoreTempF + c.ClothingHeatBufferPct,
            $"embers {e.CoreTempF}F/{e.ClothingHeatBufferPct} vs none {c.CoreTempF}F/{c.ClothingHeatBufferPct}");
    }

    // ---- 3. a cold pit's walls do nothing ----

    [Fact]
    public void ColdPit_GivesNoWindProtection()
    {
        var none = World.New(1);
        var pit = World.New(1);
        PlacePit(pit, new FireState { Tile = Map.Of(pit).Camp, LastUpdatedMinute = 0, Pit = PitType.Stone });
        Sim.Step(none, new Rest(P), 120);
        Sim.Step(pit, new Rest(P), 120);
        Assert.Equal(none.Agent(P).Body.CoreTempF, pit.Agent(P).Body.CoreTempF, 12);
        Assert.Equal(none.Agent(P).Body.ClothingHeatBufferPct, pit.Agent(P).Body.ClothingHeatBufferPct, 12);
    }

    // ---- 4. the dead do not act ----

    [Fact]
    public void DeadAgent_StepThrows_BeforeTouchingTheWorld()
    {
        var world = World.New(1);
        world.Agent(P).Body.BloodPct = 0;
        Assert.False(Body.IsAlive(world.Agent(P).Body));
        string before = Save.ToJson(world);
        Assert.Throws<InvalidOperationException>(() => Sim.Step(world, new Wait(P), 10));
        Assert.Throws<InvalidOperationException>(() => Sim.Step(world, new AddFuel(P, Fuel.Kindling, 1), 1));
        Assert.Equal(before, Save.ToJson(world));
    }

    // ---- 6. a torch in hand warms you however hot you are ----

    [Fact]
    public void TorchHeat_StillReachesASweatingBody()
    {
        World Hot(bool torch)
        {
            var w = World.New(1);
            w.Agent(P).Body.CoreTempF = 99.5; // above the sweating threshold
            if (torch) PlacePit(w, new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = 0, CarriedBy = P, TorchMinutesLeft = Fire.TorchBurnMinutes });
            Sim.Step(w, new Rest(P), 1);
            return w;
        }
        Assert.True(Hot(torch: true).Agent(P).Body.CoreTempF > Hot(torch: false).Agent(P).Body.CoreTempF);
    }

    // ---- 7. StartFire on an existing pit ----

    [Fact]
    public void StartFire_OnAPitOfAnotherType_IsRefused()
    {
        var world = LitCamp();
        var r = Sim.Step(world, new StartFire(P, FireStarter.FireStriker, Fuel.BirchBark, 0.05, 1.0, PitType.Stone), 1);
        Assert.Equal("the pit here is Open, not Stone", r.InterruptedReason);
        Assert.Equal(0, r.MinutesElapsed);
        Assert.Equal(PitType.Open, world.Fires[Fire.PitAt(world, Map.Of(world).Camp)!.Value].Pit);
    }

    [Fact]
    public void StartFire_OnABurningPit_IsRefused_AndDrawsNoRoll()
    {
        var world = LitCamp();
        string before = Save.ToJson(world);
        var r = Sim.Step(world, new StartFire(P, FireStarter.FireStriker, Fuel.BirchBark, 0.05, 1.0), 1);
        Assert.Equal("the fire is already burning", r.InterruptedReason);
        Assert.Equal(0, r.MinutesElapsed);
        Assert.DoesNotContain(r.Events, e => e is FireLit);
        Assert.Equal(before, Save.ToJson(world));
    }

    [Fact]
    public void StartFire_OnAFullPit_IsRefused_AndLightsNothing()
    {
        var world = LitCamp();
        var f = world.Fires[Fire.PitAt(world, Map.Of(world).Camp)!.Value];
        Fire.AddFuel(f, Fuel.Kindling, 50);
        Assert.Equal(Fire.MaxFuelKg(f.Pit), Fire.TotalKg(f), 9);
        var r = Sim.Step(world, new StartFire(P, FireStarter.FireStriker, Fuel.BirchBark, 0.05, 1.0), 1);
        Assert.Equal("the fire is full", r.InterruptedReason);
        Assert.DoesNotContain(r.Events, e => e is FireLit);
    }

    // ---- 8. HasLight refuses to read a stale fire ----

    [Fact]
    public void HasLight_ThrowsOnAFireNotCaughtUp()
    {
        var world = LitCamp();
        world.Agent(P).Tile = new TileId(0, 0);
        Sim.Step(world, new Wait(P), 30);
        world.Agent(P).Tile = Map.Of(world).Camp; // back, but nothing has caught the fire up
        Assert.Throws<InvalidOperationException>(() => Fire.HasLight(world, P));
    }

    // ---- 5 and 10. effects and healing ----

    [Fact]
    public void BriefBleeding_LeavesATinyBloody_NotTheTreatmentFloor()
    {
        var b = BodyState.BaselineHuman();
        Effects.Add(b, EffectKind.Bleeding, 0.1);
        Body.Update(b, new SurvivalContext { AirTempF = 70 }, Rng.ForSystem(1, "body"));
        double bloody = Effects.Severity(b, EffectKind.Bloody);
        Assert.InRange(bloody, 1e-6, 0.01);
    }

    [Fact]
    public void Heal_DoesNotRegrowADestroyedOrgan_AndStillMendsOtherWounds()
    {
        var b = BodyState.BaselineHuman();
        var eye = b.Regions.SelectMany(r => r.Organs).Single(o => o.Name == "Left Eye");
        eye.Condition = 0;
        var arm = b.Regions.Single(r => r.Name == "Left Arm");
        arm.Skin.Condition = 0.5;
        Body.Heal(b, 0.2);
        Assert.Equal(0.0, eye.Condition);
        Assert.True(arm.Skin.Condition > 0.5, "the arm should mend while the lost eye stays lost");
    }
}
