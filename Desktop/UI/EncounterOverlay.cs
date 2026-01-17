using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Desktop.Dto;

namespace text_survival.Desktop.UI;

public class EncounterOverlay
{
    public bool IsOpen { get; set; }

    private EncounterDto? _currentData;
    private float _distanceAnimProgress;
    private const float DISTANCE_ANIM_DURATION = 0.5f;

    public void Open(EncounterDto data)
    {
        IsOpen = true;
        _currentData = data;
        _distanceAnimProgress = data.IsAnimatingDistance ? 0f : 1f;
    }

    public void Update(EncounterDto data)
    {
        _currentData = data;

        if (data.IsAnimatingDistance)
        {
            _distanceAnimProgress = 0f;
        }
    }

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
        OverlaySizes.SetupStandard();

        if (ImGui.Begin("Predator Encounter", ImGuiWindowFlags.NoCollapse))
        {
            if (_currentData.Outcome != null)
            {
                result = RenderOutcome(_currentData.Outcome);
            }
            else
            {
                result = RenderActiveEncounter(_currentData, deltaTime);
            }
        }
        ImGui.End();

        return result;
    }

    private string? RenderActiveEncounter(EncounterDto data, float deltaTime)
    {
        string? result = null;

        // Predator header with threat color
        Vector4 nameColor = data.BoldnessLevel switch
        {
            >= 0.8 => new Vector4(1f, 0.3f, 0.3f, 1f),     // Aggressive - red
            >= 0.6 => new Vector4(1f, 0.6f, 0.3f, 1f),     // Bold - orange
            >= 0.4 => new Vector4(1f, 0.8f, 0.4f, 1f),     // Wary - yellow
            _ => new Vector4(0.7f, 0.8f, 0.7f, 1f)          // Cautious - light
        };

        ImGui.TextColored(nameColor, data.PredatorName);
        ImGui.Spacing();

        // Boldness indicator
        ImGui.Text("Boldness:");
        ImGui.SameLine();

        Vector4 boldnessColor = data.BoldnessDescriptor switch
        {
            "aggressive" => new Vector4(1f, 0.2f, 0.2f, 1f),
            "bold" => new Vector4(1f, 0.5f, 0.2f, 1f),
            "wary" => new Vector4(1f, 0.8f, 0.3f, 1f),
            "cautious" => new Vector4(0.5f, 0.8f, 0.5f, 1f),
            "hesitant" => new Vector4(0.4f, 0.7f, 0.9f, 1f),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
        };

        ImGui.TextColored(boldnessColor, data.BoldnessDescriptor.ToUpperInvariant());

        // Boldness bar
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, boldnessColor);
        ImGui.ProgressBar((float)data.BoldnessLevel, new Vector2(-1, 8), "");
        ImGui.PopStyleColor();

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

        // Distance zone colors
        Vector4 distanceColor = displayDistance switch
        {
            <= 3 => new Vector4(1f, 0.2f, 0.2f, 1f),         // Melee - danger
            <= 8 => new Vector4(1f, 0.5f, 0.2f, 1f),         // Close - warning
            <= 15 => new Vector4(1f, 0.8f, 0.3f, 1f),        // Mid - caution
            _ => new Vector4(0.6f, 0.8f, 0.6f, 1f)           // Far - safer
        };

        ImGui.Text("Distance:");
        ImGui.SameLine();
        ImGui.TextColored(distanceColor, $"{displayDistance:F0}m");

        // Distance zone label
        string zoneLabel = displayDistance switch
        {
            <= 3 => "MELEE - DANGER",
            <= 8 => "Close Range",
            <= 15 => "Mid Range",
            <= 25 => "Far",
            _ => "Very Far"
        };
        ImGui.SameLine();
        ImGui.TextDisabled($"({zoneLabel})");

        // Visual distance bar (inverted - full bar = danger)
        float distancePct = (float)Math.Clamp(displayDistance / 30.0, 0, 1);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, distanceColor);
        ImGui.ProgressBar(1f - distancePct, new Vector2(-1, 8), "");
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Threat factors
        if (data.ThreatFactors.Count > 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f), "The predator is drawn by:");
            foreach (var factor in data.ThreatFactors)
            {
                ImGui.Text($"  - {factor.Description}");
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

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

            // Color code dangerous vs safe choices
            bool isDangerous = choice.Id is "fight" or "charge";
            bool isSafe = choice.Id is "back" or "run" or "drop_meat";

            if (isDangerous)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.2f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.3f, 0.3f, 1f));
            }
            else if (isSafe)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.4f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.5f, 0.4f, 1f));
            }

            if (ImGui.Button(buttonLabel, new Vector2(-1, 0)))
            {
                result = choice.Id;
            }

            if (isDangerous || isSafe)
            {
                ImGui.PopStyleColor(2);
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

    private string? RenderOutcome(EncounterOutcomeDto outcome)
    {
        // Outcome header with color
        Vector4 resultColor = outcome.Result switch
        {
            "retreated" => new Vector4(0.5f, 0.8f, 0.5f, 1f),
            "escaped" => new Vector4(0.6f, 0.9f, 0.6f, 1f),
            "fight" => new Vector4(1f, 0.5f, 0.3f, 1f),
            "died" => new Vector4(1f, 0.2f, 0.2f, 1f),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
        };

        string resultTitle = outcome.Result switch
        {
            "retreated" => "Predator Retreated",
            "escaped" => "You Escaped!",
            "fight" => "Combat!",
            "died" => "You Were Killed",
            _ => "Encounter Ended"
        };

        ImGui.TextColored(resultColor, resultTitle);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Result message
        ImGui.TextWrapped(outcome.Message);

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
    /// Close the overlay and reset state.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        _currentData = null;
    }

    private static float EaseOutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
}
