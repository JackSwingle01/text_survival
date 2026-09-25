using ImGuiNET;
using System.Numerics;
using text_survival.Desktop.Input;
using text_survival.Desktop.UI;
using text_survival.Actions;
using text_survival.Items;
using text_survival.Environments.Features;
using text_survival.UI;

namespace text_survival.Tests.Desktop.UI;

[Collection("Native UI")]
public class EverydayKeyboardTests
{
    private static void Native(Action<Action<Action>, Action<ImGuiKey, Action>> test)
    {
        var previous = ImGui.GetCurrentContext();
        var context = ImGui.CreateContext();
        bool ready = UiInput.Ready;
        try
        {
            var io = ImGui.GetIO();
            unsafe { io.NativePtr->IniFilename = null; }
            io.DisplaySize = new Vector2(1280, 720);
            io.DeltaTime = 1f / 60;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            io.Fonts.AddFontDefault();
            io.Fonts.GetTexDataAsRGBA32(out IntPtr _, out int _, out int _);
            UiStyle.Apply(new UiSettings());
            UiInput.Ready = true;
            void Frame(Action render) { ImGui.NewFrame(); render(); ImGui.Render(); }
            void Key(ImGuiKey key, Action render)
            {
                io.AddKeyEvent(key, true); Frame(render); Frame(render);
                io.AddKeyEvent(key, false); Frame(render); Frame(render);
            }
            test(Frame, Key);
        }
        finally { UiInput.Ready = ready; ImGui.DestroyContext(context); ImGui.SetCurrentContext(previous); }
    }

    [Fact]
    public void ConfirmationStartsOnNoAndRequiresTraversalForYes()
    {
        Native((frame, key) =>
        {
            var dialog = new ConfirmationDialog("Set up camp?");
            bool? answer = null;
            void Render() { answer ??= dialog.Render(); }
            frame(Render); frame(Render);
            key(ImGuiKey.Enter, Render);
            Assert.Equal(false, answer);
            answer = null;
            dialog = new ConfirmationDialog("Set up camp?");
            frame(Render); frame(Render);
            key(ImGuiKey.Tab, Render); key(ImGuiKey.Enter, Render);
            Assert.Equal(true, answer);
        });
    }

    [Fact]
    public void TransferKeyboardSelectsBeforeMovingAndCanRepeat()
    {
        Native((frame, key) =>
        {
            var ctx = GameContext.CreateNewGame(42);
            var storage = new Inventory();
            storage.Add(Resource.Stone, 1);
            storage.Add(Resource.Stone, 1);
            var overlay = new TransferOverlay(); overlay.Open(storage, "Cache");
            TransferResult? move = null;
            void Render() { var next = overlay.Render(ctx, 0); move ??= next; }
            frame(Render); frame(Render);
            key(ImGuiKey.F3, Render); key(ImGuiKey.Enter, Render);
            Assert.Null(move);
            Assert.Equal(2, storage.Count(Resource.Stone));
            key(ImGuiKey.F4, Render); key(ImGuiKey.Tab, Render); key(ImGuiKey.Enter, Render);
            Assert.NotNull(move);
            Assert.False(move.FromPlayer);
            Assert.Equal(Resource.Stone, move.Resource);
            move.Apply(ctx.Inventory, storage);
            Assert.Equal(1, storage.Count(Resource.Stone));
            move = null;
            frame(Render);
            key(ImGuiKey.Enter, Render);
            Assert.NotNull(move);
            move.Apply(ctx.Inventory, storage);
            Assert.Equal(0, storage.Count(Resource.Stone));
            move = null;
            frame(Render); key(ImGuiKey.Enter, Render);
            Assert.Null(move); // Depleted selection cannot execute again.
        });
    }

