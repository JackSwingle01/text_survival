namespace Survival.Tests;

/// The curing rack (#187, spec 2026-11-24 fire-and-rack-as-stations): built through Make, food put on
/// through its menu, the world advanced, and what a person could see read back. Drying follows the weather and a
/// fire on the tile (#190). A bare world, so a day's wait stays quick.
public class RackTests
{
    static readonly AgentId P = AgentId.Player;
    /// A dry, still day, below the drying's reference air: the recipe's own time.
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, -12, 0, 0, 0.8, WindDirection.North);
    static readonly WeatherSample Blizzard = new(0, WeatherCondition.Blizzard, -25, 0.9, 0.9, 1, WindDirection.North);

    static World Camp(WeatherSample? sky = null)
    {
        var w = TestWorlds.FreshPlayer(TestWorlds.Bare(17));
        w.FixedWeather = sky ?? Mild;
        TestKit.ClearResources(Items.Pack(w, P));
        return w;
    }

    static Inventory Pack(World w) => Items.Pack(w, P);

    static void RackMaterials(World w)
    {
        TestKit.GivePieces(Pack(w), Resource.BirchBranch, 4);
        TestKit.GivePieces(Pack(w), Resource.Stick, 4);
        TestKit.GivePieces(Pack(w), Resource.Rope, 1);
    }

    static StepResult Build(World w) => Sim.Step(w, new Make(P, "curing_rack"), 60);

    static List<LocationFeatureVerbs> Racks(World w) =>
        [.. Verbs.At(w, P).LocationFeatures.Where(t => t.LocationFeature.Kind == LocationFeatureKind.Rack)];

    static IReadOnlyList<Verb> RackMenu(World w) => Racks(w).Single().Menu;

    static Verb Offer(World w, Func<Activity, bool> which) => RackMenu(w).Single(v => which(v.Activity));

    /// A world with one rack built on the camp tile, and nothing in the pack.
    static World WithRack(WeatherSample? sky = null)
    {
        var w = Camp(sky);
        RackMaterials(w);
        Assert.Equal(Shelters.BuiltReason, Build(w).InterruptedReason);
        TestKit.ClearResources(Pack(w));
        return w;
    }

    static StepResult PutOn(World w, Func<Activity, bool> which)
    {
        var v = Offer(w, a => a is PutOn && which(a));
        var step = Sim.Step(w, v.Activity, v.Minutes);
        Assert.Equal(Stations.HandlingMinutes, step.MinutesElapsed);
        return step;
    }

    static TransformationFinished FinishOf(World w, int withinMinutes)
    {
        for (int i = 0; i < withinMinutes; i++)
            if (Sim.Advance(w, 1).OfType<TransformationFinished>().FirstOrDefault() is { } done) return done;
        throw new Xunit.Sdk.XunitException($"nothing finished within {withinMinutes} minutes");
    }

    [Fact]
    public void ARack_IsBuilt_FromBranchesSticksAndRope_InFortyFiveMinutes_AndIsRefusedWithoutThem()
    {
        var w = Camp();
        var offer = Verbs.At(w, P).Make.Single(v => v.Activity is Make { Recipe: "curing_rack" });
        Assert.False(offer.Allowed);
        Assert.Contains("branch", offer.Refused!.Reason);
        Assert.Empty(Racks(w));

        RackMaterials(w);
        var r = Build(w);
        Assert.Equal(Shelters.BuiltReason, r.InterruptedReason);
        Assert.Equal(45, r.MinutesElapsed);
        Assert.Contains(r.Events, e => e is Built { Recipe: "curing_rack" });
        Assert.Single(Racks(w));
        Assert.Equal(0, Items.Count(Pack(w), Resource.Rope));
        Assert.Equal(0, Items.Count(Pack(w), Resource.Stick));
        Assert.Equal(0, Items.Count(Pack(w), Resource.BirchBranch));
    }

    [Fact]
    public void ASecondRack_IsBuilt_OnTheSameTile()
    {
        var w = WithRack();
        RackMaterials(w);
        Assert.Equal(Shelters.BuiltReason, Build(w).InterruptedReason);
        var racks = Racks(w);
        Assert.Equal(2, racks.Count);
        Assert.NotEqual(racks[0].LocationFeature.Index, racks[1].LocationFeature.Index);
    }

    [Fact]
    public void AKiloOfMeat_IsFourHundredGramsOfDriedMeat_TwentyFourHoursLater_WithNoOneActing()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.RawMeat, 1.0);
        long setAt = w.Minute;
        PutOn(w, _ => true);
        Assert.Equal(0, Items.Count(Pack(w), Resource.RawMeat));

        var done = FinishOf(w, 24 * 60 + 5);
        Assert.Equal(setAt + 24 * 60, done.Minute);
        Assert.Equal(Resource.DriedMeat, done.Made);
        Assert.Equal(0.4, done.Kg, 9);
        Assert.Equal(LocationFeatureKind.Rack, done.Station.Kind);

        var take = Offer(w, a => a is TakeFinished);
        Assert.Equal(1, Sim.Step(w, take.Activity, take.Minutes).MinutesElapsed);
        Assert.Equal(0.4, Assert.Single(Pack(w).Pieces[Resource.DriedMeat]), 9);
    }

    static double Done(World w) => w.Racks.Values.Single().Work.Working.Single().DonePct;

    static void Fire(World w, double birchKg, PitType pit = PitType.Stone)
    {
        var f = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = w.Minute, Pit = pit };
        f.BurningKg[Resource.BirchFirewood] = birchKg;
        f.UnburnedKg[Resource.BirchFirewood] = birchKg;
        w.Fires[new FireId(w.NextFireId++)] = f;
    }

    static long MeatFinish(World w)
    {
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        long setAt = w.Minute;
        PutOn(w, _ => true);
        return FinishOf(w, 24 * 60 + 5).Minute - setAt;
    }

    [Fact]
    public void Meat_MakesNoProgress_InFallingSnow_AndLosesNothing()
    {
        var w = WithRack(Blizzard);
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        PutOn(w, _ => true);
        Assert.DoesNotContain(Sim.Advance(w, 30 * 60), e => e is TransformationFinished);
        Assert.Equal(0, Done(w));
        Assert.Equal(0.5, w.Racks.Values.Single().Work.Working.Single().InputKg);
    }

    [Fact]
    public void Meat_UnderCover_DriesInFallingSnow()
    {
        var w = WithRack(Blizzard);
        Shelters.At(w, Map.Of(w).Camp, [])!.OverheadLevel = 0.8;
        Assert.True(MeatFinish(w) <= 24 * 60);
    }

    [Fact]
    public void ARack_BesideABurningFire_FinishesSooner_ButNeverInUnderHalfTheTime()
    {
        var w = WithRack();
        Fire(w, birchKg: 3);
        long took = MeatFinish(w);
        Assert.InRange(took, 12 * 60, 24 * 60 - 60);
    }

    [Fact]
    public void ARoaringFire_OnAWarmDay_DriesAtTwiceTheRate_AndNoFaster()
    {
        var w = WithRack(new(0, WeatherCondition.Clear, 15, 0, 0, 0, WindDirection.North));
        Fire(w, birchKg: 6);
        Assert.Equal(12 * 60, MeatFinish(w));
    }

    [Fact]
    public void ARack_InDeepCold_OnADryStillDay_TakesTheRecipesTime()
    {
        var w = WithRack(new(0, WeatherCondition.Clear, -40, 0, 0, 0, WindDirection.North));
        Assert.Equal(24 * 60, MeatFinish(w));
    }

    [Fact]
    public void ARack_InWind_FinishesALittleSooner()
    {
        var w = WithRack(new(0, WeatherCondition.Cloudy, -12, 0.6, 0, 0.8, WindDirection.North));
        Assert.InRange(MeatFinish(w), 20 * 60, 24 * 60 - 10);
    }

    /// A seed whose first days have snow and clear spells at the camp.
    const int StormSeed = 17;

    /// The seeded sky: some minutes snow falls on the rack and it waits, the rest it dries, and it finishes.
    [Fact]
    public void Drying_ThatStoppedForAStorm_GoesOnAfterwards_FromWhereItWas()
    {
        var w = TestWorlds.Bare(StormSeed);
        TestKit.ClearResources(Pack(w));
        RackMaterials(w);
        Build(w);
        Shelters.At(w, Map.Of(w).Camp, [])!.OverheadLevel = 0; // open to the sky
        TestKit.ClearResources(Pack(w));
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        PutOn(w, _ => true);
        int stopped = 0, dried = 0;
        for (int i = 0; i < 4 * 24 * 60; i++)
        {
            double before = Done(w);
            var sky = Exposure.Surface(w, Map.Of(w).Camp, w.Minute + 1);
            bool snowing = sky.SnowfallWeMPerMinute + sky.RainfallMPerMinute > 0;
            if (Sim.Advance(w, 1).OfType<TransformationFinished>().Any())
            {
                Assert.False(snowing);
                Assert.True(stopped > 0 && dried > 0, $"stopped {stopped}, dried {dried}");
                return;
            }
            if (snowing) { Assert.Equal(before, Done(w)); stopped++; }
            else { Assert.True(Done(w) > before); dried++; }
        }
        throw new Xunit.Sdk.XunitException($"not finished in four days: stopped {stopped}, dried {dried}");
    }

    [Fact]
    public void TheMenu_SaysWhy_WhenSnowIsFallingOnIt()
    {
        var w = WithRack(Blizzard);
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        PutOn(w, _ => true);
        var progress = Offer(w, a => a is TakeBack).Progress!;
        Assert.Null(progress.FinishMinute);
        Assert.Equal("snow is falling on it", Narration.Why(progress.Why!.Value));
    }

    [Fact]
    public void TheMenu_SaysWhatSpeedsIt_BesideAFire()
    {
        var w = WithRack();
        Fire(w, birchKg: 3);
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        PutOn(w, _ => true);
        var progress = Offer(w, a => a is TakeBack).Progress!;
        Assert.Null(progress.Why);
        Assert.Equal("the fire beside it speeds it", Narration.Why(progress.Faster!.Value));
    }

    [Fact]
    public void AFish_DriesInSixteenHours_ToAQuarterOfItsWeight()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.RawFish, 0.8); // nothing catches fish yet (#62): placed by hand
        long setAt = w.Minute;
        PutOn(w, _ => true);
        var done = FinishOf(w, 16 * 60 + 5);
        Assert.Equal(setAt + 16 * 60, done.Minute);
        Assert.Equal(Resource.DriedFish, done.Made);
        Assert.Equal(0.2, done.Kg, 9);
    }

    [Fact]
    public void HalfAKiloOfBerries_IsOneHundredFiftyGramsDried_InTwelveHours_AndAPlaceTakesNoMoreThanHalfAKilo()
    {
        var w = WithRack();
        Items.Add(Pack(w), Resource.Berries, 0.8);
        long setAt = w.Minute;
        PutOn(w, _ => true);
        Assert.Equal(0.3, Items.Have(Pack(w), Resource.Berries), 9); // the place took 0.5 kg and refused the rest
        var done = FinishOf(w, 12 * 60 + 5);
        Assert.Equal(setAt + 12 * 60, done.Minute);
        Assert.Equal(Resource.DriedBerries, done.Made);
        Assert.Equal(0.15, done.Kg, 9);
    }

    [Fact]
    public void AFifthThing_IsRefused_NamingTheRoom_FinishedBerriesHoldingTheirPlaces()
    {
        var w = WithRack();
        for (int i = 0; i < 2; i++)
        {
            Items.Add(Pack(w), Resource.Berries, 0.05);
            PutOn(w, a => a is PutOn { Recipe: "dry_berries" });
        }
        Sim.Advance(w, 12 * 60 + 5); // both are done, and wait on the rack together
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.3);
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.4);
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        PutOn(w, a => a is PutOn { Recipe: "dry_meat" });
        PutOn(w, a => a is PutOn { Recipe: "dry_meat" });
        var fifth = Offer(w, a => a is PutOn { Recipe: "dry_meat" });
        Assert.False(fifth.Allowed);
        Assert.Equal("the rack has room for 4, and all of it is taken", fifth.Refused!.Reason);
        Assert.Equal(0, Sim.Step(w, fifth.Activity, 1).MinutesElapsed);
        Assert.Equal(1, Items.Count(Pack(w), Resource.RawMeat));

        var take = Offer(w, a => a is TakeFinished);
        Sim.Step(w, take.Activity, take.Minutes);
        Assert.True(Offer(w, a => a is PutOn { Recipe: "dry_meat" }).Allowed); // taking them frees their places
    }

    [Fact]
    public void Take_MovesEverythingFinishedToThePack_AndTakeBack_ReturnsTheSamePiece_ProgressLost()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.6);
        Items.Add(Pack(w), Resource.Berries, 0.4);
        PutOn(w, a => a is PutOn { Recipe: "dry_berries" });
        PutOn(w, a => a is PutOn { Recipe: "dry_meat" });
        Sim.Advance(w, 12 * 60);
        Assert.DoesNotContain(RackMenu(w), v => v.Activity is TakeBack { Input: Resource.Berries });

        var take = Offer(w, a => a is TakeFinished);
        Assert.Equal(1, Sim.Step(w, take.Activity, take.Minutes).MinutesElapsed);
        Assert.Equal(0.12, Items.Have(Pack(w), Resource.DriedBerries), 9);
        Assert.DoesNotContain(RackMenu(w), v => v.Activity is TakeFinished);

        var back = Offer(w, a => a is TakeBack);
        Assert.Equal(1, Sim.Step(w, back.Activity, back.Minutes).MinutesElapsed);
        Assert.Equal([0.6], Pack(w).Pieces[Resource.RawMeat]);
        Assert.DoesNotContain(RackMenu(w), v => v.Activity is TakeBack);

        long again = w.Minute;
        PutOn(w, a => a is PutOn { Recipe: "dry_meat" });
        Assert.Equal(again + 24 * 60, FinishOf(w, 24 * 60 + 5).Minute); // from nothing
    }

    [Fact]
    public void AStoneOrAStick_IsNotOfferedForTheRack()
    {
        var w = WithRack();
        TestKit.GivePieces(Pack(w), Resource.Stone, 1);
        TestKit.GivePieces(Pack(w), Resource.Stick, 2);
        Assert.DoesNotContain(RackMenu(w), v => v.Activity is PutOn);
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        Assert.Equal("Put on raw meat", Assert.Single(RackMenu(w), v => v.Activity is PutOn).Label);
    }

    [Fact]
    public void EachRack_KeepsItsOwnThings()
    {
        var w = WithRack();
        RackMaterials(w);
        Build(w);
        TestKit.ClearResources(Pack(w));
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        var second = Racks(w)[1];
        var v = second.Menu.Single(x => x.Activity is PutOn);
        Sim.Step(w, v.Activity, v.Minutes);
        var racks = Racks(w);
        Assert.DoesNotContain(racks[0].Menu, x => x.Activity is TakeBack);
        Assert.Contains(racks[1].Menu, x => x.Activity is TakeBack);
    }

    [Fact]
    public void TheMenu_ForecastsTheFinish_AndItAgreesWithWhatThenHappens()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        PutOn(w, _ => true);
        Sim.Advance(w, 100);
        var progress = Offer(w, a => a is TakeBack).Progress!;
        Assert.Null(progress.Why);
        Assert.Equal(progress.FinishMinute, FinishOf(w, 24 * 60).Minute);
    }

    [Fact]
    public void TheRack_AndWhatIsOnIt_SurviveSaveAndLoad_WithTheFinishMinuteUnchanged()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.RawMeat, 0.5);
        long setAt = w.Minute;
        PutOn(w, _ => true);
        Sim.Advance(w, 600);
        var loaded = TestWorlds.Load(Save.ToJson(w));
        Assert.Single(Racks(loaded));
        Assert.Equal(setAt + 24 * 60, FinishOf(w, 24 * 60).Minute);
        Assert.Equal(setAt + 24 * 60, FinishOf(loaded, 24 * 60).Minute);
    }

    [Fact]
    public void Drying_IsNotByHand()
    {
        var w = WithRack();
        Assert.DoesNotContain(Verbs.At(w, P).Make, v => v.Activity is Make { Recipe: "dry_meat" or "dry_fish" or "dry_berries" or "cure_hide" or "cure_mammoth_hide" });
        Assert.Contains("put it on at a rack", Sim.Refusal(w, new Make(P, "dry_meat"))!.Reason);
        Assert.Contains("put it on at a rack", Sim.Refusal(w, new Make(P, "cure_hide"))!.Reason);
    }

    // ---- hides (#191) ----

    [Fact]
    public void AScrapedHide_IsACuredHide_FortyEightHoursLater_WithNoOneActing()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.ScrapedHide, 2.4);
        long setAt = w.Minute;
        PutOn(w, a => a is PutOn { Recipe: "cure_hide" });
        Assert.Equal(0, Items.Count(Pack(w), Resource.ScrapedHide));

        var done = FinishOf(w, 48 * 60 + 5);
        Assert.Equal(setAt + 48 * 60, done.Minute);
        Assert.Equal(Resource.CuredHide, done.Made);
        Assert.Equal(2.16, done.Kg, 9);
        var take = Offer(w, a => a is TakeFinished);
        Sim.Step(w, take.Activity, take.Minutes);
        Assert.Equal(2.16, Assert.Single(Pack(w).Pieces[Resource.CuredHide]), 9);
    }

    [Fact]
    public void AScrapedMammothHide_CuresToNineTenthsOfItsWeight()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.ScrapedMammothHide, 8.0);
        long setAt = w.Minute;
        PutOn(w, a => a is PutOn { Recipe: "cure_mammoth_hide" });
        var done = FinishOf(w, 48 * 60 + 5);
        Assert.Equal(setAt + 48 * 60, done.Minute);
        Assert.Equal(Resource.CuredMammothHide, done.Made);
        Assert.Equal(7.2, done.Kg, 9);
    }

    [Fact]
    public void AHalfCuredHide_SurvivesSaveAndLoad_WithTheFinishMinuteUnchanged()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.ScrapedHide, 2.4);
        long setAt = w.Minute;
        PutOn(w, a => a is PutOn { Recipe: "cure_hide" });
        Sim.Advance(w, 24 * 60);
        var loaded = TestWorlds.Load(Save.ToJson(w));
        var done = FinishOf(loaded, 24 * 60 + 5);
        Assert.Equal(setAt + 48 * 60, done.Minute);
        Assert.Equal(2.16, done.Kg, 9);
    }

    /// One place takes one hide, however many are carried.
    [Fact]
    public void ARawHide_IsNotOfferedForTheRack_AndAPlaceTakesOneScrapedHide()
    {
        var w = WithRack();
        Items.AddPiece(Pack(w), Resource.Hide, 3.0);
        Items.AddPiece(Pack(w), Resource.MammothHide, 10.0);
        Assert.DoesNotContain(RackMenu(w), v => v.Activity is PutOn);
        Items.AddPieces(Pack(w), Resource.ScrapedHide, [2.4, 1.0]);
        Assert.Equal("cure_hide", Assert.IsType<PutOn>(Assert.Single(RackMenu(w), v => v.Activity is PutOn).Activity).Recipe);
        PutOn(w, a => a is PutOn { Recipe: "cure_hide" });
        Assert.Equal(1, Items.Count(Pack(w), Resource.ScrapedHide));
        Assert.Equal(1, Items.Count(Pack(w), Resource.Hide));
    }

    [Fact]
    public void NoHandRecipe_CuresAHide()
    {
        Assert.DoesNotContain(Recipes.ByHand, r => r.Result.Resource is Resource.CuredHide or Resource.CuredMammothHide);
        Assert.DoesNotContain(Verbs.At(WithRack(), P).Make, v => v.Label.Contains("hide by the fire"));
    }

    /// The way to anything sewn from a cured hide: a hide scraped by hand, cured on the rack, cut into strips.
    [Fact]
    public void AHide_IsScraped_ThenCuredOnTheRack_ThenCutIntoStrips()
    {
        var w = WithRack();
        w.Minute = Math.Max(w.Minute, 3 * 60); // daylight, for the scraping and the cutting
        Pack(w).Gear.Add(Items.Make("flint_knife"));
        Items.AddPiece(Pack(w), Resource.Hide, 3.0);
        Assert.Null(Sim.Step(w, new Make(P, "scrape_hide"), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("scrape_hide"), null, []));
        Assert.Equal(2.4, Assert.Single(Pack(w).Pieces[Resource.ScrapedHide]), 9);

        PutOn(w, a => a is PutOn { Recipe: "cure_hide" });
        FinishOf(w, 48 * 60 + 5);
        var take = Offer(w, a => a is TakeFinished);
        Sim.Step(w, take.Activity, take.Minutes);
        Assert.Equal(2.16, Assert.Single(Pack(w).Pieces[Resource.CuredHide]), 9);

        // The sleep, food and water of the two days' wait, which this test is not about.
        var body = w.Agent(P).Body;
        (body.EnergyMinutes, body.HydrationMl) = (Body.MaxEnergyMinutes, Body.MaxHydrationMl);
        body.Effects.Clear();
        Assert.Null(Sim.Step(w, new Make(P, "hide_strips"), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("hide_strips"), null, []));
        Assert.Equal(0, Items.Count(Pack(w), Resource.CuredHide));
        Assert.True(Items.Count(Pack(w), Resource.HideStrip) >= 2);
    }
}
