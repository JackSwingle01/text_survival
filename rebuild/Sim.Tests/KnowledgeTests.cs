namespace Survival.Tests;

/// Place memory, tile journals and receipts (short-loop plan, features 4 and 5): what a person
/// believes about a tile, what happened there while they were away, and how coming back tells them.
public class KnowledgeTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    static World Mild17()
    {
        var w = World.New(17);
        w.FixedWeather = Mild;
        return w;
    }

    /// Moves an agent straight there, as if it had walked: the tests are about what it sees, not the walk.
    static void Put(World w, AgentId who, TileId t)
    {
        var a = w.Agent(who);
        a.Tile = t;
        a.Crossing = null;
    }

    /// The nearest passable tile at least min tiles from a place, in a fixed scan order.
    static TileId Away(World w, TileId from, int min)
    {
        var m = Map.Of(w);
        for (int r = min; r < 40; r++)
            for (int dx = -r; dx <= r; dx++)
                foreach (int dy in new[] { r - Math.Abs(dx), Math.Abs(dx) - r })
                {
                    var t = new TileId(from.X + dx, from.Y + dy);
                    if (m.InBounds(t) && m.TerrainAt(t).IsPassable() && m.SiteAt(t) == null) return t;
                }
        throw new InvalidOperationException($"No passable tile {min} from {from}");
    }

    static TileMemory? Memory(World w, AgentId who, TileId t) => ((PersonMind)w.Agent(who).Mind).Knows.Places.GetValueOrDefault(t);

    static FireId PlacePit(World w, TileId tile, double birchKg)
    {
        var id = new FireId(w.NextFireId++);
        w.Fires[id] = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, BurningKg = { [Resource.Birch] = birchKg } };
        return id;
    }

    /// The minute a fire would die if nobody touched it from now, worked out on a copy of the world.
    static long DiesAt(World w, FireId id)
    {
        var copy = Save.FromJson(Save.ToJson(w));
        var events = new List<WorldEvent>();
        for (int i = 0; i < 48 * 60 && events.OfType<FireDied>().All(e => e.Fire != id); i++)
        {
            copy.Minute++;
            Fire.CatchUp(copy, id, events);
        }
        return events.OfType<FireDied>().Single(e => e.Fire == id).Minute;
    }

    // ---- sight and memory ----

    [Fact]
    public void Sight_IsAFixedRadius_AroundTheAgent()
    {
        var w = World.New(17);
        var me = w.Agent(P);
        var seen = Sight.Visible(w, me).ToList();
        Assert.Contains(me.Tile, seen);
        Assert.All(seen, t => Assert.True(MapGen.Manhattan(t, me.Tile) <= Sight.RadiusTiles));
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(2 * Sight.RadiusTiles * (Sight.RadiusTiles + 1) + 1, seen.Count); // camp is not at the map's edge
    }

    /// Only what was seen, and only tiles with something worth remembering (or stood on).
    [Fact]
    public void Memory_HoldsOnlyTilesSeen_AndOnlyNotableOnes_AndSurvivesASave()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        Sim.Step(w, new Wait(P), 1);
        var places = ((PersonMind)w.Agent(P).Mind).Knows.Places;
        Assert.True(places.ContainsKey(camp) && places[camp].Visited);
        Assert.Equal("Overhang", places[camp].Shelter);
        Assert.All(places, p => Assert.True(MapGen.Manhattan(p.Key, camp) <= Sight.RadiusTiles));
        Assert.All(places.Where(p => p.Key != camp), p => Assert.True(p.Value.Trees || p.Value.Water || p.Value.Fire != null || p.Value.Shelter != null || p.Value.Danger != Danger.None));
        string json = Save.ToJson(w);
        Assert.Equal(json, Save.ToJson(Save.FromJson(json)));
    }

    // ---- the receipt done-when tests (feature 4) ----

    /// After 4 h away, the camp receipt lists an NPC's FuelAdded and the FireDied at the minutes
    /// they happened - however long you were away past that, and however the time was stepped.
    [Theory]
    [InlineData(4 * 60, 0)]
    [InlineData(6 * 60, 0)]
    [InlineData(4 * 60, 1)]
    [InlineData(4 * 60, 37)]
    public void AfterHoursAway_TheCampReceipt_ListsAnNpcsFuelAndTheFireDying_WhenTheyHappened(int awayMinutes, int chunk)
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var pit = PlacePit(w, camp, 0.6);
        Sim.Step(w, new Wait(P), 1); // the player sees the fire burning
        Assert.Equal(FirePhase.Dying, Memory(w, P, camp)!.Fire);
        long left = w.Minute;
        var far = Away(w, camp, Sight.RadiusTiles + 3);
        Put(w, P, far);
        Sim.Step(w, new Wait(P), 30);

        // Anu comes by while the player is out, feeds the fire, and goes off to her own camp.
        var npc = Npc.Spawn(w, camp, camp: Away(w, camp, 25));
        Items.Add(Items.Pack(w, npc), Resource.Kindling, 1);
        var fed = Sim.Step(w, new AddFuel(npc, Resource.Kindling, 0.5), 1).Events.OfType<FuelAdded>().Single();
        Put(w, npc, ((NpcMind)w.Agent(npc).Mind).Camp);
        long died = DiesAt(w, pit);

        long back = left + awayMinutes;
        while (w.Minute < back)
            Sim.Step(w, new Wait(P), chunk == 0 ? (int)(back - w.Minute) : (int)Math.Min(chunk, back - w.Minute));
        Assert.Equal(FirePhase.Dying, Memory(w, P, camp)!.Fire); // away, it is still as last seen
        Assert.True(died < back, $"the fire died at {died}, after the player was back at {back}");

        Put(w, P, camp);
        var r = Assert.Single(Sim.Step(w, new Wait(P), 1).Events.OfType<Noticed>()).Receipt;
        Assert.True(r.Full);
        Assert.Equal(camp, r.Tile);
        var fuel = Assert.Single(r.Happened.OfType<FuelAdded>());
        Assert.Equal((fed.Minute, npc), (fuel.Minute, fuel.By));
        Assert.Equal(died, Assert.Single(r.Happened.OfType<FireDied>()).Minute);
        Assert.True(r.Happened.Select(e => e.Minute).SequenceEqual(r.Happened.Select(e => e.Minute).Order()));
        Assert.Equal(FirePhase.Cold, Memory(w, P, camp)!.Fire); // and now memory knows
    }

    static (World World, TileId Field) VisitedField()
    {
        var w = Mild17();
        var field = Away(w, Map.Of(w).Camp, 12);
        Put(w, P, field);
        Sim.Step(w, new Wait(P), 1);
        Assert.True(Memory(w, P, field)!.Visited);
        Put(w, P, Away(w, field, Sight.RadiusTiles + 3));
        Sim.Step(w, new Wait(P), 1);
        return (w, field);
    }

    static void HerdCrosses(World w, TileId into)
    {
        var m = Map.Of(w);
        var dir = Enum.GetValues<Direction>().First(d => m.TerrainAt(Map.Step(into, d)).IsPassable() && Map.Blocked(m, Map.Step(into, d), into) == null);
        var from = Map.Step(into, dir);
        var herd = Agents.Spawn(w, Species.Caribou, from, 12);
        var back = dir switch { Direction.North => Direction.South, Direction.South => Direction.North, Direction.East => Direction.West, _ => Direction.East };
        var walk = Sim.Step(w, new Travel(herd, back), 120);
        Assert.Contains(walk.Events.OfType<Moved>(), e => e.Who == herd && e.To == into);
    }

    /// A field tile visited before and after a herd passed shows the herd's sign; after enough
    /// erosion it shows nothing. No journal in the field: nobody was there to tell.
    [Fact]
    public void AFieldTile_VisitedBeforeAndAfterAHerdPassed_ShowsItsSign()
    {
        var (w, field) = VisitedField();
        HerdCrosses(w, field);
        Put(w, P, field);
        var r = Assert.Single(Sim.Step(w, new Wait(P), 1).Events.OfType<Noticed>()).Receipt;
        Assert.False(r.Full);
        Assert.Empty(r.Happened);
        Assert.Contains(r.Signs, s => s.Maker == TrackMaker.Hoof && s.Traffic >= 10);
    }

    [Fact]
    public void AFieldTile_AfterEnoughErosion_ShowsNothing()
    {
        var (w, field) = VisitedField();
        HerdCrosses(w, field);
        w.Minute += 30 * 1440; // a month of snow and wind, with nobody there
        Put(w, P, field);
        var events = Sim.Step(w, new Wait(P), 1).Events;
        Assert.Empty(Tracks.Read(w, field));
        Assert.DoesNotContain(events.OfType<Noticed>(), n => n.Receipt.Signs.Count > 0);
    }

    // ---- catch-up tells the minute it happened ----

    /// A fire left burning, found dead hours later: FireDied carries the minute it died, not the
    /// minute it was found, whenever the player comes back and however the hours were stepped.
    [Theory]
    [InlineData(3 * 60, 180)]
    [InlineData(9 * 60, 180)]
    [InlineData(9 * 60, 1)]
    public void AFireFoundDead_DiedWhenItDied(int awayMinutes, int chunk)
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var pit = PlacePit(w, camp, 1.5);
        long died = DiesAt(w, pit);
        Put(w, P, Away(w, camp, Sight.RadiusTiles + 3));
        var events = new List<WorldEvent>();
        while (w.Minute < awayMinutes) events.AddRange(Sim.Step(w, new Wait(P), (int)Math.Min(chunk, awayMinutes - w.Minute)).Events);
        Assert.DoesNotContain(events, e => e is FireDied); // nobody saw it go
        Put(w, P, camp);
        events = Sim.Step(w, new Wait(P), 1).Events;
        Assert.Equal(died, Assert.Single(events.OfType<FireDied>()).Minute);
        Assert.True(died < w.Minute - 1, $"died {died}, found {w.Minute}");
        Assert.Equal(died, Assert.Single(w.Journal[camp].OfType<FireDied>()).Minute);
    }

    /// A snow shelter that melts away with nobody near is reported at the hour it went (old game:
    /// they never melted; the rebuild melted them without a word - bug, fixed).
    [Fact]
    public void ASnowShelterMeltingUnseen_IsReportedAtTheHourItWent()
    {
        (World, TileId) Make()
        {
            var w = World.New(17);
            w.FixedWeather = new WeatherSample(0, WeatherCondition.Cloudy, 10, 0.1, 0, 0.8, WindDirection.North);
            var t = Away(w, Map.Of(w).Camp, 12);
            Shelters.Place(w, t, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
            return (w, t);
        }
        var (hourly, t1) = Make();
        var seen = new List<WorldEvent>();
        for (int h = 0; h < 72 && Shelters.At(hourly, t1, seen) != null; h++) hourly.Minute += 60;
        var gone = Assert.Single(seen.OfType<ShelterCollapsed>());

        var (late, t2) = Make();
        late.Minute += 3 * 1440;
        var found = new List<WorldEvent>();
        Assert.Null(Shelters.At(late, t2, found));
        Assert.Equal(gone.Minute, Assert.Single(found.OfType<ShelterCollapsed>()).Minute);
        Assert.True(gone.Minute < late.Minute && gone.Minute % 60 == 0);
    }

    // ---- feature 5: the map draws what you know ----

    /// The player leaves camp with a roaring fire; it dies; their memory still says roaring until
    /// they see camp again.
    [Fact]
    public void LeavingARoaringFire_MemoryStillSaysRoaring_UntilCampIsSeenAgain()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var pit = PlacePit(w, camp, 5);
        Sim.Step(w, new Wait(P), 1);
        Assert.Equal(FirePhase.Roaring, Memory(w, P, camp)!.Fire);
        Assert.True(Memory(w, P, camp)!.FireHours > 3);
        long left = w.Minute;
        long died = DiesAt(w, pit);

        Put(w, P, Away(w, camp, Sight.RadiusTiles + 2));
        while (w.Minute <= died + 60) Sim.Step(w, new Wait(P), 60);
        Assert.Equal(FirePhase.Roaring, Memory(w, P, camp)!.Fire);
        Assert.Equal(left, Memory(w, P, camp)!.SeenMinute);

        // Back in sight of camp, not yet on it: the fire is seen to be out.
        Put(w, P, Away(w, camp, Sight.RadiusTiles - 1));
        Sim.Step(w, new Wait(P), 1);
        Assert.Equal(FirePhase.Cold, Memory(w, P, camp)!.Fire);
        Assert.Equal(w.Minute, Memory(w, P, camp)!.SeenMinute);
    }

    /// Sleeping counts as being away: the morning receipt tells what happened in the night.
    [Fact]
    public void Waking_GivesTheMorningReceipt()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var pit = PlacePit(w, camp, 1.0);
        Sim.Step(w, new Wait(P), 1);
        long died = DiesAt(w, pit);
        w.Agent(P).Body.EnergyMinutes = 0;
        var night = Sim.Step(w, new Sleep(P), 8 * 60);
        Assert.DoesNotContain(night.Events, e => e is Noticed);
        var morning = Assert.Single(Sim.Step(w, new Wait(P), 1).Events.OfType<Noticed>()).Receipt;
        Assert.Equal(died, Assert.Single(morning.Happened.OfType<FireDied>()).Minute);
        Assert.Contains(morning.Changes, c => c.Kind == ChangeKind.Fire && c.Now == nameof(FirePhase.Cold));
    }

    /// The receipt's differences: a stash emptied while away, a carcass come and gone.
    [Fact]
    public void Receipt_ListsWhatChangedInTheStash()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        Items.Add(Items.StashAt(w, camp), Resource.DriedMeat, 2);
        Sim.Step(w, new Wait(P), 1);
        Assert.Equal(2, Memory(w, P, camp)!.Stash![Resource.DriedMeat]);
        Put(w, P, Away(w, camp, Sight.RadiusTiles + 3));
        Sim.Step(w, new Wait(P), 30);
        var npc = Npc.Spawn(w, camp, camp: Away(w, camp, 25));
        Sim.Step(w, new Unstash(npc, Resource.DriedMeat, 1), 1);
        Put(w, npc, ((NpcMind)w.Agent(npc).Mind).Camp);
        Put(w, P, camp);
        var r = Assert.Single(Sim.Step(w, new Wait(P), 1).Events.OfType<Noticed>()).Receipt;
        Assert.Contains(r.Changes, c => c is { Kind: ChangeKind.Stash, Subject: nameof(Resource.DriedMeat), Was: "2.0", Now: "1.0" });
        Assert.Contains(r.Happened.OfType<Unstashed>(), u => u.Who == npc && u.Kg == 1);
    }

    /// Nothing changed, nothing to say: coming back to an unchanged tile gives no receipt.
    [Fact]
    public void AnUnchangedTile_GivesNoReceipt()
    {
        var (w, field) = VisitedField();
        Put(w, P, field);
        Assert.DoesNotContain(Sim.Step(w, new Wait(P), 1).Events, e => e is Noticed);
    }

    // ---- the journal ----

    [Fact]
    public void Journal_KeepsOnlyStakedTiles_IsBounded_AndSurvivesASave()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var field = Away(w, camp, 12);
        Assert.True(Journal.Staked(w, camp));
        Assert.False(Journal.Staked(w, field));
        var events = new List<WorldEvent>();
        for (int i = 0; i < Journal.MaxEntriesPerTile + 5; i++) events.Add(new Stashed(i, P, camp, Resource.Stone, 1));
        events.Add(new Stashed(3, P, field, Resource.Stone, 1));
        events.Add(new Moved(3, P, field, camp)); // a walk through is not news
        int filed = 0;
        Journal.File(w, events, ref filed);
        Assert.Equal(events.Count, filed);
        Assert.False(w.Journal.ContainsKey(field));
        Assert.Equal(Journal.MaxEntriesPerTile, w.Journal[camp].Count);
        Assert.Equal(5, w.Journal[camp][0].Minute); // the oldest went first
        string json = Save.ToJson(w);
        var loaded = Save.FromJson(json);
        Assert.Equal(json, Save.ToJson(loaded));
        Assert.IsType<Stashed>(loaded.Journal[camp][^1]);
    }

    [Fact]
    public void BeingHurtSomewhere_IsRemembered_AsDanger()
    {
        var w = Mild17();
        var tile = Away(w, Map.Of(w).Camp, 12);
        int filed = 0;
        Journal.File(w, [new Injured(10, P, tile, "LeftLeg", DamageType.Blunt, 0.1)], ref filed);
        Assert.True(Memory(w, P, tile)!.Danger.HasFlag(Danger.Hurt));
    }

    /// The NPC's old memory now lives in the shared one: it still remembers every tile it stood on.
    [Fact]
    public void NpcMemory_IsTheSharedModel()
    {
        var w = World.New(17);
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        NpcDriver.Live(w, npc, 1440);
        var places = ((NpcMind)w.Agent(npc).Mind).Knows.Places;
        Assert.Contains(places.Values, p => p.Visited && p.ForageDensity != null);
        Assert.Contains(places.Values, p => p.Trees && !p.Visited);
        Assert.NotEmpty(((NpcMind)w.Agent(npc).Mind).Knows.Sightings);
    }

    // ---- determinism ----

    /// Memory, journals and skills learned along the way come out the same however the minutes are
    /// stepped, and through saves: a busy camp with an NPC, a fire, work, a walk out and back, a night.
    [Theory]
    [InlineData(1, false)]
    [InlineData(7, true)]
    public void BusyCamp_SameWorld_HoweverStepped(int chunk, bool reload)
    {
        static string Run(int chunk, bool reload)
        {
            var w = Mild17();
            TestKit.Supply(w);
            var camp = Map.Of(w).Camp;
            Npc.Spawn(w, camp);
            PlacePit(w, camp, 2);
            var far = Away(w, camp, Sight.RadiusTiles + 2);
            void Do(Func<Activity> make, int total)
            {
                for (int done = 0; done < total;)
                {
                    var r = Sim.Step(w, make(), Math.Min(chunk, total - done));
                    Assert.True(r.MinutesElapsed > 0, r.InterruptedReason);
                    done += r.MinutesElapsed;
                    if (reload) w = Save.FromJson(Save.ToJson(w));
                }
            }
            Do(() => new Gather(P, 0), 90);
            Put(w, P, far);
            Do(() => new Wait(P), 40);
            Put(w, P, camp);
            Do(() => new Wait(P), 5);
            w.Agent(P).Body.EnergyMinutes = Body.MaxEnergyMinutes / 2;
            Do(() => new Sleep(P), 300);
            Do(() => new Wait(P), 30);
            return Save.ToJson(w);
        }
        Assert.Equal(Run(1000, false), Run(chunk, reload));
    }
}
