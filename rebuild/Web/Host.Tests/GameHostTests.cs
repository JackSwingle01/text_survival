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
        v.Things.SelectMany(t => t.Menu).Concat(v.Walk).Concat(v.Tile).Concat(v.Make);

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
        Assert.Equal(refused.Refused, after.Job.Reason);
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
        Assert.Equal("crossed", Parse(Scenarios.Apply(won, "won")).Outcome);
    }

    [Fact]
    public void A_calm_start_shows_no_alerts()
    {
        var v = Parse(new GameHost().NewGame(Seed));
        Assert.Empty(v.Alerts);
    }
}
