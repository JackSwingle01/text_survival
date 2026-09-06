using ImGuiNET;
using Raylib_cs;
using text_survival.Actions;
using text_survival.Desktop.Input;
using text_survival.Desktop.Rendering;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>Composes the persistent HUD and yields intents to DesktopUi's active prompt.</summary>
public sealed class HudController
{
    public HudState State { get; } = new();
    public HudLayout Layout { get; private set; } = HudLayout.Calculate(1280, 720, 16.25f, false);
    private readonly LocationInspector _inspector = new();
    private readonly CombatPanel _combat = new();
    private readonly EventLogPanel _events = new();
    private IReadOnlyList<HudAction> _actions = [];
    private PlayerAction? _action;
    private CombatInput? _combatInput;

    public void Update(GameContext ctx, WorldRenderer world, float dt)
    {
        State.Update(ctx, dt);
        Layout = HudLayout.Calculate(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), ImGui.GetFontSize(), State.HistoryExpanded);
        world.SetViewport(Layout.Map);
        _actions = HudActions.Build(ctx);
        _action = null;
        _combatInput = null;
    }

    public void Render(GameContext ctx, WorldRenderer world, bool mapInteractive, bool combatInteractive)
    {
        _action = TopBar.Render(ctx, Layout.Top, _actions, mapInteractive, world);
        var personal = SurvivorPanel.Render(ctx, Layout.Survivor, _actions, mapInteractive);
        _action ??= personal;
        if (ctx.ActiveCombat != null)
        {
            ImGui.BeginDisabled(!combatInteractive);
            var combat = _combat.Render(ctx, Layout.Inspector, world.HoveredCombatUnit);
            ImGui.EndDisabled();
            if (combatInteractive && combat.HasValue) _combatInput = new CombatInput(combat, null);
            if (combatInteractive && _combatInput == null && !ImGui.GetIO().WantCaptureMouse && world.HandleCombatClick() is { } cell)
                _combatInput = new CombatInput(null, new Environments.Grid.GridPosition(cell.x, cell.y));
        }
        else
        {
            var action = _inspector.Render(ctx, Layout.Inspector, State, _actions, mapInteractive);
            _action ??= action;
            if (mapInteractive && _action == null) _action = MapInputRouter.Read(ctx, world, State, _actions);
        }
        _events.Render(ctx, Layout.Events, State);
    }

    public PlayerAction? TakeAction() { var result = _action; _action = null; return result; }
    public CombatInput? TakeCombatInput() { var result = _combatInput; _combatInput = null; return result; }
}
