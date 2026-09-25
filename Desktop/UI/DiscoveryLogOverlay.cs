using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using System.Numerics;
using text_survival.UI;
using Raylib_cs;

namespace text_survival.Desktop.UI;

public class DiscoveryLogOverlay
{
    private bool _open, _initialFocus;
    public bool IsOpen { get => _open; set { if (value && !_open) _initialFocus = true; _open = value; } }
    private DiscoveryLogDto? _data;
    public int SelectedCategory { get; private set; }
    public void SetData(DiscoveryLogDto data) => _data = data;
    public bool Render(float deltaTime)
    {
        if (!IsOpen || _data == null) return false;
        OverlaySizes.SetupWide();
        bool open = IsOpen;
        if (ImGui.Begin("Discovery Log", ref open, ImGuiWindowFlags.NoCollapse))
        {
            UiText.Wrapped("F2 categories · F3 entries · arrows / Page Up / Page Down scroll · Esc close");
            ImGui.BeginChild("categories", new Vector2(ImGui.GetContentRegionAvail().X * .28f, -60 * UiStyle.Scale));
            UiNavigation.Initial(ref _initialFocus); UiNavigation.Region(KeyboardKey.F2);
            for (int i = 0; i < _data.Categories.Count; i++)
            {
                var category = _data.Categories[i];
                if (UiIcons.Button(CategoryIcon(i), category.Name, $"category-{i}", new(-1, 0), selected: i == SelectedCategory)) SelectedCategory = i;
            }
            ImGui.EndChild(); ImGui.SameLine();
            ImGui.BeginChild("discovery-entries", new Vector2(0, -60 * UiStyle.Scale));
            UiNavigation.ScrollRegion(KeyboardKey.F3);
            if (_data.Categories.Count > 0)
            {
                SelectedCategory = Math.Clamp(SelectedCategory, 0, _data.Categories.Count - 1);
                var category = _data.Categories[SelectedCategory];
                UiIcons.LabelColored(CategoryIcon(SelectedCategory), HudWidgets.Heading, category.CountDisplay);
                foreach (var item in category.Discovered) UiText.Wrapped(item);
                if (category.RemainingCount > 0) UiText.Disabled($"{category.RemainingCount} undiscovered");
                if (category.Discovered.Count == 0 && category.RemainingCount == 0) UiText.Disabled("Nothing to discover in this category.");
            }
            ImGui.EndChild();
            if (UiIcons.Button("back", "Close [Esc]", "close", new(-1, 0)) || UiNavigation.Key(ImGuiKey.Escape)) open = false;
        }
        ImGui.End(); IsOpen = open; return !open;
    }
    public static string CategoryIcon(int index) => index switch { 0 => "location", 1 => "beasts", 2 => "food", 3 => "medicine", _ => "project" };
}
