using System.Text;
using System.Text.Json;

namespace Survival.Web.Tests;

/// The host boundary, headless: what the page relies on. Oracles come from the design (ui-design §Phase 5
/// gate) and from Sim's own contracts, not from the view code.
public class GameHostTests
{
    const int Seed = 1;

    static ViewDto Parse(string json) => JsonSerializer.Deserialize(json, HostJson.Default.ViewDto)!;

    static IEnumerable<VerbDto> AllVerbs(ViewDto v) =>
        v.Things.SelectMany(t => t.Menu).Concat(v.Walk).Concat(v.Tile).Concat(v.PackVerbs).Concat(v.Make);

    static string Act(GameHost h, string verbId) => h.Act($$"""{"verb":"{{verbId}}"}""");

    /// Starts a verb and advances it in chunks to the end, as the page does. Returns the last view.
    static ViewDto Run(GameHost h, VerbDto verb, int chunk = 10)
    {
        var v = Parse(Act(h, verb.Id));
        for (int i = 0; v.Job is { Finished: false } && i < 10_000; i++) v = Parse(h.Act($$"""{"advance":{{chunk}}}"""));
        Assert.True(v.Job!.Finished, $"{verb.Label} never finished");
        return v;
    }

    static VerbDto Find(ViewDto v, Func<VerbDto, bool> match, string what) =>
        AllVerbs(v).FirstOrDefault(match) ?? throw new Xunit.Sdk.XunitException($"No verb {what} in: {string.Join(", ", AllVerbs(v).Select(x => x.Label))}");

    [Fact]
    public void A_new_game_lists_the_tiles_verbs_with_ids_and_the_four_walks()
    {
        var v = Parse(new GameHost().NewGame(Seed));
        Assert.Equal("surviving", v.Outcome);
        Assert.Contains(v.Tile, x => x.Kind == "wait");
        Assert.Contains(v.Tile, x => x.Kind == "sleep");
        Assert.Equal(["north", "east", "south", "west"], v.Walk.Where(w => !w.Careful).Select(w => w.Dir!));
        Assert.Equal(AllVerbs(v).Count(), AllVerbs(v).Select(x => x.Id).Distinct().Count());
        Assert.True(v.Place.Camp);
        Assert.Equal(4, v.Vitals.Count);
    }

    [Fact]
    public void An_id_from_an_older_view_is_refused_not_guessed()
    {
        var h = new GameHost();
        var first = Parse(h.NewGame(Seed));
        h.View(); // a new view: the old ids are gone
        var wait = Find(first, x => x.Kind == "wait", "wait");
        Assert.Throws<ArgumentException>(() => Act(h, wait.Id));
    }

