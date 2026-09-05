using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actions.Handlers;
using text_survival.Persistence;
using text_survival.UI;

namespace text_survival.Actions;

public enum CampAction
{
    Wait,
    TendFire,
    StartFire,
    Food,
    Inventory,
    Crafting,
    DiscoveryLog,
    NPCs,
    Storage,
    CuringRack,
    Sleep,
    MakeCamp,
    PitchTent,
    PackTent,
    TreatWounds
}

public class Choice<T>(string? prompt = null)
{
    public string? Prompt = prompt;
    private readonly Dictionary<string, T> options = [];

    public void AddOption(string label, T item)
    {
        options[label] = item;
    }

    public async Task<T> GetPlayerChoice(GameContext ctx)
    {
        if (options.Count == 0)
            throw new InvalidOperationException($"No choices available for prompt: {Prompt ?? "Choose:"}");

        string choice = await ctx.Ui.Select(Prompt ?? "Choose:", options.Keys.ToList(), label => label);
        return options[choice];
    }
}

/// <summary>
/// The action loop. Waits for what the player wants to do, does it, repeats until they
/// die, cross the pass, or quit. It never draws - the frame loop does that.
/// </summary>
public class GameRunner(GameContext ctx)
{
    private readonly GameContext ctx = ctx;

    /// <summary>Serialising the world costs a visible hitch, so it happens on a clock, not per action.</summary>
    private const double SaveIntervalSeconds = 120;
    private DateTime _lastSaveUtc = DateTime.UtcNow;

    /// <summary>Returns true if the player asked to start a new run.</summary>
    public async Task<bool> RunAsync()
    {
        // The run ends when the player dies or reaches the far side of the pass. Both are
        // derived from state - there is no separate "game over" flag to keep in sync.
        while (ctx.player.IsAlive && !ctx.CurrentLocation.IsCrossingExit)
        {
            SaveIfDue();
            CheckFireWarning();
            await ctx.ShowNotices();

            if (ctx.HasPendingEncounter)
            {
                await ctx.HandlePendingEncounter();
                continue;
            }

            if (ctx.player.GetCapacities().Moving <= 0)
            {
                await HandleIncapacitation();
                continue;
            }

            var action = await ctx.Ui.WaitForPlayerAction();

            switch (action)
            {
                case PlayerAction.Quit:
                    return false;
                case PlayerAction.Travel travel:
                    await new TravelRunner(ctx).TravelTo(travel.X, travel.Y);
                    break;
                case PlayerAction.Camp camp:
                    await ProcessCampAction(camp.Action);
                    break;
                case PlayerAction.Work work:
                    await ExecuteWork(work.Strategy);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled player action: {action.GetType().Name}");
            }
        }

        if (!ctx.player.IsAlive)
            return await HandleDeath();

        return await HandleVictory();
    }

    private void SaveIfDue()
    {
        if ((DateTime.UtcNow - _lastSaveUtc).TotalSeconds < SaveIntervalSeconds)
            return;

        _lastSaveUtc = DateTime.UtcNow;
        var (saved, saveError) = SaveManager.Save(ctx);
        if (!saved)
            Console.WriteLine($"[GameRunner] Save failed: {saveError}");
    }

    private async Task<bool> HandleDeath()
    {
        GameDisplay.AddDanger(ctx, "Your vision fades to black as you collapse...");
        GameDisplay.AddDanger(ctx, "You have died.");

        string choice = await ctx.Ui.Choose(
            "You have died. What would you like to do?",
            [("new_game", "Start New Game"), ("quit", "Quit to Desktop")]);

        // The run is over - the save must not resume past the ending.
        SaveManager.DeleteSave(ctx.SessionId);

        return choice == "new_game";
    }

    /// <summary>
    /// The player crossed the pass. Ends the run the same way death does: a closing
    /// screen, the save deleted, and the choice to start again.
    /// </summary>
    private async Task<bool> HandleVictory()
    {
        GameDisplay.ClearNarrative(ctx);
        GameDisplay.AddSuccess(ctx, "You made it.");
        GameDisplay.AddNarrative(ctx, "The pass is behind you now.");
        GameDisplay.AddNarrative(ctx, "Below, the far valley stretches green and sheltered.");
        GameDisplay.AddNarrative(ctx, "Smoke rises from distant fires. Your tribe is there.");
        GameDisplay.AddNarrative(ctx, "You survived.");

        string summary =
            $"You crossed the pass.\n\n" +
            $"Days survived: {ctx.DaysSurvived}\n" +
            $"Season: {ctx.Weather.GetSeasonLabel()}";

        string choice = await ctx.Ui.Choose(summary,
            [("new_game", "Start New Game"), ("quit", "Quit to Desktop")]);

        SaveManager.DeleteSave(ctx.SessionId);

        return choice == "new_game";
    }

    private async Task ProcessCampAction(CampAction action)
    {
        switch (action)
        {
            case CampAction.Wait:
                await Wait();
                break;
            case CampAction.TendFire:
            case CampAction.StartFire:
                await FireHandler.ManageFire(ctx);
                break;
            case CampAction.Food:
                await RunFood();
                break;
            case CampAction.Inventory:
                await ctx.Ui.ShowInventory();
                break;
            case CampAction.Crafting:
                await RunCrafting();
                break;
            case CampAction.DiscoveryLog:
                await ctx.Ui.ShowDiscoveryLog();
                break;
            case CampAction.NPCs:
                await RunNPCs();
                break;
            case CampAction.Storage:
                await RunStorage();
                break;
            case CampAction.CuringRack:
                await CuringRackHandler.UseCuringRack(ctx);
                break;
            case CampAction.Sleep:
                await Sleep();
                break;
            case CampAction.MakeCamp:
                await CampHandler.MakeCamp(ctx, ctx.CurrentLocation);
                break;
            case CampAction.PitchTent:
                await CampHandler.DeployTent(ctx, CampHandler.GetDeployableTent(ctx)!);
                break;
            case CampAction.PackTent:
                await CampHandler.PackTent(ctx);
                break;
            case CampAction.TreatWounds:
                await TreatmentHandler.ApplyTreatment(ctx);
                break;
            default:
                throw new InvalidOperationException($"Unhandled camp action: {action}");
        }
    }

