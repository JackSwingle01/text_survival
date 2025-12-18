
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

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
    public void RunForageExpedition()
    {
        // select destination
        var locations = GetGatherableLocations();
        GameDisplay.Render(ctx);
        var locChoice = new Choice<Location>("Where would you like to go?");
        foreach (Location location in locations)
        {
            var path = TravelProcessor.FindPath(ctx.CurrentLocation, location);
            if (path == null) continue;
            var estimate = TravelProcessor.GetPathMinutes(path, ctx.player);
            string label = $"{location.Name} (~{estimate} min)";
            locChoice.AddOption(label, location);
        }
        Location destination = locChoice.GetPlayerChoice();
        var travelPath = TravelProcessor.BuildRoundTripPath(ctx.CurrentLocation, destination)!;
        var travelEstimate = TravelProcessor.GetPathMinutes(travelPath, ctx.player);

        // choose work time
        GameDisplay.Render(ctx);
        var workTimeChoice = new Choice<int>("How long should you forage?");
        workTimeChoice.AddOption("Quick gather - 15 min", 15);
        workTimeChoice.AddOption("Standard search - 30 min", 30);
        workTimeChoice.AddOption("Thorough search - 60 min", 60);
        int workTime = workTimeChoice.GetPlayerChoice();

        Expedition expedition = new Expedition(travelPath, travelPath.IndexOf(destination), ctx.player, ExpeditionType.Gather, workTime);

        ExpeditionMainLoop(expedition);
    }

    // public void RunHarvestExpedition()
    // {
    //     // select destination
    //     var locations = TravelProcessor.GetReachableSites(ctx.CurrentLocation).Where(x => IsLocationValidForExpeditionType(x, ExpeditionType.Gather));
    //     var locChoice = new Choice<Location>("Where would you like to go?");
    //     foreach (Location location in locations)
    //     {
    //         var path = TravelProcessor.FindPath(ctx.CurrentLocation, location);
    //         if (path == null) continue;
    //         var estimate = TravelProcessor.GetPathMinutes(path, ctx.player);
    //         // todo explored info check
    //         var harvestables = location.Features
    //             .OfType<HarvestableFeature>()
    //             .Where(f => f.IsDiscovered)
    //             .Select(h => h.DisplayName)
    //             .ToList();
    //         string label = $"{location.Name} (~{estimate} min) - {string.Join(", ", harvestables)}";
    //         locChoice.AddOption(label, location);
    //     }
    //     Location destination = locChoice.GetPlayerChoice();
    //     var travelPath = TravelProcessor.BuildRoundTripPath(ctx.CurrentLocation, destination)!;
    //     var travelEstimate = TravelProcessor.GetPathMinutes(travelPath, ctx.player);
    //     int workTime = 30;

    //     Expedition expedition = new Expedition(travelPath, travelPath.IndexOf(destination), ctx.player, ExpeditionType.Gather, workTime);

    //     ExpeditionMainLoop(expedition);
    // }

    public List<Location> GetGatherableLocations()
    {
        return ctx.Zone.Graph.All
            .Where(l => l.Explored)
            .Where(l => l != ctx.CurrentLocation)
            .Where(l => l.HasFeature<ForageFeature>() ||
                        l.Features.OfType<HarvestableFeature>().Any(h => h.IsDiscovered))
            .ToList();
    }
    private void ExpeditionMainLoop(Expedition expedition)
    {
        // show preview
        DisplayExpeditionPreview(expedition, ctx.Camp.FireMinutesRemaining);
        GameDisplay.Render(ctx);
        if (!Input.ReadYesNo())
        {
            GameDisplay.AddNarrative("You change your mind.");
            return;
        }

        // start expedition
        GameDisplay.AddNarrative($"You set out to {expedition.Type.ToString().ToLower()}.");
        ctx.Expedition = expedition;

        // main loop
        while (!expedition.IsComplete)
        {
            var result = expedition.RunExpeditionPhase(ctx);
            ctx.Update(result.TimeElapsed);
            DisplayQueuedExpeditionLogs(expedition);

            if (result.Event is not null)
            {
                HandleEvent(result.Event);
                if (expedition.IsComplete) break;
            }

            // Prompt at phase completion
            if (expedition.ReadyToAdvanceLocation() && !expedition.IsComplete)
            {
                if (expedition.CurrentPhase != ExpeditionPhase.TravelingBack && !PromptContinueExpedition(expedition))
                {
                    CancelExpedition();
                }
                else
                {
                    expedition.AdvancePath();
                }
            }
        }

        // end expedition
        DisplayQueuedExpeditionLogs(expedition);
        ctx.Expedition = null;
    }
    private bool PromptContinueExpedition(Expedition expedition)
    {
        GameDisplay.AddNarrative("\n");
        string nextPhase = expedition.GetPhaseDisplayName(expedition.CurrentPhase + 1);
        GameDisplay.AddNarrative($"You have completed {expedition.GetPhaseDisplayName()}. You are about to start {nextPhase}.");
        // // Show fire margin for context
        // double fireRemaining = ctx.Camp.GetFireMinutesRemaining();
        // int returnTime = expedition.TravelOutTimeMinutes;
        // double margin = fireRemaining - returnTime;
        // GameDisplay.AddNarrative($"Fire margin: ~{(int)margin} min if you head back now.");
        GameDisplay.Render(ctx);

        if (expedition.CurrentPhase == ExpeditionPhase.Working)
        {
            // Check if there's reason to stay
            bool canContinue = expedition.Type switch
            {
                ExpeditionType.Gather => expedition.Destination.Features
                    .OfType<HarvestableFeature>()
                    .Any(f => f.IsDiscovered && f.HasAvailableResources()),
                ExpeditionType.Forage => true,
                _ => false
            };

            if (!canContinue)
            {
                GameDisplay.AddNarrative("There's nothing more to do here.");
                return true;
            }

            var choice = new Choice<string>("What do you want to do?");
            choice.AddOption("Keep working (15 more minutes)", "continue");
            choice.AddOption("Head back to camp", "return");
            choice.AddOption("Abort expedition", "abort");

            string result = choice.GetPlayerChoice();
            if (result == "continue")
            {
                expedition.AddWorkTimeMinutes(15);
                return true;
            }
            else if (result == "return")
            {
                GameDisplay.AddNarrative($"You start heading back.");
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            // Traveling phases - simple continue/abort
            GameDisplay.AddNarrative($"\nStart {nextPhase}? (y/n)");
            return Input.ReadYesNo();
        }
    }
    private void HandleEvent(GameEvent evt)
    {
        GameDisplay.AddNarrative("".PadRight(50, '-'));
        GameDisplay.AddNarrative("EVENT:");
        GameDisplay.AddNarrative($"** {evt.Name} **");
        GameDisplay.AddNarrative(evt.Description + "\n");
        GameDisplay.Render(ctx);
        var choice = evt.Choices.GetPlayerChoice();
        GameDisplay.AddNarrative(choice.Description + "\n");
        GameDisplay.AddNarrative("".PadRight(50, '-'));

        var outcome = choice.DetermineResult();
        HandleOutcome(outcome);
    }
    private void HandleOutcome(EventResult outcome)
    {
        GameDisplay.AddNarrative("OUTCOME:");
        GameDisplay.AddNarrative(outcome.Message);

        if (outcome.TimeAddedMinutes != 0)
        {
            GameDisplay.AddNarrative($"(+{outcome.TimeAddedMinutes} minutes)");
            ctx.Update(outcome.TimeAddedMinutes);
            ctx.Expedition?.AddDelayTime(outcome.TimeAddedMinutes);
        }

        if (outcome.NewEffect is not null)
        {
            ctx.player.EffectRegistry.AddEffect(outcome.NewEffect);
        }

        if (outcome.NewItem is not null)
        {
            ctx.player.TakeItem(outcome.NewItem);
            GameDisplay.AddNarrative($"You found: {outcome.NewItem.Name}");
        }

        if (outcome.AbortsExpedition && !ctx.Expedition!.IsComplete)
        {
            ctx.Expedition!.CancelExpedition();
        }
        GameDisplay.AddNarrative("".PadRight(50, '-'));
    }
    private static bool IsLocationValidForExpeditionType(Location location, ExpeditionType expeditionType)
    {
        switch (expeditionType)
        {
            case ExpeditionType.Forage:
                if (location.HasFeature<ForageFeature>())
                    return true;
                return false;
            case ExpeditionType.Hunt:
                return false;
            case ExpeditionType.Gather:
                if (location.HasFeature<HarvestableFeature>())
                    return true;
                return false;
            case ExpeditionType.Explore:
                return true;
            default:
                return false;
        }
    }

    public void DisplayExpeditionPreview(Expedition expedition, double FireMinutesRemaining)
    {
        int travelTime = TravelProcessor.GetPathMinutes(expedition.Path, ctx.player);
        GameDisplay.AddNarrative("".PadRight(50, '-'));
        GameDisplay.AddNarrative("PLAN:");
        GameDisplay.AddNarrative($"{expedition.Type.ToString().ToUpper()} - {expedition.Destination.Name}\n");
        GameDisplay.AddNarrative($"It's about a {travelTime / 2} minute walk each way.");
        GameDisplay.AddNarrative($"Once you get there it's {expedition.WorkTimeMinutes} minutes of {expedition.GetPhaseDisplayName(ExpeditionPhase.Working).ToLower()}.");
        GameDisplay.AddNarrative($"A {travelTime + expedition.WorkTimeMinutes} minute round trip if all goes well.\n");

        GameDisplay.AddNarrative(GetLocationNotes(expedition.Destination));
        // GameDisplay.AddNarrative(expedition.GetSummaryNotes() + '\n');

        double fireTime = FireMinutesRemaining - TravelProcessor.GetPathMinutes(expedition.Path, ctx.player);
        string fireMessage = GetFireMarginMessage(fireTime);
        if (FireMinutesRemaining > 0)
            GameDisplay.AddNarrative($"The fire has about {FireMinutesRemaining} minutes left.");
        GameDisplay.AddNarrative(fireMessage);
        GameDisplay.AddNarrative("\n");
        GameDisplay.AddNarrative("Do you want to proceed with this plan? (y/n)");
        GameDisplay.AddNarrative("".PadRight(50, '-'));
    }

    private static string GetLocationNotes(Location location)
    {
        string notes = "";
        var harvestables = location.Features
                .OfType<HarvestableFeature>()
                .Where(f => f.IsDiscovered)
                .ToList();
        if (harvestables.Count != 0)
        {
            notes += "The location has stuff to harvest:\n";
            foreach (var h in harvestables)
            {
                notes += $"{h.DisplayName} - {h.Description}\n";
            }
        }

        return notes;
    }

    private static void DisplayQueuedExpeditionLogs(Expedition exp)
    {
        exp.GetFlushLogs().ForEach(s => GameDisplay.AddNarrative(s));
    }


    private void CancelExpedition()
    {
        if (ctx.Expedition is null)
            throw new InvalidOperationException("Can't cancel expedition out of context");

        GameDisplay.AddNarrative("Are you sure you want to cancel the expedition and return to camp?");
        GameDisplay.AddNarrative("This will end your current expedition and you will start to return to camp. (y/n)");
        GameDisplay.Render(ctx);
        bool confirm = Input.ReadYesNo();
        if (!confirm)
        {
            GameDisplay.AddNarrative("You decide to keep pushing forward.");
            return;
        }
        ctx.Expedition.CancelExpedition();
        GameDisplay.AddNarrative("You decide it's best to head back. You start returning to camp.");
    }

    public static string GetFireMarginMessage(double marginMinutes)
    {
        if (double.IsNegativeInfinity(marginMinutes)) return "You have no fire.";
        if (marginMinutes < 0) return "The fire probably won't last until you return. Make sure you know what you're doing.";
        if (marginMinutes < 15) return "The fire should last until you're back. But it will be tight.";
        if (marginMinutes < 30) return "You should have enough time to get back to the fire as long as you don't have any delays.";
        if (marginMinutes < 60) return "You have a decent fire, it should last until you get back with a good margin.";
        return "You have plenty of time left on the fire. You should be good to go.";
    }

}