using ImGuiNET;
using System.Numerics;
using text_survival.Desktop.Input;
using text_survival.Desktop.UI;
using text_survival.Tests.Companions;
using text_survival.UI;
using text_survival.Actions.Expeditions.WorkStrategies;

namespace text_survival.Tests.Desktop.UI;

[Collection("Native UI")]
public class RemainingKeyboardTests
{
    private static void Native(Action<Action<Action>, Action<ImGuiKey, Action>> test)
    {
        var previous = ImGui.GetCurrentContext(); var context = ImGui.CreateContext();
        bool ready = UiInput.Ready, focused = UiInput.HudFocused; var mode = UiInput.Context;
        try
        {
            var io = ImGui.GetIO(); unsafe { io.NativePtr->IniFilename = null; }
            io.DisplaySize = new Vector2(1280, 720); io.DeltaTime = 1f / 60;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            io.Fonts.AddFontDefault(); io.Fonts.GetTexDataAsRGBA32(out IntPtr _, out int _, out int _);
            UiStyle.Apply(new UiSettings()); UiInput.Ready = true; UiInput.HudFocused = false; UiInput.Context = InputContext.Dialog;
            void Frame(Action render) { ImGui.NewFrame(); render(); ImGui.Render(); }
            void Key(ImGuiKey key, Action render) { io.AddKeyEvent(key, true); Frame(render); Frame(render); io.AddKeyEvent(key, false); Frame(render); Frame(render); }
            test(Frame, Key);
        }
        finally { UiInput.Ready = ready; UiInput.HudFocused = focused; UiInput.Context = mode; ImGui.DestroyContext(context); ImGui.SetCurrentContext(previous); }
    }

    [Fact]
    public void EventNumbersRejectUnavailableChoiceAndUnreleasedInput()
    {
        Native((frame, key) =>
        {
            var overlay = new GameEventOverlay(); overlay.ShowEvent(new("Event", "Choose", [new("no", "Unavailable", "Need supplies", false), new("yes", "Proceed", "Costs time", true)]));
            string? result = null; void Render() { result ??= overlay.Render(0); }
            frame(Render); frame(Render); key(ImGuiKey._1, Render); Assert.Null(result);
            UiInput.Ready = false; key(ImGuiKey._2, Render); Assert.Null(result);
            UiInput.Ready = true; key(ImGuiKey._2, Render); Assert.Equal("yes", result);
        });
    }

    [Fact]
    public void CompanionSelectionAndSupplyActionsUseKeyboardAndKeepSelection()
    {
        Native((frame, key) =>
        {
            var world = new CompanionWorld(); world.AddNpc("One", 0, 0); var second = world.AddNpc("Two", 0, 0);
            world.Game.Inventory.Add(Resource.Stone, .5);
            var overlay = new NPCOverlay { IsOpen = true }; void Render() => overlay.Render(world.Game, 0);
            frame(Render); frame(Render); key(ImGuiKey.F2, Render); key(ImGuiKey.Tab, Render); key(ImGuiKey.Enter, Render);
            Assert.Same(second, overlay.SelectedNpc);
            key(ImGuiKey.F4, Render); key(ImGuiKey.Enter, Render); key(ImGuiKey.Tab, Render); key(ImGuiKey.Enter, Render);
            Assert.Equal(.5, second.Inventory.Weight(Resource.Stone), 5);
            Assert.Equal(0, world.Game.Inventory.Weight(Resource.Stone));
            key(ImGuiKey.Enter, Render); Assert.Equal(.5, second.Inventory.Weight(Resource.Stone), 5);
            second.Relationships.AddMemory(text_survival.Actors.MemoryType.SavedMe, world.Game.player);
            key(ImGuiKey.Tab, Render); key(ImGuiKey.Enter, Render);
            Assert.Equal(.5, world.Game.Inventory.Weight(Resource.Stone), 5);
            key(ImGuiKey.F3, Render); key(ImGuiKey.Enter, Render); Assert.Same(world.Game.player, second.Following?.Target);
            key(ImGuiKey.Tab, Render); key(ImGuiKey.Enter, Render); // Memories toggle.
            Assert.Same(second, overlay.SelectedNpc); key(ImGuiKey.Escape, Render); Assert.False(overlay.IsOpen);
        });
    }

