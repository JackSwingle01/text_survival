using System.Text.Json;
using text_survival.Actions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Bodies;
using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Desktop.UI;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.GameCli;
using text_survival.Items;
using text_survival.Persistence;
using text_survival.UI;

namespace text_survival.Tests.Cli;

public class GameCliTests
{
    private static string Id() => "cli_test_" + Guid.NewGuid().ToString("N");
    private static GameContext World() => GameContext.CreateNewGame(42);
    private static TextControl Control(CliUi ui, string label) => Assert.Single(ui.Render().Controls, c => c.Label == label);
    private static void Click(CliUi ui, string label) => ui.Render(Control(ui, label).Id);

    private static JsonElement Wire(object value) => JsonSerializer.SerializeToElement(value, CliProtocol.Json);

    [Fact]
    public void Logs_SerializeNamedFieldsInCommandsAndSnapshots()
    {
        using var session = new CliSession(World(), Id());
        session.Context.Log.Add("A test warning", LogLevel.Warning, "12:34");
        foreach (var entries in new[] { Wire(session.Execute("log 1")), Wire(session.Look()).GetProperty("log") })
        {
            var entry = entries.EnumerateArray().Last();
            Assert.Equal("A test warning", entry.GetProperty("text").GetString());
            Assert.Equal("Warning", entry.GetProperty("level").GetString());
            Assert.Equal("12:34", entry.GetProperty("timestamp").GetString());
        }
    }

