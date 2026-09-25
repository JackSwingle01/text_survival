using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using text_survival.Actions;
using text_survival.Core;
using text_survival.Desktop;
using text_survival.Desktop.Input;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.UI;
using text_survival.UI;

// A live keyboard/mouse probe using production screens, without executing gameplay.
string output = Path.Combine(Path.GetTempPath(), "text-survival-ui-preview");
Directory.CreateDirectory(output);
Directory.SetCurrentDirectory(output); // Settings and logs cannot touch the player's saves.
string log = Path.Combine(output, "input.log");
File.WriteAllText(log, "Starting isolated UI input preview\n");
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(1280, 720, "UI Foundation Preview");
Raylib.SetExitKey(KeyboardKey.Null);
Raylib.SetTargetFPS(60);
rlImGui.Setup(true);
unsafe { ImGui.GetIO().NativePtr->IniFilename = null; }
File.AppendAllText(log, $"Backend navigation flags before configuration: {ImGui.GetIO().ConfigFlags}\n");
UiStyle.Initialize();
TileRenderer.LoadSprites();
UiIcons.Load();
var ctx = GameContext.CreateNewGame(seed: 1234);
var scheduler = new FrameScheduler();
SynchronizationContext.SetSynchronizationContext(scheduler);
var ui = new DesktopUi(ctx, scheduler);
ctx.Ui = ui;
async Task ObserveActions()
{
    while (true)
    {
        var action = await ui.WaitForPlayerAction();
        File.AppendAllText(log, $"ACTION: {action}\n");
        if (action is PlayerAction.Camp { Action: CampAction.Crafting }) await ui.ShowCrafting();
        else if (action is PlayerAction.Camp { Action: CampAction.Inventory }) await ui.ShowInventory();
    }
}
var observe = ObserveActions();
UiSettingsDialog.Requested = true; // Start on the representative shared-controls dialog.
while (!Raylib.WindowShouldClose())
{
    scheduler.Pump();
    ui.Frame(ctx, 1f / 60);
    if (observe.IsFaulted) throw observe.Exception!;
    foreach (var key in new[] { KeyboardKey.F1, KeyboardKey.Tab, KeyboardKey.Enter, KeyboardKey.Escape, KeyboardKey.I, KeyboardKey.C, KeyboardKey.W })
        if (Raylib.IsKeyPressed(key)) File.AppendAllText(log, $"KEY: {key}, ready={UiInput.Ready}, context={UiInput.Context}, capture={ImGui.GetIO().WantCaptureKeyboard}\n");
}
ui.Dispose();
UiIcons.Unload();
rlImGui.Shutdown();
Raylib.CloseWindow();