    [Fact]
    public void CompanionDismissalAndInventoryHaveDirectFocusRoutes()
    {
        Native((frame, key) =>
        {
            var world = new CompanionWorld(); var npc = world.AddNpc("Companion", 0, 0); CompanionWorld.Follow(npc, world.Game.player);
            var overlay = new NPCOverlay { IsOpen = true }; void Render() => overlay.Render(world.Game, 0);
            frame(Render); frame(Render); key(ImGuiKey.F3, Render); key(ImGuiKey.Enter, Render); Assert.Null(npc.Following);
            key(ImGuiKey.F5, Render); key(ImGuiKey.Enter, Render); Assert.True(overlay.IsOpen);
        });
    }

    [Fact]
    public void DiscoveryCategoriesAndReadOnlyScrollingNeedNoMouse()
    {
        Native((frame, key) =>
        {
            var overlay = new DiscoveryLogOverlay { IsOpen = true };
            overlay.SetData(new([new("Land", "0 found", [], 24), new("Beasts", "80 found", Enumerable.Range(0, 80).Select(i => $"Animal {i}").ToList(), 7)]));
            void Render() => overlay.Render(0);
            frame(Render); frame(Render); key(ImGuiKey.F2, Render); key(ImGuiKey.Tab, Render); key(ImGuiKey.Enter, Render);
            Assert.Equal(1, overlay.SelectedCategory);
            key(ImGuiKey.F3, Render); key(ImGuiKey.PageDown, Render);
            Assert.True(overlay.IsOpen); key(ImGuiKey.Escape, Render); Assert.False(overlay.IsOpen);
            var capture = new TextFrame(); GameGui.Capture = capture;
            try { overlay.IsOpen = true; overlay.Render(0); }
            finally { GameGui.Capture = null; }
            Assert.Contains("7 undiscovered", capture.Lines); Assert.DoesNotContain("???", capture.Lines);
        });
    }

    [Fact]
    public void KeyboardHistoryReadingStaysPausedUntilExplicitLatest()
    {
        Native((frame, key) =>
        {
            var world = new CompanionWorld(); var panel = new EventLogPanel(); var state = new HudState();
            UiInput.Context = InputContext.Exploration; UiInput.HudFocused = true;
            for (int i = 0; i < 50; i++) world.Game.Log.Add($"Event {i}", LogLevel.Warning, "12:00");
            bool focus = true;
            void Render() { if (focus) ImGui.SetNextWindowFocus(); panel.Render(world.Game, new(0, 0, 500, 300), state); focus = false; }
            frame(Render); frame(Render); key(ImGuiKey.F2, Render); Assert.True(state.HistoryExpanded);
            key(ImGuiKey.F3, Render); Assert.False(panel.Following); key(ImGuiKey.PageUp, Render);
            world.Game.Log.Add("New event", LogLevel.Success, "12:01"); frame(Render); frame(Render);
            Assert.False(panel.Following); Assert.Equal(1, panel.Unread);
            key(ImGuiKey.End, Render); frame(Render); Assert.False(panel.Following);
            key(ImGuiKey.F4, Render); Assert.True(panel.Following); Assert.Equal(0, panel.Unread);
        });
    }

    [Fact]
    public void EveryConcreteWorkStrategyHasExplicitIconAndAsset()
    {
        var types = typeof(IWorkStrategy).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(IWorkStrategy).IsAssignableFrom(t)).ToHashSet();
        Assert.Equal(types.OrderBy(t => t.Name), HudActions.WorkIcons.Keys.OrderBy(t => t.Name));
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        foreach (var icon in HudActions.WorkIcons.Values.Concat(Enum.GetValues<Resource>().Select(UiIcons.ForResource)).Concat(Enumerable.Range(0, 5).Select(DiscoveryLogOverlay.CategoryIcon))) Assert.True(File.Exists(Path.Combine(root, "assets/icons/ui", icon + ".png")), icon);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Desktop/UI"), "*.cs"))
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), "UiIcons\\.(?:Button|Label|LabelColored|LabelWrapped|Selectable)\\(\"([a-z-]+)\""))
                Assert.True(File.Exists(Path.Combine(root, "assets/icons/ui", match.Groups[1].Value + ".png")), match.Value);
    }
    [Fact]
    public void IntegerFieldCommitsWithEnterWithoutUnsupportedImGuiFlags()
    {
        Native((frame, key) =>
        {
            int value = 12; bool focus = true, submitted = false;
            void Render() { ImGui.Begin("Amount"); UiNavigation.Initial(ref focus); submitted |= UiControls.InputIntSubmitted("Quantity", ref value); ImGui.End(); }
            frame(Render); frame(Render); Assert.False(submitted);
            key(ImGuiKey.Enter, Render); Assert.True(submitted); Assert.Equal(12, value);
        });
    }

}
