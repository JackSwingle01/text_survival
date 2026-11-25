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
        v.LocationFeatures.SelectMany(t => t.Menu).Concat(v.Walk).Concat(v.Tile).Concat(v.PackVerbs).Concat(v.Make);

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

    /// The fire's menu offers Put on for carried raw meat, then Take once it is cooked (spec 2026-11-24
    /// fire-and-rack-as-stations, "Verbs").
    [Fact]
    public void The_fire_menu_puts_meat_on_and_takes_it_cooked()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var me = w.Agent(AgentId.Player);
        if (Fire.PitAt(w, me.Tile) == null)
            w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = me.Tile, LastUpdatedMinute = w.Minute };
        var pit = w.Fires[Fire.PitAt(w, me.Tile)!.Value];
        pit.BurningKg[Resource.BirchFirewood] = 5;
        Items.AddPiece(Items.Pack(w, AgentId.Player), Resource.RawMeat, 0.4);
        static IReadOnlyList<VerbDto> FireMenu(ViewDto v) => v.LocationFeatures.Single(t => t.Kind == "fire").Menu;

        var putOn = FireMenu(Parse(h.View())).Single(x => x.Kind == "puton");
        Assert.Equal("Put on raw meat", putOn.Label);
        Run(h, putOn);
        var back = FireMenu(Parse(h.View())).Single(x => x.Kind == "takeback");
        Assert.Null(back.Refused);
        var wait = Find(Parse(h.View()), x => x.Kind == "wait", "wait");
        Run(h, wait);
        var take = FireMenu(Parse(h.View())).Single(x => x.Kind == "take");
        Run(h, take);
        Assert.Equal(0.4, Items.Have(Items.Pack(w, AgentId.Player), Resource.CookedMeat), 9);
    }

    /// The fire's menu sets snow melting, needing nothing carried, and Take collects the water (#186).
    [Fact]
    public void The_fire_menu_sets_snow_melting_and_takes_the_water()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var me = w.Agent(AgentId.Player);
        if (Fire.PitAt(w, me.Tile) == null)
            w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = me.Tile, LastUpdatedMinute = w.Minute };
        w.Fires[Fire.PitAt(w, me.Tile)!.Value].BurningKg[Resource.BirchFirewood] = 5;
        static IReadOnlyList<VerbDto> FireMenu(ViewDto v) => v.LocationFeatures.Single(t => t.Kind == "fire").Menu;
        double before = Items.Have(Items.Pack(w, AgentId.Player), Resource.Water);

        Run(h, FireMenu(Parse(h.View())).Single(x => x.Label == "Melt snow"));
        Assert.Null(FireMenu(Parse(h.View())).Single(x => x.Label == "Stop: melt snow").Refused);
        Run(h, Find(Parse(h.View()), x => x.Kind == "wait", "wait"));
        Run(h, FireMenu(Parse(h.View())).Single(x => x.Kind == "take"));
        Assert.True(Items.Have(Items.Pack(w, AgentId.Player), Resource.Water) >= before + 1 - 1e-9);
    }

    /// How it is going, as the page shows it (#185): the finish on the menu, the line in the log, the mark on the tile.
    [Fact]
    public void Meat_on_the_fire_shows_its_finish_then_the_log_says_it_is_done_and_the_tile_is_marked_working_then_ready()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var me = w.Agent(AgentId.Player);
        if (Fire.PitAt(w, me.Tile) == null)
            w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = me.Tile, LastUpdatedMinute = w.Minute };
        w.Fires[Fire.PitAt(w, me.Tile)!.Value].BurningKg[Resource.BirchFirewood] = 5;
        Items.AddPiece(Items.Pack(w, AgentId.Player), Resource.RawMeat, 0.4);
        static IReadOnlyList<VerbDto> FireMenu(ViewDto v) => v.LocationFeatures.Single(t => t.Kind == "fire").Menu;
        string? Mark(ViewDto v) => v.Map.Places.SingleOrDefault(p => (p.X, p.Y) == (me.Tile.X, me.Tile.Y))?.Station;

        var v = Run(h, FireMenu(Parse(h.View())).Single(x => x.Kind == "puton"));
        long setAt = v.Minute - 1;
        Assert.Equal(setAt + 15, FireMenu(v).Single(x => x.Kind == "takeback").Progress!.FinishMinute);
        Assert.Equal("working", Mark(v));

        v = Run(h, Find(v, x => x.Kind == "wait", "wait"));
        var done = Assert.Single(v.Log, l => l.Text == "1 cut of cooked meat is done at the fire.");
        Assert.Equal(setAt + 15, done.Minute);
        Assert.Equal("ready", Mark(v));

        v = Run(h, FireMenu(v).Single(x => x.Kind == "take"));
        Assert.Null(Mark(v));
    }

    /// The page offers the rack's build in the Make drawer and the rack's menu among the location features (#187).
    [Fact]
    public void The_page_builds_a_curing_rack_and_puts_meat_on_it()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var pack = Items.Pack(w, AgentId.Player);
        for (int i = 0; i < 4; i++)
        {
            Items.AddPiece(pack, Resource.BirchBranch, 2);
            Items.AddPiece(pack, Resource.Stick, 0.3);
        }
        Items.AddPiece(pack, Resource.Rope, 0.2);
        Items.AddPiece(pack, Resource.RawMeat, 0.5);
        var me = w.Agent(AgentId.Player);
        string? Mark(ViewDto v) => v.Map.Places.SingleOrDefault(p => (p.X, p.Y) == (me.Tile.X, me.Tile.Y))?.Station;

        var build = Parse(h.View()).Make.Single(x => x.Label == "Build a curing rack");
        Assert.Equal("build", build.Kind);
        Assert.Null(build.Refused);
        var v = Run(h, build);
        var rack = Assert.Single(v.LocationFeatures, t => t.Kind == "rack");
        Assert.Equal("curing rack", rack.Name);
        v = Run(h, rack.Menu.Single(x => x.Kind == "puton"));
        Assert.Equal(w.Minute + 24 * 60 - 1, v.LocationFeatures.Single(t => t.Kind == "rack").Menu.Single(x => x.Kind == "takeback").Progress!.FinishMinute);
        Assert.Equal("working", Mark(v));
    }

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

    /// The pack shows a discrete resource by its count and a continuous one by its amount (#142).
    [Fact]
    public void The_pack_counts_stones_and_weighs_plant_fibre()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var pack = Items.Pack(h.World, AgentId.Player);
        Items.AddPieces(pack, Resource.Stone, [0.2, 0.3, 0.4]);
        Items.Add(pack, Resource.PlantFiber, 0.12);
        Items.Add(pack, Resource.Tinder, 1.2);
        var shown = Parse(h.View()).Pack.Stacks;
        Assert.Equal("3 stones", shown.Single(s => s.Resource == "Stone").Amount);
        Assert.Equal("120 g plant fibre", shown.Single(s => s.Resource == "PlantFiber").Amount);
        Assert.Equal("1.4 kg tinder", shown.Single(s => s.Resource == "Tinder").Amount); // with the starting tinder
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

    /// The Phase 5 gate: the map is drawn from memory. Light a fire, step off camp, sleep while it dies:
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

    /// Waking beside a fire that died in the night, the map draws the fire you are looking at, not the
    /// one you fell asleep to: the map and the fire panel tell the same story, so leaving, you remember
    /// what you saw.
    [Fact]
    public void Waking_by_a_dead_fire_the_map_shows_it_as_the_fire_panel_does()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var v = Parse(Scenarios.Apply(h, "supplied"));
        for (int tries = 0; v.Fire is not { Burning: true }; tries++)
        {
            Assert.True(tries < 40, "the fire never caught");
            v = Run(h, Find(v, x => x.Kind == "fire" && x.Refused == null, "light a fire"));
            if (v.Fire is not { Burning: true }) v = Run(h, Find(v, x => x.Kind == "wait", "wait"));
        }
        v = Run(h, Find(v, x => x.Kind == "sleep", "sleep"), chunk: 60);
        Assert.False(v.Fire!.Burning, "the fire should have died in the night");
        var here = v.Map.Places.Single(p => (p.X, p.Y) == (v.Place.X, v.Place.Y));
        Assert.Equal(v.Fire.Phase, here.Fire);
    }

    /// Waking where you slept, what changed in the night is the morning's news, not a homecoming:
    /// whichever view carries it, its title is "Morning".
    [Fact]
    public void What_changed_in_the_night_reads_as_morning_on_waking()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var v = Parse(Scenarios.Apply(h, "supplied"));
        for (int tries = 0; v.Fire is not { Burning: true }; tries++)
        {
            Assert.True(tries < 40, "the fire never caught");
            v = Run(h, Find(v, x => x.Kind == "fire" && x.Refused == null, "light a fire"));
            if (v.Fire is not { Burning: true }) v = Run(h, Find(v, x => x.Kind == "wait", "wait"));
        }
        var receipts = new List<ReceiptDto>();
        var sleep = Parse(Act(h, Find(v, x => x.Kind == "sleep", "sleep").Id));
        for (int i = 0; sleep.Job is { Finished: false } && i < 10_000; i++)
        {
            if (sleep.Receipt != null) receipts.Add(sleep.Receipt);
            sleep = Parse(h.Act("""{"advance":60}"""));
        }
        if (sleep.Receipt != null) receipts.Add(sleep.Receipt);
        var after = Parse(Act(h, Find(sleep, x => x.Kind == "wait", "wait").Id));
        for (int i = 0; ; i++)
        {
            if (after.Receipt != null) receipts.Add(after.Receipt);
            if (after.Job is not { Finished: false } || i > 100) break;
            after = Parse(h.Act("""{"advance":10}"""));
        }
        var news = Assert.Single(receipts);
        Assert.Equal("Morning", news.Title);
    }

    [Fact]
    public void The_map_sends_only_explored_tiles_and_the_tiles_in_sight_are_explored()
    {
        var v = Parse(new GameHost().NewGame(Seed));
        Assert.Equal(v.Map.Width * v.Map.Height, v.Map.Known.Length);
        int known = v.Map.Known.Count(c => c != MapView.UnknownCode);
        Assert.InRange(known, 10, v.Map.Known.Length / 4);
        Assert.All(v.Map.Visible, i => Assert.NotEqual(MapView.UnknownCode, v.Map.Known[i]));
    }

    [Theory]
    [InlineData(Seed)]
    [InlineData(17)]
    public void The_map_sends_exactly_the_ravines_and_cliffs_beside_known_tiles(int seed)
    {
        var v = Parse(new GameHost().NewGame(seed));
        var m = Survival.Map.Generate(seed);
        bool Known(int x, int y) => v.Map.Known[y * v.Map.Width + x] != MapView.UnknownCode;
        var expected = m.Edges
            .Where(e => e.Value is Survival.EdgeType.Ravine or Survival.EdgeType.Cliff
                && (Known(e.Key.Item1.X, e.Key.Item1.Y) || Known(e.Key.Item2.X, e.Key.Item2.Y)))
            .Select(e => (e.Key.Item1.X, e.Key.Item1.Y, e.Key.Item2.X, e.Key.Item2.Y, e.Value == Survival.EdgeType.Ravine ? "ravine" : "cliff"))
            .Order().ToList();
        var sent = v.Map.Edges.Select(e => (e.Ax, e.Ay, e.Bx, e.By, e.Kind)).Order().ToList();
        Assert.Equal(expected, sent);
        Assert.All(v.Map.Edges, e => Assert.Equal(1, Math.Abs(e.Ax - e.Bx) + Math.Abs(e.Ay - e.By)));
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
        Assert.All(v.Walk, w => Assert.Equal(Sim.InAnEncounter, w.Refused));
        var advance = v.Combat.Actions.First(a => a.Label == "Advance");
        var after = Run(h, advance);
        Assert.Equal(v.Minute + 1, after.Minute);
    }

    /// The fight's "just now" is the fight's own lines, never the walk that came before it; and the
    /// round that ends the fight says how it ended, where the page shows a job's end.
    [Fact]
    public void A_fight_tells_its_own_rounds_and_its_last_round_says_how_it_ended()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        v = Run(h, v.Walk.First(w => w.Refused == null && !w.Careful));
        Assert.Contains(v.Log, l => l.Text.StartsWith("You reach", StringComparison.Ordinal));
        v = Parse(Scenarios.Apply(h, "fight"));
        Assert.Empty(v.Combat!.Lines);
        long from = v.Minute;
        var told = new List<LogDto>();
        for (int round = 0; v.Combat != null; round++)
        {
            Assert.True(round < 200, "the fight never ended");
            Assert.All(v.Combat.Lines, l => Assert.True(l.Minute > from, $"'{l.Text}' is from before the fight"));
            told.AddRange(v.Combat.Lines.Where(l => !told.Contains(l)));
            var acts = v.Combat.Actions;
            var act = acts.FirstOrDefault(a => a.Label == "Attack" && a.Refused == null)
                ?? acts.FirstOrDefault(a => a.Label == "Advance" && a.Refused == null) ?? acts.First(a => a.Label == "Wait");
            v = Run(h, act);
        }
        Assert.NotEmpty(told); // blows were struck, one way or the other
        Assert.All(told, l => Assert.DoesNotMatch("Someone|pack", l.Text)); // each blow names the one it hurt
        Assert.StartsWith("The fight is over", v.Job!.Reason);
    }

    /// A herd standing where the player is can be hunted from the page: its verb is a hunt, and
    /// taking it puts the player on the grid with it.
    [Fact]
    public void A_herd_on_the_tile_offers_a_hunt_that_opens_a_fight()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        Agents.SpawnGroup(h.World, Species.Caribou, h.World.Agent(AgentId.Player).Tile, 3);
        var hunt = Find(Parse(h.View()), x => x.Kind == "hunt" && x.Refused == null, "hunt");
        var v = Run(h, hunt);
        Assert.NotNull(v.Combat);
        Assert.Contains(v.Combat!.Units, u => u.Team == 1);
    }

    /// Out on your feet, foraging until you drop asleep where you stand.
    static (GameHost Host, ViewDto View) Exhausted()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        h.World.Agent(AgentId.Player).Body.EnergyMinutes = 0;
        return (h, Parse(h.View()));
    }

    /// Dropped asleep from exhaustion, the player has nothing to choose until they wake: the sleep is
    /// what is under way, run in the page's chunks like any job, and the page is never handed a view
    /// with nothing under way while every verb but waiting is refused. Awake again, the verbs are back.
    [Fact]
    public void Dropping_asleep_from_exhaustion_runs_the_sleep_out_and_gives_the_verbs_back()
    {
        var (h, v) = Exhausted();
        bool dropped = false;
        for (int tries = 0; !dropped; tries++)
        {
            Assert.True(tries < 30, "never dropped asleep at zero energy");
            v = Parse(Act(h, Find(v, x => x.Kind == "gather" && x.Refused == null, "forage").Id));
            for (int i = 0; v.Job is { Finished: false } && i < 10_000; i++)
            {
                if (Body.IsAsleep(h.World.Agent(AgentId.Player).Body)) dropped = true;
                v = Parse(h.Act("""{"advance":10}"""));
            }
            dropped |= v.Log.Any(l => l.Text == "You are asleep.");
            Assert.True(v.Job!.Finished);
            Assert.False(Body.IsAsleep(h.World.Agent(AgentId.Player).Body), "the page was handed a finished job while the player sleeps on");
        }
        Assert.Contains(v.Log, l => l.Text == "You are no longer asleep.");
        Assert.Contains(AllVerbs(v), x => x.Kind == "gather" && x.Refused == null);
    }

    /// The body's own sleep is not the player's to stop: a stop while asleep leaves it under way.
    [Fact]
    public void A_stop_while_dropped_asleep_leaves_the_sleep_under_way()
    {
        var (h, v) = Exhausted();
        bool Asleep() => Body.IsAsleep(h.World.Agent(AgentId.Player).Body);
        for (int tries = 0; !Asleep(); tries++)
        {
            Assert.True(tries < 30, "never dropped asleep at zero energy");
            v = Parse(Act(h, Find(v, x => x.Kind == "gather" && x.Refused == null, "forage").Id));
            for (int i = 0; v.Job is { Finished: false } && !Asleep() && i < 10_000; i++) v = Parse(h.Act("""{"advance":1}"""));
        }
        var stopped = Parse(h.Act("""{"stop":true}"""));
        Assert.True(Asleep());
        Assert.False(stopped.Job!.Finished);
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

    /// The daylight countdown is the light work goes by: while it shows light left, nothing is too dark
    /// to see by, and it runs out the minute work would be.
    [Fact]
    public void The_daylight_countdown_runs_out_when_work_goes_dark()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        var p = h.World.Agent(AgentId.Player);
        for (int i = 0; i < 48; i++)
        {
            bool dark = Work.TooDarkToWork(h.World, p);
            Assert.True(dark == (v.Clock.DaylightLeftMinutes == 0),
                $"at {v.Clock.MinuteOfDay / 60}:{v.Clock.MinuteOfDay % 60:00} the page shows {v.Clock.DaylightLeftMinutes} min of light, and work is {(dark ? "" : "not ")}too dark");
            v = Run(h, Find(v, x => x.Kind == "wait", "wait"), chunk: 30);
        }
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
        Assert.Contains(v.PackVerbs, x => x.Kind == "putdown");
        var go = v.Walk.First(w => w.Refused == null && !w.Careful);
        var after = Run(h, go, chunk: 3);
        Assert.Contains(after.Log, l => l.Text.StartsWith("You reach ", StringComparison.Ordinal) && l.Icon == "ui/follow");
        Assert.All(after.Log, l => Assert.DoesNotMatch("[a-z][A-Z]", l.Text));
        Assert.All(after.Map.Surface, s => Assert.NotEqual('.', after.Map.Known[s.I]));
        Assert.Equal(after.Map.Surface.OrderBy(s => s.I).Select(s => s.I), after.Map.Surface.Select(s => s.I));
    }

    // ---- the ground on the map: in sight as it is, out of sight as last seen ----

    /// Nothing stores ground per tile, so a scene's ground is set on the tile's whole class (Ground.SetClassState).
    static void EditGround(World w, TileId t, Action<GroundState> edit)
    {
        var g = Ground.At(w, t);
        edit(g);
        Ground.SetClassState(w, t, g);
    }

    static void Snow(World w, TileId t, double depthM) =>
        EditGround(w, t, g => g.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = depthM, CompactionPct = 0.3, FrozenFractionPct = 1 }));

    static TileId InSightOfPlayer(World w)
    {
        var p = w.Agent(AgentId.Player);
        return Sight.InView(w, p).First(t => t != p.Tile && Map.Of(w).TerrainAt(t).IsPassable() && Map.Of(w).SiteAt(t) == null);
    }

    static int Index(World w, TileId t) => t.Y * Map.Of(w).Width + t.X;

    /// A bedding ground: a tile in sight that a herd has trodden shows as trodden on the map and in the signs,
    /// and once out of sight the person still remembers it as trodden, after a save and load.
    [Fact]
    public void A_trodden_tile_shows_on_the_map_and_in_signs_and_is_remembered_out_of_sight()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var p = w.Agent(AgentId.Player);
        var t = InSightOfPlayer(w);
        var herd = Agents.SpawnGroup(w, Species.Caribou, t, 20);
        foreach (var id in herd) w.Agent(id).Doing = new Doing { Plain = new Rest(id), MinutesLeft = 6 * 60 };
        for (long end = w.Minute + 6 * 60; w.Minute < end;) Sim.Step(w, new Wait(AgentId.Player), (int)(end - w.Minute));
        foreach (var id in herd) Agents.Remove(w, id);
        Sim.Step(w, new Wait(AgentId.Player), Memories.GlanceMinutes);
        var state = w.Tiles[t];

        Assert.True(Parse(h.View()).Map.Surface.Single(s => s.I == Index(w, t)).TroddenLevel >= Tracks.TroddenTileAt);
        Assert.Contains(Signs.At(w, t), s => s.Kind == SignKind.Trodden);
        Assert.Contains(Report.Finds(w), f => f.Tile == t && f.Kind == "trodden");

        var m = Map.Of(w);
        p.Tile = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(f => m.TerrainAt(f).IsPassable() && m.SiteAt(f) == null && Math.Abs(f.X - t.X) + Math.Abs(f.Y - t.Y) > 2 * Sight.MaxTiles);
        p.Crossing = null;
        Sim.Step(w, new Wait(AgentId.Player), 1);
        state.Trodden = null; // gone while nobody looked

        var loaded = new GameHost();
        var v = Parse(loaded.Load(h.Save()));
        Assert.DoesNotContain(Index(w, t), v.Map.Visible);
        Assert.Equal(0, Tracks.TroddenAt(loaded.World, t));
        Assert.True(v.Map.Surface.Single(s => s.I == Index(w, t)).TroddenLevel >= Tracks.TroddenTileAt);
    }

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
        Sim.Step(w, new Wait(AgentId.Player), Memories.GlanceMinutes); // ground is kept once it has lived any time
        Snow(w, t, 0.4);
        Sim.Step(w, new Wait(AgentId.Player), Memories.GlanceMinutes); // the next look sees it
        double seenCm = Math.Round(Ground.Underfoot(w, t).SnowDepthM * 100, 1);
        Assert.True(seenCm > 30, $"{seenCm} cm");

        var m = Map.Of(w);
        p.Tile = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(f => m.TerrainAt(f).IsPassable() && m.SiteAt(f) == null && Math.Abs(f.X - t.X) + Math.Abs(f.Y - t.Y) > 2 * Sight.MaxTiles);
        p.Crossing = null;
        Sim.Step(w, new Wait(AgentId.Player), 1);
        EditGround(w, t, g => g.Layers.RemoveAll(l => l.Material == SurfaceMaterial.Snow)); // gone while nobody looked

        var loaded = new GameHost();
        var v = Parse(loaded.Load(h.Save()));
        Assert.DoesNotContain(Index(w, t), v.Map.Visible);
        Assert.Equal(0, Ground.Underfoot(loaded.World, t).SnowDepthM);
        var remembered = v.Map.Surface.Single(s => s.I == Index(w, t));
        Assert.Equal(seenCm, remembered.SnowCm);
    }

    // ---- routes and prints on the map ----

    static void Tread(World w, TileId a, TileId b, double trodden) => w.Trails[new EdgeId(a, b)] = new EdgeTrodden(trodden, w.Minute);

    static TileId NeighbourInSight(World w, TileId t)
    {
        var inSight = Sight.InView(w, w.Agent(AgentId.Player)).ToHashSet();
        return new[] { Direction.East, Direction.South, Direction.West, Direction.North }.Select(d => Map.Step(t, d))
            .First(n => inSight.Contains(n) && Map.Of(w).TerrainAt(n).IsPassable());
    }

    static (int, int, int, int, string) Trail(TileId a, TileId b, string tier)
    {
        var e = new EdgeId(a, b);
        return (e.A.X, e.A.Y, e.B.X, e.B.Y, tier);
    }

    static List<(int, int, int, int, string)> Sent(ViewDto v) => [.. v.Map.Trails.Select(t => (t.Ax, t.Ay, t.Bx, t.By, t.Tier))];

    /// A route in sight is drawn as it is, even worn on since the last look; out of sight, as last seen,
    /// after a save and load too. A route never seen is not drawn.
    [Fact]
    public void Routes_on_the_map_are_as_they_are_in_sight_and_as_last_seen_out_of_sight()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var p = w.Agent(AgentId.Player);
        var a = InSightOfPlayer(w);
        var b = NeighbourInSight(w, a);
        Tread(w, a, b, Tracks.PathAt + 0.05);
        Sim.Step(w, new Wait(AgentId.Player), Memories.GlanceMinutes);
        Tread(w, a, b, Tracks.TrailAt + 0.05); // after the look
        Assert.Contains(Trail(a, b, "trail"), Sent(Parse(h.View())));

        var m = Map.Of(w);
        p.Tile = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => new TileId(x, y)))
            .First(f => m.TerrainAt(f).IsPassable() && m.SiteAt(f) == null && Math.Abs(f.X - a.X) + Math.Abs(f.Y - a.Y) > 2 * Sight.MaxTiles);
        p.Crossing = null;
        Sim.Step(w, new Wait(AgentId.Player), 1);
        var far = Enumerable.Range(0, m.Width).Select(x => new TileId(x, m.Height - 1)).First(t => !Memories.Of(p).Explored!.Contains(t));
        Tread(w, far, Map.Step(far, Direction.North), Tracks.TrailAt + 0.05); // never seen

        var v = Parse(new GameHost().Load(h.Save()));
        Assert.DoesNotContain(Index(w, a), v.Map.Visible);
        Assert.Contains(Trail(a, b, "path"), Sent(v));
        Assert.DoesNotContain(v.Map.Trails, t => t.Ax == far.X && t.Ay == far.Y - 1);
        bool Known(int x, int y) => v.Map.Known[y * v.Map.Width + x] != MapView.UnknownCode;
        Assert.All(v.Map.Trails, t =>
        {
            Assert.True(Known(t.Ax, t.Ay) && Known(t.Bx, t.By), $"{t} has an end never seen");
            Assert.Equal(1, Math.Abs(t.Ax - t.Bx) + Math.Abs(t.Ay - t.By));
        });
        Assert.Equal(Sent(v).Order(), Sent(v).Distinct().Order());
    }

    /// Prints in sight carry what the page lays them out by: maker, heading, traffic, its pattern and freshness.
    [Fact]
    public void Prints_carry_their_heading_pattern_and_freshness()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var here = w.Agent(AgentId.Player).Tile;
        var west = Map.Step(here, Direction.West);
        for (int i = 0; i < 3; i++) Tracks.RecordMove(w, west, here, TrackMaker.Paw);
        var t = Parse(h.View()).Map.Tracks.Single(t => t.X == here.X && t.Y == here.Y && t.Maker == "paw");
        Assert.Equal("east", t.Heading);
        Assert.Equal("heading", t.Pattern);
        Assert.Equal(3, t.Traffic);
        Assert.InRange(t.FreshnessPct, 0.9, 1);
    }

    /// Prints are read only near the player and in sight: none from further off, wherever they lie.
    [Fact]
    public void Prints_beyond_reading_distance_are_not_sent()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var w = h.World;
        var here = w.Agent(AgentId.Player).Tile;
        var m = Map.Of(w);
        var far = Enumerable.Range(1, 20).Select(d => new TileId(here.X + MapView.TrackReadTiles + d, here.Y))
            .First(t => m.InBounds(t) && m.TerrainAt(t).IsPassable() && m.TerrainAt(Map.Step(t, Direction.East)).IsPassable());
        for (int i = 0; i < 5; i++) Tracks.RecordMove(w, Map.Step(far, Direction.East), far, TrackMaker.Hoof);
        var v = Parse(h.View());
        Assert.NotEmpty(Tracks.Read(w, far));
        Assert.All(v.Map.Tracks, t => Assert.True(Math.Max(Math.Abs(t.X - here.X), Math.Abs(t.Y - here.Y)) <= MapView.TrackReadTiles, $"prints at {t.X},{t.Y}"));
        Assert.All(v.Map.Tracks, t => Assert.Contains(t.Y * m.Width + t.X, v.Map.Visible));
    }

    /// The renderer (terrain-core.js) knows these codes and throws on any other: every value the Sim can
    /// send must be one of them.
    [Fact]
    public void Every_route_and_print_code_is_one_the_page_draws()
    {
        string[] tiers = ["trace", "path", "trail"], headings = ["north", "east", "south", "west"],
            patterns = ["heading", "northsouth", "eastwest", "mixed"], makers = ["human", "paw", "hoof", "bird"];
        Assert.All(Enum.GetValues<TrailTier>().Where(t => t != TrailTier.None), t => Assert.Contains(Words.Code(t), tiers));
        Assert.All(Enum.GetValues<Direction>(), d => Assert.Contains(Words.Code(d), headings));
        Assert.All(Enum.GetValues<TrafficPattern>(), p => Assert.Contains(Words.Code(p), patterns));
        Assert.All(Enum.GetValues<TrackMaker>(), m => Assert.Contains(Names.Of(m), makers));
    }

    /// The trails scenario (for screenshots) opens in winter beside a worn route and grazing ground,
    /// with prints of all three makers in sight.
    [Fact]
    public void The_trails_scenario_opens_beside_routes_and_prints()
    {
        var h = new GameHost();
        h.NewGame(Seed);
        var v = Parse(Scenarios.Apply(h, "trails"));
        Assert.Equal("Winter", v.Clock.Season);
        Assert.Equal(new[] { "path", "trace", "trail" }, v.Map.Trails.Select(t => t.Tier).Distinct().Order());
        Assert.True(v.Map.Trails.Count > 20, $"{v.Map.Trails.Count} routes");
        Assert.Equal(new[] { "hoof", "human", "paw" }, v.Map.Tracks.Select(t => t.Maker).Distinct().Order());
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
        Sim.Step(w, new Wait(AgentId.Player), Memories.GlanceMinutes);
        var remembered = Memories.Of(w.Agent(AgentId.Player)).Explored!.At(t)!.Value;
        Assert.Equal(w.Minute, remembered.SeenMinute);
        Snow(w, t, 0.4); // after the look: memory says otherwise
        Assert.True(remembered.Surface.SnowDepthM < 0.1);

        var v = Parse(h.View());
        Assert.Contains(Index(w, t), v.Map.Visible);
        Assert.Equal(Math.Round(Ground.Underfoot(w, t).SnowDepthM * 100, 1), v.Map.Surface.Single(s => s.I == Index(w, t)).SnowCm);
        int width = Map.Of(w).Width;
        foreach (var i in v.Map.Visible)
        {
            var g = Ground.Underfoot(w, new TileId(i % width, i / width));
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
            Sim.Step(w, new Wait(AgentId.Player), Memories.GlanceMinutes);
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

    // ---- the forage screen, and every timed job run in chunks ----

    static VerbDto ForageVerb(ViewDto v) => Find(v, x => x.Kind == "gather" && x.Durations != null && x.Label.StartsWith("Forage", StringComparison.Ordinal) && !x.Label.Contains(" for "), "forage");

    [Fact]
    public void The_camp_shows_its_forage_ground_with_a_verb_for_each_focus_its_clues_and_keep_walking()
    {
        var v = Parse(new GameHost().NewGame(Seed));
        var f = v.Forage!;
        Assert.Null(f.Quality); // not foraged here yet
        Assert.Equal("anything", f.Foci[^1].Focus);
        Assert.All(f.Foci, x => Assert.Contains(AllVerbs(v), verb => verb.Id == x.Verb));
        Assert.NotEmpty(f.Clues);
        Assert.All(f.Clues.Where(c => c.Focus != null), c => Assert.Contains(f.Foci, x => x.Focus == c.Focus));
        Assert.Contains(AllVerbs(v), verb => verb.Id == f.WalkVerb && verb.Kind == "walkabout");
        Assert.Equal([15, 30, 60], ForageVerb(v).Durations!);
    }

    [Fact]
    public void A_forage_runs_for_the_minutes_chosen_in_chunks_as_one_search_and_shows_its_finds()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        long start = v.Minute;
        v = Parse(h.Act($$"""{"verb":"{{ForageVerb(v).Id}}","minutes":60}"""));
        int views = 0;
        for (; v.Job is { Finished: false }; views++) v = Parse(h.Act("""{"advance":5}"""));
        Assert.True(views > 5, "a forage should come back as many views");
        Assert.Equal(start + 60, v.Minute);
        Assert.Single(v.Log, l => l.Icon == "ui/foraging" && l.Minute > start);
        Assert.NotNull(v.Job!.Finds);
        Assert.Equal(v.Job.Finds!.Sum(x => x.Kg), v.Job.FoundKg!.Value, 1);
        Assert.NotNull(v.Forage!.Quality); // foraged here now: the ground has its word
    }

    [Fact]
    public void A_forage_refuses_a_length_it_does_not_offer()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        Assert.Throws<ArgumentException>(() => h.Act($$"""{"verb":"{{ForageVerb(v).Id}}","minutes":45}"""));
    }

    [Fact]
    public void A_stop_ends_a_forage_at_the_minute_it_was_pressed_with_what_it_found()
    {
        var h = new GameHost();
        var v = Parse(h.NewGame(Seed));
        long start = v.Minute;
        Parse(h.Act($$"""{"verb":"{{ForageVerb(v).Id}}","minutes":60}"""));
        Parse(h.Act("""{"advance":7}"""));
        v = Parse(h.Act("""{"advance":6}"""));
        long pressed = v.Minute;
        v = Parse(h.Act("""{"stop":true}"""));
        Assert.Equal(pressed, v.Minute);
        Assert.Equal(13, pressed - start);
        Assert.True(v.Job!.Finished);
        Assert.Equal("stopped", v.Job.Outcome);
        Assert.Single(v.Log, l => l.Icon == "ui/foraging" && l.Minute > start);
        h.Save(); // nothing held over: the search has ended
    }
}
