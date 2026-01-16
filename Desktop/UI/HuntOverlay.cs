using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Desktop.Dto;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for hunting sequences.
/// Shows animal info, distance tracking, and hunt choices.
/// </summary>
public class HuntOverlay
{
    public bool IsOpen { get; set; }

    private HuntDto? _currentData;
    private string? _selectedChoice;
    private bool _waitingForContinue;
    private float _distanceAnimProgress;
    private const float DISTANCE_ANIM_DURATION = 0.5f;

    /// <summary>
    /// Open the hunt overlay with initial data.
    /// </summary>
    public void Open(HuntDto data)
    {
        IsOpen = true;
        _currentData = data;
        _selectedChoice = null;
        _waitingForContinue = false;
        _distanceAnimProgress = data.IsAnimatingDistance ? 0f : 1f;
    }

    /// <summary>
    /// Update with new hunt data (e.g., after player choice is processed).
    /// </summary>
    public void Update(HuntDto data)
    {
        _currentData = data;
        _selectedChoice = null;

        if (data.IsAnimatingDistance)
        {
            _distanceAnimProgress = 0f;
        }

        // If outcome is set, we're in continue-wait mode
        if (data.Outcome != null)
        {
            _waitingForContinue = true;
        }
    }

    /// <summary>
    /// Render the hunt overlay.
    /// Returns the selected choice ID when player makes a choice, null otherwise.
    /// </summary>
    public string? Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen || _currentData == null) return null;

        // Update distance animation
        if (_distanceAnimProgress < 1f)
        {
            _distanceAnimProgress = Math.Min(1f, _distanceAnimProgress + deltaTime / DISTANCE_ANIM_DURATION);
        }

        string? result = null;

        // Center the window
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
            ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(450, 500), ImGuiCond.FirstUseEver);

        bool open = IsOpen;
        if (ImGui.Begin("Hunt", ref open, ImGuiWindowFlags.NoCollapse))
        {
            if (_currentData.Outcome != null)
            {
                result = RenderOutcome(_currentData.Outcome);
            }
            else
            {
                result = RenderActiveHunt(_currentData, deltaTime);
            }
        }
        ImGui.End();

        if (!open)
        {
            IsOpen = false;
        }

        return result;
    }

    private string? RenderActiveHunt(HuntDto data, float deltaTime)
    {
        string? result = null;

        // Animal header
        ImGui.TextColored(new Vector4(1f, 0.9f, 0.7f, 1f), data.AnimalName);
        ImGui.TextDisabled(data.AnimalDescription);
        ImGui.Spacing();

        // Animal state with color coding
        Vector4 stateColor = data.AnimalState switch
        {
            "idle" => new Vector4(0.5f, 0.8f, 0.5f, 1f),     // Green - safe
            "alert" => new Vector4(1f, 0.8f, 0.3f, 1f),      // Yellow - caution
            "detected" => new Vector4(1f, 0.4f, 0.4f, 1f),   // Red - danger
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
        };

        ImGui.Text("Activity:");
        ImGui.SameLine();
        ImGui.Text(data.AnimalActivity);

        ImGui.Text("State:");
        ImGui.SameLine();
        ImGui.TextColored(stateColor, data.AnimalState);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Distance display with animation
        double displayDistance = data.CurrentDistanceMeters;
        if (data.PreviousDistanceMeters.HasValue && _distanceAnimProgress < 1f)
        {
            double prev = data.PreviousDistanceMeters.Value;
            double curr = data.CurrentDistanceMeters;
            displayDistance = prev + (curr - prev) * EaseOutCubic(_distanceAnimProgress);
        }

        // Distance bar
        float distancePct = (float)Math.Clamp(displayDistance / 100.0, 0, 1);
        Vector4 distanceColor = displayDistance switch
        {
            <= 5 => new Vector4(0.3f, 0.9f, 0.3f, 1f),       // Melee - green
            <= 15 => new Vector4(0.5f, 0.8f, 0.5f, 1f),      // Close - light green
            <= 30 => new Vector4(1f, 0.8f, 0.3f, 1f),        // Mid - yellow
            _ => new Vector4(0.6f, 0.6f, 0.6f, 1f)           // Far - gray
        };

        ImGui.Text("Distance:");
        ImGui.SameLine();
        ImGui.TextColored(distanceColor, $"{displayDistance:F0}m");

        // Distance zone label
        string zoneLabel = displayDistance switch
        {
            <= 5 => "MELEE RANGE",
            <= 15 => "Close",
            <= 30 => "Mid Range",
            <= 50 => "Far",
            _ => "Very Far"
        };
        ImGui.SameLine();
        ImGui.TextDisabled($"({zoneLabel})");

        // Visual distance bar
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, distanceColor);
        ImGui.ProgressBar(1f - distancePct, new Vector2(-1, 8), "");
        ImGui.PopStyleColor();

        ImGui.Spacing();

        // Time spent
        ImGui.Text($"Time: {data.MinutesSpent} minutes");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Status message
        if (!string.IsNullOrEmpty(data.StatusMessage))
        {
            ImGui.TextWrapped(data.StatusMessage);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        // Choices
        ImGui.Text("Actions:");
        ImGui.Spacing();

        foreach (var choice in data.Choices)
        {
            if (!choice.IsAvailable)
            {
                ImGui.BeginDisabled();
            }

            string buttonLabel = choice.Label;
            if (!string.IsNullOrEmpty(choice.Description))
            {
                buttonLabel += $" - {choice.Description}";
            }

            if (ImGui.Button(buttonLabel, new Vector2(-1, 0)))
            {
                result = choice.Id;
            }

            if (!choice.IsAvailable)
            {
                ImGui.EndDisabled();

                if (!string.IsNullOrEmpty(choice.DisabledReason) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip(choice.DisabledReason);
                }
            }
        }

        return result;
    }

    private string? RenderOutcome(HuntOutcomeDto outcome)
    {
        // Outcome header with color
        Vector4 resultColor = outcome.Result switch
        {
            "success" => new Vector4(0.3f, 0.9f, 0.3f, 1f),
            "fled" => new Vector4(1f, 0.7f, 0.3f, 1f),
            "abandoned" => new Vector4(0.7f, 0.7f, 0.7f, 1f),
            "combat" => new Vector4(1f, 0.4f, 0.4f, 1f),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
        };

        string resultTitle = outcome.Result switch
        {
            "success" => "Hunt Successful!",
            "fled" => "Prey Escaped",
            "abandoned" => "Hunt Abandoned",
            "combat" => "Combat Engaged!",
            _ => "Hunt Ended"
        };

        ImGui.TextColored(resultColor, resultTitle);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Result message
        ImGui.TextWrapped(outcome.Message);

        ImGui.Spacing();

        // Time spent
        ImGui.Text($"Time spent: {outcome.TotalMinutesSpent} minutes");

        // Items gained
        if (outcome.ItemsGained.Count > 0)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.5f, 1f), "Items Gained:");
            foreach (var item in outcome.ItemsGained)
            {
                ImGui.Text($"  - {item}");
            }
        }

        // Effects applied
        if (outcome.EffectsApplied.Count > 0)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f), "Effects:");
            foreach (var effect in outcome.EffectsApplied)
            {
                ImGui.Text($"  - {effect}");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Continue button
        if (ImGui.Button("Continue", new Vector2(-1, 30)))
        {
            IsOpen = false;
            return "continue";
        }

        return null;
    }

    /// <summary>
    /// Check if the overlay has a pending choice.
    /// </summary>
    public bool HasChoice => _selectedChoice != null;

    /// <summary>
    /// Check if we're waiting for continue after outcome.
    /// </summary>
    public bool IsWaitingForContinue => _waitingForContinue && _currentData?.Outcome != null;

    /// <summary>
    /// Close the overlay and reset state.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        _currentData = null;
        _selectedChoice = null;
        _waitingForContinue = false;
    }

    private static float EaseOutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
}
