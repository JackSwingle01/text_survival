using Raylib_cs;
using text_survival.Desktop.Input;
using text_survival.Desktop.UI;

namespace text_survival.Tests.Desktop.UI;

public class UiFoundationTests
{
    [Fact]
    public void OpeningAndReturningToDialogRequireReleaseBeforeInput()
    {
        var gate = new InputGate();
        object parent = new(), child = new();
        Assert.False(gate.Enter(parent, false));
        Assert.True(gate.Enter(parent, false));
        Assert.True(gate.Enter(parent, true));
        Assert.False(gate.Enter(child, true));
        Assert.False(gate.Enter(child, true));
        Assert.True(gate.Enter(child, false));
        Assert.False(gate.Enter(parent, true));
        Assert.False(gate.Enter(parent, true));
        Assert.True(gate.Enter(parent, false));
    }

    [Theory]
    [InlineData(InputContext.Exploration, true, false, false, false, true)]
    [InlineData(InputContext.Exploration, false, false, false, false, false)]
    [InlineData(InputContext.Exploration, true, true, false, false, false)]
    [InlineData(InputContext.Exploration, true, false, true, false, false)]
    [InlineData(InputContext.Exploration, true, false, false, true, false)]
    [InlineData(InputContext.Dialog, true, false, false, false, false)]
    [InlineData(InputContext.Combat, true, false, false, false, false)]
    [InlineData(InputContext.TextEntry, true, false, false, false, false)]
    [InlineData(InputContext.Popup, true, false, false, false, false)]
    public void OnlyUncapturedExplorationReceivesGameCommands(InputContext context, bool ready,
        bool focused, bool text, bool popup, bool expected)
        => Assert.Equal(expected, UiInput.AllowsGameCommands(context, ready, focused, text, popup));

    [Fact]
    public void ModifiedLettersDoNotBecomeExplorationCommands()
        => Assert.False(UiInput.AllowsGameCommands(InputContext.Exploration, true, false, false, false, modifier: true));

    [Fact]
    public void DisabledAndBackgroundControlsCannotActivateFromEitherInputRoute()
    {
        var enabled = new UiControl("start", "Start", "confirm", KeyboardKey.Enter);
        var disabled = enabled with { DisabledReason = "Missing materials" };
        Assert.True(enabled.CanActivate(true, true));
        Assert.False(enabled.CanActivate(false, true));
        Assert.False(enabled.CanActivate(true, false));
        Assert.False(disabled.CanActivate(true, true));
        Assert.Equal("Start [Enter]", enabled.DisplayLabel);
    }

    [Fact]
    public void SettingsRoundTripAndRecoverFromMalformedOrUnknownValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ui-settings-test-" + Guid.NewGuid());
        string path = Path.Combine(directory, "settings.json");
        try
        {
            Assert.Equal(UiScale.Comfortable, UiSettings.Load(path).Scale);
            new UiSettings(UiScale.Large).Save(path);
            Assert.Equal(UiScale.Large, UiSettings.Load(path).Scale);
            new UiSettings(UiScale.Compact).Save(path);
            Assert.Equal(UiScale.Compact, UiSettings.Load(path).Scale);
            File.WriteAllText(path, "{broken");
            Assert.Equal(UiScale.Comfortable, UiSettings.Load(path).Scale);
            File.WriteAllText(path, "{\"Scale\":99}");
            Assert.Equal(UiScale.Comfortable, UiSettings.Load(path).Scale);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
