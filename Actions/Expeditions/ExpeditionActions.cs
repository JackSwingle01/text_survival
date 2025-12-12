using System.Net.Http.Headers;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

public static class ExpeditionActions
{
    /// <summary>
    /// Expeditions follow the following flow:
    /// Select Expedition Type (Forage, Hunt, Explore ...)
    /// Select Location (filtered by available resources)
    /// Show Plan (confirm Y/N)
    /// Process Chunk:
    ///     -> Make progress on stage 
    ///         -> Event gets triggered
    ///             -> event specific logic
    ///     -> Continue or Abort? (abort changes phase to HeadBack but you still have to complete progress to camp)
    /// Complete 
    ///     -> show results
    /// Return to camp    
    ///     
    /// </summary>
    /// <returns></returns>


    public static IGameAction SelectExpedition(ExpeditionType expeditionType)
    {
        return ActionBuilderExtensions.CreateAction(expeditionType.ToString())
            .ThenShow(ctx => GetDestinations(ctx, expeditionType))
            .Build();
    }

    private static List<IGameAction> GetDestinations(GameContext ctx, ExpeditionType expeditionType)
    {
        var locations = ctx.CurrentLocation.GetNearbyLocations();
        var actions = locations.Select(loc => CreateDestinationAction(loc, expeditionType, ctx)).ToList();
        if (actions.Count == 0)
        {
            actions = [ActionFactory.Common.Return("No valid locaitons to go to.")];
        }
        return actions;
    }

    private static IGameAction CreateDestinationAction(Location location, ExpeditionType type, GameContext ctx)
    {
        int minutes = MapController.CalculateLocalTravelTime(ctx.CurrentLocation, location);
        // todo - add a builder that handles the logic for this to make it more dynamic
        Expedition expedition = new Expedition(ctx.CurrentLocation, location, type, minutes, workTimeMinutes: 30, timeVarianceMinutes: 10, exposureFactor: 1, detectionRisk: .1);
        string label = $"{location.Name} (~{expedition.TotalEstimatedTimeMinutes} min) - Danger: {expedition.DangerLevel()}";
        return ActionBuilderExtensions.CreateAction(label)
                                    .When(_ => IsLocationValidForExpeditionType(location, type))
                                    .ThenShow(_ => [ShowExpedtionPlan(expedition)])
                                    .Build();
    }

    private static bool IsLocationValidForExpeditionType(Location location, ExpeditionType expeditionType)
    {
        if (expeditionType == ExpeditionType.Forage)
        {
            if (location.GetFeature<ForageFeature>() is not null)
                return true;
            return false;
        }
        else if (expeditionType == ExpeditionType.Hunt)
        {
            return false;
        }
        else if (expeditionType == ExpeditionType.Gather)
        {
            if (location.GetFeature<HarvestableFeature>() is not null)
                return true;
            return false;
        }
        else if (expeditionType == ExpeditionType.Explore)
        {
            return true;
        }
        else
        {
            return false;
        }

    }

    public static IGameAction ShowExpedtionPlan(Expedition expedition)
    {
        return ActionBuilderExtensions.CreateAction("View Expedition Plan")
            .ShowMessage("\nYou stop to plan out your expedition.")
            .Do((ctx) =>
            {
                DisplayExpeditionPreview(expedition, ctx.Camp.GetFireMinutesRemaining());
            })
            .WithPrompt("Do you wish to proceed with this expedition?")
            .ThenShow(x => [ActionFactory.Common.Return("Never mind"), StartExpedition(expedition)])
            .Build();
    }

    public static void DisplayExpeditionPreview(Expedition expedition, double FireMinutesRemaining)
    {
        Output.WriteLine($"Expedition Type: ", expedition.Type);
        Output.WriteLine($"To: ", expedition.endLocation);
        Output.WriteLine($"Estimated Travel Time: ~{expedition.TravelTimeMinutes} min each way");
        Output.WriteLine($"Estimated Work Time: ~{expedition.WorkTimeMinutes} min");
        Output.WriteLine($"Total Estimated Time: ~{expedition.TotalEstimatedTimeMinutes} min round trip");
        string dangerLevel = expedition.DangerLevel();
        if (dangerLevel != "Low")
            Output.WriteWarning($"Danger Level: {dangerLevel}");
        double fireTime = FireMinutesRemaining - expedition.TotalEstimatedTimeMinutes;
        string fireMessage = ExpeditionRunner.GetFireMarginMessage(fireTime);
        Output.WriteLine(fireMessage);
        Output.WriteLine();
    }

    public static IGameAction StartExpedition(Expedition expedition)
    {
        return ActionBuilderExtensions.CreateAction("Start Expedition")
            .Do((ctx) =>
            {
                Output.WriteLine($"You have started the expedition: {expedition.Type}.");
                expedition.AdvancePhase(); // NotStarted -> TravelingOut
            })
            .ThenShow(x => [ProcessExpeditionSegment(expedition)])
            .Build();
    }

    public static IGameAction ProcessExpeditionSegment(Expedition expedition)
    {
        return ActionBuilderExtensions.CreateAction($"Continue {expedition.GetPhaseDisplayName()}")
            .Do(ctx =>
            {
                ExpeditionRunner runner = new ExpeditionRunner();
                var result = runner.RunExpedtionSegment(expedition, ctx.player);
                DisplayQueuedExpeditionLogs(expedition);
            })
            .ThenShow(ctx => GetNextExpeditionActions(expedition))
            .Build();
    }
    private static void DisplayQueuedExpeditionLogs(Expedition exp)
    {
        exp.GetFlushLogs().ForEach(s => Output.WriteLine(s));
    }

    public static IGameAction CancelExpedition(Expedition expedition)
    {
        return ActionBuilderExtensions.CreateAction("Head Back to Camp")
            .When(x => expedition.CurrentPhase == ExpeditionPhase.TravelingOut || expedition.CurrentPhase == ExpeditionPhase.Working)
            .WithPrompt("Are you sure you want to cancel the expedition and return to camp?")
            .Do(ctx =>
            {
                Output.Write("This will end your current expedition and you will start to return to camp.");
                bool confirm = Input.ReadYesNo();
                if (!confirm)
                {
                    Output.WriteLine("You decide to continue your expedition.");
                    return;
                }
                expedition.CancelExpedition();
                Output.WriteLine("You have cancelled the expedition and are returning to camp.");
            })
            .ThenShow(x => GetNextExpeditionActions(expedition))
            .Build();
    }

    private static List<IGameAction> GetNextExpeditionActions(Expedition expedition)
    {
        // todo check for events
        List<IGameAction> actions = [];
        if (!expedition.IsComplete)
        {
            actions.Add(ProcessExpeditionSegment(expedition));
            actions.Add(CancelExpedition(expedition));
        }
        else
        {
            actions.Add(ActionFactory.Common.Return("Return to Camp"));
        }
        return actions;
    }
}