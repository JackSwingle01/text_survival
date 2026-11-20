using text_survival.Actions;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Tests.Support;

namespace text_survival.Tests.Actions;

public class EventChoiceTests
{
    /// <summary>
    /// An event stops play to ask for a decision. One with a single choice is a notice
    /// wearing a button: send it to the log or ctx.Notices instead.
    /// </summary>
    [Fact]
    public void EveryRegisteredEventOffersAChoice()
    {
        var weather = new Weather(50, GameContext.StartTime);
        var map = new GameMap(4, 1) { Weather = weather };
        for (int x = 0; x < 4; x++)
            map.SetLocation(x, 0, new Location($"Ground {x}", "", weather, 0));
        map.CurrentPosition = new GridPosition(0, 0);
        var home = map.GetLocationAt(0, 0)!;
        var player = new Player { CurrentLocation = home, Map = map };
        var ctx = new GameContext(player, home, weather) { Map = map, Ui = new ScriptedUi() };

        var singleChoice = GameEventRegistry.AllEventFactories
            .Select(f => f(ctx))
            .Where(e => e.BaseWeight > 0) // weight 0 is a factory's "not applicable here"
            .Where(e => e.AuthoredChoices.Count < 2)
            .Select(e => e.Name)
            .ToList();

        Assert.Empty(singleChoice);
    }
}
