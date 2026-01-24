using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Actions.Variants;
using text_survival.Environments.Features;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for foraging with environmental clues, focus selection, and time options.
/// Shows clues that can be clicked to auto-select suggested focus.
/// Queries domain objects directly instead of using DTOs.
/// </summary>
public class ForageOverlay
{
    public bool IsOpen { get; set; }

    private GameContext? _ctx;
    private ForageFeature? _feature;
    private List<ForageClue>? _clues;
    private string? _selectedFocusId;
    private int _selectedMinutes;
    private ForageResult? _result;

    // Standard time options for foraging
    private static readonly (string Id, int Minutes)[] TimeOptions =
    [
        ("15", 15),
        ("30", 30),
        ("60", 60)
    ];

    /// <summary>
    /// Open the forage overlay with domain objects.
    /// </summary>
    public void Open(GameContext ctx, ForageFeature feature, List<ForageClue> clues)
    {
        IsOpen = true;
        _ctx = ctx;
        _feature = feature;
        _clues = clues;
        _selectedFocusId = null;
        _selectedMinutes = 0;
        _result = null;
    }

    /// <summary>
    /// Close the overlay and reset state.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
        _ctx = null;
        _feature = null;
        _clues = null;
        _selectedFocusId = null;
        _selectedMinutes = 0;
        _result = null;
    }

    /// <summary>
    /// Render the forage overlay.
    /// Returns the result when player confirms, null while selecting.
    /// </summary>
    public ForageResult? Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen || _feature == null || _ctx == null) return null;

        ForageResult? result = null;

        // Center the window
        OverlaySizes.SetupStandard();

        if (ImGui.Begin("Foraging", ImGuiWindowFlags.NoCollapse))
        {
            result = RenderForageUI();
        }
        ImGui.End();

        return result;
    }

    private ForageResult? RenderForageUI()
    {
        if (_feature == null || _ctx == null || _clues == null) return null;

        // Quality header - query feature directly
        string quality = _feature.GetQualityDescription();
        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), $"Resources look {quality}.");

        // Exploration progress - calculate directly from current location
        double explorationPct = _ctx.CurrentLocation.GetExplorationPct();
        if (explorationPct >= 1.0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 0.5f, 1f), "Fully explored");
        }
        else
        {
            int pctDisplay = (int)(explorationPct * 100);
            ImGui.TextColored(new Vector4(0.6f, 0.7f, 0.8f, 1f), $"{pctDisplay}%% explored");
        }

        ImGui.Spacing();

        // Clues section - iterate domain objects directly
        if (_clues.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "You notice:");
            ImGui.Spacing();

            for (int i = 0; i < _clues.Count; i++)
            {
                var clue = _clues[i];
                string? suggestedFocusId = clue.GetSuggestedFocusId();

                // Make clues clickable if they suggest a focus
                if (!string.IsNullOrEmpty(suggestedFocusId))
                {
                    // Clickable clue - highlight on hover
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.8f, 1f, 1f));
                    ImGui.Bullet();
                    ImGui.SameLine();

                    // Use selectable for click handling
                    if (ImGui.Selectable($"{clue.Description}##clue_{i}", false, ImGuiSelectableFlags.None))
                    {
                        _selectedFocusId = suggestedFocusId;
                    }

                    // Tooltip explaining the click
                    if (ImGui.IsItemHovered())
                    {
                        var tooltipFocusOptions = _feature.GetAvailableFocusOptions();
                        var suggestedFocus = tooltipFocusOptions.FirstOrDefault(f => f.Id == suggestedFocusId);
                        if (suggestedFocus != default)
                        {
                            ImGui.SetTooltip($"Click to focus on {suggestedFocus.Label}");
                        }
                    }
                    ImGui.PopStyleColor();
                }
                else
                {
                    // Non-clickable clue
                    ImGui.Bullet();
                    ImGui.SameLine();
                    ImGui.TextWrapped(clue.Description);
                }
            }
            ImGui.Spacing();
        }

        // Warnings section - generate from utility
        var warnings = ForageWarnings.Generate(_ctx, _ctx.CurrentLocation);
        if (warnings.Count > 0)
        {
            ImGui.Separator();
            ImGui.Spacing();

            foreach (var warning in warnings)
            {
                // Color warnings based on content
                Vector4 color = warning.Contains("reduced") || warning.Contains("dark") || warning.Contains("full")
                    ? new Vector4(1f, 0.7f, 0.3f, 1f)  // Orange for negative
                    : new Vector4(0.5f, 0.9f, 0.5f, 1f); // Green for positive (tool bonuses)

                ImGui.TextColored(color, $"  {warning}");
            }
            ImGui.Spacing();
        }

        // Focus section - query feature directly
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.9f, 1f), "What do you focus on?");
        ImGui.Spacing();

        var focusOptions = _feature.GetAvailableFocusOptions();

        // Focus buttons in a grid
        float buttonWidth = (ImGui.GetContentRegionAvail().X - 8) / 2; // Two columns with spacing

        int focusIndex = 0;
        foreach (var focus in focusOptions)
        {
            bool isSelected = _selectedFocusId == focus.Id;

            // Two-column layout
            if (focusIndex % 2 == 1)
            {
                ImGui.SameLine();
            }

            // Style selected button differently
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.7f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.35f, 0.55f, 0.75f, 1f));
            }

            if (ImGui.Button($"{focus.Label}##{focus.Id}", new Vector2(buttonWidth, 28)))
            {
                _selectedFocusId = focus.Id;
            }

            if (isSelected)
            {
                ImGui.PopStyleColor(2);
            }

            // Show description in tooltip
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(focus.Description);
            }

            focusIndex++;
        }

        // Show selected focus description
        if (_selectedFocusId != null)
        {
            var selectedFocus = focusOptions.FirstOrDefault(f => f.Id == _selectedFocusId);
            if (selectedFocus != default)
            {
                ImGui.TextDisabled($"  {selectedFocus.Description}");
            }
        }

        ImGui.Spacing();

        // Time section - use static options
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.9f, 1f), "How long?");
        ImGui.Spacing();

        // Time buttons in a row
        float timeButtonWidth = (ImGui.GetContentRegionAvail().X - 8) / TimeOptions.Length;

        foreach (var time in TimeOptions)
        {
            bool isSelected = _selectedMinutes == time.Minutes;

            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.7f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.35f, 0.55f, 0.75f, 1f));
            }

            if (ImGui.Button($"{time.Minutes} min##{time.Id}", new Vector2(timeButtonWidth, 28)))
            {
                _selectedMinutes = time.Minutes;
            }

            if (isSelected)
            {
                ImGui.PopStyleColor(2);
            }

            if (time != TimeOptions.Last())
            {
                ImGui.SameLine();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Action buttons
        bool canSearch = _selectedFocusId != null && _selectedMinutes > 0;

        // Search button
        if (!canSearch)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Search", new Vector2(-1, 32)))
        {
            var focus = ParseFocus(_selectedFocusId!);
            _result = new ForageResult(focus, _selectedMinutes);
            IsOpen = false;
            return _result;
        }

        if (!canSearch)
        {
            ImGui.EndDisabled();
        }

        ImGui.Spacing();

        // Bottom row: Cancel and Keep Walking
        float halfWidth = (ImGui.GetContentRegionAvail().X - 8) / 2;

        if (ImGui.Button("Cancel", new Vector2(halfWidth, 28)))
        {
            _result = new ForageResult(null, 0);
            IsOpen = false;
            return _result;
        }

        ImGui.SameLine();

        // Keep Walking button
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.25f, 0.3f, 0.35f, 1f));
        if (ImGui.Button("Keep Walking (5 min)", new Vector2(halfWidth, 28)))
        {
            ImGui.PopStyleColor();
            _result = new ForageResult(null, -1); // -1 signals keep walking
            IsOpen = false;
            return _result;
        }
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Spend 5 minutes walking to find new clues");
        }

        return null;
    }

    private static ForageFocus ParseFocus(string focusId)
    {
        return focusId switch
        {
            "fuel" => ForageFocus.Fuel,
            "food" => ForageFocus.Food,
            "medicine" => ForageFocus.Medicine,
            "materials" => ForageFocus.Materials,
            _ => ForageFocus.General
        };
    }
}

/// <summary>
/// Result from the forage overlay.
/// </summary>
public record ForageResult(ForageFocus? Focus, int Minutes);
