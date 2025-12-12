using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;
using text_survival.Actions.Expeditions;

namespace text_survival.Actions;

public class ExpeditionRunner(GameContext ctx)
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

    private readonly GameContext ctx = ctx;
    public void RunExpedition(ExpeditionType expeditionType)
    {
        if (expeditionType == ExpeditionType.Forage)
        {
            RunForageExpedition();
        }

        throw new NotImplementedException("Expedition Type Not Implemented");
    }
    public void RunForageExpedition()
    {
        // select desitination
        var locations = ctx.CurrentLocation.GetNearbyLocations().Where(x => IsLocationValidForExpeditionType(x, ExpeditionType.Forage));
        var choice = new Choice<Expedition>("Where would you like to go?");
        foreach (Location location in locations)
        {
            int minutes = MapController.CalculateLocalTravelTime(ctx.CurrentLocation, location);
            // todo - add a builder that handles the logic for this to make it more dynamic
            Expedition exp = new Expedition(ctx.CurrentLocation, location, ExpeditionType.Forage, minutes, workTimeMinutes: 30, timeVarianceMinutes: 10, exposureFactor: 1, detectionRisk: .1);
            string label = $"{location.Name} (~{exp.TotalEstimatedTimeMinutes} min) - Danger: {exp.DangerLevel()}";
            choice.AddOption(label, exp);
        }
        Expedition expedition = choice.GetPlayerChoice();

        // show plan
        DisplayExpeditionPreview(expedition, ctx.Camp.GetFireMinutesRemaining());
        Output.WriteLine("Do you want to proceed with this plan?");
        if (!Input.ReadYesNo())
        {
            Output.WriteLine("You change your mind");
            return;
        }

        // start
        Output.WriteLine($"You have started the expedition: {expedition.Type}.");
        expedition.AdvancePhase(); // NotStarted -> TravelingOut

        // main loop
        while (!expedition.IsComplete)
        {
            ExpeditionProcessor runner = new ExpeditionProcessor();
            var result = runner.RunExpedtionSegment(expedition, ctx.player);
            DisplayQueuedExpeditionLogs(expedition);

            // show check if the user wants to turn back early
            if (expedition.CurrentPhase == ExpeditionPhase.TravelingOut || expedition.CurrentPhase == ExpeditionPhase.Working)
            {
                Output.WriteLine("Continue?");
                if (!Input.ReadYesNo())
                    CancelExpedition(expedition);
            }
        }

        // complete expedition
        DisplayQueuedExpeditionLogs(expedition);
        return;
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
        string fireMessage = ExpeditionProcessor.GetFireMarginMessage(fireTime);
        Output.WriteLine(fireMessage);
        Output.WriteLine();
    }

    private static void DisplayQueuedExpeditionLogs(Expedition exp)
    {
        exp.GetFlushLogs().ForEach(s => Output.WriteLine(s));
    }


    private void CancelExpedition(Expedition expedition)
    {
        Output.WriteLine("Are you sure you want to cancel the expedition and return to camp?");
        Output.Write("This will end your current expedition and you will start to return to camp.");
        bool confirm = Input.ReadYesNo();
        if (!confirm)
        {
            Output.WriteLine("You decide to keep pushing forward.");
            return;
        }
        expedition.CancelExpedition();
        Output.WriteLine("You decide it's best to head back. You start returning to camp.");
    }

}