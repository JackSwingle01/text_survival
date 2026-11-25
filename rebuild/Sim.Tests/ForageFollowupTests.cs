namespace Survival.Tests;

/// The user's decisions on the forage restore's flags: every followed clue pays, scavenging is a roll on what
/// ranges near, walking searches, and the old events' discoveries roll what is there.
public class ForageFollowupTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
    static readonly ForagePool Forage = new("forage", 1.0, Pools.MixedForest);

    static World WithPools(params PoolDef[] pools)
    {
        var w = TestWorlds.New(17);
        w.FixedWeather = Mild;
        w.PoolCache = new() { [Map.Of(w).Camp] = pools };
        return w;
    }

    static TileId Camp(World w) => Map.Of(w).Camp;
    static PoolId ForageId(World w) => new(Camp(w), 0);
    static int ClueIndex(Func<ClueDef, bool> which) => Clues.Table.Clues.Select((c, i) => (c, i)).First(x => which(x.c)).i;

    [Fact]
    public void EveryFollowedClue_Pays_NotOnlyTheFirst()
    {
        int deadfall = ClueIndex(c => c.Text == "Fresh deadfall from the wind");
        int torn = ClueIndex(c => c.Text == "Branches torn down by recent wind");
        double one = Clues.YieldFactor([deadfall], ForageFocus.Fuel, Resource.Stick);
        double both = Clues.YieldFactor([deadfall, torn], ForageFocus.Fuel, Resource.Stick);
        Assert.True(one > 1);
        Assert.True(both > one, $"two clues naming kindling pay {both}, one {one}");
        Assert.True(Clues.YieldFactor([torn, deadfall], ForageFocus.Fuel, Resource.Stick) > Clues.YieldFactor([torn], ForageFocus.Fuel, Resource.Stick));
    }

    [Fact]
    public void TheForageWords_NeverSayPickedOver_ThePlaceGoesBarren()
    {
        var lines = Clues.Table.Clues.Select(c => c.Text).Concat(Clues.Table.Failures.SelectMany(f => f.Lines)).ToList();
        Assert.DoesNotContain(lines, l => l.Contains("picked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Clues.Table.Clues, c => c.When == ClueWhen.NearlyDepleted && c.Text.Contains("barren"));
    }

    // ---- scavenging ----

    /// Only the given kinds of herd are left in the world, each brought to the camp tile.
    static World WithHerdsOnly(params Species[] kinds)
    {
        var w = WithPools(Forage);
        foreach (var herd in w.Agents.Values.Where(a => a.Mind is HerdMind).ToList())
        {
            if (!kinds.Contains(herd.Species)) { w.Agents.Remove(herd.Id); continue; }
            herd.Tile = Camp(w);
        }
        return w;
    }

    /// Ends a forage begun on each draw in turn, the draw then reset, and returns the carcasses laid.
    static List<Carcass> ScavengeOverDraws(World w, int clue, int draws)
    {
        var laid = new List<Carcass>();
        for (int draw = 0; draw < draws; draw++)
        {
            Pools.ForageStateFor(w, ForageId(w)).ClueDraw = draw;
            Work.End(w, w.Agent(AgentId.Player), new WorkSession
            {
                Activity = new Gather(P, 0),
                Pool = ForageId(w),
                Minutes = 15,
                LuckFactor = 1,
                Clues = [clue],
                ClueDraw = draw,
            }, []);
            if (w.Tiles.GetValueOrDefault(Camp(w))?.Carcasses is { } carcasses) { laid.AddRange(carcasses); carcasses.Clear(); }
        }
        return laid;
    }

    [Fact]
    public void AScavengeClue_LaysACarcassOnlySometimes_AndOnlyOfAnAnimalThatRangesHere()
    {
        int torn = ClueIndex(c => c.Text == "Ground torn up, blood frozen"); // caribou, bison or megaloceros
        var w = WithHerdsOnly(Species.Caribou);
        var laid = ScavengeOverDraws(w, torn, 200);
        Assert.NotEmpty(laid);
        Assert.True(laid.Count < 200, "a scavenge clue is not a promise");
        Assert.All(laid, c => Assert.Equal("Caribou", c.Animal));
    }

    [Fact]
    public void AScavengeClue_WhoseAnimalsAllRangeElsewhere_IsNeverNoticed_NorLaysAnything()
    {
        int torn = ClueIndex(c => c.Text == "Ground torn up, blood frozen");
        var w = WithHerdsOnly();
        Assert.Empty(ScavengeOverDraws(w, torn, 100));
        for (int draw = 0; draw < 200; draw++)
        {
            Pools.ForageStateFor(w, ForageId(w)).ClueDraw = draw;
            Assert.DoesNotContain(Clues.At(w, P), c => c.Index == torn);
        }
    }

    [Fact]
    public void SmallGame_RangesEverywhere_UntilSmallGameIsBuilt()
    {
        int fur = ClueIndex(c => c.Text == "Scattered fur and blood");
        Assert.All(ScavengeOverDraws(WithHerdsOnly(), fur, 100), c => Assert.Equal("Hare", c.Animal));
        Assert.NotEmpty(ScavengeOverDraws(WithHerdsOnly(), fur, 100));
    }

    [Fact]
    public void AScavengeClue_PaysOnceADraw_HoweverManySearchedOnIt()
    {
        int fur = ClueIndex(c => c.Text == "Scattered fur and blood");
        var w = WithHerdsOnly();
        int draw = Enumerable.Range(0, 200).First(d => LaysOn(WithHerdsOnly(), fur, d));
        Pools.ForageStateFor(w, ForageId(w)).ClueDraw = draw;
        var session = () => new WorkSession { Activity = new Gather(P, 0), Pool = ForageId(w), Minutes = 15, LuckFactor = 1, Clues = [fur], ClueDraw = draw };
        Work.End(w, w.Agent(P), session(), []);
        Work.End(w, w.Agent(P), session(), []); // a second searcher on the same draw, ending after
        Assert.Single(w.Tiles[Camp(w)].Carcasses!);
    }

    static bool LaysOn(World w, int clue, int draw)
    {
        Pools.ForageStateFor(w, ForageId(w)).ClueDraw = draw;
        Work.End(w, w.Agent(P), new WorkSession { Activity = new Gather(P, 0), Pool = ForageId(w), Minutes = 15, LuckFactor = 1, Clues = [clue], ClueDraw = draw }, []);
        return w.Tiles.GetValueOrDefault(Camp(w))?.Carcasses is { Count: > 0 };
    }

    [Fact]
    public void SomethingLargeFrozenSolid_LaysACarcassFrozenThrough_NotRotted()
    {
        int frozen = ClueIndex(c => c.Text.StartsWith("Something large"));
        var w = WithHerdsOnly(Species.Bison, Species.Caribou);
        w.FixedWeather = Mild with { TemperatureC = -25 };
        w.Minute = 12 * 60;
        var laid = ScavengeOverDraws(w, frozen, 200);
        Assert.NotEmpty(laid);
        foreach (var c in laid)
        {
            w.Tiles[Camp(w)].Carcasses!.Add(c);
            Assert.True(Carcasses.IsFrozen(w, c), $"{c.Animal} laid at {c.LastKnownTempF:0} F");
            Assert.True(Carcasses.DecayLevelNow(w, Camp(w), c) < 0.05);
            w.Tiles[Camp(w)].Carcasses!.Clear();
        }
    }

    // ---- walking searches ----

    [Fact]
    public void KeepWalking_SearchesTheGround_AtTheWalkingRate()
    {
        var w = WithPools(Forage);
        var r = Sim.Step(w, new WalkAbout(P), Work.WalkAboutMinutes);
        double searched = Memories.Of(w.Agent(P)).Places[Camp(w)].SearchedHours;
        Assert.True(searched > 0);
        Assert.True(searched <= r.MinutesElapsed * Body.Perception(w.Agent(P).Body) / 60.0 * Traversal.TravelSearchPct + 1e-9);
    }

    [Fact]
    public void ACrossing_SearchesTheTileLeftAndTheTileEntered_HalfEach()
    {
        var w = TestWorlds.New(17);
        w.FixedWeather = Mild;
        var from = w.Agent(P).Tile;
        var go = Verbs.At(w, P).Walk.First(v => v.Allowed);
        var r = Sim.Step(w, go.Activity, go.Minutes);
        var to = w.Agent(P).Tile;
        Assert.NotEqual(from, to);
        var places = Memories.Of(w.Agent(P)).Places;
        double left = places[from].SearchedHours, entered = places[to].SearchedHours;
        Assert.True(left > 0 && entered > 0, $"left {left}, entered {entered}");
        Assert.Equal(left, entered, 6);
        Assert.True(left + entered <= r.MinutesElapsed * Body.Perception(w.Agent(P).Body) / 60.0 * Traversal.TravelSearchPct + 1e-9);
    }

    // ---- the old events' discoveries roll what is there ----

    static List<PoolDef> Rolled(string find, int count) =>
        [.. Enumerable.Range(0, count).Select(i => Pools.Make(Content.Tables.Discoveries.Finds[find], Rng.ForSystem(i, $"find {find}"), 1))];

    static IReadOnlyList<string> FoundOf(PoolDef p) => p switch { StockPool s => s.Found!, PileFind f => f.Found!, _ => throw new InvalidOperationException(p.Name) };

    [Theory]
    [InlineData("OldCampsite"), InlineData("KillSite"), InlineData("HiddenCache"), InlineData("BoneScatter"), InlineData("SquirrelCache"),
     InlineData("FireOrigin"), InlineData("BearCache")]
    public void AnOldEventsDiscovery_RollsWhatIsThere_SoTwoAreNotAlike(string find)
    {
        var opening = Content.Tables.Discoveries.Finds[find].Found!.Single();
        var rolled = Rolled(find, 200);
        Assert.All(rolled, p => Assert.All(FoundOf(p), line => Assert.StartsWith(opening, line)));
        Assert.True(rolled.Select(p => FoundOf(p).Single()).Distinct().Count() > 1, $"every {find} reads the same");
    }

    [Fact]
    public void AnOldCampsite_CanHoldNothing_OrALeftBehindTool()
    {
        var camps = Rolled("OldCampsite", 200).Cast<StockPool>().ToList();
        Assert.Contains(camps, c => c.Items.Count == 0 && c.Gear == null);
        Assert.Contains(camps, c => c.Gear is { Count: > 0 });
    }

    [Theory]
    [InlineData("KillSite"), InlineData("BearCache")]
    public void AKillSiteOrABearsCache_IsMeatToSalvage_AsTheOldEventGaveIt(string find)
    {
        Assert.All(Rolled(find, 50), p =>
        {
            var s = Assert.IsType<StockPool>(p);
            Assert.Equal(StockKind.Salvage, s.Kind);
            Assert.Contains(s.Items, i => i.Resource == Resource.RawMeat);
        });
    }
}
