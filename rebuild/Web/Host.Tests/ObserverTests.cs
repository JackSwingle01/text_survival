using System.Text.Json;
using Survival;
using Survival.Web;

namespace Survival.Web.Tests;

/// Observer mode (GLOSSARY): a world with no player in it, drawn as it truly is, run at the page's rate.
public class ObserverTests
{
    const int Seed = 1;

    static ObserverStartDto Start(string json) => JsonSerializer.Deserialize(json, HostJson.Default.ObserverStartDto)!;
    static ObserverViewDto View(string json) => JsonSerializer.Deserialize(json, HostJson.Default.ObserverViewDto)!;
    static MapDto Map(string json) => JsonSerializer.Deserialize(json, HostJson.Default.MapDto)!;
    static TileDto Tile(string json) => JsonSerializer.Deserialize(json, HostJson.Default.TileDto)!;

    /// The observer's map is the player's MapDto with every tile explored and in sight: the page draws it
    /// with the same renderer, so whatever the map gains (ravines, routes) the observer shows too.
    [Fact]
    public void A_new_world_has_no_player_and_its_map_is_the_whole_valley_in_sight()
    {
        var o = new Observer();
        var start = Start(o.New(Seed));
        Assert.False(o.World.Agents.ContainsKey(AgentId.Player));
        var map = Map(o.Map());
        int tiles = map.Width * map.Height;
        Assert.Equal(tiles, map.Known.Length);
        Assert.DoesNotContain(MapView.UnknownCode, map.Known);
        Assert.Equal(Enumerable.Range(0, tiles), map.Visible);
        var m = Survival.Map.Of(o.World);
        Assert.Equal((m.Camp.X, m.Camp.Y), (map.CampX, map.CampY));
        Assert.Equal(m.Edges.Count(e => e.Value is EdgeType.Ravine or EdgeType.Cliff), map.Edges.Count);
        Assert.Contains(map.Named, n => n.X == m.Camp.X && n.Y == m.Camp.Y);
        Assert.Empty(map.Sightings); // the view's actors stand in for them
        Assert.Empty(map.Places);    // and its places, which change by the minute
        Assert.NotEmpty(start.View.Actors); // the herds World.New populates
        Assert.All(start.View.Actors, a => Assert.Equal("herd", a.Kind));
    }

    [Fact]
    public void The_map_shows_every_worn_route_and_every_print_on_the_valley()
    {
        var o = new Observer();
        o.New(Seed);
        var w = o.World;
        var m = Survival.Map.Of(w);
        var a = m.Camp;
        var b = Survival.Map.Step(a, Direction.East);
        for (int i = 0; i < 40; i++) Tracks.RecordMove(w, i % 2 == 0 ? a : b, i % 2 == 0 ? b : a, TrackMaker.Human);
        var map = Map(o.Map());
        var worn = w.Trails.Keys.Where(e => Tracks.TierOf(w, e.A, e.B) != TrailTier.None).Select(e => (e.A.X, e.A.Y, e.B.X, e.B.Y)).ToList();
        Assert.Equal(worn, map.Trails.Select(t => (t.Ax, t.Ay, t.Bx, t.By)));
        Assert.Contains(map.Trails, t => t.Tier == "trail");
        Assert.Contains(map.Tracks, t => (t.X, t.Y) == (a.X, a.Y) && t.Maker == Names.Of(TrackMaker.Human));
        Assert.All(map.Tracks, t => Assert.NotEmpty(Tracks.Read(w, new TileId(t.X, t.Y))));
    }

    [Fact]
    public void Advance_moves_the_clock_and_the_view_says_when_the_ground_last_stepped()
    {
        var o = new Observer();
        long start = Start(o.New(Seed)).View.Minute;
        var v = View(o.Advance(7));
        Assert.Equal(start + 7, v.Minute);
        Assert.Equal(start, v.GroundMinute); // still inside Ground's first 15-minute step
        v = View(o.Advance(8));
        Assert.Equal(start + 15, v.Minute);
        Assert.Equal(start + Ground.StepMinutes, v.GroundMinute);
    }

    [Fact]
    public void A_spawned_npc_stands_on_its_tile_and_lives_through_a_day_with_no_player_in_the_world()
    {
        var o = new Observer();
        var start = Start(o.New(Seed));
        var camp = Survival.Map.Of(o.World).Camp;
        Assert.Contains(start.Spawnables, s => s.Id == "npc");
        var v = View(o.Spawn("npc", camp.X, camp.Y));
        var npc = Assert.Single(v.Actors, a => a.Kind == "npc");
        Assert.Equal((camp.X, camp.Y), (npc.X, npc.Y));
        for (int i = 0; i < 24; i++) v = View(o.Advance(60));
        Assert.Equal(24 * 60, v.Minute);
        Assert.Contains(v.Actors, a => a.Id == npc.Id); // alive, wherever it went
    }

    [Fact]
    public void Spawning_on_ground_nothing_can_stand_on_is_refused()
    {
        var o = new Observer();
        o.New(Seed);
        var map = Map(o.Map());
        int i = map.Known.IndexOf('X'); // mountain
        Assert.Throws<ArgumentException>(() => o.Spawn("npc", i % map.Width, i / map.Width));
        Assert.Throws<ArgumentException>(() => o.Spawn("dragon", map.CampX, map.CampY));
        Assert.Throws<ArgumentException>(() => o.Spawn("npc", -1, 0));
    }

    [Fact]
    public void Inspect_names_what_is_on_the_tile_as_a_person_standing_there_would_and_who_is_there()
    {
        var o = new Observer();
        o.New(Seed);
        var camp = Survival.Map.Of(o.World).Camp;
        o.Spawn("npc", camp.X, camp.Y);
        var t = Tile(o.Inspect(camp.X, camp.Y));
        Assert.Equal(Survival.Map.Describe(Survival.Map.Of(o.World), camp), t.Place);
        Assert.Contains(t.Features, f => f.Kind == "shelter"); // the camp overhang World.New places
        Assert.Contains(t.Features, f => f.Kind == "agent" && f.Name == "a person");
        var person = Assert.Single(t.Actors);
        Assert.Contains(person.Lines, l => l.StartsWith("Warmth"));
    }

    /// Fires live on the clock only when someone near them catches them up: with nobody there the
    /// observer must, or the map would show yesterday's fire (and Verbs' names would throw reading it).
    [Fact]
    public void A_fire_nobody_tends_burns_down_on_the_map()
    {
        var o = new Observer();
        o.New(Seed);
        var w = o.World;
        var tile = Survival.Map.Of(w).Camp;
        var pit = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(pit, Resource.Kindling, 1);
        Fire.IgniteAll(pit);
        w.Fires[new FireId(w.NextFireId++)] = pit;
        Assert.True(View(o.View()).Places.Single(p => p.X == tile.X && p.Y == tile.Y).FireHoursLeft > 0);
        var v = View(o.Advance(12 * 60));
        Assert.Equal(w.Minute, pit.LastUpdatedMinute);
        Assert.Null(v.Places.Single(p => p.X == tile.X && p.Y == tile.Y).FireHoursLeft);
        Assert.Contains(Tile(o.Inspect(tile.X, tile.Y)).Features, f => f.Kind == "fire");
    }
}
