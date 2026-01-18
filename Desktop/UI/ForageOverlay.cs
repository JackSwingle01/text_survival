using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Variants;
using text_survival.Desktop.Dto;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for foraging with environmental clues, focus selection, and time options.
/// Shows clues that can be clicked to auto-select suggested focus.
/// </summary>
public class ForageOverlay
{
    public bool IsOpen { get; set; }

    private ForageDto? _currentData;
    private string? _selectedFocusId;
    private int _selectedMinutes;
    private ForageResult? _result;

    /// <summary>
    /// Open the forage overlay with data.
    /// </summary>
    public void Open(ForageDto data)
    {
        IsOpen = true;
        _currentData = data;
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
        _currentData = null;
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
        if (!IsOpen || _currentData == null) return null;

        ForageResult? result = null;

        // Center the window
        OverlaySizes.SetupStandard();

        if (ImGui.Begin("Foraging", ImGuiWindowFlags.NoCollapse))
        {
            result = RenderForageUI(_currentData);
        }
        ImGui.End();

        return result;
    }

    private ForageResult? RenderForageUI(ForageDto data)
    {
        // Quality header
        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), $"Resources look {data.LocationQuality}.");
        ImGui.Spacing();

        // Clues section
        if (data.Clues.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), "You notice:");
            ImGui.Spacing();

            foreach (var clue in data.Clues)
            {
                // Make clues clickable if they suggest a focus
                if (!string.IsNullOrEmpty(clue.SuggestedFocusId))
                {
                    // Clickable clue - highlight on hover
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.8f, 1f, 1f));
                    ImGui.Bullet();
                    ImGui.SameLine();

                    // Use selectable for click handling
                    if (ImGui.Selectable($"{clue.Description}##clue_{clue.Id}", false, ImGuiSelectableFlags.None))
                    {
                        _selectedFocusId = clue.SuggestedFocusId;
                    }

                    // Tooltip explaining the click
                    if (ImGui.IsItemHovered())
                    {
                        var suggestedFocus = data.FocusOptions.FirstOrDefault(f => f.Id == clue.SuggestedFocusId);
                        if (suggestedFocus != null)
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

        // Warnings section
        if (data.Warnings.Count > 0)
        {
            ImGui.Separator();
            ImGui.Spacing();

            foreach (var warning in data.Warnings)
            {
                // Color warnings based on content
                Vector4 color = warning.Contains("reduced") || warning.Contains("dark") || warning.Contains("full")
                    ? new Vector4(1f, 0.7f, 0.3f, 1f)  // Orange for negative
                    : new Vector4(0.5f, 0.9f, 0.5f, 1f); // Green for positive (tool bonuses)

                ImGui.TextColored(color, $"  {warning}");
            }
            ImGui.Spacing();
        }

        // Focus section
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.9f, 1f), "What do you focus on?");
        ImGui.Spacing();

        // Focus buttons in a grid
        float buttonWidth = (ImGui.GetContentRegionAvail().X - 8) / 2; // Two columns with spacing

        int focusIndex = 0;
        foreach (var focus in data.FocusOptions)
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
            var selectedFocus = data.FocusOptions.FirstOrDefault(f => f.Id == _selectedFocusId);
            if (selectedFocus != null)
            {
                ImGui.TextDisabled($"  {selectedFocus.Description}");
            }
        }

        ImGui.Spacing();

        // Time section
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.9f, 1f), "How long?");
        ImGui.Spacing();

        // Time buttons in a row
        float timeButtonWidth = (ImGui.GetContentRegionAvail().X - 8) / data.TimeOptions.Count;

        foreach (var time in data.TimeOptions)
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

            if (time != data.TimeOptions.Last())
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
