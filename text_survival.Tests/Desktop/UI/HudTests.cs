using System.Numerics;
using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Desktop.Input;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.UI;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.UI;

namespace text_survival.Tests.Desktop.UI;

public class HudTests
{
    [Theory]
    [InlineData(1280, 720, 16.25f, false)]
    [InlineData(1280, 720, 19.5f, true)]
    [InlineData(1600, 900, 16.25f, true)]
    [InlineData(1920, 1080, 19.5f, false)]
    public void RegionsFitWithoutOverlapAndCameraPickingSurvivesLayoutChanges(int width, int height, float font, bool expanded)
    {
        var layout = HudLayout.Calculate(width, height, font, expanded);
        var rects = new[] { layout.Top, layout.Survivor, layout.Map, layout.Inspector, layout.Events };
        foreach (var rect in rects)
        {
            Assert.True(rect.Width > 0 && rect.Height > 0);
            Assert.InRange(rect.X, 0, width - rect.Width);
            Assert.InRange(rect.Y, 0, height - rect.Height);
        }
        for (int i = 0; i < rects.Length; i++)
        for (int j = i + 1; j < rects.Length; j++)
            Assert.False(Overlaps(rects[i], rects[j]));
        var camera = new Camera(12, 12);
        camera.Pan(new Vector2(.3f, .2f), 40, 40, true);
        foreach (bool history in new[] { false, true, false })
        {
            var viewport = HudLayout.Calculate(width, height, font, history).Map;
            camera.ConfigureForViewport(viewport.X, viewport.Y, viewport.Width, viewport.Height);
            Assert.True(camera.GridWidth <= viewport.Width && camera.GridHeight <= viewport.Height);
            Assert.Equal((12, 12), camera.ScreenToWorld(camera.GetTileCenter(12, 12)));
            Assert.False(camera.IsFollowingPlayer);
            Assert.Null(camera.ScreenToWorld(layout.Survivor.Position));
        }
    }

    private static bool Overlaps(HudRect a, HudRect b) => a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static GameContext Context()
    {
        var player = new Player();
        var weather = new Weather(-10, GameContext.StartTime);
        var camp = new Location("Camp", "camp", weather);
        var map = new GameMap(3, 1) { Weather = weather };
        for (int x = 0; x < 3; x++)
        {
            var location = x == 0 ? camp : new Location($"Field {x}", "field", weather);
            map.SetLocation(x, 0, location);
            location.Visibility = TileVisibility.Visible;
        }
        map.CurrentPosition = new(0, 0);
        player.CurrentLocation = camp;
        player.Map = map;
        return new GameContext(player, camp, weather) { Map = map };
    }

    [Fact]
    public void SelectionPersistsWithoutMovementAndResetsAfterTravel()
    {
        var ctx = Context();
        var state = new HudState();
        state.Update(ctx, 0);
        state.Select(ctx, 1, 0);
        state.Update(ctx, 1);
        Assert.Equal((1, 0), state.SelectedTile);
        state.Select(ctx, -1, 0);
        Assert.Equal((1, 0), state.SelectedTile);
        ctx.Map!.CurrentPosition = new(1, 0);
        state.Update(ctx, 0);
        Assert.Null(state.SelectedTile);
    }

    [Fact]
    public void SelectingCurrentLocationOrChangingRunClearsDestination()
    {
        var ctx = Context();
        var state = new HudState();
        state.Update(ctx, 0);
        state.Select(ctx, 1, 0);
        state.Select(ctx, 0, 0);
        Assert.Null(state.SelectedTile);
        state.Select(ctx, 1, 0);
        state.Update(Context(), 0);
        Assert.Null(state.SelectedTile);
    }

    [Fact]
    public void TravelRechecksBlockedEdgesAndMatchesAuthoritativePreview()
    {
        var ctx = Context();
        var target = ctx.Map!.GetLocationAt(1, 0)!;
        var expected = TravelProcessor.PreviewCrossing(ctx.CurrentLocation, target, ctx.player, ctx.Weather, ctx.Inventory, ctx.Map);
        var inspection = TravelInspection.Build(ctx, (1, 0));
        Assert.Null(inspection.Reason);
        Assert.Contains($"{expected.QuickMinutes} min", Assert.Single(inspection.Actions).Label);
        Assert.Equal(new PlayerAction.Travel(1, 0), inspection.Actions[0].Payload);
        ctx.Map.AddEdge(new(0, 0), new(1, 0), new TileEdge { Impassable = true });
        Assert.Empty(TravelInspection.Build(ctx, (1, 0)).Actions);
    }

    [Fact]
    public void HazardousTravelExposesBothPacesAndDistantTilesNeverOfferTravel()
    {
        var ctx = Context();
        ctx.Map!.GetLocationAt(1, 0)!.TerrainHazardLevel = .9;
        var actions = TravelInspection.Build(ctx, (1, 0)).Actions;
        Assert.Equal(2, actions.Count);
        Assert.Contains(actions, a => a.Payload is PlayerAction.Travel { HazardMode: "careful" });
        Assert.Contains(actions, a => a.Payload is PlayerAction.Travel { HazardMode: "quick" });
        Assert.Empty(TravelInspection.Build(ctx, (2, 0)).Actions);
        Assert.Empty(TravelInspection.Build(ctx, (-1, 0)).Actions);
    }

    [Fact]
    public void WorkOptionsKeepDomainIdsAndEveryOptionIsRepresented()
    {
        var ctx = GameContext.CreateNewGame(seed: 1234);
        var actions = HudActions.Build(ctx);
        foreach (var option in ctx.CurrentLocation.GetWorkOptions(ctx))
            Assert.Contains(actions, a => a.Id == $"work:{option.Id}" && a.Payload is PlayerAction.Work);
        Assert.Equal(actions.Count, actions.Select(a => a.Id).Distinct().Count());
    }

    [Fact]
    public void UnavailableFireAndStorageHaveReasonsForBothButtonsAndShortcuts()
    {
        var actions = HudActions.Build(Context());
        foreach (var key in new[] { HotkeyAction.Fire, HotkeyAction.Storage })
        {
            var action = Assert.Single(actions, a => a.Shortcut == key);
            Assert.False(action.Enabled);
            Assert.False(string.IsNullOrWhiteSpace(action.DisabledReason));
        }
    }

    [Fact]
    public void ReadingHistoryWinsOverDelayedBottomPositionAndCountsNewEntries()
    {
        var reading = new LogFollowState();
        reading.Reset(10);
        reading.UpdateViewport(atBottom: true, userScrolling: true, incoming: false);
        Assert.False(reading.Following);
        reading.Observe(13);
        reading.UpdateViewport(atBottom: false, userScrolling: false, incoming: true);
        Assert.False(reading.Following);
        Assert.Equal(3, reading.Unread);
        reading.JumpToLatest();
        Assert.True(reading.Following);
        Assert.Equal(0, reading.Unread);
    }

    [Fact]
    public void LogRevisionKeepsAdvancingWhenRetainedHistoryIsFullAndNarrativeAppearsOnce()
    {
        var log = new NarrativeLog();
        for (int i = 0; i < 205; i++) log.Add($"Entry {i}");
        Assert.Equal(200, log.Entries.Count);
        Assert.Equal(205, log.Revision);
        Assert.Equal("Entry 5", log.Entries[0].Text);
        log.Add("Entry 204");
        Assert.Equal(205, log.Revision);
        var ctx = Context();
        GameDisplay.AddWarning(ctx, "Test warning");
        Assert.Single(ctx.Log.Entries, e => e.Text == "Test warning");
    }
}