    [Fact]
    public void FoodKeyboardMovesFromItemToActionAndPreservesIdentityOnReopen()
    {
        Native((frame, key) =>
        {
            var ctx = GameContext.CreateNewGame(42);
            ctx.Inventory.Add(Resource.CookedMeat, 1);
            var food = new FoodOverlay(); food.Open();
            void Render() => food.Render(ctx, 0);
            frame(Render); frame(Render);
            key(ImGuiKey.F2, Render); key(ImGuiKey.Enter, Render);
            Assert.Null(food.PendingAction);
            key(ImGuiKey.F3, Render); key(ImGuiKey.Enter, Render);
            Assert.NotNull(food.PendingAction);
            string id = food.PendingAction.ItemId;
            food.ClearPendingAction(); food.Open();
            frame(Render); frame(Render);
            key(ImGuiKey.F3, Render); key(ImGuiKey.Enter, Render);
            Assert.Equal(id, food.PendingAction?.ItemId);
        });
    }

    [Fact]
    public void FireKeyboardCanRefuelRepeatedlyWithoutReturningToTheList()
    {
        Native((frame, key) =>
        {
            var ctx = GameContext.CreateNewGame(42);
            var source = ctx.CurrentLocation.GetFeature<HeatSourceFeature>()!;
            source.AddFuel(1, FuelType.Kindling); source.IgniteAll();
            ctx.Inventory.Add(Resource.Stick, 1);
            var fire = new FireOverlay(); fire.Open();
            FireOverlayResult? result = null;
            void Render() { var next = fire.Render(ctx, 0); result ??= next; }
            frame(Render); frame(Render);
            key(ImGuiKey.F3, Render); key(ImGuiKey.Enter, Render);
            Assert.Equal(FireAction.AddFuel, result?.Action);
            Assert.NotNull(result);
            var resource = result.FuelResource;
            result = null;
            fire.SetTendMessage("Fuel added.");
            key(ImGuiKey.Enter, Render);
            Assert.Equal(resource, result?.FuelResource);
        });
    }

    [Fact]
    public void KeepWalkingShortcutReturnsTheDomainSentinel()
    {
        Native((frame, key) =>
        {
            var ctx = GameContext.CreateNewGame(42);
            var feature = new ForageFeature().AddSticks();
            var forage = new ForageOverlay(); forage.Open(ctx, feature, []);
            ForageResult? result = null;
            void Render() { var next = forage.Render(ctx, 0); result ??= next; }
            frame(Render); frame(Render);
            key(ImGuiKey.K, Render);
            Assert.NotNull(result);
            Assert.Null(result.Focus);
            Assert.Equal(-1, result.Minutes);
        });
    }

    [Fact]
    public void EverydayOpeningBindingsAreUniqueAndAvoidMovement()
    {
        var bindings = Enum.GetValues<HotkeyAction>().Select(HotkeyRegistry.KeyFor).ToArray();
        Assert.Equal(bindings.Length, bindings.Distinct().Count());
        Assert.DoesNotContain(bindings, k => k is Raylib_cs.KeyboardKey.W or Raylib_cs.KeyboardKey.A or Raylib_cs.KeyboardKey.S or Raylib_cs.KeyboardKey.D);
        var actions = HudActions.Build(GameContext.CreateNewGame(42));
        Assert.Equal("Food", actions.Single(a => a.Shortcut == HotkeyAction.Food).Id);
        Assert.Equal("rest", actions.Single(a => a.Shortcut == HotkeyAction.Rest).Id);
        Assert.Equal("TreatWounds", actions.Single(a => a.Shortcut == HotkeyAction.Treatment).Id);
    }

    [Fact]
    public void TransferRevalidatesAmountAndRejectsDepletedStacks()
    {
        var player = new Inventory(); var storage = new Inventory();
        player.Add(Resource.Stone, 1); player.Add(Resource.Stone, 2);
        var move = new TransferResult { FromPlayer = true, Resource = Resource.Stone, Amount = 3 };
        move.Apply(player, storage);
        Assert.Equal(2, player.Count(Resource.Stone));
        Assert.Equal(0, storage.Count(Resource.Stone));
        move.Amount = 2;
        move.Apply(player, storage);
        Assert.Equal(0, player.Count(Resource.Stone));
        Assert.Equal(3, storage.Weight(Resource.Stone));
        move.Apply(player, storage);
        Assert.Equal(2, storage.Count(Resource.Stone));
    }
}