    [Fact]
    public void A_refused_verb_finishes_at_once_with_its_reason_and_no_time_passes()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        var refused = AllVerbs(v).First(x => x.Refused != null);
        var after = Parse(Act(h, refused.Id));
        Assert.True(after.Job!.Finished);
        Assert.Equal(("refused", refused.Refused), (after.Job.Outcome, after.Job.Reason));
        Assert.Equal(v.Minute, after.Minute);
    }

    [Fact]
    public void Every_trip_preview_sums_to_its_total_and_the_walk_takes_that_long()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        foreach (var w in v.Walk.Where(w => w.Trip != null))
            Assert.Equal(w.Trip!.TotalMinutes, w.Trip.Terms.Sum(t => t.Minutes));
        var go = v.Walk.First(w => w.Refused == null && !w.Careful);
        var after = Run(h, go, chunk: 3);
        Assert.Equal(go.Trip!.TotalMinutes, after.Job!.DoneMinutes);
        Assert.Equal(("finished", "arrived"), (after.Job.Outcome, after.Job.Reason)); // an arrival is not an interruption
        Assert.Equal(v.Minute + go.Trip.TotalMinutes, after.Minute);
        Assert.False(after.Place.Camp);
    }

    [Fact]
    public void Every_odds_sheet_adds_up_to_its_total()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var v = Parse(Scenarios.Apply(h, "supplied"));
        var sheets = AllVerbs(v).Select(x => x.Odds).Concat(AllVerbs(v).SelectMany(x => x.Make?.Outcomes.Select(o => o.Odds) ?? [])).OfType<OddsDto>().ToList();
        Assert.NotEmpty(sheets);
        Assert.Contains(AllVerbs(v), x => x.Kind == "fire" && x.Odds != null);
        foreach (var o in sheets)
        {
            Assert.Equal(o.RawPct, o.BasePct + o.Terms.Sum(t => t.DeltaPct), 9);
            Assert.Equal(Math.Clamp(o.RawPct, 0, 1), o.TotalPct, 9);
        }
    }

    /// Chunking is the page's pacing, not the game's: waiting an hour in six steps leaves the world
    /// exactly as one step of an hour does (Sim: the world moves with the clock however steps are chunked).
    [Fact]
    public void A_wait_run_in_chunks_leaves_the_same_world_as_one_step()
    {
        var chunked = new GameHost();
        var wait = Find(Parse(chunked.NewGame(Seed)), x => x.Kind == "wait", "wait");
        Run(chunked, wait, chunk: 7);

        var whole = new GameHost();
        whole.NewGame(Seed);
        Sim.Step(whole.World, new Wait(AgentId.Player), wait.Minutes!.Value);

        Assert.Equal(Encoding.UTF8.GetString(whole.Save()), Encoding.UTF8.GetString(chunked.Save()));
    }

    [Fact]
    public void A_crossing_run_in_chunks_leaves_the_same_world_as_one_step()
    {
        var chunked = new GameHost();
        var v = Parse(chunked.NewGame(Seed));
        var go = v.Walk.First(w => w.Refused == null && !w.Careful);
        Run(chunked, go, chunk: 2);

        var whole = new GameHost();
        whole.NewGame(Seed);
        Sim.Step(whole.World, new Travel(AgentId.Player, Enum.Parse<Direction>(go.Dir!, ignoreCase: true)), Verbs.UntilDoneMinutes);

        Assert.Equal(Encoding.UTF8.GetString(whole.Save()), Encoding.UTF8.GetString(chunked.Save()));
    }

    [Fact]
    public void Making_a_staff_runs_in_chunks_to_the_end_and_puts_one_staff_in_the_pack()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var v = Parse(Scenarios.Apply(h, "supplied"));
        var staff = Find(v, x => x.Make?.Recipe == "staff", "make staff");
        Assert.Null(staff.Refused);
        Assert.Equal(1, staff.Make!.PlainPct + staff.Make.Outcomes.Sum(o => o.Odds.TotalPct), 9);
        var after = Run(h, staff, chunk: 2);
        Assert.True(after.Job!.DoneMinutes > 2, "it ran in more than one chunk");
        Assert.Empty(after.Pack.Projects);
        // It either made the staff (the plain result) or failed (its one outcome): never a second project.
        bool made = after.Pack.Gear.Count(g => g.Kind == "staff") == 1;
        Assert.True(made || after.Log.Any(l => l.Text.Contains("failed")), "the staff was neither made nor failed");
    }

    /// The Phase 5 gate: the map is drawn from beliefs. Light a fire, step off camp, sleep while it dies:
    /// right after the sleep the map still shows it burning (asleep is away), though the truth is cold.
    [Fact]
    public void The_map_shows_the_camp_fire_as_last_seen_not_as_it_is()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var v = Parse(Scenarios.Apply(h, "supplied"));
        for (int tries = 0; v.Fire is not { Burning: true }; tries++)
        {
            Assert.True(tries < 40, "the fire never caught");
            var light = Find(v, x => x.Kind == "fire" && x.Refused == null, "light a fire");
            v = Run(h, light);
            if (v.Fire is not { Burning: true }) v = Run(h, Find(v, x => x.Kind == "wait", "wait")); // let the hands warm
        }
        var camp = (v.Place.X, v.Place.Y);
        v = Run(h, v.Walk.First(w => w.Refused == null && !w.Careful));
        Assert.Contains(v.Map.Places, p => (p.X, p.Y) == camp && p.Fire is not (null or "Cold" or "Embers"));

        v = Run(h, Find(v, x => x.Kind == "sleep", "sleep"), chunk: 60);
        var pit = h.World.Fires.Values.Single(f => f.CarriedBy == null && (f.Tile.X, f.Tile.Y) == camp);
        Fire.CatchUp(h.World, h.World.Fires.Single(x => x.Value == pit).Key, []);
        Assert.False(Fire.Burns(Fire.Phase(pit)), "the fire should have died in the night");
        var remembered = v.Map.Places.Single(p => (p.X, p.Y) == camp);
        Assert.True(remembered.Fire is not (null or "Cold" or "Embers"), $"the map shows {remembered.Fire}: it read the truth");

        v = Run(h, Find(v, x => x.Kind == "wait", "wait"));
        Assert.Contains(v.Map.Places, p => (p.X, p.Y) == camp && p.Fire is null or "Cold" or "Embers");
    }

    [Fact]
    public void The_map_sends_only_explored_tiles_and_the_tiles_in_sight_are_explored()
    {
        var v = Parse(new GameHost().NewGame(Seed));
        Assert.Equal(v.Map.Width * v.Map.Height, v.Map.Known.Length);
        int known = v.Map.Known.Count(c => c != ViewBuilder.UnknownCode);
        Assert.InRange(known, 10, v.Map.Known.Length / 4);
        Assert.All(v.Map.Visible, i => Assert.NotEqual(ViewBuilder.UnknownCode, v.Map.Known[i]));
    }

    [Fact]
    public void Save_and_load_give_back_the_same_world_and_view()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        Run(h, v.Walk.First(w => w.Refused == null && !w.Careful));
        var bytes = h.Save();
        var before = Parse(h.View());

        var loaded = new GameHost();
        var after = Parse(loaded.Load(bytes));
        Assert.Equal(Encoding.UTF8.GetString(bytes), Encoding.UTF8.GetString(loaded.Save()));
        Assert.Equal(before.Minute, after.Minute);
        Assert.Equal(before.Map.Known, after.Map.Known);
        Assert.Equal(before.Place, after.Place);
        Assert.Equal(AllVerbs(before).Select(x => (x.Label, x.Refused)), AllVerbs(after).Select(x => (x.Label, x.Refused)));
    }

    [Fact]
    public void A_fight_lists_the_grid_and_its_actions_and_a_round_takes_a_minute()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var v = Parse(Scenarios.Apply(h, "fight"));
        Assert.NotNull(v.Combat);
        Assert.Contains(v.Combat!.Units, u => u.IsPlayer);
        Assert.Contains(v.Combat.Units, u => u.Team == 1);
        Assert.Contains(v.Combat.Actions, a => a.Label == "Wait" && a.Refused == null);
        Assert.All(v.Walk, w => Assert.Equal(Sim.InAFight, w.Refused));
        var advance = v.Combat.Actions.First(a => a.Label == "Advance");
        var after = Run(h, advance);
        Assert.Equal(v.Minute + 1, after.Minute);
    }

    [Fact]
    public void Death_and_the_crossing_are_the_outcome()
    {
        var dead = new GameHost();
        dead.NewGame(Seed);
        var d = Parse(Scenarios.Apply(dead, "dead"));
        Assert.Equal("died", d.Outcome);
        Assert.NotNull(d.DeathCause);

        var won = new GameHost();
        won.NewGame(Seed);
        var w = Parse(Scenarios.Apply(won, "won"));
        Assert.Equal("crossed", w.Outcome);
        Assert.All(w.Map.Surface, s => Assert.NotEqual('.', w.Map.Known[s.I])); // in sight from a tile never looked around: no ground for the unknown
    }

    [Fact]
    public void A_calm_start_shows_no_alerts()
    {
        var v = Parse(new GameHost().NewGame(Seed));
        Assert.Empty(v.Alerts);
    }

    /// Sim's gaps the page used to work around: the journal is Sim's narration, the pack has its verbs,
    /// and the map carries the ground in sight.
    [Fact]
    public void The_journal_reads_in_words_the_pack_has_verbs_and_the_map_has_the_ground()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        Assert.Contains(v.PackVerbs, x => x.Kind == "takeoff");
        Assert.Contains(v.PackVerbs, x => x.Kind == "stash");
        var go = v.Walk.First(w => w.Refused == null && !w.Careful);
        var after = Run(h, go, chunk: 3);
        Assert.Contains(after.Log, l => l.Text.StartsWith("You reach ", StringComparison.Ordinal) && l.Icon == "ui/follow");
        Assert.All(after.Log, l => Assert.DoesNotMatch("[a-z][A-Z]", l.Text));
        Assert.All(after.Map.Surface, s => Assert.NotEqual('.', after.Map.Known[s.I]));
        Assert.Equal(after.Map.Surface.OrderBy(s => s.I).Select(s => s.I), after.Map.Surface.Select(s => s.I));
    }

    // ---- the ground on the map: in sight as it is, out of sight as last seen ----

    static void Snow(World w, TileId t, double depthM) =>
        Ground.CatchUp(w, t).Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = depthM, CompactionPct = 0.3, FrozenFractionPct = 1 });

    static TileId InSightOfPlayer(World w)
    {
        var p = w.Agent(AgentId.Player);
        return Sight.InView(w, p).First(t => t != p.Tile && Map.Of(w).TerrainAt(t).IsPassable() && Map.Of(w).SiteAt(t) == null);
    }

    static int Index(World w, TileId t) => t.Y * Map.Of(w).Width + t.X;

    /// Regression: a tile seen under snow, then out of sight while the snow went, still shows that
    /// snow after a save and load (the page keeps nothing across a load), not the bare ground there now.
    [Fact]
    public void Ground_out_of_sight_is_as_last_seen_after_a_save_and_load_not_as_it_is_now()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var p = w.Agent(AgentId.Player);
        var t = InSightOfPlayer(w);
        Sim.Step(w, new Wait(AgentId.Player), Knowledge.GlanceMinutes); // ground is kept once it has lived any time
        Snow(w, t, 0.4);
        Sim.Step(w, new Wait(AgentId.Player), Knowledge.GlanceMinutes); // the next look sees it
        double seenCm = Math.Round(Ground.SurfaceNow(w, t).SnowDepthM * 100, 1);
        Assert.True(seenCm > 30, $"{seenCm} cm");

        var m = Map.Of(w);
        p.Tile = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(f => m.TerrainAt(f).IsPassable() && m.SiteAt(f) == null && Math.Abs(f.X - t.X) + Math.Abs(f.Y - t.Y) > 2 * Sight.MaxTiles);
        p.Crossing = null;
        Sim.Step(w, new Wait(AgentId.Player), 1);
        Ground.CatchUp(w, t).Layers.RemoveAll(l => l.Material == SurfaceMaterial.Snow); // gone while nobody looked

        var loaded = new GameHost();
        var v = Parse(loaded.Load(h.Save()));
        Assert.DoesNotContain(Index(w, t), v.Map.Visible);
        Assert.Equal(0, Ground.SurfaceNow(loaded.World, t).SnowDepthM);
        var remembered = v.Map.Surface.Single(s => s.I == Index(w, t));
        Assert.Equal(seenCm, remembered.SnowCm);
    }

    /// The winter scenario opens in mid-January on snow.
    [Fact]
    public void The_winter_scenario_opens_on_snow()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var v = Parse(Scenarios.Apply(h, "winter"));
        Assert.Equal("Winter", v.Clock.Season);
        var inSight = v.Map.Visible.ToHashSet();
        Assert.True(v.Map.Surface.Count(s => inSight.Contains(s.I) && s.SnowCm >= 15) > inSight.Count / 2,
            $"{v.Map.Surface.Count(s => inSight.Contains(s.I) && s.SnowCm >= 15)} of {inSight.Count} in sight under full snow");
    }

    /// A tile in sight shows its ground as it is now, not as the last look left it.
    [Fact]
    public void Ground_in_sight_is_the_ground_as_it_is_now()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var t = InSightOfPlayer(w);
        Sim.Step(w, new Wait(AgentId.Player), Knowledge.GlanceMinutes);
        var remembered = Knowledge.Of(w.Agent(AgentId.Player)).Explored!.At(t)!.Value;
        Assert.Equal(w.Minute, remembered.SeenMinute);
        Snow(w, t, 0.4); // after the look: memory says otherwise
        Assert.True(remembered.Surface.SnowDepthM < 0.1);

        var v = Parse(h.View());
        Assert.Contains(Index(w, t), v.Map.Visible);
        Assert.Equal(Math.Round(Ground.SurfaceNow(w, t).SnowDepthM * 100, 1), v.Map.Surface.Single(s => s.I == Index(w, t)).SnowCm);
        int width = Map.Of(w).Width;
        foreach (var i in v.Map.Visible)
        {
            var g = Ground.SurfaceNow(w, new TileId(i % width, i / width));
            var s = v.Map.Surface.SingleOrDefault(x => x.I == i);
            Assert.Equal(Math.Round(g.SnowDepthM * 100, 1), s?.SnowCm ?? 0);
            Assert.Equal(Math.Round(g.WaterDepthM * 100, 1), s?.WaterCm ?? 0);
        }
    }

    /// The map says which lakes are ice, from the Sim: open in July, frozen in January, and out of
    /// sight as they were when last seen.
    [Fact]
    public void The_map_marks_a_lake_open_in_July_and_frozen_in_January_as_last_seen()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var m = Map.Of(w);
        var p = w.Agent(AgentId.Player);
        var all = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y))).ToList();
        bool Land(TileId t) => m.InBounds(t) && m.TerrainAt(t) is not (Terrain.Water or Terrain.Mountain or Terrain.DeepWater) && m.CaveAt(t) == Cave.None;
        TileId? Shore(TileId t) => Enum.GetValues<Direction>().Select(d => Map.Step(t, d)).Where(Land).Cast<TileId?>().FirstOrDefault();
        var lake = all.First(t => m.TerrainAt(t) == Terrain.Water && m.SiteAt(t) == null && m.CaveAt(t) == Cave.None && Shore(t) != null);
        var shore = Shore(lake)!.Value;
        var far = all.First(f => m.TerrainAt(f).IsPassable() && m.SiteAt(f) == null && Math.Abs(f.X - lake.X) + Math.Abs(f.Y - lake.Y) > 2 * Sight.MaxTiles);
        void Look(TileId at)
        {
            p.Tile = at;
            p.Crossing = null;
            Sim.Step(w, new Wait(AgentId.Player), Knowledge.GlanceMinutes);
        }

        Look(shore);
        var july = Parse(h.View());
        Assert.Contains(Index(w, lake), july.Map.Visible);
        Assert.DoesNotContain(Index(w, lake), july.Map.FrozenWater);

        Look(far);
        w.Minute = 198 * 1440L; // 15 January, as the winter scenario
        Sim.Step(w, new Wait(AgentId.Player), 1);
        var unseen = Parse(h.View());
        Assert.DoesNotContain(Index(w, lake), unseen.Map.Visible);
        Assert.DoesNotContain(Index(w, lake), unseen.Map.FrozenWater); // remembered open, as it was in July

        Look(shore);
        var january = Parse(h.View());
        Assert.Contains(Index(w, lake), january.Map.FrozenWater);
        Assert.All(january.Map.FrozenWater, i => Assert.Contains(january.Map.Known[i], "WD"));
    }
}
