using ImGuiNET;
using System.Numerics;
using text_survival.Desktop.Dto;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for viewing the Discovery Log.
/// Shows 5 categories: The Land, Beasts, Provisions, Medicine, Works.
/// </summary>
public class DiscoveryLogOverlay
{
    public bool IsOpen { get; set; }

    private DiscoveryLogDto? _data;
    private int _selectedTab;

    /// <summary>
    /// Set the discovery log data to display.
    /// </summary>
    public void SetData(DiscoveryLogDto data)
    {
        _data = data;
    }

    /// <summary>
    /// Render the discovery log overlay. Returns true if overlay should close.
    /// </summary>
    public bool Render(float deltaTime)
    {
        if (!IsOpen || _data == null) return false;

        bool shouldClose = false;

        OverlaySizes.SetupWide();

        bool open = IsOpen;
        if (ImGui.Begin("Discovery Log", ref open, ImGuiWindowFlags.NoCollapse))
        {
            // Tab bar for categories
            if (ImGui.BeginTabBar("DiscoveryTabs"))
            {
                for (int i = 0; i < _data.Categories.Count; i++)
                {
                    var category = _data.Categories[i];
                    string tabLabel = $"{category.Name}";

                    if (ImGui.BeginTabItem(tabLabel))
                    {
                        _selectedTab = i;
                        RenderCategory(category);
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }

            // Close button
            ImGui.Separator();
            if (ImGui.Button("Close [Esc]", new Vector2(-1, 0)))
            {
                shouldClose = true;
            }
        }
        ImGui.End();

        if (!open) shouldClose = true;
        if (shouldClose) IsOpen = false;

        return shouldClose;
    }

    private void RenderCategory(DiscoveryLogCategoryDto category)
    {
        // Category count header
        ImGui.TextColored(new Vector4(0.7f, 0.8f, 0.9f, 1f), category.CountDisplay);
        ImGui.Separator();

        // Scrollable content area
        ImGui.BeginChild("CategoryContent", new Vector2(0, -35), ImGuiChildFlags.Border);

        // Discovered items
        foreach (var item in category.Discovered)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.8f, 1f), item);
        }

        // Undiscovered placeholders
        if (category.RemainingCount > 0)
        {
            ImGui.Spacing();

            // Show up to 20 placeholders to avoid visual overload
            int placeholdersToShow = Math.Min(category.RemainingCount, 20);

            for (int i = 0; i < placeholdersToShow; i++)
            {
                ImGui.TextDisabled("???");
            }

            // If there are more than we're showing, indicate that
            if (category.RemainingCount > placeholdersToShow)
            {
                ImGui.TextDisabled($"... and {category.RemainingCount - placeholdersToShow} more");
            }
        }

        // If nothing discovered and nothing remaining, show empty message
        if (category.Discovered.Count == 0 && category.RemainingCount == 0)
        {
            ImGui.TextDisabled("Nothing to discover in this category.");
        }

        ImGui.EndChild();
    }
}
