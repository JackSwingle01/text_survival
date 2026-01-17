using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Desktop.Dto;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for displaying game events with choices.
/// </summary>
public class GameEventOverlay
{
    public bool IsOpen { get; set; }

    private EventDto? _currentEvent;
    private EventOutcomeDto? _outcome;
    private string? _selectedChoiceId;

    /// <summary>
    /// Show an event with choices.
    /// </summary>
    public void ShowEvent(EventDto eventData)
    {
        _currentEvent = eventData;
        _outcome = eventData.Outcome;
        _selectedChoiceId = null;
        IsOpen = true;
    }

    /// <summary>
    /// Show just an outcome (for events resolved elsewhere).
    /// </summary>
    public void ShowOutcome(EventOutcomeDto outcome)
    {
        _currentEvent = null;
        _outcome = outcome;
        _selectedChoiceId = null;
        IsOpen = true;
    }

    /// <summary>
    /// Render the event overlay.
    /// Returns the selected choice ID if a choice was made, null otherwise.
    /// </summary>
    public string? Render(float deltaTime)
    {
        if (!IsOpen) return null;

        string? choiceResult = null;

        // Center the popup
        OverlaySizes.SetupStandard();

        var flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize;

        string title = _currentEvent?.Name ?? "Event";
        if (ImGui.Begin(title, flags))
        {
            if (_outcome != null)
            {
                // Show outcome
                RenderOutcome(_outcome);

                ImGui.Separator();
                if (ImGui.Button("Continue", new Vector2(-1, 30)))
                {
                    IsOpen = false;
                    _currentEvent = null;
                    _outcome = null;
                }
            }
            else if (_currentEvent != null)
            {
                // Show event description and choices
                choiceResult = RenderEventChoices(_currentEvent);
            }
        }
        ImGui.End();

        return choiceResult;
    }

    private string? RenderEventChoices(EventDto evt)
    {
        string? choiceResult = null;

        // Event description
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextWrapped(evt.Description);
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.Spacing();

        // Choices
        ImGui.Text("What do you do?");
        ImGui.Spacing();

        foreach (var choice in evt.Choices)
        {
            bool isSelected = _selectedChoiceId == choice.Id;
            bool isAvailable = choice.IsAvailable;

            // Choice button
            if (!isAvailable)
            {
                ImGui.BeginDisabled();
            }

            Vector4 buttonColor = isSelected
                ? new Vector4(0.3f, 0.5f, 0.7f, 1f)
                : new Vector4(0.2f, 0.2f, 0.25f, 1f);
            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);

            if (ImGui.Button(choice.Label, new Vector2(-1, 28)))
            {
                _selectedChoiceId = choice.Id;
            }

            ImGui.PopStyleColor();

            if (!isAvailable)
            {
                ImGui.EndDisabled();
            }

            // Always show description
            ImGui.Indent();
            ImGui.TextDisabled(choice.Description);
            if (choice.Cost != null)
            {
                ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f), $"Cost: {choice.Cost}");
            }
            ImGui.Unindent();
            ImGui.Spacing();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Confirm button (only enabled when a choice is selected)
        if (_selectedChoiceId != null)
        {
            if (ImGui.Button("Confirm", new Vector2(-1, 30)))
            {
                choiceResult = _selectedChoiceId;
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("Select an option", new Vector2(-1, 30));
            ImGui.EndDisabled();
        }

        return choiceResult;
    }

    private void RenderOutcome(EventOutcomeDto outcome)
    {
        // Outcome message
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextWrapped(outcome.Message);
        ImGui.PopTextWrapPos();

        // Time cost
        if (outcome.TimeAddedMinutes > 0)
        {
            ImGui.TextDisabled($"(+{outcome.TimeAddedMinutes} minutes)");
        }

        ImGui.Spacing();

        // Items gained
        if (outcome.ItemsGained.Count > 0)
        {
            foreach (var item in outcome.ItemsGained)
            {
                ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), $"+ {item}");
            }
        }

        // Items lost
        if (outcome.ItemsLost.Count > 0)
        {
            foreach (var item in outcome.ItemsLost)
            {
                ImGui.TextColored(new Vector4(0.9f, 0.5f, 0.4f, 1f), $"- {item}");
            }
        }

        // Damage taken
        if (outcome.DamageTaken.Count > 0)
        {
            foreach (var damage in outcome.DamageTaken)
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"! {damage}");
            }
        }

        // Effects applied
        if (outcome.EffectsApplied.Count > 0)
        {
            foreach (var effect in outcome.EffectsApplied)
            {
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), $"* {effect}");
            }
        }

        // Tension changes
        if (outcome.TensionsChanged.Count > 0)
        {
            foreach (var tension in outcome.TensionsChanged)
            {
                Vector4 color = tension.StartsWith("+")
                    ? new Vector4(1f, 0.6f, 0.3f, 1f)
                    : tension.StartsWith("-")
                        ? new Vector4(0.5f, 0.8f, 0.5f, 1f)
                        : new Vector4(0.8f, 0.8f, 0.5f, 1f);
                ImGui.TextColored(color, $"  {tension}");
            }
        }
    }

    /// <summary>
    /// Create an EventDto from a GameEvent for display.
    /// </summary>
    public static EventDto CreateEventDto(GameEvent evt, GameContext ctx)
    {
        var choices = evt.GetAvailableChoices(ctx)
            .Select((c, i) => new EventChoiceDto(
                Id: $"choice_{i}",
                Label: c.Label,
                Description: c.Description,
                IsAvailable: true,
                Cost: c.GetMaxCost() is { } cost ? $"{cost.Amount} {cost.Type}" : null
            ))
            .ToList();

        return new EventDto(
            Name: evt.Name,
            Description: evt.Description,
            Choices: choices
        );
    }
}
