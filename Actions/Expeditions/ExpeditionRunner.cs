
using text_survival.Actors.NPCs;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions;

public class ExpeditionRunner(GameContext ctx)
{
    private record TickResult(int MinutesElapsed, GameEvent? Event);
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
            var estimate = TravelProcessor.GetPathMinutes(path, ctx.player, ctx.Inventory);
            string label = $"{location.Name} (~{estimate} min)";
            locChoice.AddOption(label, location);
        }
        Location destination = locChoice.GetPlayerChoice();
        var travelPath = TravelProcessor.BuildRoundTripPath(ctx.CurrentLocation, destination)!;
        var travelEstimate = TravelProcessor.GetPathMinutes(travelPath, ctx.player, ctx.Inventory);

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

    public List<Location> GetGatherableLocations()
    {
        return ctx.Zone.Graph.All
            .Where(l => l.Explored)
            .Where(l => l != ctx.CurrentLocation)
            .Where(l => l.HasFeature<ForageFeature>() ||
                        l.Features.OfType<HarvestableFeature>().Any(h => h.IsDiscovered))
            .ToList();
    }

    public void RunHuntExpedition()
    {
        var locations = GetHuntableLocations();
        if (locations.Count == 0)
        {
            GameDisplay.AddNarrative("You don't know of any hunting grounds with game.");
            return;
        }

        GameDisplay.Render(ctx);
        var locChoice = new Choice<Location>("Where would you like to hunt?");
        foreach (Location location in locations)
        {
            var path = TravelProcessor.FindPath(ctx.CurrentLocation, location);
            if (path == null) continue;
            var estimate = TravelProcessor.GetPathMinutes(path, ctx.player, ctx.Inventory);
            string hint = GetHuntingHint(location);
            string label = $"{location.Name} (~{estimate} min) - {hint}";
            locChoice.AddOption(label, location);
        }
        Location destination = locChoice.GetPlayerChoice();
        var travelPath = TravelProcessor.BuildRoundTripPath(ctx.CurrentLocation, destination)!;

        // Hunt expeditions are open-ended (workTime = 0, we'll track manually)
        Expedition expedition = new Expedition(travelPath, travelPath.IndexOf(destination), ctx.player, ExpeditionType.Hunt, 0);

        ExpeditionMainLoop(expedition);
    }

    public List<Location> GetHuntableLocations()
    {
        return ctx.Zone.Graph.All
            .Where(l => l.Explored)
            .Where(l => l != ctx.CurrentLocation)
            .Where(l => l.HasFeature<AnimalTerritoryFeature>())
            .ToList();
    }

    private static string GetHuntingHint(Location location)
    {
        var feature = location.GetFeature<AnimalTerritoryFeature>();
        if (feature == null) return "no signs of game";
        return feature.GetDescription();
    }

    private void RunHuntingWorkPhase(Expedition expedition)
    {
        var destination = expedition.Destination;
        var feature = destination.GetFeature<AnimalTerritoryFeature>();
        int totalHuntingMinutes = 0;

        if (feature == null)
        {
            GameDisplay.AddNarrative("There's no game to be found here.");
            return;
        }

        while (true)
        {
            GameDisplay.AddNarrative($"\nYou scan the area for signs of game...");
            GameDisplay.AddNarrative($"Game density: {feature.GetQualityDescription()}");
            GameDisplay.Render(ctx);

            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Search for game (~15 min)", "search");
            choice.AddOption("Done hunting - head back", "done");

            string action = choice.GetPlayerChoice();
            if (action == "done")
                break;

            // Search costs time
            int searchTime = 15;
            ctx.Update(searchTime);
            totalHuntingMinutes += searchTime;

            Animal? found = feature.SearchForGame(searchTime);
            if (found == null)
            {
                GameDisplay.AddNarrative("You find no game. The area seems quiet.");
                continue;
            }

            GameDisplay.AddNarrative($"You spot a {found.Name}!");
            GameDisplay.Render(ctx);

            var huntChoice = new Choice<bool>("Do you want to stalk it?");
            huntChoice.AddOption($"Stalk the {found.Name}", true);
            huntChoice.AddOption("Let it go", false);

            if (!huntChoice.GetPlayerChoice())
                continue;

            int huntMinutes = RunSingleHunt(found, destination, expedition);
            totalHuntingMinutes += huntMinutes;
            ctx.Update(huntMinutes);

            // Record successful hunt if animal was killed
            if (!found.IsAlive)
            {
                feature.RecordSuccessfulHunt();
            }
        }

        expedition.AddLog($"You spent {totalHuntingMinutes} minutes hunting.");
    }

    private int RunSingleHunt(Animal target, Location location, Expedition expedition)
    {
        ctx.player.stealthManager.StartHunting(target);
        int minutesSpent = 0;

        while (ctx.player.stealthManager.IsHunting && ctx.player.stealthManager.IsTargetValid())
        {
            GameDisplay.AddNarrative($"\nDistance: {target.DistanceFromPlayer:F0}m | State: {target.State}");
            GameDisplay.Render(ctx);

            var choice = new Choice<string>("What do you do?");
            choice.AddOption("Approach carefully", "approach");
            choice.AddOption("Assess target", "assess");
            choice.AddOption("Give up this hunt", "stop");

            string action = choice.GetPlayerChoice();

            switch (action)
            {
                case "approach":
                    bool success = ctx.player.stealthManager.AttemptApproach(location);
                    minutesSpent += 7;

                    if (success)
                        ctx.player.Skills.GetSkill("Hunting").GainExperience(1);

                    // Check if we got close enough for melee
                    if (target.DistanceFromPlayer <= 5 && target.State != AnimalState.Detected)
                    {
                        GameDisplay.AddNarrative("You're close enough to strike!");
                        GameDisplay.Render(ctx);

                        var attackChoice = new Choice<bool>("Attack?");
                        attackChoice.AddOption("Strike now!", true);
                        attackChoice.AddOption("Back off", false);

                        if (attackChoice.GetPlayerChoice())
                        {
                            PerformKill(target, expedition);
                        }
                    }
                    break;

                case "assess":
                    ctx.player.stealthManager.AssessTarget();
                    minutesSpent += 2;
                    Input.WaitForKey();
                    break;

                case "stop":
                    ctx.player.stealthManager.StopHunting("You abandon the hunt.");
                    break;
            }
        }

        return minutesSpent;
    }

    private void PerformKill(Animal target, Expedition expedition)
    {
        GameDisplay.AddNarrative($"You strike! The {target.Name} falls.");

        // Deal lethal damage (stealth kill - massive damage to heart)
        target.Body.Damage(new DamageInfo(1000, DamageType.Pierce, "stealth kill", "Heart"));

        ctx.player.stealthManager.StopHunting();

        // Butcher and collect meat
        var loot = ButcheringProcessor.Butcher(target);
        ctx.Inventory.Add(loot);

        expedition.CollectionLog.AddRange(loot.Descriptions);
        expedition.AddLog($"You killed and butchered a {target.Name}, collecting {loot.TotalWeightKg:F1}kg of meat.");

        ctx.player.Skills.GetSkill("Hunting").GainExperience(5);
        GameDisplay.AddNarrative($"You butcher the {target.Name} and collect {loot.TotalWeightKg:F1}kg of meat.");
        Input.WaitForKey();
    }

    // --- Travel Progress Helpers ---

    private TickResult RunExpeditionTick(Expedition expedition)
    {
        var tickResult = GameEventRegistry.RunTicks(ctx, 1);
        ctx.Update(tickResult.MinutesElapsed);
        expedition.IncrementTime(tickResult.MinutesElapsed);
        return new TickResult(tickResult.MinutesElapsed, tickResult.TriggeredEvent);
    }

    private (int total, int elapsed, string destination) GetTravelInfo(Expedition expedition)
    {
        int total = TravelProcessor.GetTraversalMinutes(expedition.CurrentLocation, ctx.player, ctx.Inventory);
        int elapsed = expedition.MinutesSpentAtLocation;
        string dest = expedition.CurrentPhase == ExpeditionPhase.TravelingOut
            ? expedition.Destination.Name
            : ctx.CurrentLocation.Name;
        return (total, elapsed, dest);
    }

    private void HandleTravelInterrupt(Expedition expedition, GameEvent evt)
    {
        GameDisplay.Render(ctx);
        HandleEvent(evt);
    }

    private void FinishTravelSegment(Expedition expedition, int minutesTraveled)
    {
        expedition.AddLog($"You walk for {minutesTraveled} minutes.");
        DisplayQueuedExpeditionLogs(expedition);
    }

    private void RunTravelWithProgress(Expedition expedition)
    {
        var (totalTime, startElapsed, destination) = GetTravelInfo(expedition);
        int elapsed = startElapsed;
        GameEvent? triggeredEvent = null;

        while (elapsed < totalTime && triggeredEvent == null)
        {
            Output.Progress($"Traveling to {destination}...", totalTime, task =>
            {
                task.Increment(elapsed);

                while (elapsed < totalTime && triggeredEvent == null)
                {
                    var result = RunExpeditionTick(expedition);
                    elapsed += result.MinutesElapsed;
                    task.Increment(result.MinutesElapsed);
                    triggeredEvent = result.Event;
                    Thread.Sleep(100);
                }
            });

            if (triggeredEvent != null)
            {
                HandleTravelInterrupt(expedition, triggeredEvent);
                triggeredEvent = null;
                if (expedition.IsComplete) break;
            }
        }

        FinishTravelSegment(expedition, elapsed - startElapsed);
    }

    private void RunWorkWithProgress(Expedition expedition)
    {
        int startElapsed = expedition.MinutesSpentAtLocation;
        int elapsed = startElapsed;
        GameEvent? triggeredEvent = null;
        string workType = expedition.GetPhaseDisplayName();

        while (elapsed < expedition.WorkTimeMinutes && triggeredEvent == null)
        {
            Output.Progress($"{char.ToUpper(workType[0]) + workType[1..]} at {expedition.Destination.Name}...",
                expedition.WorkTimeMinutes, task =>
            {
                task.Increment(elapsed);

                while (elapsed < expedition.WorkTimeMinutes && triggeredEvent == null)
                {
                    var result = RunExpeditionTick(expedition);
                    elapsed += result.MinutesElapsed;
                    task.Increment(result.MinutesElapsed);
                    triggeredEvent = result.Event;
                    Thread.Sleep(100);
                }
            });

            if (triggeredEvent != null)
            {
                GameDisplay.Render(ctx);
                HandleEvent(triggeredEvent);
                triggeredEvent = null;
                if (expedition.IsComplete) break;
            }
        }

        // Apply work results after time has passed
        int workedMinutes = elapsed - startElapsed;
        expedition.DoWork(workedMinutes, ctx);
    }

    // --- End Travel Progress Helpers ---

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
            if (expedition.CurrentPhase == ExpeditionPhase.TravelingOut ||
                expedition.CurrentPhase == ExpeditionPhase.TravelingBack)
            {
                RunTravelWithProgress(expedition);
            }
            else if (expedition.Type == ExpeditionType.Hunt)
            {
                // Hunt has interactive work phase
                RunHuntingWorkPhase(expedition);
                DisplayQueuedExpeditionLogs(expedition);
            }
            else
            {
                RunWorkWithProgress(expedition);
                DisplayQueuedExpeditionLogs(expedition);
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
                ExpeditionType.Hunt => false, // Hunt phase handles its own continuation
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
        GameDisplay.AddNarrative("EVENT:");
        GameDisplay.AddNarrative($"** {evt.Name} **");
        GameDisplay.AddNarrative(evt.Description + "\n");
        GameDisplay.Render(ctx);
        var choice = evt.Choices.GetPlayerChoice();
        GameDisplay.AddNarrative(choice.Description + "\n");

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

        if (outcome.RewardPool != RewardPool.None)
        {
            var resources = RewardGenerator.Generate(outcome.RewardPool);
            if (!resources.IsEmpty)
            {
                ctx.Inventory.Add(resources);
                foreach (var desc in resources.Descriptions)
                {
                    GameDisplay.AddNarrative($"You found {desc}");
                }
            }
        }

        if (outcome.AbortsExpedition && !ctx.Expedition!.IsComplete)
        {
            ctx.Expedition!.CancelExpedition();
        }
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
        int travelTime = TravelProcessor.GetPathMinutes(expedition.Path, ctx.player, ctx.Inventory);
        GameDisplay.AddNarrative("PLAN:");
        GameDisplay.AddNarrative($"{expedition.Type.ToString().ToUpper()} - {expedition.Destination.Name}\n");
        GameDisplay.AddNarrative($"It's about a {travelTime / 2} minute walk each way.");

        if (expedition.Type == ExpeditionType.Hunt)
        {
            GameDisplay.AddNarrative("Hunting time varies - you'll decide when to head back.");
            GameDisplay.AddNarrative($"Travel alone is a {travelTime} minute round trip.\n");
        }
        else
        {
            GameDisplay.AddNarrative($"Once you get there it's {expedition.WorkTimeMinutes} minutes of {expedition.GetPhaseDisplayName(ExpeditionPhase.Working).ToLower()}.");
            GameDisplay.AddNarrative($"A {travelTime + expedition.WorkTimeMinutes} minute round trip if all goes well.\n");
        }

        GameDisplay.AddNarrative(GetLocationNotes(expedition.Destination));

        double fireTime = FireMinutesRemaining - TravelProcessor.GetPathMinutes(expedition.Path, ctx.player, ctx.Inventory);
        string fireMessage = GetFireMarginMessage(fireTime);
        if (FireMinutesRemaining > 0)
            GameDisplay.AddNarrative($"The fire has about {(int)FireMinutesRemaining} minutes left.");
        GameDisplay.AddNarrative(fireMessage);
        GameDisplay.AddNarrative("\n");
        GameDisplay.AddNarrative("Do you want to proceed with this plan? (y/n)");
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