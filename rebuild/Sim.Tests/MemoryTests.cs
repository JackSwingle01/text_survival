namespace Survival.Tests;

/// Place memory, tile journals and receipts (short-loop plan, features 4 and 5): what a person
/// remembers of a tile, what happened there while they were away, and how coming back tells them.
public class MemoryTests
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

    static TileMemory? Memory(World w, AgentId who, TileId t) => ((PersonMind)w.Agent(who).Mind).Memory.Places.GetValueOrDefault(t);

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

    /// A tile out of anyone's view of here: beyond the farthest anyone can see.
    static TileId OutOfView(World w, TileId from) => Away(w, from, Sight.MaxTiles + 1);

    /// The nearest tile, two or more steps off, from which target is in view.
    static TileId InViewOf(World w, TileId target)
    {
        var m = Map.Of(w);
        for (int r = 2; r < Sight.MaxTiles; r++)
            for (int dx = -r; dx <= r; dx++)
                foreach (int dy in new[] { r - Math.Abs(dx), Math.Abs(dx) - r })
                {
                    var t = new TileId(target.X + dx, target.Y + dy);
                    if (m.InBounds(t) && m.TerrainAt(t).IsPassable() && m.SiteAt(t) == null && Sight.Visible(w, t).Contains(target)) return t;
                }
        throw new InvalidOperationException($"Nowhere two tiles or more from {target} has it in view");
    }

    /// One sight model: what a glance takes in is what the rays show (Sight.Visible), no more.
    [Fact]
    public void AGlance_SeesWhatTheRaysShow()
    {
        var w = Mild17();
        var me = w.Agent(P);
        Sim.Step(w, new Wait(P), 1);
        var view = Sight.Visible(w, me.Tile, Body.GetCapacities(me.Body).Sight);
        Assert.All(w.Agents.Values.Where(a => a.Id != P), a => Assert.Equal(view.Contains(a.Tile), Sight.InSight(w, me, a.Tile)));
        var places = ((PersonMind)me.Mind).Memory.Places;
        Assert.All(places.Keys, t => Assert.Contains(t, view));
    }

    /// Only what was seen, and only tiles with something worth remembering (or stood on).
    [Fact]
    public void Memory_HoldsOnlyTilesSeen_AndOnlyNotableOnes_AndSurvivesASave()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        Sim.Step(w, new Wait(P), 1);
        var places = ((PersonMind)w.Agent(P).Mind).Memory.Places;
        Assert.True(places.ContainsKey(camp) && places[camp].Visited);
        Assert.Equal("Overhang", places[camp].Shelter);
        Assert.All(places, p => Assert.Contains(p.Key, Sight.Visible(w, camp)));
        Assert.All(places.Where(p => p.Key != camp), p => Assert.True(p.Value.Trees || p.Value.Water || p.Value.Fire != null || p.Value.Shelter != null || p.Value.Danger != Danger.None));
        string json = Save.ToJson(w);
        Assert.Equal(json, Save.ToJson(Save.FromJson(json)));
    }

    // ---- the ground explored (Memory.Explored) ----

    static GroundMemory Explored(World w, AgentId who) => ((PersonMind)w.Agent(who).Mind).Memory.Explored!;

    static double Eyes(World w, AgentId who) => Body.GetCapacities(w.Agent(who).Body).Sight;

    /// A new world has already looked once: the player stands at camp and has taken in what is in
    /// view, before any step (World.New). The same seed looks the same.
    [Fact]
    public void ANewWorld_HasLookedAroundOnce()
    {
        var w = World.New(17);
        var camp = Map.Of(w).Camp;
        var inView = Sight.Visible(w, camp, Eyes(w, P));
        Assert.Equal(inView.Count, Explored(w, P).Count);
        Assert.All(inView, t => Assert.True(Explored(w, P).Contains(t), $"{t} in view but not explored"));
        Assert.Equal(Save.ToJson(w), Save.ToJson(World.New(17)));
    }

    /// Everything in view is explored, bare ground too, which Places leaves out.
    [Fact]
    public void Looking_ExploresEveryTileInView_BareGroundToo()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        Sim.Step(w, new Wait(P), 1);
        var inView = Sight.Visible(w, camp, Eyes(w, P));
        Assert.True(inView.Count > ((PersonMind)w.Agent(P).Mind).Memory.Places.Count);
        Assert.Equal(inView.Count, Explored(w, P).Count);
        Assert.All(inView, t => Assert.True(Explored(w, P).Contains(t), $"{t} in view but not explored"));
    }

    /// Ground once seen stays explored when it is out of view; "visible" is only what is in view now.
    [Fact]
    public void ExploredGround_StaysExplored_AfterWalkingAway()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        Sim.Step(w, new Wait(P), 1);
        var atCamp = Sight.Visible(w, camp, Eyes(w, P));
        var far = Away(w, camp, 3 * Sight.OpenGroundTiles);
        Put(w, P, far);
        Sim.Step(w, new Wait(P), 1);
        var there = Sight.Visible(w, far, Eyes(w, P));
        Assert.DoesNotContain(camp, there);
        Assert.All(atCamp.Concat(there), t => Assert.True(Explored(w, P).Contains(t)));
        Assert.Equal(atCamp.Union(there).Count(), Explored(w, P).Count);
    }

    /// Asleep, nobody looks: nothing new is explored.
    [Fact]
    public void Asleep_NothingIsExplored()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        Sim.Step(w, new Wait(P), 1);
        int before = Explored(w, P).Count;
        Put(w, P, Away(w, camp, 3 * Sight.OpenGroundTiles));
        Sim.Step(w, new Sleep(P), 60);
        Assert.Equal(before, Explored(w, P).Count);
    }

    /// Saved, and the same however the steps are cut and wherever a save and load falls.
    [Fact]
    public void Explored_IsSaved_AndTheSameHoweverTheWalkIsStepped()
    {
        World Walk(int chunk, bool reload)
        {
            var w = World.New(17);
            foreach (var dir in new[] { Direction.East, Direction.East, Direction.South, Direction.East })
            {
                for (int done = 0; done < 90; done += chunk)
                {
                    if (Sim.Step(w, new Travel(P, dir), chunk).InterruptedReason is "arrived") break;
                    if (reload) w = Save.FromJson(Save.ToJson(w));
                }
                Sim.Step(w, new Wait(P), 30);
            }
            return w;
        }
        var whole = Walk(90, false);
        Assert.True(Explored(whole, P).Count > 0);
        string json = Save.ToJson(whole);
        Assert.Equal(json, Save.ToJson(Walk(1, false)));
        Assert.Equal(json, Save.ToJson(Walk(7, true)));
        var loaded = Save.FromJson(json);
        Assert.Equal(Explored(whole, P).Count, Explored(loaded, P).Count);
        Assert.Equal(json, Save.ToJson(loaded));
    }

    // ---- the ground as last seen (Memory.Explored) ----

    static readonly WeatherSample Cold = new(0, WeatherCondition.Cloudy, -10, 0.1, 0, 0.8, WindDirection.North);

    static void Snow(World w, TileId t, double depthM) =>
        TestKit.EditGround(w, t, (g, _) => g.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = depthM, CompactionPct = 0.3, FrozenFractionPct = 1 }));

    /// The first passable tile in view from here, other than here.
    static TileId InViewFrom(World w, TileId from) =>
        Sight.Visible(w, from, Eyes(w, P)).First(t => t != from && Map.Of(w).TerrainAt(t).IsPassable() && Map.Of(w).SiteAt(t) == null);

    /// Snow seen on a tile, which then goes while the player is out of sight of it, is still snow in
    /// their memory, after a save and load too: memory, not the ground as it is now.
    [Fact]
    public void GroundOutOfSight_IsTheGroundAsLastSeen_AfterASaveAndLoad()
    {
        var w = World.New(17);
        w.FixedWeather = Cold;
        var camp = Map.Of(w).Camp;
        var t = InViewFrom(w, camp);
        Sim.Step(w, new Wait(P), Memories.GlanceMinutes);
        Snow(w, t, 0.4);
        Sim.Step(w, new Wait(P), Memories.GlanceMinutes); // the clock's next look sees it
        double seenM = Ground.Underfoot(w, t).SnowDepthM;
        Assert.True(seenM > 0.35);
        Assert.Equal(seenM, Explored(w, P).At(t)!.Value.Surface.SnowDepthM, 3);

        Put(w, P, OutOfView(w, t));
        Sim.Step(w, new Wait(P), 1);
        Assert.False(Sight.InSight(w, w.Agent(P), t));
        TestKit.EditGround(w, t, (g, _) => g.Layers.RemoveAll(l => l.Material == SurfaceMaterial.Snow)); // gone while nobody looked
        Sim.Step(w, new Wait(P), Memories.GlanceMinutes);

        var loaded = Save.FromJson(Save.ToJson(w));
        var remembered = Explored(loaded, P).At(t)!.Value;
        Assert.Equal(seenM, remembered.Surface.SnowDepthM, 3);
        Assert.Equal(2 * Memories.GlanceMinutes, remembered.SeenMinute);
        Assert.Equal(0, Ground.Underfoot(loaded, t).SnowDepthM, 3);
    }

    /// Every tile in view has its ground remembered, with the minute of the look.
    [Fact]
    public void Looking_RemembersTheGroundOfEveryTileInView()
    {
        var w = Mild17();
        Sim.Step(w, new Wait(P), 1);
        var inView = Sight.Visible(w, Map.Of(w).Camp, Eyes(w, P));
        Assert.All(inView, t => Assert.Equal(1, Explored(w, P).At(t)!.Value.SeenMinute));
        var far = Away(w, Map.Of(w).Camp, 3 * Sight.OpenGroundTiles);
        Assert.Null(Explored(w, P).At(far));
    }

    /// Kept to the millimetre and the hundredth, as a view shows it; refused, not clipped, out of range.
    [Fact]
    public void GroundMemory_HoldsMillimetresAndHundredths_AndRefusesWhatItCannotHold()
    {
        var g = new GroundMemory(4, 3);
        var t = new TileId(2, 1);
        g.Remember(t, new SeenSurface(0.12345, true, 0.456, 0.0071, true), 1234567);
        Assert.Equal(new RememberedGround(new SeenSurface(0.123, true, 0.46, 0.007, true), 1234567), g.At(t));
        var loaded = GroundMemory.Parse(g.Format());
        Assert.Equal(g.At(t), loaded.At(t));
        Assert.Equal(1, loaded.Count);
        Assert.Null(loaded.At(new TileId(0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => g.Remember(t, new SeenSurface(-0.01, false, 0, 0, false), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => g.Remember(t, new SeenSurface(0, false, 1.2, 0, false), 0));
        Assert.Equal(new RememberedGround(new SeenSurface(0.123, true, 0.46, 0.007, true), 1234567), g.At(t)); // a refused value leaves the tile as it was
        Assert.Equal(1, g.Count);
        Assert.Throws<System.Text.Json.JsonException>(() => GroundMemory.Parse("4x3:AA==:"));
        var one = g.Format();
        var records = one[(one.LastIndexOf(':') + 1)..];
        Assert.Throws<System.Text.Json.JsonException>(() => GroundMemory.Parse(one[..one.LastIndexOf(':')] + ":")); // a seen tile with no record
        Assert.Throws<System.Text.Json.JsonException>(() => GroundMemory.Parse(one + "AAAAAAAAAAAAAA==")); // not base64 at that length
        Assert.Throws<System.Text.Json.JsonException>(() => GroundMemory.Parse("4xthree:AAA=:" + records));
        var badFlags = Convert.FromBase64String(records);
        badFlags[9] = 0;
        Assert.Throws<System.Text.Json.JsonException>(() => GroundMemory.Parse(one[..one.LastIndexOf(':')] + ":" + Convert.ToBase64String(badFlags)));
    }

    // ---- routes as last seen (Memory.Explored trail tiers) ----

    static void Tread(World w, TileId a, TileId b, double trodden) => w.Trails[new EdgeId(a, b)] = new EdgeTrodden(trodden, w.Minute);

    /// A neighbour of t, also in view from the camp, both passable.
    static TileId NeighbourInView(World w, TileId t)
    {
        var view = Sight.Visible(w, Map.Of(w).Camp, Eyes(w, P)).ToHashSet();
        foreach (var dir in new[] { Direction.East, Direction.South, Direction.West, Direction.North })
        {
            var n = Map.Step(t, dir);
            if (view.Contains(n) && Map.Of(w).TerrainAt(n).IsPassable()) return n;
        }
        throw new InvalidOperationException($"No neighbour of {t} in view");
    }

    /// A route seen as a path, trodden into a trail while the player is away, is still a path in their
    /// memory, after a save and load too; back in sight it is a trail.
    [Fact]
    public void RouteOutOfSight_IsTheRouteAsLastSeen_AfterASaveAndLoad()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var a = InViewFrom(w, camp);
        var b = NeighbourInView(w, a);
        Tread(w, a, b, Tracks.PathAt + 0.05);
        Sim.Step(w, new Wait(P), Memories.GlanceMinutes);
        Assert.Equal(TrailTier.Path, Explored(w, P).TrailTierAt(a, b));
        Assert.Equal(TrailTier.Path, Explored(w, P).TrailTierAt(b, a));

        Put(w, P, OutOfView(w, camp));
        Sim.Step(w, new Wait(P), 1);
        Assert.False(Sight.InSight(w, w.Agent(P), a) || Sight.InSight(w, w.Agent(P), b));
        Tread(w, a, b, Tracks.TrailAt + 0.05); // trodden in while nobody looked
        Sim.Step(w, new Wait(P), Memories.GlanceMinutes);
        var loaded = Save.FromJson(Save.ToJson(w));
        Assert.Equal(TrailTier.Path, Explored(loaded, P).TrailTierAt(a, b));

        Put(loaded, P, camp);
        Sim.Step(loaded, new Wait(P), 1);
        Assert.Equal(TrailTier.Trail, Explored(loaded, P).TrailTierAt(a, b));
    }

    /// A route seen gone is forgotten; one never trodden is none.
    [Fact]
    public void RouteSeenFaded_IsForgotten()
    {
        var w = Mild17();
        var a = InViewFrom(w, Map.Of(w).Camp);
        var b = NeighbourInView(w, a);
        Tread(w, a, b, Tracks.TraceAt + 0.05);
        Sim.Step(w, new Wait(P), Memories.GlanceMinutes);
        Assert.Equal(TrailTier.Trace, Explored(w, P).TrailTierAt(a, b));
        w.Trails.Remove(new EdgeId(a, b));
        Sim.Step(w, new Wait(P), Memories.GlanceMinutes);
        Assert.Equal(TrailTier.None, Explored(w, P).TrailTierAt(a, b));
        Assert.Empty(Explored(w, P).Trails());
    }

    /// Kept beside the ground of the tile on the west or north end: a new look at the ground keeps it,
    /// it is saved, and a route with an end never seen is refused.
    [Fact]
    public void GroundMemory_KeepsRoutesBetweenSeenTiles()
    {
        var g = new GroundMemory(4, 3);
        TileId a = new(1, 1), east = new(2, 1), south = new(1, 2);
        var ground = new SeenSurface(0.1, false, 0, 0, false);
        foreach (var t in new[] { a, east, south }) g.Remember(t, ground, 5);
        g.RememberTrailTier(east, a, TrailTier.Trail);
        g.RememberTrailTier(a, south, TrailTier.Trace);
        g.Remember(a, new SeenSurface(0.2, true, 0.5, 0, true), 6);
        Assert.Equal(TrailTier.Trail, g.TrailTierAt(a, east));
        Assert.Equal(TrailTier.Trace, g.TrailTierAt(south, a));
        var loaded = GroundMemory.Parse(g.Format());
        Assert.Equal(TrailTier.Trail, loaded.TrailTierAt(a, east));
        Assert.Equal(TrailTier.Trace, loaded.TrailTierAt(a, south));
        Assert.Equal(new[] { (a, east, TrailTier.Trail), (a, south, TrailTier.Trace) }, loaded.Trails());
        Assert.Throws<ArgumentException>(() => g.RememberTrailTier(a, new TileId(0, 1), TrailTier.Path)); // never seen
        Assert.Throws<ArgumentException>(() => g.RememberTrailTier(a, new TileId(3, 1), TrailTier.Path)); // not a neighbour
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
        var far = OutOfView(w, camp);
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
        Put(w, P, OutOfView(w, field));
        Sim.Step(w, new Wait(P), 1);
        return (w, field);
    }

    static void HerdCrosses(World w, TileId into)
    {
        var m = Map.Of(w);
        var dir = Enum.GetValues<Direction>().First(d => m.TerrainAt(Map.Step(into, d)).IsPassable() && Map.Blocked(m, Map.Step(into, d), into) == null);
        var from = Map.Step(into, dir);
        var herd = Agents.SpawnGroup(w, Species.Caribou, from, 12)[0]; // its herd follows it
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
        Put(w, P, OutOfView(w, camp));
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

        Put(w, P, OutOfView(w, camp));
        while (w.Minute <= died + 60) Sim.Step(w, new Wait(P), 60);
        Assert.Equal(FirePhase.Roaring, Memory(w, P, camp)!.Fire);
        Assert.Equal(left, Memory(w, P, camp)!.SeenMinute);

        // Back in sight of camp, not yet on it: the fire is seen to be out.
        Put(w, P, InViewOf(w, camp));
        Sim.Step(w, new Wait(P), 1);
        Assert.Equal(FirePhase.Cold, Memory(w, P, camp)!.Fire);
        Assert.Equal(w.Minute, Memory(w, P, camp)!.SeenMinute);
    }

    /// From a distance you see whether a fire burns, not how much is on it (review: a glance updated the
    /// phase to Embers and left "5.0 h left" beside it). Seen burning, the reckoning from beside it stands;
    /// seen out, there are no hours left.
    [Fact]
    public void AGlance_SeesTheFireOut_AndForgetsItsHours()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var pit = PlacePit(w, camp, 5);
        Sim.Step(w, new Wait(P), 1);
        double hours = Memory(w, P, camp)!.FireHours!.Value;
        Assert.True(hours > 3);
        long died = DiesAt(w, pit);

        Put(w, P, InViewOf(w, camp)); // in sight of camp, not on it
        Sim.Step(w, new Wait(P), Memories.GlanceMinutes);
        Assert.True(Fire.Burns(Memory(w, P, camp)!.Fire));
        Assert.Equal(hours, Memory(w, P, camp)!.FireHours);

        while (!w.Fires[pit].HasEmbers && w.Minute < died) Sim.Step(w, new Wait(P), Memories.GlanceMinutes);
        Assert.Equal(FirePhase.Embers, Memory(w, P, camp)!.Fire);
        Assert.Null(Memory(w, P, camp)!.FireHours);
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
        Put(w, P, OutOfView(w, camp));
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

    /// A full journal lets go of what happened longest ago, not what was filed first: catch-up files
    /// the fire that died in the night after the morning's news (review). Among notable entries alike.
    [Fact]
    public void AFullJournal_EvictsTheEarliestHappened_NotTheFirstFiled()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var events = new List<WorldEvent>();
        for (int i = 0; i < Journal.MaxEntriesPerTile; i++) events.Add(new FireDied(600 + i, new FireId(i + 1), camp));
        events.Add(new FireDied(300, new FireId(0), camp)); // found late: it went out at minute 300
        events.Add(new FireDied(700, new FireId(99), camp));
        int filed = 0;
        Journal.File(w, events, ref filed);
        var kept = w.Journal[camp];
        Assert.Equal(Journal.MaxEntriesPerTile, kept.Count);
        Assert.DoesNotContain(kept, e => e.Minute == 300); // the earliest-happened went, though it was filed late
        Assert.DoesNotContain(kept, e => e.Minute == 600); // then the next earliest made room for minute 700
        Assert.Contains(kept, e => e.Minute == 601);
        Assert.Contains(kept, e => e.Minute == 700);
    }

    /// Review: only fuel and stash chores made way, so 31 failed strikes, meals or cooked meat pushed
    /// out the fire going out. Everything not notable goes before anything notable: the fire dying, a
    /// shelter down, a wound, a kill, and the stash taken by someone whose camp it is not.
    [Fact]
    public void ADayOfMealsAndMisses_KeepsWhatMattered_InTheJournal()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var stranger = Npc.Spawn(w, camp, Away(w, camp, 12));
        var events = new List<WorldEvent>
        {
            new FireDied(100, new FireId(0), camp),
            new Injured(101, P, camp, "Left Leg", DamageType.Pierce, 1),
            new Unstashed(102, stranger, camp, Resource.Birch, 3, Taken: true), // someone else's camp is not theirs to take from
            new Killed(103, new AgentId(99), Species.Caribou, 100, camp, P),
        };
        for (int i = 0; i < 3 * Journal.MaxEntriesPerTile; i++)
            events.Add((i % 4) switch
            {
                0 => new FireStartFailed(200 + i, P, camp, 0.1),
                1 => new Ate(200 + i, P, camp, Resource.Berries, 0.1, 50),
                2 => new FireLit(200 + i, new FireId(1), camp, P),
                _ => new Unstashed(200 + i, P, camp, Resource.Birch, 1, Taken: false), // their own camp: a chore
            });
        int filed = 0;
        Journal.File(w, events, ref filed);
        var kept = w.Journal[camp];
        Assert.Equal(Journal.MaxEntriesPerTile, kept.Count);
        Assert.Contains(kept, e => e is FireDied);
        Assert.Contains(kept, e => e is Injured);
        Assert.Contains(kept, e => e is Unstashed u && u.Who == stranger);
        Assert.Contains(kept, e => e is Killed);
        Assert.Equal(200 + 3 * Journal.MaxEntriesPerTile - 1, kept[^1].Minute); // and the latest of the rest
    }

    /// Chores make way first: a night of feeding the fire and fetching from the stash cannot push out
    /// the fire going out (review: an overnight FireDied evicted by FuelAdded and Stashed).
    [Fact]
    public void ABusyNight_KeepsTheFireDying_InTheJournal()
    {
        var w = Mild17();
        var camp = Map.Of(w).Camp;
        var events = new List<WorldEvent> { new FireDied(100, new FireId(0), camp) };
        for (int i = 0; i < 3 * Journal.MaxEntriesPerTile; i++)
            events.Add(i % 2 == 0 ? new FuelAdded(200 + i, new FireId(1), camp, P, Resource.Pine, 1) : new Stashed(200 + i, P, camp, Resource.Pine, 1));
        int filed = 0;
        Journal.File(w, events, ref filed);
        var kept = w.Journal[camp];
        Assert.Equal(Journal.MaxEntriesPerTile, kept.Count);
        Assert.Contains(kept, e => e is FireDied);
        Assert.Equal(200 + 3 * Journal.MaxEntriesPerTile - 1, kept[^1].Minute); // the latest chores are the ones kept
        Assert.Equal(200 + 3 * Journal.MaxEntriesPerTile - (Journal.MaxEntriesPerTile - 1), kept[1].Minute);
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
        var places = ((NpcMind)w.Agent(npc).Mind).Memory.Places;
        Assert.Contains(places.Values, p => p.Visited && p.ForageDensity != null);
        Assert.Contains(places.Values, p => p.Trees && !p.Visited);
        Assert.NotEmpty(((NpcMind)w.Agent(npc).Mind).Memory.Sightings);
    }

    // ---- determinism ----

    /// Memory, journals and skills learned along the way come out the same however the minutes are
    /// stepped, and through saves: a busy camp with an NPC, a fire, work, a walk out and back, a night.
    [Trait("Category", TestCategories.Slow)]
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
            var far = OutOfView(w, camp);
            void Do(Func<Activity> make, int total)
            {
                for (int done = 0; done < total;)
                {
                    int n = Math.Min(chunk, total - done);
                    var r = Sim.Step(w, make(), n, more: done + n < total); // one job: work held between its chunks
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
