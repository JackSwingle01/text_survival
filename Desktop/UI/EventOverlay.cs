using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for displaying game events with choices.
/// </summary>
public class GameEventOverlay
{
    public bool IsOpen { get; set; }

    private bool _initialFocus;
    private EventDto? _currentEvent;
    private EventOutcomeDto? _outcome;

    /// <summary>
    /// Show an event with choices.
    /// </summary>
    public void ShowEvent(EventDto eventData)
    {
        _currentEvent = eventData;
        _outcome = eventData.Outcome;
        IsOpen = true; _initialFocus = true;
    }

    /// <summary>
    /// Show just an outcome (for events resolved elsewhere).
    /// </summary>
    public void ShowOutcome(EventOutcomeDto outcome)
    {
        _currentEvent = null;
        _outcome = outcome;
        IsOpen = true; _initialFocus = true;
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
                UiNavigation.Initial(ref _initialFocus);
                if (UiIcons.Button("continue", "Continue [Enter]", "continue", new Vector2(-1, 0)) || ImGui.IsKeyPressed(KeyboardKey.Enter))
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
        UiText.Wrapped(evt.Description);
        ImGui.PopTextWrapPos();

        ImGui.Separator();
        ImGui.Spacing();

        // Choices
        UiText.Text("What do you do?");
        ImGui.Spacing();

        UiText.Disabled("1-9: choose · Tab / arrows · Enter: select");
        for (int i = 0; i < evt.Choices.Count; i++)
        {
            var choice = evt.Choices[i];
            if (choice.IsAvailable) UiNavigation.Initial(ref _initialFocus);
            bool clicked = UiControls.Button(new(choice.Id, UiNavigation.Numbered(choice.Label, i), "continue",
                DisabledReason: choice.IsAvailable ? null : "Requirements are not met."), size: new(-1, 0));
            if (choice.IsAvailable && (clicked || UiNavigation.Number(i))) choiceResult = choice.Id;

            // Always show description
            ImGui.Indent();
            UiText.Wrapped(choice.Description);
            if (choice.Cost != null)
            {
                UiIcons.LabelWrapped("cost", UiStyle.Caution, $"Cost: {choice.Cost}");
            }
            ImGui.Unindent();
            ImGui.Spacing();
        }

        return choiceResult;
    }

    private void RenderOutcome(EventOutcomeDto outcome)
    {
        // Outcome message
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        UiText.Wrapped(outcome.Message);
        ImGui.PopTextWrapPos();

        // Time cost
        if (outcome.TimeAddedMinutes > 0)
        {
            UiIcons.Label("clock", $"(+{outcome.TimeAddedMinutes} minutes)");
        }

        ImGui.Spacing();

        // Items gained
        if (outcome.ItemsGained.Count > 0)
        {
            foreach (var item in outcome.ItemsGained)
            {
                UiIcons.LabelWrapped("give", UiStyle.Success, $"+ {item}");
            }
        }

        // Items lost
        if (outcome.ItemsLost.Count > 0)
        {
            foreach (var item in outcome.ItemsLost)
            {
                UiIcons.LabelWrapped("drop", UiStyle.Caution, $"- {item}");
            }
        }

        // Damage taken
        if (outcome.DamageTaken.Count > 0)
        {
            foreach (var damage in outcome.DamageTaken)
            {
                UiIcons.LabelWrapped("blood", UiStyle.Danger, $"! {damage}");
            }
        }

        // Effects applied
        if (outcome.EffectsApplied.Count > 0)
        {
            foreach (var effect in outcome.EffectsApplied)
            {
                UiIcons.LabelWrapped("effects", UiStyle.Caution, $"* {effect}");
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
                UiIcons.LabelWrapped("warning", color, tension);
            }
        }
    }

}