    [Fact]
    public void ChoppingWithoutAnAxe_ReportsWhyNoWorkWasDone()
    {
        using var session = new CliSession(World(), Id());
        var ctx = session.Context;
        ctx.Inventory.Tools.Clear();
        ctx.CurrentLocation.AddFeature(new WoodedAreaFeature("Test trees", Resource.Birch, 60));
        var before = ctx.GameTime;
        var result = Wire(session.Execute("act work:chop_wood"));
        Assert.Equal(before, ctx.GameTime);
        Assert.Contains(result.GetProperty("log").EnumerateArray(), e =>
            e.GetProperty("text").GetString() == "You need an axe to fell trees." &&
            e.GetProperty("level").GetString() == "Warning");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    public void MammothScouting_UnsuccessfulSearchReportsAnOutcome(int minutes)
    {
        using var session = new CliSession(World(), Id());
        // Seed 0's first draw is above the 40% discovery threshold.
        text_survival.Utils.Seed(0);
        var task = new MegafaunaStrategy(AnimalType.Mammoth).Execute(session.Context, session.Context.CurrentLocation, minutes);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Contains(Wire(session.Execute("log")).EnumerateArray(), e =>
            e.GetProperty("text").GetString() == "You find nothing conclusive. Old tracks, maybe.");
    }

    [Fact]
    public void InjuryRows_LabelDamageAndDistinguishHealthyTissueFromDestroyedOrgans()
    {
        var ctx = World();
        var head = ctx.player.Body.Parts.Single(p => p.Name == BodyRegionNames.Head);
        head.Organs.Single(o => o.Name == OrganNames.Brain).Condition = 0;
        var lines = ((TextFrame)Observations.Status(ctx)).Lines;
        int headIndex = lines.FindIndex(l => l.Trim() == "Head");
        int brainIndex = lines.FindIndex(l => l.Trim() == "Brain");
        Assert.True(headIndex >= 0 && brainIndex >= 0);
        Assert.Equal("0% damage", lines[headIndex + 1]);
        Assert.Equal("Healthy", lines[headIndex + 2]);
        Assert.Equal("100% damage", lines[brainIndex + 1]);
        Assert.Equal("Destroyed", lines[brainIndex + 2]);
    }

    [Fact]
    public void RealLoop_InspectionDoesNotTick_WaitDoes_TravelUsesTheRealRunner()
    {
        using var session = new CliSession(World(), Id());
        var ctx = session.Context;
        var start = ctx.GameTime;
        var position = ctx.Map!.CurrentPosition;
        foreach (var command in new[] { "look", "status", "weather", "map 2", "actions", "log", $"inspect {position.X},{position.Y} {position.X + 1},{position.Y}" })
            Assert.NotEmpty(JsonSerializer.Serialize(session.Execute(command)));
        Assert.Equal(start, ctx.GameTime);
        Assert.Throws<ArgumentException>(() => session.Execute("travel -1,-1"));
        Assert.Equal(position, ctx.Map.CurrentPosition);
        session.Execute("act Wait");
        Assert.Equal(start.AddMinutes(5), ctx.GameTime);
        var target = new[] { new GridPosition(position.X + 1, position.Y), new GridPosition(position.X - 1, position.Y),
            new GridPosition(position.X, position.Y + 1), new GridPosition(position.X, position.Y - 1) }
            .First(p => TravelInspection.Build(ctx, (p.X, p.Y)).Actions.Count > 0);
        session.Execute($"travel {target.X},{target.Y}");
        for (int i = 0; i < 30 && session.Ui.Screen != "map"; i++)
        {
            var control = session.Ui.Render().Controls.First(c => c.Enabled);
            session.Execute("click " + control.Id);
        }
        Assert.Equal(target, ctx.Map.CurrentPosition);
        Assert.True(ctx.GameTime > start.AddMinutes(5));
    }

    [Fact]
    public void Inventory_UsesActualItemDetailsAndDropBehavior()
    {
        using var session = new CliSession(World(), Id());
        var ctx = session.Context;
        ctx.Inventory.Add(Resource.Stone, 1);
        int before = ctx.Inventory.Count(Resource.Stone);
        session.Execute("act Inventory");
        var item = session.Ui.Render().Controls.Single(c => c.Id.EndsWith("/res_Stone"));
        Assert.Contains("Stone", item.Label);
        session.Execute("click " + item.Id);
        Assert.Contains(session.Ui.Render().Lines, l => l.StartsWith("Quantity:"));
        Assert.Throws<ArgumentException>(() => session.Execute("act Wait"));
        session.Execute("click Drop 1");
        Assert.Equal(before - 1, ctx.Inventory.Count(Resource.Stone));
        session.Execute("close");
        Assert.Equal("map", session.Ui.Screen);
    }

    [Fact]
    public async Task Prompts_RejectDisabledChoicesAndInvalidNumbers_KeepDecisionPending()
    {
        var ui = new CliUi(World());
        var select = ui.Select("Pick", new[] { "locked", "open" }, s => s, s => s == "locked");
        Assert.Throws<ArgumentException>(() => Click(ui, "locked"));
        Assert.False(select.IsCompleted);
        Click(ui, "open"); Assert.Equal("open", (await select));
        var number = ui.ReadInt("Hours", 1, 8, true);
        ui.Render(Control(ui, "value").Id, "99");
        Assert.Throws<ArgumentException>(() => Click(ui, "OK"));
        Assert.False(number.IsCompleted);
        ui.Render(Control(ui, "value").Id, "3"); Click(ui, "OK");
        Assert.Equal(3, (await number));
        var confirm = ui.Confirm("Continue?"); Click(ui, "No"); Assert.False((await confirm));
        var choice = ui.Choose("Pick", [("first", "First"), ("second", "Second")]);
        Click(ui, "Second"); Assert.Equal("second", (await choice));
    }

    [Fact]
    public async Task EventsAndProgress_ExposeOutcomesAndRequireExplicitAcknowledgment()
    {
        var ui = new CliUi(World());
        var evt = new EventDto("Test event", "Description", [new("no", "Unavailable", "Needs supplies", false), new("yes", "Proceed", "Costs time", true, "5 minutes")]);
        var task = ui.ShowEventChoices(evt);
        Assert.Contains("Description", ui.Render().Lines);
        Assert.Throws<ArgumentException>(() => Click(ui, "Unavailable"));
        Assert.False(task.IsCompleted);
        Click(ui, "Proceed"); Assert.Equal("yes", (await task));
        var outcome = ui.ShowEventOutcome(evt with { Choices = [], Outcome = new("Outcome", 5, ["effect"], ["damage"], ["gain"], ["loss"], ["tension"]) });
        foreach (var word in new[] { "Outcome", "effect", "damage", "gain", "loss", "tension" })
            Assert.Contains(ui.Render().Lines, l => l.Contains(word));
        Assert.False(outcome.IsCompleted); Click(ui, "Continue [Enter]"); Assert.True(outcome.IsCompleted);
        using (var progress = ui.BeginProgress(ProgressKind.Crafting, "Making"))
        {
            progress.Section("Materials").Lines.Add(new("Stone"));
            var acknowledgement = progress.WaitForContinue();
            Assert.False(acknowledgement.IsCompleted); Click(ui, "Continue"); Assert.True(acknowledgement.IsCompleted);
        }
        Assert.Single(ui.CompletedProgress);
    }

    [Fact]
    public async Task EveryOverlayRendersWithoutNativeLibrariesAndCanClose()
    {
        var ctx = World(); var ui = new CliUi(ctx); ctx.Ui = ui;
        ctx.Inventory.Add(Resource.RawMeat, 1);
        ctx.Inventory.Add(Resource.Water, 1);
        ctx.NPCs.Add(NPCFactory.CreateTestNPC(ctx.CurrentLocation, ctx.Map!, ctx.Camp));
        foreach (Func<Task> open in new Func<Task>[] { ui.ShowInventory, ui.ShowCrafting, ui.ShowFood, ui.ShowNPCs,
            ui.ShowDiscoveryLog, () => ui.ShowTransfer(new Inventory(), "Test storage"), () => ui.ShowFire(null) })
        {
            var task = open(); var frame = ui.Render();
            Assert.NotEmpty(frame.Lines.Concat(frame.Controls.Select(c => c.Label)));
            Assert.False(task.IsCompleted); ui.Close(); Assert.True(task.IsCompleted);
        }
        var feature = ctx.Map!.AllLocations.Select(l => l.GetFeature<ForageFeature>()).First(f => f != null)!;
        var forage = ui.SelectForageOptions(feature, []);
        Assert.Contains(ui.Render().Controls, c => c.Label == "Search [F]");
        Assert.Equal(true, ui.Render().Controls.Single(c => c.Id.EndsWith("/30")).Value);
        ui.Render(ui.Render().Controls.Single(c => c.Id.EndsWith("/15")).Id);
        Assert.Equal(true, ui.Render().Controls.Single(c => c.Id.EndsWith("/15")).Value);
        Assert.Equal(false, ui.Render().Controls.Single(c => c.Id.EndsWith("/30")).Value);
        ui.Close(); Assert.Equal(0, (await forage).minutes);
        var message = ui.ShowWorkResult(new("Work", "Done", ["Stone"], ["Narrative"], ["Warning"]));
        Assert.Contains(ui.Render().Lines, s => s.Contains("Warning") && s.Contains("Stone"));
        Click(ui, "Continue"); Assert.True(message.IsCompleted);
    }

    [Fact]
    public async Task FoodAndFire_CommitRealScreenResults()
    {
        var ctx = World(); var ui = new CliUi(ctx); ctx.Ui = ui;
        ctx.Inventory.Add(Resource.Water, 1);
        var food = ui.ShowFood();
        var water = ui.Render().Controls.First(c => c.Id.EndsWith("/Water") || c.Id.EndsWith("/water") || c.Label.Contains("Water"));
        ui.Render(water.Id);
        var drink = ui.Render().Controls.Single(c => c.Label.StartsWith("Drink ("));
        ui.Render(drink.Id);
        Assert.Equal(FoodAction.Drink, (await food)!.Action);
        var fire = ui.ShowFire(null);
        var start = ui.Render().Controls.Single(c => c.Id.EndsWith("/start_fire"));
        Assert.True(start.Enabled); ui.Render(start.Id);
        Assert.Equal(FireAction.StartFire, (await fire)!.Action);
    }

    [Fact]
    public async Task Combat_ProvidesUnitCoordinatesDetailsAndActualControls()
    {
        var ctx = World(); var ui = new CliUi(ctx); ctx.Ui = ui;
        var wolf = AnimalFactory.FromType(AnimalType.Wolf, ctx.CurrentLocation, ctx.Map!)!;
        ctx.ActiveCombat = CombatScenario.Create([ctx.player], [wolf], ctx.CurrentLocation, 1, AwarenessState.Engaged, AwarenessState.Engaged, ctx.player);
        var input = ui.WaitForCombatAction();
        Assert.Contains("Attack", ui.Render().Controls.Select(c => c.Label));
        Assert.Contains("Wolf", JsonSerializer.Serialize(Observations.Combat(ctx)));
        Assert.Contains("WOLF", JsonSerializer.Serialize(Observations.Unit(ctx, 1)));
        Click(ui, "Attack"); Assert.Equal(CombatActions.Attack, (await input)!.Action);
        var move = ui.WaitForCombatAction(); ui.Render(); ui.MoveCombat(new(null, new(5, 6)));
        Assert.Equal(new GridPosition(5, 6), (await move)!.MoveTarget);
    }

    [Fact]
    public async Task EmberFire_DoesNotLoseTheStartingTabAction()
    {
        var ctx = World(); var ui = new CliUi(ctx); ctx.Ui = ui;
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>()!;
        fire.AddFuel(0.01, FuelType.Kindling); fire.IgniteAll();
        for (int i = 0; i < 100 && !fire.HasEmbers; i++) fire.Update(1);
        Assert.True(fire.HasEmbers);
        var task = ui.ShowFire(null);
        var frame = ui.Render();
        Assert.Contains("Start Fire", frame.Lines); Assert.Contains("Tend Fire", frame.Lines);
        ui.Render(frame.Controls.Single(c => c.Id.EndsWith("/start_fire")).Id);
        Assert.True(task.IsCompleted);
        Assert.Equal(FireAction.StartFire, (await task)!.Action);
    }

    [Fact]
    public async Task Transfer_UseScreenCallbacks()
    {
        var ctx = World(); var ui = new CliUi(ctx); ctx.Ui = ui;
        ctx.Inventory.Add(Resource.Stone, 1);
        ctx.Inventory.Add(Resource.Stone, 0.5);
        ctx.Inventory.Add(Resource.Stone, 2);
        int stoneCount = ctx.Inventory.Count(Resource.Stone);
        double stoneWeight = ctx.Inventory.Weight(Resource.Stone);
        var storage = new Inventory();
        var transfer = ui.ShowTransfer(storage, "Test cache");
        var stone = ui.Render().Controls.Single(c => c.Id.Contains("PlayerInv/") && c.Label.Contains("Stone"));
        ui.Render(stone.Id); Assert.Equal(stoneCount, storage.Count(Resource.Stone));
        Assert.Equal(0, ctx.Inventory.Count(Resource.Stone));
        Assert.Equal(stoneWeight, storage.Weight(Resource.Stone));
        var back = ui.Render().Controls.Single(c => !c.Id.Contains("PlayerInv/") && c.Label.Contains("Stone"));
        ui.Render(back.Id); Assert.Equal(0, storage.Count(Resource.Stone));
        Assert.Equal(stoneCount, ctx.Inventory.Count(Resource.Stone));
        Assert.Equal(stoneWeight, ctx.Inventory.Weight(Resource.Stone));
        ui.Close(); await transfer;
    }

    [Fact]
    public async Task Crafting_UseScreenCallbacks()
    {
        var ctx = World(); var ui = new CliUi(ctx); ctx.Ui = ui;
        var crafting = ui.ShowCrafting();
        ui.Render(ui.Render().Controls.Single(c => c.Kind == "text").Id, "knife");
        var recipe = ui.Render().Controls.First(c => c.Kind == "select" && c.Label.StartsWith("Stone Knife"));
        ui.Render(recipe.Id);
        Assert.Contains(ui.Render().Lines, l => l.Contains("Stone", StringComparison.OrdinalIgnoreCase));
        // Supplying resources exposes the real readiness check and commit control.
        foreach (var r in Enum.GetValues<Resource>()) ctx.Inventory.Add(r, 5);
        var knapping = new text_survival.Crafting.NeedCraftingSystem().AllOptions.Single(o => o.Id == "knapping-stone");
        ctx.Inventory.Tools.Add(knapping.GearFactory!(knapping.Durability));
        var ready = ui.Render().Controls.First(c => c.Enabled && c.Label.StartsWith("Make" ) && !c.Id.Contains("Actions"));
        ui.Render(ready.Id);
        Assert.True(crafting.IsCompleted);
        Assert.NotNull(await crafting);
    }

    [Fact]
    public async Task Butchering_RequiresACuttingToolForFullProcessing()
    {
        var ctx = World(); var ui = new CliUi(ctx);
        var animal = AnimalFactory.FromType(AnimalType.Wolf, ctx.CurrentLocation, ctx.Map!)!;
        var task = ui.SelectButcherMode(new CarcassFeature(animal), ["A warning"], false);
        Assert.DoesNotContain(ui.Render().Controls, c => c.Label.StartsWith("Full Processing"));
        Assert.Contains(ui.Render().Lines, l => l.Contains("A warning"));
        Click(ui, "Cancel"); Assert.Null(await task);
        task = ui.SelectButcherMode(new CarcassFeature(animal), [], true);
        var full = ui.Render().Controls.Single(c => c.Label.StartsWith("Full Processing"));
        ui.Render(full.Id); Assert.Equal("full", await task);
    }

    [Fact]
    public void HiddenTiles_DoNotExposeNamesFeaturesOrAnimals()
    {
        var ctx = World();
        var hidden = ctx.Map!.AllLocations.First(l => l.Visibility == TileVisibility.Unexplored);
        string detail = JsonSerializer.Serialize(Observations.Inspect(ctx, ctx.Map.GetPosition(hidden)));
        Assert.DoesNotContain("Lines", detail);
        Assert.DoesNotContain("animals", detail);
        Assert.DoesNotContain($"\"{hidden.Name}\"", detail);
        Assert.Contains("Out of bounds", JsonSerializer.Serialize(Observations.Inspect(ctx, new(-1, -1))));
    }

    [Fact]
    public void Saves_AreIsolatedAndOnlyAllowedAtMapDecisions()
    {
        string id = Id();
        using var session = new CliSession(World(), id);
        try
        {
            session.Execute("save");
            var (loaded, error) = SaveManager.Load(id);
            Assert.Null(error); Assert.NotNull(loaded); Assert.Equal(id, loaded.SessionId);
            Assert.Equal(session.Context.GameTime, loaded.GameTime);
            session.Execute("act Inventory");
            Assert.Throws<ArgumentException>(() => session.Execute("save"));
        }
        finally { SaveManager.DeleteSave(id); }
        Assert.Throws<ArgumentException>(() => new CliSession(World(), "../save"));
    }
}
