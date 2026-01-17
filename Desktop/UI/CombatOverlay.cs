using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Desktop.Dto;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for strategic distance-based combat.
/// Shows distance zones, player/enemy state, and combat actions.
/// </summary>
public class CombatOverlay
{
    public bool IsOpen { get; set; }

    private CombatDto? _currentData;
    private float _distanceAnimProgress;
    private float _autoAdvanceTimer;
    private const float DISTANCE_ANIM_DURATION = 0.3f;

    /// <summary>
    /// Open the combat overlay with initial data.
    /// </summary>
    public void Open(CombatDto data)
    {
        IsOpen = true;
        _currentData = data;
        _distanceAnimProgress = 1f;
        _autoAdvanceTimer = 0f;
    }

    /// <summary>
    /// Update with new combat data.
    /// </summary>
    public void Update(CombatDto data)
    {
        bool distanceChanged = _currentData?.DistanceMeters != data.DistanceMeters;
        _currentData = data;

        if (distanceChanged && data.PreviousDistanceMeters.HasValue)
        {
            _distanceAnimProgress = 0f;
        }

        // Reset auto-advance timer when data changes
        if (data.AutoAdvanceMs.HasValue)
        {
            _autoAdvanceTimer = data.AutoAdvanceMs.Value / 1000f;
        }
    }

