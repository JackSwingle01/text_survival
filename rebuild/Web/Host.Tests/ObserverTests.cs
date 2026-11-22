using System.Text.Json;
using Survival;
using Survival.Web;

namespace Survival.Web.Tests;

/// Observer mode (GLOSSARY): a world with no player in it, drawn as it truly is, run at the page's rate.
public class ObserverTests
{
    const int Seed = 1;

    static ObserverMapDto Map(string json) => JsonSerializer.Deserialize(json, HostJson.Default.ObserverMapDto)!;
    static ObserverViewDto View(string json) => JsonSerializer.Deserialize(json, HostJson.Default.ObserverViewDto)!;
    static TileDto Tile(string json) => JsonSerializer.Deserialize(json, HostJson.Default.TileDto)!;

    [Fact]
    public void A_new_world_has_no_player_and_its_whole_map_is_known()
    {
        var o = new Observer();
        var map = Map(o.New(Seed));
        Assert.False(o.World.Agents.ContainsKey(AgentId.Player));
        Assert.Equal(map.Width * map.Height, map.Terrain.Length);
        Assert.DoesNotContain(ViewBuilder.UnknownCode, map.Terrain);
        var view = View(o.View());
        Assert.NotEmpty(view.Actors); // the herds World.New populates
        Assert.All(view.Actors, a => Assert.Equal("herd", a.Kind));
        Assert.NotNull(view.Ground); // the first view carries the ground
    }

    [Fact]
    public void Advance_moves_the_clock_by_the_minutes_asked_and_sends_the_ground_only_when_it_has_stepped()
    {
        var o = new Observer();
        o.New(Seed);
        long start = View(o.View()).Minute;
        var v = View(o.Advance(7));
        Assert.Equal(start + 7, v.Minute);
        Assert.Null(v.Ground); // still inside Ground's first 15-minute step
        v = View(o.Advance(8));
        Assert.Equal(start + 15, v.Minute);
        Assert.NotNull(v.Ground);
    }

    [Fact]
    public void A_spawned_npc_stands_on_its_tile_and_lives_through_a_day_with_no_player_in_the_world()
    {
        var o = new Observer();
        var map = Map(o.New(Seed));
        Assert.Contains(map.Spawnables, s => s.Id == "npc");
        var v = View(o.Spawn("npc", map.CampX, map.CampY));
        var npc = Assert.Single(v.Actors, a => a.Kind == "npc");
        Assert.Equal((map.CampX, map.CampY), (npc.X, npc.Y));
        for (int i = 0; i < 24; i++) v = View(o.Advance(60));
        Assert.Equal(24 * 60, v.Minute);
        Assert.Contains(v.Actors, a => a.Id == npc.Id); // alive, wherever it went
    }

    [Fact]
    public void Spawning_on_ground_nothing_can_stand_on_is_refused()
    {
        var o = new Observer();
        var map = Map(o.New(Seed));
        int i = map.Terrain.IndexOf('X'); // mountain
        Assert.Throws<ArgumentException>(() => o.Spawn("npc", i % map.Width, i / map.Width));
        Assert.Throws<ArgumentException>(() => o.Spawn("dragon", map.CampX, map.CampY));
        Assert.Throws<ArgumentException>(() => o.Spawn("npc", -1, 0));
    }

    [Fact]
    public void Inspect_names_what_is_on_the_tile_as_a_person_standing_there_would_and_who_is_there()
    {
        var o = new Observer();
        var map = Map(o.New(Seed));
        o.Spawn("npc", map.CampX, map.CampY);
        var t = Tile(o.Inspect(map.CampX, map.CampY));
        var camp = Survival.Map.Of(o.World).Camp;
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
        var map = Map(o.New(Seed));
        var w = o.World;
        var tile = new TileId(map.CampX, map.CampY);
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
