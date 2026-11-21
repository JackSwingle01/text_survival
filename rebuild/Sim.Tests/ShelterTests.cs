namespace Survival.Tests;

/// Shelters, bedding and building them. Temperature goldens are the old Location.GetTemperatureBreakdown
/// with the old camp Overhang (tmp harness p4b/oldshelter).
public class ShelterTests
{
    static readonly AgentId P = AgentId.Player;
    const int Digits = 9;
    static readonly WeatherSample Calm = new(0, WeatherCondition.Clear, -10, 3 / 20.1, 0, 0.3, WindDirection.North);
    static readonly WeatherSample Snowy = new(0, WeatherCondition.LightSnow, -20, 0.6, 0.5, 0.9, WindDirection.North);
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
    static readonly ShelterCover Overhang = new(0.55, 0.3, 0.4);

    static World At(WeatherSample sky)
    {
        var w = World.New(17);
        w.FixedWeather = sky;
        return w;
    }

    /// An ordinary open tile: not camp, a cave, a pass stage or beside a river.
    static TileId Open(World w)
    {
        var m = Map.Of(w);
        return (from x in Enumerable.Range(0, m.Width) from y in Enumerable.Range(0, m.Height)
                let t = new TileId(x, y)
                where m.TerrainAt(t) == Terrain.Plain && t != m.Camp && m.CaveAt(t) == Cave.None && m.StageAt(t) == null && !m.BesideRiver(t)
                select t).First();
    }

