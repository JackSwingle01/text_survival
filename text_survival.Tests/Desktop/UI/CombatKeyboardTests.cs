using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Desktop.Input;
using text_survival.Desktop.UI;
using text_survival.UI;

namespace text_survival.Tests.Desktop.UI;

[Collection("Native UI")]
public class CombatKeyboardTests
{
    private static GameContext World()
    {
        var ctx = GameContext.CreateNewGame(42);
        var wolves = Enumerable.Range(0, 2).Select(_ => AnimalFactory.FromType(AnimalType.Wolf, ctx.CurrentLocation, ctx.Map!)!).ToList();
        ctx.ActiveCombat = CombatScenario.Create([ctx.player], [.. wolves], ctx.CurrentLocation, 1, AwarenessState.Engaged, AwarenessState.Engaged, ctx.player);
        ctx.ActiveCombat.Player!.Position = new(25, 25);
        ctx.ActiveCombat.Team2[0].Position = new(26, 25);
        ctx.ActiveCombat.Team2[1].Position = new(25, 26);
        return ctx;
    }
    private static void Native(Action<GameContext, CombatUiState, Action<ImGuiKey>, Func<CombatInput?>, Action, Action<Action>> test)
    {
        var previous = ImGui.GetCurrentContext(); var context = ImGui.CreateContext();
        var oldContext = UiInput.Context; bool ready = UiInput.Ready, focused = UiInput.HudFocused;
        try
        {
            var io = ImGui.GetIO();
            unsafe { io.NativePtr->IniFilename = null; }
            io.DisplaySize = new Vector2(1280, 720); io.DeltaTime = 1f / 60;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            io.Fonts.AddFontDefault(); io.Fonts.GetTexDataAsRGBA32(out IntPtr _, out int _, out int _);
            UiStyle.Apply(new UiSettings()); UiInput.Context = InputContext.Combat; UiInput.Ready = true; UiInput.HudFocused = false;
            var ctx = World(); var state = new CombatUiState(); var panel = new CombatPanel(); CombatInput? result = null;
            Action? custom = null;
            void Frame() { ImGui.NewFrame(); if (custom != null) custom(); else result ??= panel.Render(ctx, new(0, 0, 360, 720), null, state, true); ImGui.Render(); }
            void Key(ImGuiKey key) { io.AddKeyEvent(key, true); Frame(); Frame(); io.AddKeyEvent(key, false); Frame(); Frame(); }
            Frame(); Frame(); test(ctx, state, Key, () => result, Frame, render => custom = render);
        }
        finally { UiInput.Context = oldContext; UiInput.Ready = ready; UiInput.HudFocused = focused; ImGui.DestroyContext(context); ImGui.SetCurrentContext(previous); }
    }
    [Fact]
    public void CyclingTargetsChangesAttackRecipient()
    {
        Native((ctx, state, key, result, frame, render) =>
        {
            var first = state.Target; key(ImGuiKey.E); Assert.NotSame(first, state.Target);
            key(ImGuiKey.F); Assert.Equal(CombatActions.Attack, result()!.Action); Assert.Same(state.Target, result()!.Target);
        });
    }
    [Fact]
    public void MovementPreviewsAndEscapeDoNotSpendTurn()
    {
        Native((ctx, state, key, result, frame, render) =>
        {
            var position = ctx.ActiveCombat!.Player!.Position;
            key(ImGuiKey.W); Assert.Null(result()); Assert.Equal(position, ctx.ActiveCombat.Player.Position);
            key(ImGuiKey.Escape); Assert.Null(state.Cursor); key(ImGuiKey.Enter); Assert.Null(result());
            key(ImGuiKey.W); key(ImGuiKey.Enter); Assert.Equal(new(25, 24), result()!.MoveTarget);
        });
    }
    [Fact]
    public void InspectionNeverMovesOrAttacksAndKeepsTarget()
    {
        Native((ctx, state, key, result, frame, render) =>
        {
            var target = state.Target; key(ImGuiKey.X); key(ImGuiKey.D); key(ImGuiKey.Enter); key(ImGuiKey.F);
            Assert.Null(result()); Assert.True(state.Inspecting); Assert.Same(target, state.Target);
            key(ImGuiKey.Escape); Assert.False(state.Inspecting);
        });
    }
    [Fact]
    public void OutOfRangeActionIsRejectedWithoutChangingSlots()
    {
        Native((ctx, state, key, result, frame, render) =>
        {
            state.Target!.Position = new(40, 25); key(ImGuiKey.F);
            Assert.Null(result()); Assert.Contains("range", state.Feedback);
        });
    }
    [Fact]
    public void DialogContextAndUnreleasedOwnerCannotAttack()
    {
        Native((ctx, state, key, result, frame, render) =>
        {
            UiInput.Context = InputContext.Dialog; key(ImGuiKey.F); Assert.Null(result());
            UiInput.Context = InputContext.Combat; UiInput.Ready = false; key(ImGuiKey.F); Assert.Null(result());
        });
    }
    [Fact]
    public void ShiftFOnlyFleesAndEscapeDoesNot()
    {
        Native((ctx, state, key, result, frame, render) =>
        {
            ctx.ActiveCombat!.Player!.Position = new(0, 25); frame();
            key(ImGuiKey.Escape); Assert.Null(result());
            ImGui.GetIO().AddKeyEvent(ImGuiKey.ModShift, true); key(ImGuiKey.F);
            Assert.Equal(CombatActions.Flee, result()!.Action);
        });
    }
    [Fact]
    public void DepletedTargetIsReplacedAndMovementRejectsOccupiedCells()
    {
        var ctx = World(); var state = new CombatUiState(); var combat = ctx.ActiveCombat!; state.Update(combat);
        var removed = state.Target!; combat.Units.Remove(removed); state.Update(combat); Assert.NotSame(removed, state.Target);
        state.Step(combat, 0, 1); Assert.Contains("occupied", state.MoveReason(combat));
    }
    [Fact]
    public void MapInspectionDoesNotChangePositionOrTime()
    {
        var ctx = World(); var state = new HudState(); state.Update(ctx, 0);
        var position = ctx.Map!.CurrentPosition; var time = ctx.GameTime;
        state.BeginInspection(ctx); state.StepInspection(ctx, 1, 0);
        Assert.True(state.Inspecting); Assert.Equal(position, ctx.Map.CurrentPosition); Assert.Equal(time, ctx.GameTime);
        state.ClearSelection(); Assert.False(state.Inspecting); Assert.Null(state.SelectedTile);
    }
    [Fact]
    public void ThrowablePopupRequiresSelectionAndDoesNotLeakAttack()
    {
        Native((ctx, state, key, result, frame, render) =>
        {
            state.Target!.Position = new(35, 25);
            ctx.Inventory.Add(Resource.Stone, 1);
            key(ImGuiKey.T); Assert.Null(result());
            key(ImGuiKey.F); Assert.Null(result());
            key(ImGuiKey.Tab); key(ImGuiKey.Enter);
            Assert.Equal(CombatActions.ThrowStone, result()!.Action);
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TravelConfirmationWaitsForReleaseAndDefaultsToCancel(bool hazardous)
    {
        Native((ctx, combatState, key, ignored, frame, render) =>
        {
            ctx.ActiveCombat = null; UiInput.Context = InputContext.Exploration;
            var state = new HudState(); state.Update(ctx, 0); state.BeginInspection(ctx);
            var position = ctx.Map!.CurrentPosition;
            var adjacent = position.GetCardinalNeighbors().First(p => ctx.Map.CanMoveTo(p.X, p.Y));
            ctx.Map.GetLocationAt(adjacent.X, adjacent.Y)!.Visibility = text_survival.Environments.Grid.TileVisibility.Visible;
            ctx.CurrentLocation.TerrainHazardLevel = hazardous ? .9 : 0;
            ctx.Map.GetLocationAt(adjacent.X, adjacent.Y)!.TerrainHazardLevel = hazardous ? .9 : 0;
            state.StepInspection(ctx, adjacent.X - position.X, adjacent.Y - position.Y);
            Assert.NotEmpty(TravelInspection.Build(ctx, state.SelectedTile!.Value).Actions);
            var panel = new LocationInspector(); PlayerAction? chosen = null;
            render(() => chosen ??= panel.Render(ctx, new(0, 0, 360, 720), state, HudActions.Build(ctx), true));
            state.ConfirmTravel = true;
            ImGui.GetIO().AddKeyEvent(ImGuiKey.Enter, true); frame(); frame(); Assert.Null(chosen);
            ImGui.GetIO().AddKeyEvent(ImGuiKey.Enter, false); frame(); frame();
            key(ImGuiKey.Enter); Assert.Null(chosen); Assert.True(state.Inspecting);
            Assert.False(ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId));
            state.ConfirmTravel = true; frame(); frame();
            ImGui.GetIO().AddKeyEvent(ImGuiKey.ModShift, true); key(ImGuiKey.Tab);
            ImGui.GetIO().AddKeyEvent(ImGuiKey.ModShift, false); frame();
            key(ImGuiKey.Enter); var travel = Assert.IsType<PlayerAction.Travel>(chosen);
            Assert.Equal(hazardous ? "quick" : null, travel.HazardMode);
            Assert.Equal(position, ctx.Map.CurrentPosition);
        });
    }

    [Fact]
    public void ActionSlotsKeepIdsAndOrderAcrossRangeAndAwareness()
    {
        var ctx = World(); var state = new CombatUiState(); state.Update(ctx.ActiveCombat!);
        var panel = new CombatPanel();
        TextControl[] Capture()
        {
            var capture = new TextFrame(); GameGui.Capture = capture;
            try { panel.Render(ctx, new(0, 0, 360, 720), null, state, true); return capture.Controls.ToArray(); }
            finally { GameGui.Capture = null; }
        }
        var before = Capture();
        state.Target!.Position = new(45, 25); state.Target.Awareness = AwarenessState.Unaware;
        var after = Capture();
        Assert.Equal(before.Select(c => c.Id), after.Select(c => c.Id));
        Assert.True(before.Single(c => c.Label == "Attack [F]").Enabled);
        Assert.False(after.Single(c => c.Label == "Attack [F]").Enabled);
        Assert.True(after.Single(c => c.Label == "Block [R]").Enabled); // Other enemy remains adjacent.
    }

}
