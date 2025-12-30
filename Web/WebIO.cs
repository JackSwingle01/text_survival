using text_survival.Actions;
using text_survival.Actions.Variants;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
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
    private static readonly Dictionary<string, ForageDto> _currentForage = new();
    private static readonly Dictionary<string, HuntDto> _currentHunt = new();
    private static readonly Dictionary<string, TransferDto> _currentTransfer = new();
    private static readonly Dictionary<string, FireManagementDto> _currentFire = new();
    private static readonly Dictionary<string, CookingDto> _currentCooking = new();
    private static readonly Dictionary<string, ButcherDto> _currentButcher = new();
    private static readonly Dictionary<string, EncounterDto> _currentEncounter = new();
    private static readonly Dictionary<string, CombatDto> _currentCombat = new();

    private static WebGameSession GetSession(GameContext ctx) =>
        SessionRegistry.Get(ctx.SessionId)
        ?? throw new InvalidOperationException($"No session found for ID: {ctx.SessionId}");

    /// <summary>
    /// Generate a semantic ID from a label for more debuggable choice matching.
    /// Format: slugified_label_index (e.g., "forage_for_supplies_0")
    /// </summary>
    public static string GenerateSemanticId(string label, int index)
    {
        // Create a slug from the label: lowercase, replace non-alphanumeric with underscore
        var slug = System.Text.RegularExpressions.Regex.Replace(label.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        // Truncate if too long
        if (slug.Length > 30) slug = slug[..30];
        return $"{slug}_{index}";
    }

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
            if (_currentForage.TryGetValue(sessionId, out var forage))
                overlays.Add(new ForageOverlay(forage));
            if (_currentHunt.TryGetValue(sessionId, out var hunt))
                overlays.Add(new HuntOverlay(hunt));
            if (_currentTransfer.TryGetValue(sessionId, out var transfer))
                overlays.Add(new TransferOverlay(transfer));
            if (_currentFire.TryGetValue(sessionId, out var fire))
                overlays.Add(new FireOverlay(fire));
            if (_currentCooking.TryGetValue(sessionId, out var cooking))
                overlays.Add(new CookingOverlay(cooking));
            if (_currentButcher.TryGetValue(sessionId, out var butcher))
                overlays.Add(new ButcherOverlay(butcher));
            if (_currentEncounter.TryGetValue(sessionId, out var encounter))
                overlays.Add(new EncounterOverlay(encounter));
            if (_currentCombat.TryGetValue(sessionId, out var combat))
                overlays.Add(new CombatOverlay(combat));
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
    /// Clear the current forage display for a session.
    /// </summary>
    public static void ClearForage(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentForage.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current hunt display for a session.
    /// </summary>
    public static void ClearHunt(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentHunt.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current transfer display for a session.
    /// </summary>
    public static void ClearTransfer(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentTransfer.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current fire display for a session.
    /// </summary>
    public static void ClearFire(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentFire.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current cooking display for a session.
    /// </summary>
    public static void ClearCooking(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentCooking.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current butcher display for a session.
    /// </summary>
    public static void ClearButcher(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentButcher.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current encounter display for a session.
    /// </summary>
    public static void ClearEncounter(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentEncounter.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current combat display for a session.
    /// </summary>
    public static void ClearCombat(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentCombat.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear all overlays for a session. Used on reconnect to prevent stale overlays.
    /// </summary>
    public static void ClearAllOverlays(string sessionId)
    {
        _currentInventory.Remove(sessionId);
        _currentCrafting.Remove(sessionId);
        _currentEvent.Remove(sessionId);
        _currentHazard.Remove(sessionId);
        _currentConfirm.Remove(sessionId);
        _currentForage.Remove(sessionId);
        _currentHunt.Remove(sessionId);
        _currentTransfer.Remove(sessionId);
        _currentFire.Remove(sessionId);
        _currentCooking.Remove(sessionId);
        _currentButcher.Remove(sessionId);
        _currentEncounter.Remove(sessionId);
        _currentCombat.Remove(sessionId);
    }

    /// <summary>
    /// Set the hunt overlay data. Will be included in subsequent frames.
    /// </summary>
    public static void RenderHunt(GameContext ctx, HuntDto huntData)
    {
        if (ctx.SessionId != null)
            _currentHunt[ctx.SessionId] = huntData;
    }

    /// <summary>
    /// Render hunt overlay and wait for player choice.
    /// Returns the choice ID selected by the player.
    /// </summary>
    public static string WaitForHuntChoice(GameContext ctx, HuntDto huntData)
    {
        var session = GetSession(ctx);

        // Set hunt overlay
        RenderHunt(ctx, huntData);

        // Build choice list from available hunt choices
        var choices = huntData.Choices
            .Where(c => c.IsAvailable)
            .Select(c => new ChoiceDto(c.Id, c.Label))
            .ToList();

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "hunt", "What do you do?", choices)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        return response.ChoiceId ?? "stop";
    }

    /// <summary>
    /// Wait for user to acknowledge the hunt outcome (Continue button).
    /// The hunt overlay must already be set via RenderHunt with an outcome.
    /// </summary>
    public static void WaitForHuntContinue(GameContext ctx)
    {
        WaitForEventContinue(ctx);
        ClearHunt(ctx);
    }

    // ========================================================================
    // Encounter Overlay Methods
    // ========================================================================

    /// <summary>
    /// Set the encounter overlay data. Will be included in subsequent frames.
    /// </summary>
    public static void RenderEncounter(GameContext ctx, EncounterDto encounterData)
    {
        if (ctx.SessionId != null)
            _currentEncounter[ctx.SessionId] = encounterData;
    }

    /// <summary>
    /// Render encounter overlay and wait for player choice.
    /// Returns the choice ID selected by the player.
    /// </summary>
    public static string WaitForEncounterChoice(GameContext ctx, EncounterDto encounterData)
    {
        var session = GetSession(ctx);

        // Set encounter overlay
        RenderEncounter(ctx, encounterData);

        // Build choice list from available encounter choices
        var choices = encounterData.Choices
            .Where(c => c.IsAvailable)
            .Select(c => new ChoiceDto(c.Id, c.Label))
            .ToList();

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "encounter", "What do you do?", choices)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        return response.ChoiceId ?? "back";
    }

    /// <summary>
    /// Wait for user to acknowledge the encounter outcome (Continue button).
    /// The encounter overlay must already be set via RenderEncounter with an outcome.
    /// </summary>
    public static void WaitForEncounterContinue(GameContext ctx)
    {
        WaitForEventContinue(ctx);
        ClearEncounter(ctx);
    }

    // ========================================================================
    // Combat Overlay Methods (Reusable)
    // ========================================================================

    /// <summary>
    /// Set the combat overlay data. Will be included in subsequent frames.
    /// </summary>
    public static void RenderCombat(GameContext ctx, CombatDto combatData)
    {
        if (ctx.SessionId != null)
            _currentCombat[ctx.SessionId] = combatData;
    }

    /// <summary>
    /// Render combat overlay and wait for player choice.
    /// Returns the choice ID selected by the player.
    /// </summary>
    public static string WaitForCombatChoice(GameContext ctx, CombatDto combatData)
    {
        var session = GetSession(ctx);

        // Set combat overlay
        RenderCombat(ctx, combatData);

        // Build choice list from available combat actions
        var choices = combatData.Actions
            .Where(a => a.IsAvailable)
            .Select(a => new ChoiceDto(a.Id, a.Label))
            .ToList();

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "combat", "Your move:", choices)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        return response.ChoiceId ?? "hold_ground";
    }

    /// <summary>
    /// Wait for player to select a target (two-stage targeting for Close range attacks).
    /// Returns the target ID selected by the player.
    /// </summary>
    public static string WaitForTargetChoice(GameContext ctx, List<CombatActionDto> targetingOptions, string animalName)
    {
        var session = GetSession(ctx);

        // Build choice list from targeting options
        var choices = targetingOptions
            .Where(t => t.IsAvailable)
            .Select(t => new ChoiceDto(t.Id, $"{t.Label} ({t.HitChance})"))
            .ToList();

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "targeting", $"Where on the {animalName}?", choices)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        return response.ChoiceId ?? "target_torso";
    }

    /// <summary>
    /// Wait for user to acknowledge the combat outcome (Continue button).
    /// The combat overlay must already be set via RenderCombat with an outcome.
    /// </summary>
    public static void WaitForCombatContinue(GameContext ctx)
    {
        WaitForEventContinue(ctx);
        ClearCombat(ctx);
    }

    /// <summary>
    /// Present a selection menu and wait for player choice.
    /// </summary>
    public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices, Func<T, string> display, Func<T, bool>? isDisabled = null)
    {
        var session = GetSession(ctx);
        var list = choices.ToList();

        if (list.Count == 0)
            throw new ArgumentException("Choices cannot be empty", nameof(choices));

        // Generate choices with semantic IDs for reliable button identity
        var choiceDtos = list.Select((item, i) => {
            var label = display(item);
            var semanticId = GenerateSemanticId(label, i);
            var disabled = isDisabled?.Invoke(item) ?? false;
            return new ChoiceDto(semanticId, label, disabled);
        }).ToList();

        // Detect continue/stop pattern (2 choices, first is Continue)
        // These should show as a ConfirmOverlay so they're clearly visible
        bool isContinuePrompt = list.Count == 2 &&
            choiceDtos[0].Label.Contains("Continue");

        var overlays = GetCurrentOverlays(ctx.SessionId);
        if (isContinuePrompt)
        {
            overlays = [..overlays, new ConfirmOverlay(prompt)];
        }

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            overlays,
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
            // NOTE: Keep same inputId - this is the same logical request, just resent
            ctx.PendingTravelTarget = null;
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
            // NOTE: Keep same inputId - this is the same logical request, just resent
            ClearInventory(ctx);
            ClearCrafting(ctx);
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

        // Handle "continue" response - this is from a Continue/Close button
        // that was clicked during a Select (e.g., stale event overlay). Resend frame and wait again.
        // NOTE: Keep same inputId - this is the same logical request, just resent
        if (response.ChoiceId == "continue")
        {
            frame = new WebFrame(
                GameStateDto.FromContext(ctx),
                GetCurrentMode(ctx),
                GetCurrentOverlays(ctx.SessionId),
                new InputRequestDto(inputId, "select", prompt, choiceDtos)
            );
            session.Send(frame);
            response = session.WaitForResponse(inputId, ResponseTimeout);
        }

        // Match by choice ID - if unknown, resend frame and wait for valid response
        var matchIndex = choiceDtos.FindIndex(c => c.Id == response.ChoiceId);
        while (matchIndex < 0)
        {
            // Log mismatch for debugging - this indicates a frontend/backend sync issue (stale button click)
            Console.WriteLine($"[WebIO] WARNING: Unknown choice ID '{response.ChoiceId}', " +
                              $"available: [{string.Join(", ", choiceDtos.Select(c => c.Id))}]. Waiting for valid response.");

            // Resend frame with same inputId and wait for a valid response
            frame = new WebFrame(
                GameStateDto.FromContext(ctx),
                GetCurrentMode(ctx),
                GetCurrentOverlays(ctx.SessionId),
                new InputRequestDto(inputId, "select", prompt, choiceDtos)
            );
            session.Send(frame);
            response = session.WaitForResponse(inputId, ResponseTimeout);
            matchIndex = choiceDtos.FindIndex(c => c.Id == response.ChoiceId);
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
    /// Present a numeric selection and wait for player choice.
    /// Shows as buttons with the available numbers.
    /// </summary>
    public static int ReadInt(GameContext ctx, string prompt, int min, int max, bool allowCancel = false)
    {
        var session = GetSession(ctx);

        var numbers = Enumerable.Range(min, max - min + 1).ToList();
        var choiceDtos = numbers.Select(n => new ChoiceDto($"num_{n}", n.ToString())).ToList();

        // Add cancel option if requested
        if (allowCancel)
            choiceDtos.Add(new ChoiceDto("cancel", "Cancel"));

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "select", prompt, choiceDtos)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        // Check for cancel
        if (response.ChoiceId == "cancel")
            return -1;

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
    /// Wait for user to acknowledge the current event overlay (Continue button).
    /// The event overlay must already be set via RenderEvent.
    /// </summary>
    public static void WaitForEventContinue(GameContext ctx)
    {
        var session = GetSession(ctx);

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "anykey", "Continue", null)
        );

        session.Send(frame);
        session.WaitForResponse(inputId, ResponseTimeout);
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

        // Convert game minutes to real seconds (~7 game-min per real second)
        int estimatedSeconds = Math.Max(1, estimatedMinutes / 7);

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
    /// Render travel progress with synchronized camera animation.
    /// Sends TravelProgressMode so frontend animates camera pan and progress bar together.
    /// Called AFTER MoveTo() completes - grid state reflects destination.
    /// </summary>
    public static void RenderTravelProgress(
        GameContext ctx,
        string statusText,
        int estimatedMinutes,
        int originX,
        int originY)
    {
        var session = SessionRegistry.Get(ctx.SessionId);
        if (session == null) return;

        int estimatedSeconds = Math.Max(1, estimatedMinutes / 7);

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new TravelProgressMode(
                GridStateDto.FromContext(ctx),
                statusText,
                estimatedSeconds,
                originX,
                originY
            ),
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
    /// Show inventory overlay and wait for user to close it.
    /// </summary>
    public static void ShowInventoryAndWait(GameContext ctx, Inventory inventory, string title)
    {
        var session = GetSession(ctx);

        // Set inventory overlay
        _currentInventory[ctx.SessionId!] = InventoryDto.FromInventory(inventory, title);

        // Send frame with close button
        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "select", "Viewing inventory", [new ChoiceDto("close", "Close")])
        );

        session.Send(frame);
        session.WaitForResponse(inputId, ResponseTimeout);

        // Clear after user closes
        ClearInventory(ctx);
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
        string hazardDescription, int quickTimeMinutes, int carefulTimeMinutes, double injuryRisk)
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
            injuryRisk
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
    /// Show forage overlay and wait for player to select focus and time.
    /// Returns (focus, minutes), (null, 0) if cancelled, or (null, -1) for "keep walking".
    /// </summary>
    public static (ForageFocus? focus, int minutes) SelectForageOptions(GameContext ctx, ForageDto forageData)
    {
        var session = GetSession(ctx);

        // Set forage as overlay
        if (ctx.SessionId != null)
            _currentForage[ctx.SessionId] = forageData;

        // Build choice list - each is a focus_time combo
        var choices = new List<ChoiceDto>();
        foreach (var focusOption in forageData.FocusOptions)
        {
            foreach (var time in forageData.TimeOptions)
            {
                string id = $"{focusOption.Id}_{time.Minutes}";
                string label = $"{focusOption.Label} - {time.Label}";
                choices.Add(new ChoiceDto(id, label));
            }
        }
        choices.Add(new ChoiceDto("keep_walking", "Keep Walking (5 min)"));
        choices.Add(new ChoiceDto("cancel", "Cancel"));

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "forage", "Choose focus and time", choices)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        // Clear forage overlay after response
        ClearForage(ctx);

        if (response.ChoiceId == "cancel" || response.ChoiceId == null)
            return (null, 0);

        // "Keep Walking" - signal to reroll clues
        if (response.ChoiceId == "keep_walking")
            return (null, -1);

        // Parse response: "fuel_30" -> (Fuel, 30)
        var parts = response.ChoiceId.Split('_');
        if (parts.Length != 2)
            return (null, 0);

        var focusId = parts[0];
        var focus = focusId switch
        {
            "fuel" => ForageFocus.Fuel,
            "food" => ForageFocus.Food,
            "medicine" => ForageFocus.Medicine,
            "materials" => ForageFocus.Materials,
            "general" => ForageFocus.General,
            _ => ForageFocus.General
        };

        if (!int.TryParse(parts[1], out int minutes))
            minutes = 30;

        return (focus, minutes);
    }

    /// <summary>
    /// Show butcher popup and get player's mode selection.
    /// Returns the selected mode ID, or null if cancelled.
    /// </summary>
    public static string? SelectButcherOptions(GameContext ctx, ButcherDto butcherData)
    {
        var session = GetSession(ctx);

        // Set butcher as overlay
        if (ctx.SessionId != null)
            _currentButcher[ctx.SessionId] = butcherData;

        // Build choice list from mode options
        var choices = new List<ChoiceDto>();
        foreach (var mode in butcherData.ModeOptions)
        {
            choices.Add(new ChoiceDto(mode.Id, mode.Label));
        }
        choices.Add(new ChoiceDto("cancel", "Cancel"));

        int inputId = session.GenerateInputId();
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            GetCurrentOverlays(ctx.SessionId),
            new InputRequestDto(inputId, "butcher", "Choose butchering approach", choices)
        );

        session.Send(frame);
        var response = session.WaitForResponse(inputId, ResponseTimeout);

        // Clear butcher overlay after response
        ClearButcher(ctx);

        if (response.ChoiceId == "cancel" || response.ChoiceId == null)
            return null;

        return response.ChoiceId;
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
        WaitForEventContinue(ctx);
        ClearEvent(ctx);
    }

    /// <summary>
    /// Show death screen overlay and wait for player to click restart.
    /// </summary>
    public static void ShowDeathScreen(GameContext ctx, DeathScreenDto data)
    {
        var session = GetSession(ctx);

        int inputId = session.GenerateInputId();
        var overlay = new DeathScreenOverlay(data);
        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            GetCurrentMode(ctx),
            [overlay],
            new InputRequestDto(inputId, "deathScreen", "", [new ChoiceDto("restart", "Start New Game")])
        );

        session.Send(frame);
        session.WaitForResponse(inputId, ResponseTimeout);
        // After response, the session loop will end and restart
    }

    /// <summary>
    /// Run the side-by-side transfer UI for camp storage or caches.
    /// Handles click-to-transfer with real-time updates.
    /// </summary>
    public static void RunTransferUI(GameContext ctx, Inventory storage, string storageName)
    {
        var session = GetSession(ctx);

        while (true)
        {
            // Build transfer DTO
            var transferData = TransferDto.FromInventories(ctx.Inventory, storage, storageName);
            _currentTransfer[ctx.SessionId!] = transferData;

            // Send frame with transfer overlay
            var choices = new List<ChoiceDto> { new("done", "Done") };

            int inputId = session.GenerateInputId();
            var frame = new WebFrame(
                GameStateDto.FromContext(ctx),
                GetCurrentMode(ctx),
                GetCurrentOverlays(ctx.SessionId),
                new InputRequestDto(inputId, "transfer", "Click items to transfer", choices)
            );

            session.Send(frame);
            var response = session.WaitForResponse(inputId, ResponseTimeout);

            // Handle response
            if (response.ChoiceId == "done" || response.Type == "close")
                break;

            if (response.TransferItemId != null)
            {
                int count = response.TransferCount ?? 1;
                ExecuteTransfer(ctx.Inventory, storage, response.TransferItemId, count);
                // Loop continues to refresh UI
            }
        }

        ClearTransfer(ctx);
    }

    /// <summary>
    /// Execute a transfer based on item ID.
    /// Item IDs are formatted as: "player_resource_Pine", "storage_tool_0", etc.
    /// </summary>
    private static void ExecuteTransfer(
        Inventory player,
        Inventory storage,
        string itemId,
        int count)
    {
        // Parse itemId: "player_resource_Pine", "storage_tool_0", etc.
        var parts = itemId.Split('_', 3);
        if (parts.Length < 3) return;

        string source = parts[0];        // "player" or "storage"
        string itemType = parts[1];      // "resource", "tool", "accessory", "water"
        string itemKey = parts[2];       // Resource name or index

        var sourceInv = source == "player" ? player : storage;
        var targetInv = source == "player" ? storage : player;

        switch (itemType)
        {
            case "resource":
                if (Enum.TryParse<Resource>(itemKey, out var resource))
                {
                    for (int i = 0; i < count && sourceInv.Count(resource) > 0; i++)
                    {
                        double weight = sourceInv.Pop(resource);
                        if (targetInv == player && !player.CanCarry(weight))
                        {
                            sourceInv.Add(resource, weight);  // Put back
                            break;
                        }
                        targetInv.Add(resource, weight);
                    }
                }
                break;

            case "water":
                double waterAmount = Math.Min(0.5 * count, sourceInv.WaterLiters);
                if (targetInv == player && !player.CanCarry(waterAmount))
                    break;
                sourceInv.WaterLiters -= waterAmount;
                targetInv.WaterLiters += waterAmount;
                break;

            case "tool":
                if (int.TryParse(itemKey, out int toolIdx) && toolIdx < sourceInv.Tools.Count)
                {
                    var tool = sourceInv.Tools[toolIdx];
                    if (targetInv == player && !player.CanCarry(tool.Weight))
                        break;
                    sourceInv.Tools.RemoveAt(toolIdx);
                    targetInv.Tools.Add(tool);
                }
                break;

            case "accessory":
                if (int.TryParse(itemKey, out int accIdx) && accIdx < sourceInv.Accessories.Count)
                {
                    var acc = sourceInv.Accessories[accIdx];
                    if (targetInv == player && !player.CanCarry(acc.Weight))
                        break;
                    sourceInv.Accessories.RemoveAt(accIdx);
                    targetInv.Accessories.Add(acc);
                }
                break;
        }
    }

    // ========================================================================
    // Fire Management UI
    // ========================================================================

    /// <summary>
    /// Run the fire management UI - handles both starting and tending modes.
    /// </summary>
    public static void RunFireUI(GameContext ctx, HeatSourceFeature fire)
    {
        var session = GetSession(ctx);
        string? selectedToolId = null;
        string? selectedTinderId = null;

        while (true)
        {
            // Build fire DTO with current selections
            var fireData = FireManagementDto.FromContext(ctx, fire, selectedToolId, selectedTinderId);
            _currentFire[ctx.SessionId!] = fireData;

            // Build choices based on mode
            var choices = new List<ChoiceDto> { new("done", "Done") };
            if (fireData.Mode == "starting" && fireData.Fire.HasKindling &&
                (fireData.Tools?.Count ?? 0) > 0 && (fireData.Tinders?.Count ?? 0) > 0)
            {
                choices.Insert(0, new ChoiceDto("start_fire", "Start Fire"));
            }
            // Add wait button to watch fire catch/burn (in tending mode or when fire exists)
            if (fire.IsActive || fire.HasEmbers)
            {
                choices.Insert(choices.Count - 1, new ChoiceDto("wait", "Wait (2 min)"));
            }

            int inputId = session.GenerateInputId();
            var frame = new WebFrame(
                GameStateDto.FromContext(ctx),
                GetCurrentMode(ctx),
                GetCurrentOverlays(ctx.SessionId),
                new InputRequestDto(inputId, "fire", fireData.Mode == "starting" ? "Start Fire" : "Add Fuel", choices)
            );

            session.Send(frame);
            var response = session.WaitForResponse(inputId, ResponseTimeout);

            // Handle response
            if (response.ChoiceId == "done" || response.Type == "close")
                break;

            // Tending mode: add fuel
            if (response.FuelItemId != null)
            {
                int count = response.FuelCount ?? 1;
                ExecuteAddFuel(ctx, fire, response.FuelItemId, count);
                ctx.Update(1, ActivityType.TendingFire);
            }
            // Starting mode: select tool
            else if (response.FireToolId != null)
            {
                selectedToolId = response.FireToolId;
            }
            // Starting mode: select tinder
            else if (response.TinderId != null)
            {
                selectedTinderId = response.TinderId;
            }
            // Starting mode: attempt ignition
            else if (response.ChoiceId == "start_fire")
            {
                bool success = ExecuteStartFire(ctx, fire, selectedToolId, selectedTinderId);
                if (success)
                {
                    // Fire now active, mode switches to tending automatically
                    selectedToolId = null;
                    selectedTinderId = null;
                }
                // On failure, loop continues to show updated UI (may have consumed materials)
            }
            // Wait to watch fire
            else if (response.ChoiceId == "wait")
            {
                ctx.Update(2, ActivityType.TendingFire);
            }
        }

        ClearFire(ctx);
    }

    /// <summary>
    /// Execute adding fuel to fire.
    /// </summary>
    private static void ExecuteAddFuel(GameContext ctx, HeatSourceFeature fire, string fuelItemId, int count)
    {
        // Parse fuelItemId: "fuel_Pine", "fuel_Stick", etc.
        if (!fuelItemId.StartsWith("fuel_")) return;
        var resourceName = fuelItemId[5..];
        if (!Enum.TryParse<Resource>(resourceName, out var resource)) return;

        // Map resource to fuel type
        var fuelType = resource switch
        {
            Resource.Stick => FuelType.Kindling,
            Resource.Pine => FuelType.PineWood,
            Resource.Birch => FuelType.BirchWood,
            Resource.Oak => FuelType.OakWood,
            Resource.Tinder => FuelType.Tinder,
            Resource.BirchBark => FuelType.BirchBark,
            Resource.Usnea => FuelType.Usnea,
            Resource.Chaga => FuelType.Chaga,
            Resource.Charcoal => FuelType.Kindling,
            Resource.Bone => FuelType.Bone,
            _ => FuelType.Kindling
        };

        // Add fuel up to count
        for (int i = 0; i < count && ctx.Inventory.Count(resource) > 0; i++)
        {
            if (!fire.CanAddFuel(fuelType)) break;
            double weight = ctx.Inventory.Pop(resource);
            fire.AddFuel(weight, fuelType);
        }
    }

    /// <summary>
    /// Execute starting a fire with selected tool and tinder.
    /// Returns true on success.
    /// </summary>
    private static bool ExecuteStartFire(
        GameContext ctx,
        HeatSourceFeature fire,
        string? selectedToolId,
        string? selectedTinderId)
    {
        var inv = ctx.Inventory;

        // Get selected tool
        var fireTools = inv.Tools
            .Where(t => t.ToolType == ToolType.HandDrill ||
                       t.ToolType == ToolType.BowDrill ||
                       t.ToolType == ToolType.FireStriker)
            .ToList();

        int toolIndex = 0;
        if (selectedToolId != null && selectedToolId.StartsWith("tool_"))
            int.TryParse(selectedToolId[5..], out toolIndex);

        if (toolIndex >= fireTools.Count) return false;
        var tool = fireTools[toolIndex];

        // Check kindling
        if (inv.Count(Resource.Stick) <= 0) return false;

        // Get selected tinder
        Resource tinderResource = Resource.Tinder;
        FuelType tinderFuelType = FuelType.Tinder;
        if (selectedTinderId != null && selectedTinderId.StartsWith("tinder_"))
        {
            var resourceName = selectedTinderId[7..];
            if (Enum.TryParse<Resource>(resourceName, out var res))
            {
                tinderResource = res;
                tinderFuelType = res switch
                {
                    Resource.BirchBark => FuelType.BirchBark,
                    Resource.Usnea => FuelType.Usnea,
                    Resource.Chaga => FuelType.Chaga,
                    _ => FuelType.Tinder
                };
            }
        }

        if (inv.Count(tinderResource) <= 0) return false;

        // Calculate success chance
        int baseChance = tool.ToolType switch
        {
            ToolType.HandDrill => 35,
            ToolType.BowDrill => 55,
            ToolType.FireStriker => 75,
            _ => 30
        };

        int tinderBonus = (int)(FuelDatabase.Get(tinderFuelType).IgnitionBonus * 100);
        int finalChance = Math.Min(95, baseChance + tinderBonus);

        // Consume materials (tinder + kindling)
        inv.Pop(tinderResource);
        inv.Pop(Resource.Stick);

        // Use tool (decrement durability)
        if (tool.Durability > 0)
            tool.Durability--;

        // Time cost for fire-starting attempt
        ctx.Update(10, ActivityType.TendingFire);

        // Roll for success
        bool success = Utils.RandInt(0, 99) < finalChance;

        if (success)
        {
            // Light the fire with kindling
            fire.AddFuel(0.1, FuelType.Tinder);  // Initial tinder
            fire.AddFuel(0.2, FuelType.Kindling); // Initial kindling
            fire.IgniteAll();

            // Add fire to location if it's new
            if (!ctx.CurrentLocation.Features.Contains(fire))
                ctx.CurrentLocation.Features.Add(fire);
        }

        return success;
    }

    // ============================================
    // Cooking UI
    // ============================================

    private const int CookMeatTimeMinutes = 15;
    private const int MeltSnowTimeMinutes = 10;
    private const double MeltSnowWaterLiters = 1.0;

    /// <summary>
    /// Run the web-based cooking UI overlay.
    /// </summary>
    public static void RunCookingUI(GameContext ctx)
    {
        var session = GetSession(ctx);
        CookingResultDto? lastResult = null;

        while (true)
        {
            var cookingData = BuildCookingDto(ctx, lastResult);
            _currentCooking[ctx.SessionId!] = cookingData;
            lastResult = null; // Clear after displaying

            var choices = new List<ChoiceDto>
            {
                new("melt_snow", "Melt Snow"),
                new("done", "Done")
            };

            if (ctx.Inventory.Count(Resource.RawMeat) > 0)
            {
                choices.Insert(0, new ChoiceDto("cook_meat", "Cook Meat"));
            }

            int inputId = session.GenerateInputId();
            var frame = new WebFrame(
                GameStateDto.FromContext(ctx),
                GetCurrentMode(ctx),
                GetCurrentOverlays(ctx.SessionId),
                new InputRequestDto(inputId, "cooking", "Cook & Melt", choices)
            );

            session.Send(frame);
            var response = session.WaitForResponse(inputId, ResponseTimeout);

            if (response.ChoiceId == "done")
                break;

            if (response.ChoiceId == "cook_meat")
            {
                lastResult = ExecuteCookMeat(ctx);
            }
            else if (response.ChoiceId == "melt_snow")
            {
                lastResult = ExecuteMeltSnow(ctx);
            }
        }

        ClearCooking(ctx);
    }

    private static CookingDto BuildCookingDto(GameContext ctx, CookingResultDto? lastResult)
    {
        var inv = ctx.Inventory;
        var options = new List<CookingOptionDto>();

        // Cook meat option
        double rawMeat = inv.Weight(Resource.RawMeat);
        bool hasMeat = rawMeat > 0;
        options.Add(new CookingOptionDto(
            "cook_meat",
            $"Cook raw meat ({rawMeat:F1}kg)",
            "outdoor_grill",
            CookMeatTimeMinutes,
            hasMeat,
            hasMeat ? null : "No raw meat"
        ));

        // Melt snow option (always available)
        options.Add(new CookingOptionDto(
            "melt_snow",
            $"Melt snow (+{MeltSnowWaterLiters:F1}L water)",
            "water_drop",
            MeltSnowTimeMinutes,
            true,
            null
        ));

        return new CookingDto(
            options,
            inv.WaterLiters,
            inv.Weight(Resource.RawMeat),
            inv.Weight(Resource.CookedMeat),
            lastResult
        );
    }

    private static CookingResultDto ExecuteCookMeat(GameContext ctx)
    {
        var inv = ctx.Inventory;
        if (inv.Count(Resource.RawMeat) <= 0)
            return new CookingResultDto("No raw meat to cook!", "error", false);

        // Cook meat
        ctx.Update(CookMeatTimeMinutes, ActivityType.Cooking);
        double weight = inv.Pop(Resource.RawMeat);
        inv.Add(Resource.CookedMeat, weight);

        return new CookingResultDto($"+{weight:F1}kg cooked meat", "outdoor_grill", true);
    }

    private static CookingResultDto ExecuteMeltSnow(GameContext ctx)
    {
        ctx.Update(MeltSnowTimeMinutes, ActivityType.Cooking);
        ctx.Inventory.WaterLiters += MeltSnowWaterLiters;

        return new CookingResultDto($"+{MeltSnowWaterLiters:F1}L water", "water_drop", true);
    }
}