    static void LightCampFire(World w)
    {
        var f = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = 0, Pit = PitType.Open };
        Fire.AddFuel(f, Resource.BirchBark, 0.05);
        Fire.AddFuel(f, Resource.Kindling, 1.0);
        Fire.IgniteAll(f);
        Fire.AddFuel(f, Resource.Birch, 3.0);
        var events = new List<WorldEvent>();
        for (int i = 1; i <= 30; i++) Fire.Tick(f, new FireId(0), i, events);
        w.Fires[new FireId(w.NextFireId++)] = f;
        w.Minute = f.LastUpdatedMinute;
    }

    // ---- the old temperature step ----

    [Theory]
    [InlineData("calm", 30.53625944553523, 19.419973477794883)]
    [InlineData("snowy", 6.145251967907093, -22.3255645003846)]
    public void ShelterStep_MatchesOld(string sky, double oldIdleF, double oldMovingF)
    {
        var w = At(sky == "calm" ? Calm : Snowy);
        var camp = Map.Of(w).Camp;
        var s = Weather.At(w, 0);
        // The old game never read wind coverage: with none, the port is the old step exactly.
        Assert.Equal(oldIdleF, Weather.ShelteredAirF(w, camp, s, 0, Overhang with { WindLevel = 0 }).AirF, Digits);
        Assert.Equal(oldMovingF, Weather.ShelteredAirF(w, camp, s, 0, default).AirF, Digits);
        Assert.Equal(oldMovingF, Weather.AirAtF(w, camp, s, 0), Digits);
    }

    /// Wind coverage cuts the wind that reaches you: the old step on a tile whose wind factor is the
    /// camp's 0.4 times what the walls let through. Moving forfeits the shelter (old IsStationary).
    [Theory]
    [InlineData("calm", 30.617350893324215, 19.419973477794883, 111.43729522069367)]
    [InlineData("snowy", 9.3047090110841, -22.3255645003846, 90.42666263762786)]
    public void CampOverhang_ThroughSurroundings_MatchesOldWithWindCoverageRead(string sky, double idleF, double movingF, double effectiveF)
    {
        var w = At(sky == "calm" ? Calm : Snowy);
        Assert.NotNull(Shelters.At(w, Map.Of(w).Camp, []));
        Assert.Equal(idleF, Sim.Surroundings(w, w.Agent(P), new Wait(P), []).AirTempF, Digits);
        Assert.Equal(movingF, Sim.Surroundings(w, w.Agent(P), new Travel(P, Direction.North), []).AirTempF, Digits);
        LightCampFire(w);
        var ctx = Sim.Surroundings(w, w.Agent(P), new Wait(P), []);
        Assert.Equal(effectiveF, ctx.AirTempF + ctx.FireBonusF, Digits);
    }

    [Fact]
    public void ShelterStep_NeverCoolsAWarmDay()
    {
        var s = Mild with { TemperatureC = 25 };
        Assert.Equal(77, Weather.ShelterStep(77, s, 0.9, 1, 0, 0).AirF, Digits);
    }

    /// old game: wind coverage was improved with materials and shown, and nothing read it (bug, fixed).
    /// Walls cut the wind as a fire pit's do: the chill, and the wind through your clothes (drying by a
    /// fire is the exception: see ShelterWalls_DoNotSlowDryingByAFire). Sealed in a snow shelter, the body has next to no wind; walking out, it has the sky's.
    [Fact]
    public void WindCoverage_SealedSnowShelter_LeavesTheBodyNoWind()
    {
        var w = At(Snowy);
        var tile = Open(w);
        w.Agent(P).Tile = tile;
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter, Wind: 1.0, WindCap: 1.0));
        Assert.True(Sim.Surroundings(w, w.Agent(P), new Wait(P), []).WindLevel < 0.01);
        Assert.Equal(0.6, Sim.Surroundings(w, w.Agent(P), new Travel(P, Direction.North), []).WindLevel, Digits);

        var camp = Map.Of(w).Camp;
        w.Agent(P).Tile = camp;
        Assert.Equal(0.6 * (1 - Overhang.WindLevel), Sim.Surroundings(w, w.Agent(P), new Wait(P), []).WindLevel, Digits);
        var s = Weather.At(w, 0);
        Assert.True(Weather.ShelteredAirF(w, camp, s, 0, Overhang).AirF > Weather.ShelteredAirF(w, camp, s, 0, Overhang with { WindLevel = 0 }).AirF);
    }

    /// Old game parity: drying at a fire read the open wind less the pit's walls (old SurvivalContext
    /// CalculateEffectiveWindSpeed into SurvivalProcessor's drying), never a shelter's walls. With no
    /// fire, ambient drying reads the sheltered wind, so walls slow it.
    [Fact]
    public void ShelterWalls_DoNotSlowDryingByAFire_ButSlowAmbientDrying()
    {
        // Ten minutes from soaked: part dried, so the rate shows (a fire dries soaked clothing in about an hour).
        static double WetAfter(SurvivalContext ctx)
        {
            var b = BodyState.BaselineHuman();
            Effects.Set(b, EffectKind.Wet, 1.0);
            double wet = Body.Process(b, ctx, 10, Rng.ForSystem(1, "test")).Effects.Single(e => e.Kind == EffectKind.Wet).Severity;
            Assert.InRange(wet, 0.01, 0.999);
            return wet;
        }
        var w = At(Calm with { TemperatureC = 8 }); // above freezing, so ambient drying runs too
        var camp = Map.Of(w).Camp;
        Assert.NotNull(Shelters.At(w, camp, []));
        double openWind = Weather.At(w, w.Minute).WindLevel;

        // No fire: the walls cut the drying wind.
        var still = Sim.Surroundings(w, w.Agent(P), new Wait(P), []);
        Assert.True(still.FireBonusF == 0 && still.WindLevel < openWind - 1e-6);
        Assert.True(WetAfter(still) > WetAfter(still with { WindLevel = openWind }) + 1e-6, "walls should slow ambient drying");

        // By a fire: the walls cut the chill (WindLevel) and not the drying.
        LightCampFire(w);
        var byFire = Sim.Surroundings(w, w.Agent(P), new Wait(P), []);
        var warmth = Fire.WarmthFor(w, P, Shelters.CoverFor(w, camp, stationary: true, events: []));
        Assert.True(byFire.FireBonusF > 0);
        Assert.Equal(openWind * (1 - warmth.WindProtectionLevel), byFire.FireDryingWindLevel!.Value, Digits);
        Assert.True(byFire.WindLevel < byFire.FireDryingWindLevel.Value - 1e-6);
        Assert.Equal(WetAfter(byFire with { WindLevel = byFire.FireDryingWindLevel.Value }), WetAfter(byFire), Digits);
    }

    /// Old SurvivalContext: a roof over someone keeping still, else the sky's own cover. The ground
    /// under it still gets the rain (Ground reads Map.OverheadCover only).
    [Fact]
    public void Roof_KeepsTheRainOffAStillPerson_NotTheGround()
    {
        var w = At(Snowy);
        var tile = Open(w);
        w.Agent(P).Tile = tile;
        Shelters.Place(w, tile, new ShelterDef("Rock Overhang", ShelterKind.RockOverhang));
        Assert.Equal(0.7, Sim.Surroundings(w, w.Agent(P), new Wait(P), []).OverheadCoverLevel, Digits);
        Assert.Equal(0.0, Sim.Surroundings(w, w.Agent(P), new Travel(P, Direction.North), []).OverheadCoverLevel, Digits);
        Assert.Equal(0.0, Map.OverheadCover(Map.Of(w), tile));
    }

    /// old game: the fire's heat a shelter held in was air, so a sheltered sleeper by a big fire could
    /// not shift away from it and sweated out every litre (bug, fixed): it is withheld with the
    /// radiant heat once the body sweats.
    [Fact]
    public void ShelterHeldFireHeat_IsWithheldFromASweatingBody()
    {
        var w = At(Calm);
        LightCampFire(w);
        var warmth = Fire.WarmthFor(w, P, Overhang);
        Assert.True(warmth.ShelterHeatF > 10);
        var ctx = Sim.Surroundings(w, w.Agent(P), new Sleep(P), []);
        double open = Weather.ShelteredAirF(w, Map.Of(w).Camp, Weather.At(w, w.Minute), 0, Overhang).AirF + warmth.AirHeatF;
        Assert.Equal(open, ctx.AirTempF, Digits);
        Assert.Equal(warmth.PitHeatF * 2 + warmth.ShelterHeatF, ctx.FireBonusF, Digits);
    }

    // ---- bedding ----

    [Fact]
    public void CampBedding_SleepsAtFullQuality_AndKeepsTheGroundOff()
    {
        var w = At(Mild);
        var sleep = Sim.Surroundings(w, w.Agent(P), new Sleep(P), []);
        Assert.Equal(1.0, sleep.BeddingQualityPct);
        Assert.Equal(0.9, sleep.GroundProtectionLevel, Digits); // old 0.5 + quality * 0.4
        Assert.Equal(0.0, Sim.Surroundings(w, w.Agent(P), new Wait(P), []).GroundProtectionLevel); // on your feet
        w.Agent(P).Tile = Open(w);
        sleep = Sim.Surroundings(w, w.Agent(P), new Sleep(P), []);
        Assert.Equal(SurvivalContext.NoBeddingQualityPct, sleep.BeddingQualityPct);
        Assert.Equal(0.0, sleep.GroundProtectionLevel);
    }

    [Fact]
    public void BetterBedding_HealsSleepFaster()
    {
        double Healed(double quality)
        {
            var w = At(Mild);
            var body = w.Agent(P).Body;
            Body.Damage(body, new DamageInfo(10, DamageType.Blunt, BodyTarget.LeftLeg), w.Rng("test"));
            double Condition() => body.Regions.Sum(r => r.Layers.Sum(l => l.Condition));
            double before = Condition();
            var ctx = Sim.Surroundings(w, w.Agent(P), new Sleep(P), []) with { BeddingQualityPct = quality };
            for (int i = 0; i < 10; i++) Body.Update(body, ctx, w.Rng("test"));
            return Condition() - before;
        }
        Assert.True(Healed(1.0) > Healed(SurvivalContext.NoBeddingQualityPct));
    }

    /// old game: WarmthBonus was authored ("applied during sleep") and never read (bug, fixed).
    [Fact]
    public void SleepingBag_WarmsTheSleeper()
    {
        var w = At(Mild);
        Shelters.PlaceBedding(w, Map.Of(w).Camp, BeddingKind.SleepingBag);
        double awake = Sim.Surroundings(w, w.Agent(P), new Rest(P), []).AirTempF;
        Assert.Equal(awake + 5, Sim.Surroundings(w, w.Agent(P), new Sleep(P), []).AirTempF, Digits);
    }

    /// Old MakeCamp was offered only where there was no bedding: a bed no better than the one there
    /// is refused, and costs nothing.
    [Fact]
    public void Bedding_NoBetterThanTheOneThere_IsRefused()
    {
        var w = At(Mild);
        var camp = Map.Of(w).Camp;
        var r = Sim.Step(w, new Build(P, Structure.MakeshiftBedding), 60);
        Assert.Equal(0, r.MinutesElapsed);
        Assert.Contains("as good as", r.InterruptedReason);
        Assert.Equal(BeddingKind.Camp, w.Beddings[camp]);
        Assert.Empty(w.Projects);
        // A sleeping bag is as good to sleep on and warmer: it replaces the sleeping area.
        Items.Add(Items.Pack(w, P), Resource.CuredHide, 4);
        Items.Add(Items.Pack(w, P), Resource.Sinew, 0.6);
        Assert.Null(Shelters.Refusal(w, new Build(P, Structure.SleepingBag)));
    }

    /// old game: new bedding went in behind the old and GetFeature found the old one, so crafting a
    /// better bed at camp did nothing (bug, fixed).
    [Fact]
    public void NewBedding_ReplacesTheOld()
    {
        var w = At(Mild);
        Shelters.PlaceBedding(w, Map.Of(w).Camp, BeddingKind.Makeshift);
        Shelters.PlaceBedding(w, Map.Of(w).Camp, BeddingKind.Padded);
        Assert.Equal(0.8, Shelters.BeddingAt(w, Map.Of(w).Camp)!.QualityPct);
    }

    // ---- building ----

    static World Builder(out TileId tile)
    {
        var w = At(Mild);
        tile = Open(w);
        w.Agent(P).Tile = tile;
        return w;
    }

    [Fact]
    public void BranchFrame_TakesItsSticksAndHalfAnHour()
    {
        var w = Builder(out var tile);
        var pack = Items.Pack(w, P);
        Items.Add(pack, Resource.Kindling, 1.5);
        double sticks = Items.Have(pack, Resource.Kindling);
        var r = Sim.Step(w, new Build(P, Structure.BranchFrame), 120);
        Assert.Equal("built", r.InterruptedReason);
        Assert.Equal(30, r.MinutesElapsed);
        Assert.Contains(r.Events, e => e is Built { What: Structure.BranchFrame });
        Assert.Equal(sticks - 4 * 0.3, Items.Have(pack, Resource.Kindling), Digits);
        var s = Shelters.At(w, tile, [])!;
        Assert.Equal((0.15, 0.25, 0.20), (s.InsulationLevel, s.OverheadLevel, s.WindLevel));
        Assert.Contains("already a shelter", Sim.Step(w, new Build(P, Structure.LogFrame), 10).InterruptedReason);
    }

    /// Resuming a half-built snow shelter checks its shovel and the frost again, and so does every
    /// minute of the work.
    [Fact]
    public void SnowShelter_Resumed_StillNeedsItsShovel()
    {
        var w = At(Snowy);
        var tile = Open(w);
        w.Agent(P).Tile = tile;
        var shovel = Items.Make("bone_shovel");
        Items.Pack(w, P).Gear.Add(shovel);
        Assert.Null(Sim.Step(w, new Build(P, Structure.SnowShelter), 10).InterruptedReason);
        Items.Pack(w, P).Gear.Remove(shovel);
        double done = w.Projects[tile].MinutesDone;
        var r = Sim.Step(w, new Build(P, Structure.SnowShelter), 30);
        Assert.Equal((0, "you need a shovel"), (r.MinutesElapsed, r.InterruptedReason));
        Assert.Equal(done, w.Projects[tile].MinutesDone);
        // Lost mid-work, the same reason stops it.
        Assert.Equal("you need a shovel", Shelters.Minute(w, w.Agent(P), new Build(P, Structure.SnowShelter), []));
        Assert.Equal(done, w.Projects[tile].MinutesDone);
    }

    [Fact]
    public void SnowShelter_Resumed_InAThaw_IsRefused()
    {
        var w = Builder(out var tile); // 41 °F
        Items.Pack(w, P).Gear.Add(Items.Make("bone_shovel"));
        w.Projects[tile] = new Project { Building = Structure.SnowShelter, MinutesDone = 60 };
        var r = Sim.Step(w, new Build(P, Structure.SnowShelter), 30);
        Assert.Equal((0, "too warm to build in snow"), (r.MinutesElapsed, r.InterruptedReason));
        Assert.Equal("too warm to build in snow", Shelters.Minute(w, w.Agent(P), new Build(P, Structure.SnowShelter), []));
        Assert.Equal(60, w.Projects[tile].MinutesDone);
    }

    [Fact]
    public void Build_Refusals_CostNothing()
    {
        var w = Builder(out _);
        var r = Sim.Step(w, new Build(P, Structure.BranchFrame), 30);
        Assert.Equal(0, r.MinutesElapsed);
        Assert.Contains("you need", r.InterruptedReason);
        Assert.Equal("you need a shovel", Sim.Step(w, new Build(P, Structure.SnowShelter), 30).InterruptedReason);
        Items.Pack(w, P).Gear.Add(Items.Make("bone_shovel"));
        Assert.Equal("too warm to build in snow", Sim.Step(w, new Build(P, Structure.SnowShelter), 30).InterruptedReason);
        Assert.Equal("you have no tent", Sim.Step(w, new Build(P, Structure.Tent), 10).InterruptedReason);
    }

    /// Work left off is picked up again, and anything else waits for it.
    [Fact]
    public void Project_PersistsAcrossSteps_AndBlocksAnotherBuild()
    {
        var w = Builder(out var tile);
        Items.Add(Items.Pack(w, P), Resource.Kindling, 1.2);
        Items.Add(Items.Pack(w, P), Resource.Pine, 6);
        Assert.Null(Sim.Step(w, new Build(P, Structure.BranchFrame), 10).InterruptedReason);
        Assert.Contains("half-built", Sim.Step(w, new Build(P, Structure.LogFrame), 10).InterruptedReason);
        Assert.Equal(6, Items.Have(Items.Pack(w, P), Resource.Pine));
        var r = Sim.Step(w, new Build(P, Structure.BranchFrame), 60);
        Assert.Equal(("built", 20), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Empty(w.Projects);
    }

    static string BuildAndImprove(int chunk, bool reload = false)
    {
        var w = Builder(out _);
        Items.Add(Items.Pack(w, P), Resource.Kindling, 1.2);
        Items.Add(Items.Pack(w, P), Resource.Hide, 3);
        foreach (var (a, total) in new (Activity, int)[] { (new Build(P, Structure.BranchFrame), 30), (new ImproveShelter(P, ShelterStat.Insulation, Resource.Hide), 50) })
            for (int done = 0; done < total; done += chunk)
            {
                Sim.Step(w, a, Math.Min(chunk, total - done));
                if (reload) w = Save.FromJson(Save.ToJson(w));
            }
        return Save.ToJson(w);
    }

    [Fact]
    public void BuildAndImprove_SameWorld_HoweverTheStepsAreCut_AndThroughASave()
    {
        string whole = BuildAndImprove(80);
        Assert.Equal(whole, BuildAndImprove(1));
        Assert.Equal(whole, BuildAndImprove(7, reload: true));
    }

    /// Old Improve: 0.05 x effectiveness x the room under the cap, per piece, ten minutes a piece.
    [Fact]
    public void Improve_UsesAPieceInTenMinutes_WithDiminishingReturns()
    {
        var w = Builder(out var tile);
        var s = Shelters.Place(w, tile, new ShelterDef("Frame", ShelterKind.BranchFrame));
        Items.Add(Items.Pack(w, P), Resource.Hide, 5);
        Sim.Step(w, new ImproveShelter(P, ShelterStat.Insulation, Resource.Hide), 10);
        Assert.Equal(4, Items.Have(Items.Pack(w, P), Resource.Hide), Digits);
        // Ten tenths of a piece come close to the old one-piece gain 0.05 * 0.9 * (1 - 0.15/0.5) = 0.0315.
        Assert.InRange(s.InsulationLevel - 0.15, 0.030, 0.0315);
        s.InsulationLevel = s.InsulationCap;
        Assert.Contains("can't be improved", Sim.Step(w, new ImproveShelter(P, ShelterStat.Insulation, Resource.Hide), 10).InterruptedReason);
        Assert.Contains("no use", Sim.Step(w, new ImproveShelter(P, ShelterStat.Wind, Resource.Berries), 10).InterruptedReason);
    }

    /// old game: the pitched tent's item was [JsonIgnore], so after a load it could never be packed (bug, fixed).
    [Fact]
    public void Tent_PitchesAndPacks_ThroughASave()
    {
        var w = Builder(out var tile);
        Items.Pack(w, P).Gear.Add(Items.Make("hide_tent"));
        var r = Sim.Step(w, new Build(P, Structure.Tent), 60);
        Assert.Equal(("built", 10), (r.InterruptedReason, r.MinutesElapsed));
        Assert.DoesNotContain(Items.Pack(w, P).Gear, g => g.Kind == "hide_tent");
        Assert.Equal(0.9, Shelters.At(w, tile, [])!.OverheadLevel);
        w = Save.FromJson(Save.ToJson(w));
        Assert.Equal("packed", Sim.Step(w, new PackTent(P), 5).InterruptedReason);
        Assert.Null(Shelters.At(w, tile, []));
        Assert.Contains(Items.Pack(w, P).Gear, g => g.Kind == "hide_tent");
        Assert.Contains("no tent", Sim.Step(w, new PackTent(P), 5).InterruptedReason);
    }

    /// old game: packing gave back the tent as it was first pitched, so work put into it was lost (bug, fixed).
    [Fact]
    public void Tent_KeepsItsImprovements_WhenPackedAndPitchedAgain()
    {
        var w = Builder(out var tile);
        Items.Pack(w, P).Gear.Add(Items.Make("hide_tent"));
        Sim.Step(w, new Build(P, Structure.Tent), 10);
        Items.Add(Items.Pack(w, P), Resource.Hide, 2);
        Sim.Step(w, new ImproveShelter(P, ShelterStat.Insulation, Resource.Hide), 10);
        var improved = Shelters.At(w, tile, [])!;
        double insulation = improved.InsulationLevel;
        Assert.True(insulation > 0.5);
        Sim.Step(w, new PackTent(P), 1);
        w = Save.FromJson(Save.ToJson(w)); // packed through a save too
        w.Agent(P).Tile = tile;
        Assert.Equal("built", Sim.Step(w, new Build(P, Structure.Tent), 10).InterruptedReason);
        var again = Shelters.At(w, tile, [])!;
        Assert.Equal((insulation, 0.9, 0.7), (again.InsulationLevel, again.OverheadLevel, again.WindLevel));
        Assert.Equal((0.6, 0.95, 0.8), (again.InsulationCap, again.OverheadCap, again.WindCap));
    }

    // ---- snow ----

    /// Melting is settled by the hour whenever it is read; a refusal or a report reads without
    /// settling (Shelters.Peek), so being turned away leaves the save as it was.
    [Fact]
    public void SnowShelter_MeltsByTheHour_HoweverOftenItIsLookedAt()
    {
        World Thaw(bool look, bool settle)
        {
            var w = At(Mild with { TemperatureC = 2 });
            var tile = Open(w);
            var camp = Map.Of(w).Camp;
            Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
            for (int i = 0; i < 600; i++)
            {
                Sim.Advance(w, 1); // the player waits at camp, away from the snow
                if (!look) continue;
                w.Agent(P).Tile = tile; // and is turned away at the shelter, every way there is
                Assert.Equal(0, Sim.Step(w, new PackTent(P), 1).MinutesElapsed);
                Assert.Equal(0, Sim.Step(w, new ImproveShelter(P, ShelterStat.Wind, Resource.Hide), 1).MinutesElapsed);
                Assert.Equal(0, Sim.Step(w, new Build(P, Structure.BranchFrame), 1).MinutesElapsed);
                Assert.NotNull(Shelters.Peek(w, tile));
                w.Agent(P).Tile = camp;
            }
            if (settle) Shelters.At(w, tile, []);
            return w;
        }
        Assert.Equal(Save.ToJson(Thaw(true, settle: false)), Save.ToJson(Thaw(false, settle: false)));
        var a = Thaw(true, settle: true);
        Assert.Equal(Save.ToJson(a), Save.ToJson(Thaw(false, settle: true)));
        var s = a.Shelters.Values.Single(x => x.Kind == ShelterKind.SnowShelter);
        Assert.True(s.InsulationLevel < 0.6);
        // Left to the thaw long enough it is gone.
        var b = Thaw(false, settle: true);
        b.Minute += 3 * 24 * 60;
        Assert.Null(Shelters.At(b, Open(b), []));
    }

    /// Snow melts at the air that reaches it: a gale at 37 °F chills a person below freezing but
    /// thaws snow all the same.
    [Fact]
    public void SnowShelter_MeltsInAWindyThaw()
    {
        var w = At(new(0, WeatherCondition.Cloudy, 3, 0.6, 0, 0.9, WindDirection.North));
        var tile = Open(w);
        Assert.True(Weather.AirAtF(w, tile, Weather.At(w, 0), 0) < 32);
        var s = Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        w.Minute += 10 * 60;
        Assert.Same(s, Shelters.At(w, tile, []));
        Assert.Equal(0.6 - 10 * 0.01 * (37.4 - 32), s.InsulationLevel, Digits);
    }

    [Fact]
    public void SnowShelter_HoldsBelowFreezing()
    {
        var w = At(Snowy);
        var tile = Open(w);
        var s = Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        w.Minute += 24 * 60;
        Assert.Same(s, Shelters.At(w, tile, []));
        Assert.Equal(0.6, s.InsulationLevel);
    }

    [Fact]
    public void SnowShelter_IsDugWithAShovel_InTheCold()
    {
        var w = At(Snowy);
        var tile = Open(w);
        w.Agent(P).Tile = tile;
        Items.Pack(w, P).Gear.Add(Items.Make("bone_shovel"));
        w.Agent(P).Body.CoreTempF = 98.6;
        var r = Sim.Step(w, new Build(P, Structure.SnowShelter), 200);
        Assert.Equal(("built", 68), (r.InterruptedReason, r.MinutesElapsed)); // 135 minutes, doubled
        Assert.Equal(ShelterKind.SnowShelter, Shelters.At(w, tile, [])!.Kind);
    }

    // ---- shelter finds and the tile they stand on ----

    /// A tile in the valley holding a hidden shelter find (seed 17), with the player on it in daylight.
    static (World W, TileId Tile, int Pool) FindTile(WeatherSample sky)
    {
        var w = At(sky);
        var m = Map.Of(w);
        var tile = (from x in Enumerable.Range(0, m.Width) from y in Enumerable.Range(0, m.Height)
                    let t = new TileId(x, y)
                    where m.TerrainAt(t).IsPassable() && m.CaveAt(t) == Cave.None && !w.Shelters.ContainsKey(t)
                        && Pools.At(w, t).Any(p => p is ShelterFind)
                    select t).First();
        w.Agent(P).Tile = tile;
        w.Minute = 3 * 60;
        int pool = Pools.At(w, tile).ToList().FindIndex(p => p is ShelterFind);
        return (w, tile, pool);
    }

    static string FindName(World w, TileId tile, int pool) => ((ShelterFind)Pools.At(w, tile)[pool]).Shelter.Name;

    /// old game (and the rebuild until now): a find placed under a half-built frame stranded it - the
    /// frame could not be resumed ("already a shelter"), and nothing else could be built there, ever;
    /// found by someone else mid-build, the frame's completion threw. The frame is finished; the find
    /// waits while it is being built, and once the frame stands for good it is only discovered: a
    /// waiting entry behind a shelter that never comes down waited forever (third review).
    [Fact]
    public void Find_UnderAHalfBuiltFrame_Waits_AndTheFrameIsFinished()
    {
        var (w, tile, pool) = FindTile(Mild);
        Items.Add(Items.Pack(w, P), Resource.Kindling, 2);
        Assert.Null(Sim.Step(w, new Build(P, Structure.BranchFrame), 10).InterruptedReason);
        var events = new List<WorldEvent>();
        Pools.Search(w, w.Agent(P), 1000, events);
        Assert.Contains(events, e => e is PoolDiscovered d && d.Pool == new PoolId(tile, pool));
        Assert.Null(Shelters.Peek(w, tile));
        Assert.Equal(pool, w.WaitingShelters[tile]); // while the frame is going up
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Kind == "shelter find" && f.Text.Contains("waiting"));
        var r = Sim.Step(w, new Build(P, Structure.BranchFrame), 60);
        Assert.Equal("built", r.InterruptedReason);
        Assert.Equal(ShelterKind.BranchFrame, Shelters.At(w, tile, [])!.Kind);
        Assert.Empty(w.WaitingShelters);
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Kind == "shelter find" && f.Text.EndsWith("the Branch Frame Shelter stands in its place"));
        // And the tile is not shut to other work: bedding goes down beside the frame.
        Assert.Equal("built", Sim.Step(w, new Build(P, Structure.MakeshiftBedding), 60).InterruptedReason);
    }

    /// Found under a pitched tent, the find stands as soon as the tent is packed.
    [Fact]
    public void Find_UnderATent_StandsWhenTheTentIsPacked()
    {
        var (w, tile, pool) = FindTile(Mild);
        Items.Pack(w, P).Gear.Add(Items.Make("hide_tent"));
        Assert.Equal("built", Sim.Step(w, new Build(P, Structure.Tent), 30).InterruptedReason);
        Pools.Search(w, w.Agent(P), 1000, []);
        Assert.Equal("Hide Tent", Shelters.At(w, tile, [])!.Name);
        Sim.Step(w, new PackTent(P), 1);
        Assert.Equal(FindName(w, tile, pool), Shelters.At(w, tile, [])!.Name);
        Assert.Empty(w.WaitingShelters);
    }

    /// Found beside a snow shelter, it stands when the snow has melted: seen so at once by a
    /// read that writes nothing, and placed by the next one that settles the melt.
    [Fact]
    public void Find_UnderASnowShelter_StandsWhenItMelts()
    {
        var (w, tile, pool) = FindTile(Mild); // 41 °F: about eleven hours of melt
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        Pools.Search(w, w.Agent(P), 1000, []);
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Text.Contains("waiting"));
        w.Minute += 24 * 60;
        string before = Save.ToJson(w);
        Assert.Equal(FindName(w, tile, pool), Shelters.Peek(w, tile)!.Name);
        Assert.Equal(before, Save.ToJson(w));
        Assert.Equal(FindName(w, tile, pool), Shelters.At(w, tile, [])!.Name);
        Assert.Empty(w.WaitingShelters);
    }

    /// A find someone has found is not reported hidden, though another shelter stands on its tile.
    [Fact]
    public void Find_FoundWhileAnotherStands_IsNotReportedHidden()
    {
        var (w, tile, _) = FindTile(Mild);
        Shelters.Place(w, tile, new ShelterDef("Branch Frame Shelter", ShelterKind.BranchFrame));
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Text.EndsWith("hidden"));
        Pools.Search(w, w.Agent(P), 1000, []);
        Assert.DoesNotContain(Report.Finds(w), f => f.Tile == tile && f.Text.EndsWith("hidden"));
    }

    /// Third review: a second person finding a find that already stands added a waiting entry for
    /// it, which stood it again (throwing "already has a shelter") once the tile came free. A find
    /// knows itself by its pool, not its name.
    [Fact]
    public void Find_FoundAgainByASecondPerson_AddsNothing()
    {
        var (w, tile, pool) = FindTile(Mild);
        Pools.Search(w, w.Agent(P), 1000, []);
        var standing = Shelters.At(w, tile, [])!;
        Assert.Equal(pool, standing.FromFind);
        var npc = Npc.Spawn(w, tile);
        string before = System.Text.Json.JsonSerializer.Serialize(w.Shelters) + System.Text.Json.JsonSerializer.Serialize(w.WaitingShelters);
        Pools.Search(w, w.Agent(npc), 1000, []);
        Assert.Contains(new PoolId(tile, pool), ((PersonMind)w.Agent(npc).Mind).Knows.Discovered);
        Assert.Empty(w.WaitingShelters);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(w.Shelters) + System.Text.Json.JsonSerializer.Serialize(w.WaitingShelters));
        Assert.DoesNotContain(Report.Finds(w), f => f.Tile == tile && f.Kind == "shelter find");
    }

    /// Behind a shelter that stands for good (natural, or a finished frame) a find is only
    /// discovered: there is nothing for it to wait for. Behind a tent or snow it waits.
    [Theory]
    [InlineData(ShelterKind.RockOverhang, false)]
    [InlineData(ShelterKind.BranchFrame, false)]
    [InlineData(ShelterKind.SnowShelter, true)]
    public void Find_WaitsOnlyBehindAShelterThatWillGo(ShelterKind kind, bool waits)
    {
        var (w, tile, pool) = FindTile(Mild);
        Shelters.Place(w, tile, new ShelterDef("In the way", kind));
        Pools.Search(w, w.Agent(P), 1000, []);
        Assert.Equal(waits, w.WaitingShelters.ContainsKey(tile));
        Assert.Equal("In the way", Shelters.Peek(w, tile)!.Name);
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Kind == "shelter find"
            && f.Text.EndsWith(waits ? "waiting for the tile" : "the In the way stands in its place"));
    }

    /// Report.cs used to hide a waiting find whose name matched the standing shelter's, as if it
    /// stood: a find named like the tent in its way vanished from the report while it waited.
    [Fact]
    public void Report_KnowsAWaitingFindByItsPool_NotItsName()
    {
        var (w, tile, pool) = FindTile(Mild);
        Shelters.Place(w, tile, new ShelterDef(FindName(w, tile, pool), ShelterKind.SnowShelter)); // same name, another shelter
        Pools.Search(w, w.Agent(P), 1000, []);
        Assert.Equal(pool, w.WaitingShelters[tile]);
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Kind == "shelter find" && f.Text.EndsWith("waiting for the tile"));
    }

    /// The frost a snow shelter needs is the tile's surface air, the air that melts one: in the cold
    /// of the pass a regional thaw still leaves snow to dig, and a shelter there does not melt.
    [Fact]
    public void SnowShelter_Frost_IsTheTilesAir_AsMeltIs()
    {
        var w = At(new WeatherSample(0, WeatherCondition.Cloudy, 2, 0.1, 0, 0.8, WindDirection.North)); // 35.6 °F
        var m = Map.Of(w);
        var proper = m.PassStageIndex.Single(s => s.Value == 2).Key; // 15 °F colder
        Assert.True(Ground.WeatherAt(w, proper, 0).AirTemperatureF <= 32);
        w.Agent(P).Tile = proper;
        Items.Pack(w, P).Gear.Add(Items.Make("bone_shovel"));
        Assert.Null(Shelters.Refusal(w, new Build(P, Structure.SnowShelter)));
        var s = Shelters.Place(w, proper, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        w.Minute += 24 * 60;
        Assert.Same(s, Shelters.At(w, proper, []));
        Assert.Equal(0.6, s.InsulationLevel);

        w.Agent(P).Tile = Open(w); // open ground at the regional air: a thaw
        Assert.Equal("too warm to build in snow", Shelters.Refusal(w, new Build(P, Structure.SnowShelter)));
    }

    /// Old DiscoveryFeatureFactory rolled overhangs past the RockOverhang kind's caps (0.40), so the
    /// first repair or improvement clamped a good one down. A find's caps are at least its levels.
    [Fact]
    public void RockOverhangFind_IsNeverClampedDownByWork()
    {
        var finds = new List<ShelterDef>();
        for (int seed = 0; seed < 6 && finds.Count < 5; seed++)
        {
            var w = World.New(seed);
            var m = Map.Of(w);
            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                    foreach (var p in Pools.At(w, new TileId(x, y)))
                        if (p is ShelterFind { Shelter: { Kind: ShelterKind.RockOverhang } def } && (def.Insulation > 0.4 || def.Wind > 0.4)) finds.Add(def);
        }
        Assert.NotEmpty(finds);
        foreach (var def in finds)
        {
            var w = At(Mild);
            var tile = Open(w);
            var s = Shelters.Place(w, tile, def);
            Shelters.Repair(s, 0);
            Assert.Equal((def.Insulation!.Value, def.Wind!.Value), (s.InsulationLevel, s.WindLevel));
            Assert.True(Shelters.Improve(s, ShelterStat.Wind, Resource.Pine, 1) >= 0);
        }
    }

    /// A shelter is never put down above its own caps.
    [Fact]
    public void Place_RefusesLevelsAboveTheirCaps()
    {
        var w = At(Mild);
        Assert.Throws<ArgumentException>(() => Shelters.Place(w, Open(w), new ShelterDef("Rock Overhang", ShelterKind.RockOverhang, Insulation: 0.5)));
    }
}