    private async Task ExecuteWork(IWorkStrategy strategy)
    {
        var work = new WorkRunner(ctx);
        var result = await work.Execute(ctx.CurrentLocation, strategy);

        if (result == null) return;

        if (result.DiscoveredLocation != null)
        {
            GameDisplay.AddNarrative(ctx, $"Discovered: {result.DiscoveredLocation.Name}");
            if (await WorkRunner.PromptTravelToDiscovery(ctx, result.DiscoveredLocation))
                await new TravelRunner(ctx).TravelToLocation(result.DiscoveredLocation);
        }

        // Time during a hunt is tracked by combat itself.
        if (result.FoundAnimal != null)
            await HuntRunner.Run(result.FoundAnimal, ctx);
    }

    private async Task RunCrafting()
    {
        var recipe = await ctx.Ui.ShowCrafting();
        if (recipe == null) return;

        await CraftingHandler.Craft(ctx, recipe);
    }

    private async Task RunFood()
    {
        var pending = await ctx.Ui.ShowFood();
        if (pending == null) return;

        await CookingHandler.RunPendingAction(ctx, pending);
    }

    private async Task RunNPCs()
    {
        bool anyoneHere = ctx.NPCs.Any(n => n.CurrentLocation == ctx.CurrentLocation);
        if (!anyoneHere)
        {
            GameDisplay.AddNarrative(ctx, "There's nobody here.");
            return;
        }

        await ctx.Ui.ShowNPCs();
    }

    private async Task RunStorage()
    {
        var storage = ctx.Camp.GetFeature<CacheFeature>();
        if (storage == null)
        {
            GameDisplay.AddWarning(ctx, "There's no storage at camp.");
            return;
        }

        await ctx.Ui.ShowTransfer(storage.Storage, "CAMP STORAGE");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FIRE
    // ═══════════════════════════════════════════════════════════════════════════

    private void CheckFireWarning()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null || (!fire.IsActive && !fire.HasEmbers))
            return;

        // Don't warn when fire is growing
        string phase = fire.GetFirePhase();
        if (phase == "Igniting" || phase == "Building")
            return;

        int minutes = (int)(fire.BurningHoursRemaining * 60);

        if (minutes <= 5)
            GameDisplay.AddDanger(ctx, $"Your fire will die in {minutes} minutes!");
        else if (minutes <= 15)
            GameDisplay.AddWarning(ctx, $"Fire burning low - {Utils.FormatFireTime(minutes)} remaining.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TIME
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task Sleep()
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool hasFire = fire != null && (fire.IsActive || fire.HasEmbers);
        int fireMinutes = hasFire ? (int)(fire!.TotalHoursRemaining * 60) : 0;

        int hours = await ctx.Ui.ReadInt("How many hours would you like to sleep?", 1, 8, allowCancel: true);
        if (hours < 0)
        {
            GameDisplay.AddNarrative(ctx, "You decide to stay awake.");
            return;
        }

        int sleepMinutes = hours * 60;

        if (hasFire && fireMinutes < sleepMinutes)
        {
            int shortfall = (sleepMinutes - fireMinutes) / 60;
            string warning = $"Your fire will die {shortfall} hour{(shortfall != 1 ? "s" : "")} before you wake. You'll freeze without it.\n\nSleep anyway?";

            if (!await ctx.Ui.Confirm(warning))
            {
                GameDisplay.AddNarrative(ctx, "You decide to stay awake.");
                return;
            }
        }
        else if (!hasFire)
        {
            if (!await ctx.Ui.Confirm("There's no fire. You'll freeze to death in your sleep.\n\nSleep without fire?"))
            {
                GameDisplay.AddNarrative(ctx, "You decide to stay awake.");
                return;
            }
        }

        using var view = ctx.Ui.BeginProgress(ProgressKind.Activity, "Sleeping...");

        // Recovery rides on the sleeping activity itself, so one pass covers the whole night.
        var (slept, interrupted) = await Pacing.PassTime(ctx, sleepMinutes, ActivityType.Sleeping, view);

        if (slept > 0)
        {
            string duration = Utils.FormatFireTime(slept);
            GameDisplay.AddNarrative(ctx, interrupted
                ? $"You wake after {duration}."
                : $"You slept for {duration}.");
        }
    }

    private async Task Wait()
    {
        // Resting has an event multiplier of 0, so nothing can interrupt these five minutes.
        using var view = ctx.Ui.BeginProgress(ProgressKind.Activity, "Resting");
        await Pacing.PassTime(ctx, 5, ActivityType.Resting, view);
    }

    private async Task HandleIncapacitation()
    {
        GameDisplay.AddNarrative(ctx, "You cannot move. All you can do now is wait.");

        const int chunkMinutes = 5;
        const double recoveryThreshold = 0.01;  // >1% moving capacity to recover

        while (ctx.player.IsAlive)
        {
            if (ctx.player.GetCapacities().Moving > recoveryThreshold)
            {
                GameDisplay.AddNarrative(ctx, "You can move again.");
                return;
            }

            using var view = ctx.Ui.BeginProgress(ProgressKind.Activity, "Incapacitated");
            await Pacing.PassTime(ctx, chunkMinutes, ActivityType.Incapacitated, view);
        }
    }
}
