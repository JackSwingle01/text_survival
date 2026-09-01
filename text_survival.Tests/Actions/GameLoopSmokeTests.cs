using text_survival.Actions;
using text_survival.Environments.Grid;
using text_survival.Tests.Support;
using text_survival.UI;

namespace text_survival.Tests.Actions;

/// <summary>
/// The whole action loop, run against a scripted UI with no window and no scheduler.
/// This is the only test that exercises GameRunner end to end, so it stays cheap:
/// one world, a handful of actions.
/// </summary>
public class GameLoopSmokeTests
{
    [Fact]
    public async Task RunAsync_WaitsTravelsAndQuits()
    {
        var ctx = GameContext.CreateNewGame();
        var start = ctx.Map!.CurrentPosition;
        var destination = FirstReachableNeighbour(ctx, start);

        var ui = new ScriptedUi { AutoResolveEvents = true };
        ctx.Ui = ui;

        ui.PlayerActions.Enqueue(new PlayerAction.Camp(CampAction.Wait));
        ui.PlayerActions.Enqueue(new PlayerAction.Travel(destination.X, destination.Y));
        ui.PlayerActions.Enqueue(new PlayerAction.Camp(CampAction.Wait));
        ui.PlayerActions.Enqueue(new PlayerAction.Quit());

        // Travel may run into hazardous terrain or be interrupted by an event; answer
        // both so a random world cannot leave a prompt unanswered.
        ui.Choices.Enqueue("quick");
        ui.Confirmations.Enqueue(true);

        var startTime = ctx.GameTime;

        bool restart = await new GameRunner(ctx).RunAsync();

        Assert.False(restart);
        Assert.True(ctx.GameTime > startTime, "Game time should have advanced.");
        Assert.True(ui.FramesRequested > 0, "Timed activity should have driven frames.");
        Assert.Equal(destination, ctx.Map.CurrentPosition);
        Assert.Empty(ui.PlayerActions);
    }

    private static GridPosition FirstReachableNeighbour(GameContext ctx, GridPosition from)
    {
        var map = ctx.Map!;
        var season = ctx.Weather.CurrentSeason;

        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var target = new GridPosition(from.X + dx, from.Y + dy);
            if (!map.CanMoveTo(target.X, target.Y)) continue;
            if (map.IsEdgeBlocked(from, target, season)) continue;

            return target;
        }

        throw new InvalidOperationException("Camp has no reachable neighbour to travel to.");
    }
}
