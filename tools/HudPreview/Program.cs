using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using text_survival.Actions;
using text_survival.Desktop.Rendering;
using text_survival.Desktop.UI;
using text_survival.Environments.Grid;
using text_survival.UI;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Combat;
using text_survival.Actors.Animals;
using text_survival.Desktop;
using text_survival.Core;

// Repeatable screenshots of the production HUD. No save loading or gameplay actions.
string output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "hud-preview");
Directory.CreateDirectory(output);
Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(1600, 900, "HUD Preview");
Raylib.SetTargetFPS(60);
rlImGui.Setup(true);
unsafe { ImGui.GetIO().NativePtr->IniFilename = null; } // Preview must not rewrite the player's UI settings.
ImGui.GetIO().FontGlobalScale = 1.25f;
TileRenderer.LoadSprites();
UiIcons.Load();
var ctx = GameContext.CreateNewGame(seed: 1234);
var hud = new HudController();
var world = new WorldRenderer(hud.State);
world.RecenterOnPlayer(ctx);
for (int scenario = 0; scenario < 8; scenario++)
{
    if (scenario == 1)
    {
        var pos = ctx.Map!.CurrentPosition;
        var adjacent = pos.GetCardinalNeighbors().First(p => ctx.Map.CanMoveTo(p.X, p.Y));
        ctx.Map.GetLocationAt(adjacent.X, adjacent.Y)!.Visibility = TileVisibility.Visible;
        hud.State.Select(ctx, adjacent.X, adjacent.Y);
    }
    if (scenario == 2)
    {
        hud.State.ClearSelection();
        ctx.player.Body.BodyTemperature = 96.5;
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Bleeding(.3));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Pain(.5));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Fear(.7));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Hypothermia(.6));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Wet(.4));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.SprainedAnkle(.3));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Nauseous(.2));
        ctx.player.EffectRegistry.AddEffect(EffectFactory.Shivering(.2));
        for (int i = 0; i < 30; i++) ctx.Log.Add($"A long event {i}: you collect branches and return to your shelter before the weather worsens.", LogLevel.Warning, "2:35");
        hud.State.HistoryExpanded = true;
    }
    if (scenario == 3) { Raylib.SetWindowSize(1280, 720); hud.State.HistoryExpanded = false; }
    if (scenario == 4) { ImGui.GetIO().FontGlobalScale = 1.5f; }
    if (scenario == 5)
    {
        ctx = GameContext.CreateNewGame(seed: 1234);
        ImGui.GetIO().FontGlobalScale = 1.25f;
        Raylib.SetWindowSize(1600, 900);
        ctx.CurrentLocation.AddFeature(new CuringRackFeature());
        if (ctx.CurrentLocation.GetFeature<ForageFeature>() is { } oldForage) ctx.CurrentLocation.RemoveFeature(oldForage);
        ctx.CurrentLocation.AddFeature(new ForageFeature().AddSticks().AddTinder().AddBerries());
        ctx.CurrentLocation.AddFeature(new WaterFeature("stream", "Stream").AsOpenWater().WithFishAbundance(.8));
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>() ?? new HeatSourceFeature();
        fire.AddFuel(.2, FuelType.Tinder);
        fire.AddFuel(1, FuelType.Kindling);
        fire.IgniteAll();
        if (!ctx.CurrentLocation.Features.Contains(fire)) ctx.CurrentLocation.AddFeature(fire);
    }
    if (scenario == 6)
    {
        var wolf = AnimalFactory.FromType(AnimalType.Wolf, ctx.CurrentLocation, ctx.Map!)!;
        ctx.ActiveCombat = CombatScenario.Create([ctx.player], [wolf], ctx.CurrentLocation, 5,
            AwarenessState.Engaged, AwarenessState.Engaged, ctx.player);
    }
    if (scenario == 7)
    {
        ctx = GameContext.CreateNewGame(seed: 1234);
        ctx.Herds.Clear();
        var wolves = Herd.Create(AnimalType.Wolf, ctx.CurrentLocation, ctx.Map!, [ctx.Map!.CurrentPosition]);
        for (int i = 0; i < 3; i++) wolves.AddMember(AnimalFactory.FromType(AnimalType.Wolf, ctx.CurrentLocation, ctx.Map)!);
        wolves.Hunger = 0.8;
        ctx.Herds.Add(wolves);
        PredatorInteractions.Update(wolves, 1, ctx);
        PredatorInteractions.Observe(ctx);
    }
    for (int frame = 0; frame < 15; frame++)
    {
        hud.Update(ctx, world, 1f/60);
        world.Update(ctx, 1f/60);
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(20,25,30,255));
        world.Render(ctx);
        rlImGui.Begin();
        hud.Render(ctx, world, scenario != 6, scenario == 6);
        rlImGui.End();
        Raylib.EndDrawing();
    }
    var screenshot = Raylib.LoadImageFromScreen();
    Raylib.ExportImage(screenshot, Path.Combine(output, $"hud-{scenario}.png"));
    Raylib.UnloadImage(screenshot);
}
world.Dispose();
ctx.ActiveCombat = null;
var scheduler = new FrameScheduler();
using (var ui = new DesktopUi(ctx, scheduler))
{
    ctx.Ui = ui;
    _ = ui.WaitForPlayerAction();
    _ = ui.Confirm("HUD check: background actions must remain disabled while this dialog is open.");
    for (int frame = 0; frame < 15; frame++) ui.Frame(ctx, 1f/60);
    var screenshot = Raylib.LoadImageFromScreen();
    Raylib.ExportImage(screenshot, Path.Combine(output, "hud-7-modal.png"));
    Raylib.UnloadImage(screenshot);
}
var predatorScheduler = new FrameScheduler();
using (var ui = new DesktopUi(ctx, predatorScheduler))
{
    ctx.Ui = ui;
    var evt = text_survival.Actions.Events.PredatorEventFactory.Create(ctx, ctx.Herds[0]);
    _ = ui.ShowEventChoices(new EventDto(evt.Name, evt.Description,
        evt.GetAvailableChoices(ctx).Select((c, i) => new EventChoiceDto(i.ToString(), c.Label, c.Description, true, null)).ToList()));
    for (int frame = 0; frame < 15; frame++) ui.Frame(ctx, 1f/60);
    var screenshot = Raylib.LoadImageFromScreen();
    Raylib.ExportImage(screenshot, Path.Combine(output, "predator-response.png"));
    Raylib.UnloadImage(screenshot);
}
// Authored scenes use their own decisions over the same live herd.
ctx.Inventory.Add(Resource.RawMeat, 1);
foreach (var title in new[] { "Rustle at Camp Edge", "Wolves Smell Blood" })
{
    if (title == "Wolves Smell Blood")
    {
        ctx.CurrentLocation.AddFeature(new CarcassFeature(AnimalFactory.FromType(AnimalType.Mammoth, ctx.CurrentLocation, ctx.Map!)!));
        ctx.UpdateWithoutEvents(0, ActivityType.Butchering);
    }
    using var ui = new DesktopUi(ctx, new FrameScheduler());
    ctx.Ui = ui;
    var evt = GameEventRegistry.AllEventFactories.Select(f => f(ctx)).First(e => e.Name == title);
    evt.BindPredatorEncounters(ctx);
    _ = ui.ShowEventChoices(new EventDto(evt.Name, evt.Description,
        evt.GetAvailableChoices(ctx).Select((c, i) => new EventChoiceDto(i.ToString(), c.Label, c.Description, true, null)).ToList()));
    for (int frame = 0; frame < 15; frame++) ui.Frame(ctx, 1f/60);
    var screenshot = Raylib.LoadImageFromScreen();
    Raylib.ExportImage(screenshot, Path.Combine(output, title == "Wolves Smell Blood" ? "authored-carcass.png" : "authored-camp.png"));
    Raylib.UnloadImage(screenshot);
}
UiIcons.Unload();
TerrainRenderer.Unload();
rlImGui.Shutdown();
Raylib.CloseWindow();
