using Xunit.Abstractions;

namespace Survival.Tests;

/// The old forage screen restored (porting restore 1): one luck a search, clues, Keep Walking, scavenging, the
/// discoveries that lay real things, search effort, the ground's word and the tutorials.
public class ForageRestoreTests(ITestOutputHelper output)
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
    static readonly ForagePool Forage = new("forage", 1.0, Pools.MixedForest);

    static World WithPools(params PoolDef[] pools)
    {
        var w = World.New(17);
        w.FixedWeather = Mild;
        w.PoolCache = new() { [Map.Of(w).Camp] = pools };
        return w;
    }

    static TileId Camp(World w) => Map.Of(w).Camp;
    static PoolId ForageId(World w) => new(Camp(w), 0);
    static int ClueIndex(Func<ClueDef, bool> which) => Clues.Table.Clues.Select((c, i) => (c, i)).First(x => which(x.c)).i;

    // ---- one luck a search ----

    [Fact]
    public void ASearch_RollsItsLuckOnce_HoweverItsStepsAreCut()
    {
        var w = WithPools(Forage);
        var luck = new HashSet<double>();
        var events = new List<WorldEvent>();
        for (int i = 0; i < 6; i++)
        {
            var r = Sim.Step(w, new Gather(P, 0), 10, more: i < 5);
            luck.Add(r.Work!.LuckFactor);
            events.AddRange(r.Events);
        }
        Assert.Single(luck);
        var gathered = Assert.Single(events.OfType<Gathered>());
        Assert.Equal(Pools.TierOf(luck.Single()), gathered.Luck);
    }

    [Fact]
    public void Luck_IsSwingierBySearch_ThanMinuteByMinute()
    {
        // The same hour's ground, searched as one search or as sixty: one roll spreads the hauls wider.
        double Spread(bool oneSearch)
        {
            var hauls = new List<double>();
            var w = WithPools(Forage);
            for (int seed = 0; seed < 400; seed++)
            {
                w.Tiles.Clear(); // fresh ground each time
                var rng = Rng.ForSystem(seed, "luck spread");
                double luck = Pools.RollLuck(rng), kg = 0;
                for (int m = 0; m < 60; m++) kg += Pools.Forage(w, ForageId(w), 1 / 60.0, rng, oneSearch ? luck : null).Sum(x => x.Kg);
                hauls.Add(kg);
            }
            double mean = hauls.Average();
            return Math.Sqrt(hauls.Average(h => (h - mean) * (h - mean))) / mean;
        }
        double session = Spread(oneSearch: true), perMinute = Spread(oneSearch: false);
        output.WriteLine($"an hour's haul, coefficient of variation: one luck a search {session:0.000}, a luck a minute {perMinute:0.000}");
        Assert.True(session > perMinute);
    }

    [Fact]
    public void TheMoodLine_ShowsLeanLuckAtOnce_AndGoodLuckOnlyOnceSomethingTurnsUp()
    {
        WorkSession Session(double luck) => new() { Activity = new Gather(P, 0), LuckFactor = luck };
        Assert.Equal(Clues.Table.Luck[LuckTier.Lean], Work.Mood(Session(0.5)));
        Assert.Null(Work.Mood(Session(1.0)));
        var good = Session(1.5);
        Assert.Null(Work.Mood(good));
        Items.Add(good.Got, Resource.Kindling, 0.3);
        Assert.Equal(Clues.Table.Luck[LuckTier.Good], Work.Mood(good));
    }

    // ---- clues ----

    [Fact]
    public void Clues_AreTheSameOnEveryLook_UntilASearchOrAWalkMovesThemOn()
    {
        var w = WithPools(Forage);
        int Draw() => (Pools.StateOf(w, ForageId(w)) as ForageState)?.ClueDraw ?? 0;
        Assert.Equal(Clues.At(w, P), Clues.At(w, P));
        Assert.NotEmpty(Clues.At(w, P)); // always at least one line
        Sim.Step(w, new Gather(P, 0), 15);
        Assert.Equal(1, Draw());
        var r = Sim.Step(w, new WalkAbout(P), 60);
        Assert.Equal(Work.WalkAboutMinutes, r.MinutesElapsed);
        Assert.Equal(StepOutcome.Finished, r.Outcome);
        Assert.Equal(2, Draw());
    }

    [Fact]
    public void WaterClues_AreOnlyNoticedByWater_AndForestOnesOnlyWithTrees()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        int checkedTiles = 0;
        for (int x = 0; x < m.Width && checkedTiles < 60; x += 3)
            for (int y = 0; y < m.Height && checkedTiles < 60; y += 3)
            {
                var t = new TileId(x, y);
                if (!m.TerrainAt(t).IsPassable() || m.SiteAt(t) != null || Pools.ForageAt(w, t) is not PoolId id) continue;
                checkedTiles++;
                w.Agent(P).Tile = t;
                bool water = m.BesideRiver(t) || m.TerrainAt(t) == Terrain.Water;
                bool trees = Pools.At(w, t).Any(p => p is TreePool);
                for (int draw = 0; draw < 15; draw++)
                {
                    Pools.ForageStateFor(w, id).ClueDraw = draw;
                    foreach (var c in Clues.At(w, P))
                    {
                        if (c.Def.When == ClueWhen.Water) Assert.True(water, $"{c.Def.Text} at {t}");
                        if (c.Def.When == ClueWhen.Forest) Assert.True(trees, $"{c.Def.Text} at {t}");
                    }
                }
            }
        Assert.True(checkedTiles > 20);
    }

    [Fact]
    public void AResourceClue_MultipliesItsOwnResources_OnlyUnderTheFocusItMatches()
    {
        int deadfall = ClueIndex(c => c.Text == "Fresh deadfall from the wind");
        var clue = Clues.Table.Clues[deadfall];
        Assert.Equal(clue.Yield, Clues.YieldFactor([deadfall], ForageFocus.Fuel, Resource.Kindling));
        Assert.Equal(1, Clues.YieldFactor([deadfall], ForageFocus.General, Resource.Kindling));
        Assert.Equal(1, Clues.YieldFactor([deadfall], ForageFocus.Food, Resource.Kindling));
        Assert.Equal(1, Clues.YieldFactor([deadfall], ForageFocus.Fuel, Resource.Tinder)); // fuel, but not what it promised
        Assert.Equal(ForageFocus.Fuel, Clues.Suggests(clue));
    }

    [Fact]
    public void ANegativeClue_CutsTheWholeSearch()
    {
        int frozen = ClueIndex(c => c.Kind == ClueKind.Negative);
        foreach (var r in new[] { Resource.Kindling, Resource.Berries, Resource.Stone })
            foreach (var focus in Enum.GetValues<ForageFocus>())
                Assert.True(Clues.YieldFactor([frozen], focus, r) < 1);
    }

    [Fact]
    public void GameClues_AreNoticedOnlyWithHerdsNear()
    {
        var w = WithPools(Forage);
        foreach (var herd in w.Agents.Values.Where(a => a.Mind is HerdMind).ToList()) w.Agents.Remove(herd.Id);
        for (int draw = 0; draw < 40; draw++)
        {
            Pools.ForageStateFor(w, ForageId(w)).ClueDraw = draw;
            Assert.DoesNotContain(Clues.At(w, P), c => c.Def.Kind == ClueKind.Game);
        }
    }

    // ---- scavenging ----

    [Fact]
    public void AScavengeClue_LaysARealCarcass_PartEaten_WhenTheSearchEnds()
    {
        int ravens = ClueIndex(c => c.Text == "Circling ravens ahead");
        var clue = Clues.Table.Clues[ravens];
        var w = WithPools(Forage);
        var me = w.Agent(P);
        var events = new List<WorldEvent>();
        Work.End(w, me, new WorkSession { Activity = new Gather(P, 0), Pool = ForageId(w), Minutes = 15, LuckFactor = 1, Clues = [ravens] }, events);
        var c = Assert.Single(w.Tiles[Camp(w)].Carcasses!);
        Assert.Contains(clue.Animals!, a => a.Animal.ToString() == c.Animal);
        Assert.Contains(events, e => e is Scavenged s && s.Animal != null);
        Assert.Equal(Pools.FoundDead, c.Cause!.Value.What);
    }

    [Fact]
    public void ABonesOnlyClue_LeavesBoneScraps_AndNoCarcass()
    {
        int pellets = ClueIndex(c => c.Kind == ClueKind.Scavenge && c.Animals == null);
        var w = WithPools(Forage);
        double before = Items.Have(Items.Pack(w, P), Resource.Bone);
        Work.End(w, w.Agent(P), new WorkSession { Activity = new Gather(P, 0), Pool = ForageId(w), Minutes = 15, LuckFactor = 1, Clues = [pellets] }, []);
        Assert.True(Items.Have(Items.Pack(w, P), Resource.Bone) > before);
        Assert.Null(w.Tiles.GetValueOrDefault(Camp(w))?.Carcasses);
    }

    // ---- discoveries that lay real things ----

    [Fact]
    public void FindingACarcassFind_LaysTheCarcass_AndThePoolIsNoLongerListed()
    {
        var w = WithPools(Forage, new CarcassFind("Frozen Hare", Species.Hare, 0, 0.01));
        int scavenged = Sim.Step(w, new Gather(P, 0), 30).Events.OfType<Scavenged>().Count(e => e.Animal != null); // a scavenge clue lays its own
        var c = w.Tiles[Camp(w)].Carcasses!.First();
        Assert.Equal(1 + scavenged, w.Tiles[Camp(w)].Carcasses!.Count);
        Assert.Equal("Hare", c.Animal);
        Assert.DoesNotContain(Verbs.At(w, P).LocationFeatures, f => f.LocationFeature.Kind == LocationFeatureKind.Pool && f.LocationFeature.Index == 1);
        // A finder who comes after (here, the same person with the find forgotten) finds what is left, not a second carcass.
        Memories.Of(w.Agent(P)).Discovered.Clear();
        int before = w.Tiles[Camp(w)].Carcasses!.Count;
        scavenged = Sim.Step(w, new Gather(P, 0), 30).Events.OfType<Scavenged>().Count(e => e.Animal != null);
        Assert.Equal(before + scavenged, w.Tiles[Camp(w)].Carcasses!.Count);
    }

    [Fact]
    public void FindingAStashFind_PutsWhatItHoldsOnTheTile()
    {
        var w = WithPools(Forage, new StashFind("Hidden Cache", [new StockItem(Resource.Pine, 1, 2.5)], [Items.Make("fire_striker")], 0.01));
        Sim.Step(w, new Gather(P, 0), 30);
        var stash = w.Tiles[Camp(w)].Stash!;
        Assert.Equal(2.5, Items.Have(stash, Resource.Pine), 6);
        Assert.Single(stash.Gear);
    }

    [Fact]
    public void TheOldEventDiscoveries_AreRealFinds_InTheirOldTerrains()
    {
        var finds = Content.Tables.Discoveries;
        foreach (var (terrain, id) in new[] { (Terrain.Plain, "FrozenSmallGame"), (Terrain.Forest, "KillSite"), (Terrain.Hills, "HiddenCache"),
            (Terrain.Forest, "OldCampsite"), (Terrain.Forest, "SquirrelCache"), (Terrain.Hills, "WaterSource"), (Terrain.Plain, "BoneScatter") })
            Assert.Contains(finds.Terrains[terrain], d => d.Find == id);
        Assert.Contains(Content.ById["bear_cave"].All.OfType<SiteFind>(), f => f.Find == "BearCache");
    }

    [Fact]
    public void EveryFindHasItsFoundLine_AndTheLineNeverRerolls()
    {
        var w = WithPools(Forage, new StockPool("Bush", StockKind.Harvest, 5, [new StockItem(Resource.Berries, 5, 0.4, 168)], 0.5, Found: ["a", "b", "c"]));
        var id = new PoolId(Camp(w), 1);
        Assert.Equal(Pools.FoundLine(w, id), Pools.FoundLine(w, id));
        Assert.Contains(Pools.FoundLine(w, id), new[] { "a", "b", "c" });
        Assert.All(Content.Tables.Discoveries.Finds.Values, f => Assert.NotEmpty(f.Found!));
    }

    // ---- search effort and the ground's word ----

    [Fact]
    public void SearchEffort_RisesWithSearching_AndWalkingSearchesAQuarterAsHard()
    {
        var w = WithPools(Forage);
        var bands = Clues.Table.SearchBands.Select(b => b.Word).ToList();
        int Band() => bands.IndexOf(Pools.SearchEffort(w, P, Camp(w)));
        Assert.Equal(0, Band());
        int last = 0;
        for (int i = 0; i < 12; i++)
        {
            Sim.Step(w, new Gather(P, 0), 30);
            Assert.True(Band() >= last);
            last = Band();
        }
        Assert.Equal(bands.Count - 1, last);

        var walker = World.New(17);
        walker.FixedWeather = Mild;
        var from = walker.Agent(P).Tile;
        var go = Verbs.At(walker, P).Walk.First(v => v.Allowed);
        var r = Sim.Step(walker, go.Activity, go.Minutes);
        double walked = Memories.Of(walker.Agent(P)).Places[from].SearchedHours;
        Assert.True(walked > 0);
        double perMinute = Body.Perception(walker.Agent(P).Body) / 60.0;
        Assert.True(walked <= r.MinutesElapsed * perMinute * Traversal.TravelSearchPct + 1e-9);
    }

    [Fact]
    public void TheGroundsWord_RisesWithDensity_AndLabelsForageOnceForagedHere()
    {
        var words = Clues.Table.Quality.Select(q => q.Word).ToList();
        int last = int.MaxValue;
        foreach (var d in new[] { 0.0, 0.3, 0.5, 0.9, 1.3, 2.1, 3.5 })
        {
            int at = words.IndexOf(Pools.QualityWord(d));
            Assert.True(at <= last);
            last = at;
        }
        var w = WithPools(Forage);
        string Label() => Verbs.At(w, P).LocationFeatures.Single(f => f.LocationFeature.Kind == LocationFeatureKind.Pool)
            .Menu.Single(v => v.Activity == new Gather(P, 0)).Label;
        Assert.Equal("Forage", Label());
        Sim.Step(w, new Gather(P, 0), 30);
        Assert.Equal($"Forage ({Pools.QualityWord(Pools.ForageDensity(w, ForageId(w)))})", Label());
    }

    [Fact]
    public void ForagingIsNeverRefused_OnSparseGround()
    {
        var w = WithPools(new ForagePool("forage", 0.05, Pools.MixedForest));
        Assert.Null(Sim.Refusal(w, new Gather(P, 0)));
    }

    // ---- tutorials ----

    [Fact]
    public void TheFirstClueTutorial_ShowsOnce_AndRemembersAcrossASave()
    {
        var w = WithPools(Forage);
        var first = Sim.Step(w, new Gather(P, 0), 10).Events.OfType<TutorialShown>().ToList();
        Assert.Contains(first, t => t.Tutorial == Tutorial.FirstClue);
        var loaded = Save.FromJson(Save.ToJson(w));
        loaded.PoolCache = new() { [Camp(loaded)] = [Forage] };
        Assert.DoesNotContain(Sim.Step(loaded, new Gather(P, 0), 10).Events.OfType<TutorialShown>(), t => t.Tutorial == Tutorial.FirstClue);
        Assert.All(Enum.GetValues<Tutorial>(), t => Assert.False(string.IsNullOrWhiteSpace(Tutorials.Text(t, 3))));
    }

    // ---- darkness and held work ----

    [Fact]
    public void ForagingInTheDarkWithoutLight_FindsLessThanByDay()
    {
        double Haul(int hour)
        {
            double kg = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var w = WithPools(Forage);
                w.Rngs[StreamName.Of("work", P)] = Rng.ForSystem(seed, "work");
                while (Weather.MinuteOfDay(w.Minute) / 60 != hour) w.Minute++;
                double before = Items.CarriedKg(w.Agent(P));
                Sim.Step(w, new Gather(P, 0), 30);
                kg += Items.CarriedKg(w.Agent(P)) - before;
            }
            return kg;
        }
        Assert.True(Haul(1) < Haul(11));
    }

    [Fact]
    public void HeldWork_IsEndedByFinish_WithWhatItGathered()
    {
        var w = WithPools(Forage);
        var r = Sim.Step(w, new Gather(P, 0), 20, more: true);
        Assert.DoesNotContain(r.Events, e => e is Gathered);
        Assert.NotNull(Sim.WorkUnderWay(w, P));
        var ended = Sim.Finish(w, P);
        Assert.Single(ended.OfType<Gathered>());
        Assert.Null(Sim.WorkUnderWay(w, P));
        Assert.Empty(Sim.Finish(w, P));
    }

    [Fact]
    public void TimeSpentOnNothing_EndsHeldWork_SoALaterSearchIsANewOne()
    {
        var w = WithPools(Forage);
        Sim.Step(w, new Gather(P, 0), 20, more: true);
        Assert.Single(Sim.Advance(w, 5).OfType<Gathered>());
        Assert.Null(Sim.WorkUnderWay(w, P));
    }

    [Fact]
    public void AnotherActivity_EndsHeldWork_AsIfItHadStopped()
    {
        var w = WithPools(Forage);
        Sim.Step(w, new Gather(P, 0), 20, more: true);
        var r = Sim.Step(w, new Wait(P), 1);
        Assert.Single(r.Events.OfType<Gathered>());
        Assert.Null(Sim.WorkUnderWay(w, P));
    }

    // ---- the species renamed ----

    [Fact]
    public void TheHareAndTheSabreTooth_HaveTheirWords()
    {
        Assert.Equal("hare", Names.Of(Species.Hare));
        Assert.Equal("sabre-tooth", Names.Of(Species.SaberTooth));
    }
}
