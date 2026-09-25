using text_survival.Actions;
using text_survival.Desktop.UI;
using text_survival.Environments.Features;

namespace text_survival.Tests.Desktop.UI;

/// <summary>
/// A picked-clean tile keeps its Forage button, disabled with a reason. It used to vanish,
/// so a tile someone else stripped looked like a tile that never had forage at all.
/// </summary>
public class DepletedForageActionTests
{
    [Fact]
    public void DepletedForage_IsShownDisabledWithAReason()
    {
        var ctx = GameContext.CreateNewGame();
        var location = ctx.CurrentLocation;
        foreach (var existing in location.Features.OfType<ForageFeature>().ToList())
            location.RemoveFeature(existing);
        var forage = new ForageFeature(1.0);
        forage.Deplete(10);
        location.AddFeature(forage);

        var action = Assert.Single(HudActions.Build(ctx), a => a.Id == "work:forage");

        Assert.False(action.Enabled);
        Assert.NotNull(action.DisabledReason);
    }
}
