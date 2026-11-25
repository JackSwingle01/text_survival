namespace Survival.Tests;

/// The fire as a station (spec 2026-11-24 fire-and-rack-as-stations): a piece put on through the verbs,
/// the world advanced, and what a person could see read back: the fire's menu, the pack, the pile and
/// the events with their minutes.
public class StationTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    static World Camp(out TileId camp)
    {
        var w = TestWorlds.Bare(17);
        w.FixedWeather = Mild;
        camp = Map.Of(w).Camp;
        TestKit.ClearResources(Items.Pack(w, P));
        return w;
    }

    static FireId Pit(World w, double birchKg, PitType pit = PitType.Open)
    {
        var id = new FireId(w.NextFireId++);
        var f = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = w.Minute, Pit = pit };
        if (birchKg > 0) f.BurningKg[Resource.BirchFirewood] = birchKg;
        w.Fires[id] = f;
        return id;
    }

    static double Meat(World w, double kg)
    {
        Items.AddPiece(Items.Pack(w, P), Resource.RawMeat, kg);
        return kg;
    }

    static Verb Offer(World w, Func<Activity, bool> which) =>
        Verbs.At(w, P).LocationFeatures.Single(t => t.LocationFeature.Kind == LocationFeatureKind.Fire).Menu.Single(v => which(v.Activity));

    static IReadOnlyList<Verb> FireMenu(World w) =>
        Verbs.At(w, P).LocationFeatures.Single(t => t.LocationFeature.Kind == LocationFeatureKind.Fire).Menu;

    static void PutOnMeat(World w)
    {
        var step = Sim.Step(w, new PutOn(P, "cook_meat"), 1);
        Assert.Equal(1, step.MinutesElapsed);
    }

    /// The minute the cooked piece finished, and its kilos: advanced a minute at a time until it does.
    static TransformationFinished FinishOf(World w, int withinMinutes)
    {
        for (int i = 0; i < withinMinutes; i++)
            if (Sim.Advance(w, 1).OfType<TransformationFinished>().FirstOrDefault() is { } done) return done;
        throw new Xunit.Sdk.XunitException($"nothing finished within {withinMinutes} minutes");
    }

    [Fact]
    public void Meat_AtAGoodFire_IsCookedFifteenMinutesLater_AtTheSameWeight_WithNoOneActing()
    {
        var w = Camp(out _);
        Pit(w, 5);
        double kg = Meat(w, 0.4);
        Sim.Advance(w, 1);
        long setAt = w.Minute; // the minute it goes on, the minute of putting it on included
        PutOnMeat(w);
        Assert.Equal(0, Items.Count(Items.Pack(w, P), Resource.RawMeat));

        var done = FinishOf(w, 60);
        Assert.Equal(setAt + 15, done.Minute);
        Assert.Equal(Resource.CookedMeat, done.Made);
        Assert.Equal(kg, done.Kg, 12);
        Assert.Equal(P, done.SetBy);
    }

    static long Finish(World w, Action<World> fire)
    {
        fire(w);
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        long setAt = w.Minute;
        PutOnMeat(w);
        return FinishOf(w, 240).Minute - setAt;
    }

    [Fact]
    public void Meat_AtAWeakFire_TakesLonger_AndOnEmbersLongerStill()
    {
        long good = Finish(Camp(out _), w => Pit(w, 5));
        long weak = Finish(Camp(out _), w => Pit(w, 0.45));
        long embers = Finish(Camp(out _), w =>
        {
            var f = w.Fires[Pit(w, 0)];
            f.HasEmbers = true;
            f.EmberStartTempF = 400;
            f.EmberDurationHours = f.EmberHoursRemaining = 3;
        });
        Assert.Equal(15, good);
        Assert.True(weak > good, $"weak {weak}");
        Assert.True(embers > weak, $"embers {embers}, weak {weak}");
        Assert.Equal(60, embers); // a quarter of the rate, a full hour on embers that outlast it
    }

    [Fact]
    public void AFireThatDies_LeavesTheMeatRaw_AndRelightingFinishesItInTheTimeLeft()
    {
        var w = Camp(out _);
        var id = Pit(w, 0);
        var f = w.Fires[id];
        f.HasEmbers = true;
        f.EmberStartTempF = 300;
        f.EmberDurationHours = f.EmberHoursRemaining = 5 / 60.0; // dies within minutes
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        PutOnMeat(w);
        var events = Sim.Advance(w, 120);
        Assert.Contains(events, e => e is FireDied);
        Assert.DoesNotContain(events, e => e is TransformationFinished);
        Assert.Contains(FireMenu(w), v => v.Activity is TakeBack && v.Allowed); // still raw, and still there
        Assert.DoesNotContain(FireMenu(w), v => v.Activity is TakeFinished);

        // Four minutes on embers at a quarter of the rate: one minute of the fifteen done, fourteen left.
        f.BurningKg[Resource.BirchFirewood] = 5;
        long relit = w.Minute;
        Assert.Equal(relit + 14, FinishOf(w, 60).Minute);
    }

    [Fact]
    public void PuttingOn_AtAColdFire_IsAllowed_AndNothingProgresses()
    {
        var w = Camp(out _);
        Pit(w, 0);
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        Assert.True(Offer(w, a => a is PutOn).Allowed);
        PutOnMeat(w);
        Assert.DoesNotContain(Sim.Advance(w, 180), e => e is TransformationFinished);
        Assert.Contains(FireMenu(w), v => v.Activity is TakeBack);
    }

    [Fact]
    public void AThirdPiece_IsRefusedAtAnOpenFire_NamingTheRoom_AndAcceptedAtAMoundPit()
    {
        foreach (var (pit, refused) in new[] { (PitType.Open, true), (PitType.Mound, false) })
        {
            var w = Camp(out _);
            Pit(w, 5, pit);
            for (int i = 0; i < 3; i++) Meat(w, 0.3 + 0.1 * i);
            Sim.Advance(w, 1);
            PutOnMeat(w);
            PutOnMeat(w);
            var third = Offer(w, a => a is PutOn);
            Assert.Equal(refused, !third.Allowed);
            var step = Sim.Step(w, third.Activity, 1);
            if (refused)
            {
                Assert.Equal("the fire has room for 2, and all of it is taken", third.Refused!.Reason);
                Assert.Equal(third.Refused.Reason, step.InterruptedReason);
                Assert.Equal(0, step.MinutesElapsed);
                Assert.Equal(1, Items.Count(Items.Pack(w, P), Resource.RawMeat));
            }
            else Assert.Equal(0, Items.Count(Items.Pack(w, P), Resource.RawMeat));
        }
    }

    [Fact]
    public void Take_MovesEverythingFinishedToThePack_AndWhatDoesNotFitToThePile()
    {
        var w = Camp(out var camp);
        Pit(w, 5, PitType.Mound);
        Meat(w, 0.5);
        Meat(w, 0.6);
        Sim.Advance(w, 1);
        PutOnMeat(w);
        PutOnMeat(w);
        Sim.Advance(w, 20);
        var take = Offer(w, a => a is TakeFinished);
        Assert.True(take.Allowed);
        var pack = Items.Pack(w, P);
        Items.Add(pack, Resource.Water, Items.CapacityKg(pack) - Items.CarriedKg(w.Agent(P)) - 0.55); // room for the lighter piece only

        Assert.Equal(1, Sim.Step(w, take.Activity, take.Minutes).MinutesElapsed);
        Assert.Equal([0.5], pack.Pieces[Resource.CookedMeat]);
        Assert.Equal([0.6], Items.PileAt(w, camp).Pieces[Resource.CookedMeat]);
        Assert.DoesNotContain(FireMenu(w), v => v.Activity is TakeFinished);
    }

    [Fact]
    public void TakeBack_ReturnsTheSamePiece_AndPuttingItOnAgainStartsFromNothing()
    {
        var w = Camp(out _);
        Pit(w, 5);
        Meat(w, 0.37);
        Sim.Advance(w, 1);
        PutOnMeat(w);
        Sim.Advance(w, 10);
        var back = Offer(w, a => a is TakeBack);
        Assert.Equal(1, Sim.Step(w, back.Activity, back.Minutes).MinutesElapsed);
        Assert.Equal([0.37], Items.Pack(w, P).Pieces[Resource.RawMeat]);

        long again = w.Minute;
        PutOnMeat(w);
        Assert.Equal(again + 15, FinishOf(w, 60).Minute);
    }

    [Fact]
    public void TheFinish_CarriesTheMinuteItHappened_WhenNobodyWasOnTheTile()
    {
        var w = Camp(out var camp);
        Pit(w, 5);
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        long setAt = w.Minute;
        PutOnMeat(w);
        var away = Map.Step(Map.Step(Map.Step(camp, Direction.East), Direction.East), Direction.East);
        w.Agent(P).Tile = away;
        var done = Sim.Advance(w, 90).OfType<TransformationFinished>().Single();
        Assert.Equal(setAt + 15, done.Minute);
        Assert.Equal(camp, done.Tile);
        Assert.Equal(away, w.Agent(P).Tile);
    }

    [Fact]
    public void TheMakeMenu_OffersNoStationRecipe_AndMakeRefusesOne()
    {
        var w = Camp(out _);
        Pit(w, 5);
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        Assert.DoesNotContain(Verbs.At(w, P).Make, v => v.Activity is Make m && !Recipes.Get(m.Recipe).ByHand);
        Assert.Equal("cook meat is not done by hand: put it on at a fire", Sim.Step(w, new Make(P, "cook_meat"), 15).InterruptedReason);
    }

    [Fact]
    public void SavingAndLoading_WithMeatHalfCooked_LeavesTheFinishMinuteUnchanged()
    {
        var w = Camp(out _);
        Pit(w, 5);
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        long setAt = w.Minute;
        PutOnMeat(w);
        Sim.Advance(w, 7);
        var loaded = TestWorlds.Load(Save.ToJson(w));
        Assert.Equal(setAt + 15, FinishOf(w, 30).Minute);
        Assert.Equal(setAt + 15, FinishOf(loaded, 30).Minute);
    }

    /// A Take back offered before the first piece finished still takes back its own piece after it has.
    [Fact]
    public void TakeBack_NamesItsPiece_NotItsPlaceOnTheFire()
    {
        var w = Camp(out _);
        Pit(w, 5);
        Meat(w, 0.3);
        Sim.Advance(w, 1);
        PutOnMeat(w);
        Sim.Advance(w, 10);
        Meat(w, 0.7);
        PutOnMeat(w);
        var back = Offer(w, a => a is TakeBack { Kg: 0.7 });
        Sim.Advance(w, 5); // the first piece is done, and leaves the work
        Assert.Contains(FireMenu(w), v => v.Activity is TakeFinished);
        Assert.Equal(1, Sim.Step(w, back.Activity, back.Minutes).MinutesElapsed);
        Assert.Equal([0.7], Items.Pack(w, P).Pieces[Resource.RawMeat]);
    }

    // ---- how it is going (#185) ----

    static Verb TakeBackOffer(World w) => Offer(w, a => a is TakeBack);

    [Fact]
    public void TheFiresMenu_ShowsAnExpectedFinish_ForMeatCooking_AndTheFireIsOut_ForMeatAtAColdFire()
    {
        var w = Camp(out _);
        Pit(w, 5);
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        long setAt = w.Minute;
        PutOnMeat(w);
        var going = TakeBackOffer(w).Progress!;
        Assert.Equal(setAt + 15, going.FinishMinute);
        Assert.Null(going.Why); // a good fire: the recipe's own time

        var cold = Camp(out _);
        Pit(cold, 0);
        Meat(cold, 0.4);
        Sim.Advance(cold, 1);
        PutOnMeat(cold);
        var stopped = TakeBackOffer(cold).Progress!;
        Assert.Null(stopped.FinishMinute);
        Assert.Equal("the fire is out", Narration.Why(stopped.Why!.Value));
    }

    [Fact]
    public void AWeakFire_SaysTheFireIsLow_AndEmbers_ThatTheFireIsDownToEmbers()
    {
        var weak = Camp(out _);
        Pit(weak, 0.45);
        Meat(weak, 0.4);
        Sim.Advance(weak, 1);
        PutOnMeat(weak);
        Assert.Equal("the fire is low", Narration.Why(TakeBackOffer(weak).Progress!.Why!.Value));

        var embers = Camp(out _);
        var f = embers.Fires[Pit(embers, 0)];
        f.HasEmbers = true;
        f.EmberStartTempF = 400;
        f.EmberDurationHours = f.EmberHoursRemaining = 3;
        Meat(embers, 0.4);
        Sim.Advance(embers, 1);
        PutOnMeat(embers);
        Assert.Equal("the fire is down to embers", Narration.Why(TakeBackOffer(embers).Progress!.Why!.Value));
    }

    /// The player walks off the camp for a while, comes back and stands a minute: the receipt of that minute.
    static Receipt TripAway(World w, TileId camp, int minutes)
    {
        w.Agent(P).Tile = Map.Step(Map.Step(Map.Step(camp, Direction.East), Direction.East), Direction.East);
        Sim.Step(w, new Wait(P), minutes);
        w.Agent(P).Tile = camp;
        return Assert.Single(Sim.Step(w, new Wait(P), 1).Events.OfType<Noticed>()).Receipt;
    }

    [Fact]
    public void AfterATripAway_TheReceipt_ListsTheMeatThatCooked_AndTheMinuteItFinished()
    {
        var w = Camp(out var camp);
        Pit(w, 5);
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        long setAt = w.Minute;
        PutOnMeat(w);
        var r = TripAway(w, camp, 60);
        var done = Assert.Single(r.Happened.OfType<TransformationFinished>());
        Assert.Equal(setAt + 15, done.Minute);
        Assert.Equal("1 cut of cooked meat is done at the fire.", Narration.Of(w, done)!.Text);
        Assert.Contains(done, Journal.Between(w, camp, setAt, w.Minute + 1)); // the tile journal keeps it
    }

    [Fact]
    public void AfterATripInWhichTheFireDiedFirst_TheReceipt_SaysTheFireWentOutWithFoodOnIt()
    {
        var w = Camp(out var camp);
        var id = Pit(w, 0);
        var f = w.Fires[id];
        f.HasEmbers = true;
        f.EmberStartTempF = 300;
        f.EmberDurationHours = f.EmberHoursRemaining = 5 / 60.0;
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        PutOnMeat(w);
        var r = TripAway(w, camp, 60);
        var stop = Assert.Single(r.Happened.OfType<TransformationStopped>());
        var died = Assert.Single(r.Happened.OfType<FireDied>());
        Assert.Equal((died.Minute, camp, StopReason.FireOut), (stop.Minute, stop.Tile, stop.Why));
        Assert.Equal([Resource.RawMeat], stop.Left);
        Assert.Equal("The fire goes out with raw meat on it.", Narration.Of(w, stop)!.Text);
        Assert.Contains(stop, Journal.Between(w, camp, 0, w.Minute + 1));
    }

    [Fact]
    public void PuttingOnAtAColdFire_RaisesNoStop_AndRelightingThenLettingItDie_RaisesOne()
    {
        var w = Camp(out _);
        var id = Pit(w, 0);
        Meat(w, 0.4);
        Sim.Advance(w, 1);
        PutOnMeat(w);
        Assert.DoesNotContain(Sim.Advance(w, 30), e => e is TransformationStopped);

        var f = w.Fires[id];
        f.HasEmbers = true;
        f.EmberStartTempF = 300;
        f.EmberDurationHours = f.EmberHoursRemaining = 3 / 60.0;
        Assert.Single(Sim.Advance(w, 30).OfType<TransformationStopped>());
    }
}