    /// <summary>
    /// Render the combat overlay.
    /// Returns the selected action ID when player makes a choice, null otherwise.
    /// Returns "continue" for auto-advance or outcome dismissal.
    /// </summary>
    public string? Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen || _currentData == null) return null;

        // Update animations
        if (_distanceAnimProgress < 1f)
        {
            _distanceAnimProgress = Math.Min(1f, _distanceAnimProgress + deltaTime / DISTANCE_ANIM_DURATION);
        }

        // Auto-advance timer
        if (_autoAdvanceTimer > 0)
        {
            _autoAdvanceTimer -= deltaTime;
            if (_autoAdvanceTimer <= 0)
            {
                return "continue";
            }
        }

        string? result = null;

        // Center the window
        OverlaySizes.SetupStandard();

        string title = _currentData.Phase == CombatPhase.Outcome ? "Combat Result" : "Combat";

        if (ImGui.Begin(title, ImGuiWindowFlags.NoCollapse))
        {
            if (_currentData.Outcome != null)
            {
                result = RenderOutcome(_currentData.Outcome);
            }
            else
            {
                result = RenderActiveCombat(_currentData, deltaTime);
            }
        }
        ImGui.End();

        return result;
    }

    private string? RenderActiveCombat(CombatDto data, float deltaTime)
    {
        string? result = null;

        // Combat phase header
        string phaseText = data.Phase switch
        {
            CombatPhase.Intro => "Combat Begins!",
            CombatPhase.PlayerChoice => "Your Turn",
            CombatPhase.PlayerAction => "Your Action",
            CombatPhase.AnimalAction => "Enemy Attack!",
            CombatPhase.BehaviorChange => "Enemy Behavior",
            _ => "Combat"
        };

        Vector4 phaseColor = data.Phase switch
        {
            CombatPhase.PlayerChoice => new Vector4(0.5f, 0.8f, 1f, 1f),
            CombatPhase.PlayerAction => new Vector4(0.5f, 0.9f, 0.5f, 1f),
            CombatPhase.AnimalAction => new Vector4(1f, 0.5f, 0.3f, 1f),
            _ => new Vector4(0.8f, 0.8f, 0.8f, 1f)
        };

        ImGui.TextColored(phaseColor, phaseText);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Distance zone display
        RenderDistanceZone(data);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Player status
        RenderPlayerStatus(data);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Threat factors
        if (data.ThreatFactors.Count > 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f), "Threat Factors:");
            foreach (var factor in data.ThreatFactors)
            {
                ImGui.Text($"  - {factor.Description}");
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        // Narrative message
        if (!string.IsNullOrEmpty(data.NarrativeMessage))
        {
            ImGui.TextWrapped(data.NarrativeMessage);
            ImGui.Spacing();
        }

        // Actions (only in PlayerChoice phase)
        if (data.Phase == CombatPhase.PlayerChoice && data.Actions.Count > 0)
        {
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Text("Actions:");
            ImGui.Spacing();

            result = RenderActions(data.Actions);
        }

        // Auto-advance indicator for non-player phases
        if (data.AutoAdvanceMs.HasValue && _autoAdvanceTimer > 0)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            float progress = 1f - (_autoAdvanceTimer / (data.AutoAdvanceMs.Value / 1000f));
            ImGui.ProgressBar(progress, new Vector2(-1, 8), "");
        }

        // Continue button for non-auto phases
        if (data.Phase != CombatPhase.PlayerChoice && !data.AutoAdvanceMs.HasValue)
        {
            ImGui.Spacing();
            if (ImGui.Button("Continue", new Vector2(-1, 30)))
            {
                result = "continue";
            }
        }

        return result;
    }

    private void RenderDistanceZone(CombatDto data)
    {
        // Calculate animated distance
        double displayDistance = data.DistanceMeters;
        if (data.PreviousDistanceMeters.HasValue && _distanceAnimProgress < 1f)
        {
            double prev = data.PreviousDistanceMeters.Value;
            double curr = data.DistanceMeters;
            displayDistance = prev + (curr - prev) * EaseOutCubic(_distanceAnimProgress);
        }

        // Distance zone colors
        Vector4 zoneColor = data.DistanceZone switch
        {
            "melee" => new Vector4(1f, 0.2f, 0.2f, 1f),      // Red - danger
            "close" => new Vector4(1f, 0.5f, 0.2f, 1f),      // Orange
            "mid" => new Vector4(1f, 0.8f, 0.3f, 1f),        // Yellow
            "far" => new Vector4(0.6f, 0.8f, 0.6f, 1f),      // Green - safer
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
        };

        string zoneLabel = data.DistanceZone switch
        {
            "melee" => "MELEE (0-3m)",
            "close" => "CLOSE (3-8m)",
            "mid" => "MID (8-15m)",
            "far" => "FAR (15-25m)",
            _ => data.DistanceZone.ToUpperInvariant()
        };

        ImGui.Text("Distance:");
        ImGui.SameLine();
        ImGui.TextColored(zoneColor, $"{displayDistance:F0}m");
        ImGui.SameLine();
        ImGui.TextColored(zoneColor, $"[{zoneLabel}]");

        // Distance bar (inverted - full = close)
        float distancePct = (float)Math.Clamp(displayDistance / 25.0, 0, 1);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, zoneColor);
        ImGui.ProgressBar(1f - distancePct, new Vector2(-1, 12), "");
        ImGui.PopStyleColor();

        // Zone markers
        ImGui.TextDisabled("  Melee     Close        Mid           Far");
    }

    private void RenderPlayerStatus(CombatDto data)
    {
        // Vitality bar
        Vector4 vitalityColor = data.PlayerVitality switch
        {
            >= 0.7 => new Vector4(0.4f, 0.9f, 0.4f, 1f),
            >= 0.4 => new Vector4(1f, 0.8f, 0.3f, 1f),
            _ => new Vector4(1f, 0.3f, 0.3f, 1f)
        };

        ImGui.Text("Your Status:");
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, vitalityColor);
        ImGui.ProgressBar((float)data.PlayerVitality, new Vector2(-1, 0),
            $"Vitality: {data.PlayerVitality * 100:F0}%");
        ImGui.PopStyleColor();

        // Energy bar
        Vector4 energyColor = data.PlayerEnergy switch
        {
            >= 0.5 => new Vector4(0.3f, 0.7f, 1f, 1f),
            >= 0.25 => new Vector4(1f, 0.7f, 0.3f, 1f),
            _ => new Vector4(0.6f, 0.3f, 0.3f, 1f)
        };

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, energyColor);
        ImGui.ProgressBar((float)data.PlayerEnergy, new Vector2(-1, 0),
            $"Energy: {data.PlayerEnergy * 100:F0}%");
        ImGui.PopStyleColor();

        // Braced status
        if (data.PlayerBraced)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), "BRACED - Ready to counter charge");
        }
    }

    private string? RenderActions(List<CombatActionDto> actions)
    {
        string? result = null;

        foreach (var action in actions)
        {
            if (!action.IsAvailable)
            {
                ImGui.BeginDisabled();
            }

            // Build label with hit chance if available
            string label = action.Label;
            if (!string.IsNullOrEmpty(action.HitChance))
            {
                label += $" ({action.HitChance})";
            }
            if (!string.IsNullOrEmpty(action.Description))
            {
                label += $" - {action.Description}";
            }

            // Color code action types
            bool isAttack = action.Id.Contains("attack") || action.Id.Contains("strike") || action.Id.Contains("thrust");
            bool isDefense = action.Id.Contains("dodge") || action.Id.Contains("block") || action.Id.Contains("brace");
            bool isRetreat = action.Id.Contains("back") || action.Id.Contains("retreat") || action.Id.Contains("disengage");

            if (isAttack)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.25f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.35f, 0.3f, 1f));
            }
            else if (isDefense)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.35f, 0.5f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.45f, 0.6f, 1f));
            }
            else if (isRetreat)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.4f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.5f, 0.4f, 1f));
            }

            if (ImGui.Button(label, new Vector2(-1, 0)))
            {
                result = action.Id;
            }

            if (isAttack || isDefense || isRetreat)
            {
                ImGui.PopStyleColor(2);
            }

            if (!action.IsAvailable)
            {
                ImGui.EndDisabled();

                if (!string.IsNullOrEmpty(action.DisabledReason) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip(action.DisabledReason);
                }
            }
        }

        return result;
    }

    private string? RenderOutcome(CombatOutcomeDto outcome)
    {
        // Outcome header
        Vector4 resultColor = outcome.Result switch
        {
            "victory" => new Vector4(0.3f, 0.9f, 0.3f, 1f),
            "defeat" => new Vector4(1f, 0.2f, 0.2f, 1f),
            "fled" => new Vector4(0.8f, 0.8f, 0.4f, 1f),
            "animal_fled" => new Vector4(0.6f, 0.8f, 0.6f, 1f),
            "disengaged" => new Vector4(0.7f, 0.7f, 0.7f, 1f),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
        };

        string resultTitle = outcome.Result switch
        {
            "victory" => "Victory!",
            "defeat" => "Defeated",
            "fled" => "Escaped",
            "animal_fled" => "Enemy Fled",
            "disengaged" => "Disengaged",
            _ => "Combat Ended"
        };

        ImGui.TextColored(resultColor, resultTitle);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Result message
        ImGui.TextWrapped(outcome.Message);

        // Rewards
        if (outcome.Rewards != null && outcome.Rewards.Count > 0)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.5f, 1f), "Rewards:");
            foreach (var reward in outcome.Rewards)
            {
                ImGui.Text($"  - {reward}");
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
    /// Close the overlay and reset state.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        _currentData = null;
    }

    private static float EaseOutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
}
