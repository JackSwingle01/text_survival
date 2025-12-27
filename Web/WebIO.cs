using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Web.Dto;

namespace text_survival.Web;

/// <summary>
/// Web-based I/O implementation. Sends frames via WebSocket and waits for responses.
/// All methods are synchronous to match the game's blocking I/O model.
/// </summary>
public static class WebIO
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<string, InventoryDto> _currentInventory = new();
    private static readonly Dictionary<string, CraftingDto> _currentCrafting = new();
    private static readonly Dictionary<string, EventDto> _currentEvent = new();
    private static readonly Dictionary<string, HazardPromptDto> _currentHazard = new();
    private static readonly Dictionary<string, string> _currentConfirm = new();

    private static WebGameSession GetSession(GameContext ctx) =>
        SessionRegistry.Get(ctx.SessionId)
        ?? throw new InvalidOperationException($"No session found for ID: {ctx.SessionId}");

    /// <summary>
    /// Get the current UI mode based on context state.
    /// </summary>
    private static FrameMode GetCurrentMode(
        GameContext ctx,
        int? estimatedDurationSeconds = null,
        string? activityText = null)
    {
        // Progress mode takes priority
        if (estimatedDurationSeconds.HasValue)
            return new ProgressMode(activityText ?? "Working...", estimatedDurationSeconds.Value);

        // Travel mode when map is present
        if (ctx.Map != null)
            return new TravelMode(GridStateDto.FromContext(ctx));

        // Default to location mode
        return new LocationMode();
    }

    /// <summary>
    /// Get all currently active overlays.
    /// </summary>
    private static List<Overlay> GetCurrentOverlays(string? sessionId)
    {
        var overlays = new List<Overlay>();

        if (sessionId != null)
        {
            if (_currentInventory.TryGetValue(sessionId, out var inv))
                overlays.Add(new InventoryOverlay(inv));
            if (_currentCrafting.TryGetValue(sessionId, out var craft))
                overlays.Add(new CraftingOverlay(craft));
            if (_currentEvent.TryGetValue(sessionId, out var evt))
                overlays.Add(new EventOverlay(evt));
            if (_currentHazard.TryGetValue(sessionId, out var hazard))
                overlays.Add(new HazardOverlay(hazard));
            if (_currentConfirm.TryGetValue(sessionId, out var confirm))
                overlays.Add(new ConfirmOverlay(confirm));
        }

        return overlays;
    }

    /// <summary>
    /// Clear the current inventory display for a session.
    /// </summary>
    public static void ClearInventory(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentInventory.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current crafting display for a session.
    /// </summary>
    public static void ClearCrafting(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentCrafting.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current event display for a session.
    /// </summary>
    public static void ClearEvent(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentEvent.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current hazard display for a session.
    /// </summary>
    public static void ClearHazard(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentHazard.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current confirm display for a session.
    /// </summary>
    public static void ClearConfirm(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentConfirm.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Present a selection menu and wait for player choice.
    /// </summary>
    public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices, Func<T, string> display)
    {
        var session = GetSession(ctx);
        var list = choices.ToList();

        if (list.Count == 0)
            throw new ArgumentException("Choices cannot be empty", nameof(choices));

        // Generate choices with IDs for reliable button identity
        var choiceDtos = list.Select((item, i) => new ChoiceDto($"choice_{i}", display(item))).ToList();

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "select", prompt, choiceDtos)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        // Handle travel_to response: player clicked a map tile to start travel
        if (response.Type == "travel_to" && response.TargetX.HasValue && response.TargetY.HasValue)
        {
            ctx.PendingTravelTarget = (response.TargetX.Value, response.TargetY.Value);

            // Find the "Travel" option in the list
            for (int i = 0; i < choiceDtos.Count; i++)
            {
                if (choiceDtos[i].Label.Contains("Travel"))
                    return list[i];
            }

            // No "Travel" option available - clear pending target and wait for another choice
            ctx.PendingTravelTarget = null;
            inputId = session.GenerateInputId();
            frame = new WebFrame(
                GameStateDto.FromContext(ctx),
                GetCurrentMode(ctx),
                GetCurrentOverlays(ctx.SessionId),
                new InputRequestDto(inputId, "select", prompt, choiceDtos)
            );
            session.Send(frame);
            response = session.WaitForResponse(inputId, ResponseTimeout);
            // Fall through to process the new response normally
        }

        // Handle action response: player clicked persistent inventory/crafting/storage button
        if (response.Type == "action" && !string.IsNullOrEmpty(response.Action))
        {
            // Map action names to menu option labels
            var targetLabel = response.Action switch
            {
                "inventory" => "Inventory",
                "crafting" => "Crafting",
                "storage" => "Storage",
                _ => null
            };

            if (targetLabel != null)
            {
                for (int i = 0; i < choiceDtos.Count; i++)
                {
                    if (choiceDtos[i].Label.Contains(targetLabel))
                        return list[i];
                }
            }

            // Action not available in current menu - clear overlays and wait for another response
            // (resend the frame and wait again)
            ClearInventory(ctx);
            ClearCrafting(ctx);
            inputId = session.GenerateInputId();
            frame = new WebFrame(
                GameStateDto.FromContext(ctx),
                GetCurrentMode(ctx),
                GetCurrentOverlays(ctx.SessionId),
                new InputRequestDto(inputId, "select", prompt, choiceDtos)
            );
            session.Send(frame);
            response = session.WaitForResponse(inputId, ResponseTimeout);
            // Fall through to process the new response normally
        }

        // Handle examine response: player clicked to examine an environmental detail
        if (response.Type == "examine" && !string.IsNullOrEmpty(response.DetailId))
        {
            // Find the detail in the current location
            var detail = ctx.CurrentLocation.Features
                .OfType<EnvironmentalDetail>()
                .FirstOrDefault(d => d.Id == response.DetailId);

            if (detail != null && detail.CanInteract)
            {
                var (loot, examinationText) = detail.Interact();

                // Log the examination result
                if (examinationText != null)
                {
                    ctx.Log.Add(examinationText, UI.LogLevel.Normal);
                }

                // Add any loot to inventory
                if (loot != null && !loot.IsEmpty)
                {
                    ctx.Log.Add($"  Found: {loot.GetDescription()}", UI.LogLevel.Normal);
                    var leftovers = ctx.Inventory.CombineWithCapacity(loot);
                    if (!leftovers.IsEmpty)
                    {
                        ctx.Log.Add($"  Your pack is full. Left behind: {leftovers.GetDescription()}", UI.LogLevel.Warning);
                    }
                }
            }

            // Resend frame with updated state and wait for another response
            inputId = session.GenerateInputId();
            frame = new WebFrame(
                GameStateDto.FromContext(ctx),
                GetCurrentMode(ctx),
                GetCurrentOverlays(ctx.SessionId),
                new InputRequestDto(inputId, "select", prompt, choiceDtos)
            );
            session.Send(frame);
            response = session.WaitForResponse(inputId, ResponseTimeout);
            // Fall through to process the new response normally
        }

        // Match by choice ID
        var matchIndex = choiceDtos.FindIndex(c => c.Id == response.ChoiceId);
        if (matchIndex < 0)
        {
            // Invalid choice ID - default to first option
            matchIndex = 0;
        }
        return list[matchIndex];
    }

    /// <summary>
    /// Present a yes/no confirmation and wait for player choice.
    /// </summary>
    public static bool Confirm(GameContext ctx, string prompt)
    {
        var session = GetSession(ctx);

        // Set confirm overlay
        if (ctx.SessionId != null)
            _currentConfirm[ctx.SessionId] = prompt;

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "confirm", prompt, [new ChoiceDto("yes", "Yes"), new ChoiceDto("no", "No")])
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        // Clear confirm overlay after response
        ClearConfirm(ctx);

        return response.ChoiceId == "yes";
    }

    /// <summary>
    /// Wait for any key press (continue button in web UI).
    /// </summary>
    public static void WaitForKey(GameContext ctx, string message = "Continue")
    {
        var session = GetSession(ctx);

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "anykey", message, null)
        );

        session.Send(frame);
        session.WaitForResponse(inputId, ResponseTimeout);
    }

    /// <summary>
    /// Wait for user to dismiss an overlay (e.g., event outcome popup).
    /// Sends a frame with no input request - the overlay provides its own button.
    /// </summary>
    public static void WaitForOverlayDismiss(GameContext ctx)
    {
        var session = GetSession(ctx);

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "anykey", "Continue", null)  // Overlay has its own Continue button
        );

        session.Send(frame);
        session.WaitForResponse(inputId, ResponseTimeout);
    }

    /// <summary>
    /// Present a numeric selection and wait for player choice.
    /// Shows as buttons with the available numbers.
    /// </summary>
    public static int ReadInt(GameContext ctx, string prompt, int min, int max)
    {
        var session = GetSession(ctx);

        var numbers = Enumerable.Range(min, max - min + 1).ToList();
        var choiceDtos = numbers.Select(n => new ChoiceDto($"num_{n}", n.ToString())).ToList();

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "select", prompt, choiceDtos)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        // Parse the number from the choice ID (format: "num_X")
        if (response.ChoiceId != null && response.ChoiceId.StartsWith("num_"))
        {
            if (int.TryParse(response.ChoiceId[4..], out int result))
                return result;
        }

        // Default to min if parsing fails
        return min;
    }

    /// <summary>
    /// Render current game state without requesting input.
    /// </summary>
    public static void Render(GameContext ctx, string? statusText = null)
    {
        var session = SessionRegistry.Get(ctx.SessionId);
        if (session == null) return; // Silently skip if no session

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            null,  // No input
            statusText
        );

        session.Send(frame);
    }

    /// <summary>
    /// Render with estimated duration for client-side progress animation.
    /// Client will animate a progress bar locally based on the duration.
    /// </summary>
    public static void RenderWithDuration(GameContext ctx, string statusText, int estimatedMinutes)
    {
        var session = SessionRegistry.Get(ctx.SessionId);
        if (session == null) return;

        // Convert game minutes to real seconds (~5 game-min per real second)
        int estimatedSeconds = Math.Max(1, estimatedMinutes / 5);

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx, estimatedSeconds, statusText),
            GetCurrentOverlays(ctx.SessionId),
            null,  // No input during progress
            statusText
        );

        session.Send(frame);
    }

    /// <summary>
    /// Add narrative text to the context's log for web display.
    /// </summary>
    public static void AddNarrative(GameContext ctx, string text, UI.LogLevel level = UI.LogLevel.Normal)
    {
        ctx.Log.Add(text, level);
    }

    /// <summary>
    /// Add multiple narrative entries.
    /// </summary>
    public static void AddNarrative(GameContext ctx, IEnumerable<string> texts, UI.LogLevel level = UI.LogLevel.Normal)
    {
        ctx.Log.AddRange(texts, level);
    }

    /// <summary>
    /// Set the inventory to display. Will be included in subsequent input frames.
    /// </summary>
    public static void RenderInventory(GameContext ctx, Inventory inventory, string title)
    {
        if (ctx.SessionId == null) return;
        _currentInventory[ctx.SessionId] = InventoryDto.FromInventory(inventory, title);
    }

    /// <summary>
    /// Set the crafting screen to display. Will be included in subsequent input frames.
    /// </summary>
    public static void RenderCrafting(GameContext ctx, NeedCraftingSystem crafting, string title = "CRAFTING")
    {
        if (ctx.SessionId == null) return;
        _currentCrafting[ctx.SessionId] = CraftingDto.FromContext(ctx, crafting);
    }

    public static void RenderCampImprovementScreen(GameContext ctx, NeedCraftingSystem crafting)
    {
        if (ctx.SessionId == null) return;
        // Reuse CraftingDto but filter to only CampInfrastructure category
        _currentCrafting[ctx.SessionId] = CraftingDto.FromContext(ctx, crafting, filterCategory: Crafting.NeedCategory.CampInfrastructure);
    }

    // Grid Mode Methods //

    /// <summary>
    /// Render grid state and wait for player to click a tile.
    /// Returns the tile they clicked, or null if they clicked a non-grid action.
    /// </summary>
    public static PlayerResponse RenderGridAndWaitForInput(GameContext ctx, string? statusText = null)
    {
        if (ctx.Map == null)
            throw new InvalidOperationException("RenderGridAndWaitForInput requires a map");

        var session = GetSession(ctx);

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new TravelMode(GridStateDto.FromContext(ctx)),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "grid", statusText ?? "Click a tile to move", null),
            statusText
        );

        session.Send(frame);
        return session.WaitForResponse(inputId, ResponseTimeout);
    }

    /// <summary>
    /// Render grid with hazard prompt (quick vs careful choice).
    /// </summary>
    public static bool PromptHazardChoice(GameContext ctx, Location targetLocation, int targetX, int targetY,
        string hazardDescription, int quickTimeMinutes, int carefulTimeMinutes, double injuryRiskPercent)
    {
        if (ctx.Map == null)
            throw new InvalidOperationException("PromptHazardChoice requires a map");

        var session = GetSession(ctx);

        // Set hazard as overlay
        var hazardPrompt = new HazardPromptDto(
            targetX,
            targetY,
            hazardDescription,
            quickTimeMinutes,
            carefulTimeMinutes,
            injuryRiskPercent
        );

        if (ctx.SessionId != null)
            _currentHazard[ctx.SessionId] = hazardPrompt;

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new TravelMode(GridStateDto.FromContext(ctx)),  // No hazard in mode
            GetCurrentOverlays(ctx.SessionId),              // Hazard is in overlays
            new InputRequestDto(inputId, "hazard_choice", $"Hazardous terrain: {hazardDescription}",
                [new ChoiceDto("quick", "Quick"), new ChoiceDto("careful", "Careful")])
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        // Clear hazard overlay after response
        ClearHazard(ctx);

        // QuickTravel field takes precedence, fall back to ChoiceId
        if (response.QuickTravel.HasValue)
            return response.QuickTravel.Value;

        return response.ChoiceId == "quick";
    }

    /// <summary>
    /// Render grid state without waiting for input.
    /// </summary>
    public static void RenderGrid(GameContext ctx, string? statusText = null)
    {
        if (ctx.Map == null)
            throw new InvalidOperationException("RenderGrid requires a map");

        var session = SessionRegistry.Get(ctx.SessionId);
        if (session == null) return;

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new TravelMode(GridStateDto.FromContext(ctx)),
            GetCurrentOverlays(ctx.SessionId),
            null,  // No input request
            statusText
        );

        session.Send(frame);
    }

    /// <summary>
    /// Set the event to display. Will be included as an overlay in subsequent frames.
    /// </summary>
    public static void RenderEvent(GameContext ctx, EventDto eventData)
    {
        if (ctx.SessionId == null) return;
        _currentEvent[ctx.SessionId] = eventData;
    }

    /// <summary>
    /// Show work results as an event overlay. Uses outcome-only mode for display.
    /// </summary>
    public static void ShowWorkResult(GameContext ctx, string activityName, string message, List<string> itemsGained)
    {
        // Calculate stats delta if snapshot exists
        StatsDeltaDto? statsDelta = null;
        if (ctx.StatsBeforeWork.HasValue)
        {
            var before = ctx.StatsBeforeWork.Value;
            statsDelta = new StatsDeltaDto(
                ctx.player.Body.Energy - before.Energy,
                ctx.player.Body.CalorieStore - before.Calories,
                ctx.player.Body.Hydration - before.Hydration,
                ctx.player.Body.BodyTemperature - before.Temp
            );
            ctx.StatsBeforeWork = null; // Clear after use
        }

        var outcome = new EventOutcomeDto(
            Message: message,
            TimeAddedMinutes: 0,
            EffectsApplied: [],
            DamageTaken: [],
            ItemsGained: itemsGained,
            ItemsLost: [],
            TensionsChanged: [],
            StatsDelta: statsDelta
        );

        var eventData = new EventDto(
            Name: activityName,
            Description: "",
            Choices: [],
            Outcome: outcome
        );

        RenderEvent(ctx, eventData);
    }
}
