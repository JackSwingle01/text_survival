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
        Assert.NotNull(Shelters.At(w, Map.Of(w).Camp));
        Assert.Equal(idleF, Sim.Surroundings(w, w.Agent(P), new Wait(P)).AirTempF, Digits);
        Assert.Equal(movingF, Sim.Surroundings(w, w.Agent(P), new Travel(P, Direction.North)).AirTempF, Digits);
        LightCampFire(w);
        var ctx = Sim.Surroundings(w, w.Agent(P), new Wait(P));
        Assert.Equal(effectiveF, ctx.AirTempF + ctx.FireBonusF, Digits);
    }

    [Fact]
    public void ShelterStep_NeverCoolsAWarmDay()
    {
        var s = Mild with { TemperatureC = 25 };
        Assert.Equal(77, Weather.ShelterStep(77, s, 0.9, 1, 0, 0).AirF, Digits);
    }

    /// old game: wind coverage was improved with materials and shown, and nothing read it (bug, fixed).
    [Fact]
    public void WindCoverage_CutsTheWindOnTheBody_WhenKeepingStill()
    {
        var w = At(Snowy);
        Assert.Equal(0.6 * (1 - 0.4), Sim.Surroundings(w, w.Agent(P), new Wait(P)).WindLevel, Digits);
        Assert.Equal(0.6, Sim.Surroundings(w, w.Agent(P), new Travel(P, Direction.North)).WindLevel, Digits);
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
        Assert.Equal(0.7, Sim.Surroundings(w, w.Agent(P), new Wait(P)).OverheadCoverLevel, Digits);
        Assert.Equal(0.0, Sim.Surroundings(w, w.Agent(P), new Travel(P, Direction.North)).OverheadCoverLevel, Digits);
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
        var ctx = Sim.Surroundings(w, w.Agent(P), new Sleep(P));
        double open = Weather.ShelteredAirF(w, Map.Of(w).Camp, Weather.At(w, w.Minute), 0, Overhang).AirF + warmth.AirHeatF;
        Assert.Equal(open, ctx.AirTempF, Digits);
        Assert.Equal(warmth.PitHeatF * 2 + warmth.ShelterHeatF, ctx.FireBonusF, Digits);
    }

    // ---- bedding ----

    [Fact]
    public void CampBedding_SleepsAtFullQuality_AndKeepsTheGroundOff()
    {
        var w = At(Mild);
        var sleep = Sim.Surroundings(w, w.Agent(P), new Sleep(P));
        Assert.Equal(1.0, sleep.BeddingQualityPct);
        Assert.Equal(0.9, sleep.GroundProtectionLevel, Digits); // old 0.5 + quality * 0.4
        Assert.Equal(0.0, Sim.Surroundings(w, w.Agent(P), new Wait(P)).GroundProtectionLevel); // on your feet
        w.Agent(P).Tile = Open(w);
        sleep = Sim.Surroundings(w, w.Agent(P), new Sleep(P));
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
            var ctx = Sim.Surroundings(w, w.Agent(P), new Sleep(P)) with { BeddingQualityPct = quality };
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
        double awake = Sim.Surroundings(w, w.Agent(P), new Rest(P)).AirTempF;
        Assert.Equal(awake + 5, Sim.Surroundings(w, w.Agent(P), new Sleep(P)).AirTempF, Digits);
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
        var s = Shelters.At(w, tile)!;
        Assert.Equal((0.15, 0.25, 0.20), (s.InsulationLevel, s.OverheadLevel, s.WindLevel));
        Assert.Contains("already a shelter", Sim.Step(w, new Build(P, Structure.LogFrame), 10).InterruptedReason);
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
        Assert.Equal(0.9, Shelters.At(w, tile)!.OverheadLevel);
        w = Save.FromJson(Save.ToJson(w));
        Assert.Equal("packed", Sim.Step(w, new PackTent(P), 5).InterruptedReason);
        Assert.Null(Shelters.At(w, tile));
        Assert.Contains(Items.Pack(w, P).Gear, g => g.Kind == "hide_tent");
        Assert.Contains("no tent", Sim.Step(w, new PackTent(P), 5).InterruptedReason);
    }

    // ---- snow ----

    [Fact]
    public void SnowShelter_MeltsByTheHour_HoweverOftenItIsLookedAt()
    {
        World Thaw(bool look)
        {
            var w = At(Mild with { TemperatureC = 2 });
            var tile = Open(w);
            Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
            for (int i = 0; i < 600; i++)
            {
                Sim.Advance(w, 1);
                if (look) Shelters.At(w, tile);
            }
            Shelters.At(w, tile);
            return w;
        }
        var a = Thaw(true);
        Assert.Equal(Save.ToJson(a), Save.ToJson(Thaw(false)));
        var s = a.Shelters.Values.Single(x => x.Kind == ShelterKind.SnowShelter);
        Assert.True(s.InsulationLevel < 0.6);
        // Left to the thaw long enough it is gone.
        var b = Thaw(false);
        b.Minute += 3 * 24 * 60;
        Assert.Null(Shelters.At(b, Open(b)));
    }

    [Fact]
    public void SnowShelter_HoldsBelowFreezing()
    {
        var w = At(Snowy);
        var tile = Open(w);
        var s = Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        w.Minute += 24 * 60;
        Assert.Same(s, Shelters.At(w, tile));
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
        Assert.Equal(ShelterKind.SnowShelter, Shelters.At(w, tile)!.Kind);
    }
}
