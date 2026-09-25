using System.Numerics;
using ImGuiNET;
using text_survival.UI;
using ImGui = text_survival.Desktop.UI.GameGui;

namespace text_survival.Desktop.UI;

public sealed class WorkResultDialog(WorkResultView view)
{
    private bool _focus = true;
    public bool Render()
    {
        OverlaySizes.SetupStandard(); ImGui.Begin(view.Title, ImGuiWindowFlags.NoCollapse);
        ImGui.BeginChild("result-body", new Vector2(0, -60 * UiStyle.Scale));
        UiNavigation.ScrollRegion(Raylib_cs.KeyboardKey.F3);
        foreach (var line in view.Narrative ?? []) UiText.Wrapped(line);
        UiIcons.LabelWrapped("project", UiStyle.Text, view.Message);
        foreach (var warning in view.Warnings ?? []) UiIcons.LabelWrapped("warning", UiStyle.Caution, warning);
        foreach (var item in view.ItemsGained) UiIcons.LabelWrapped("give", UiStyle.Success, $"+ {item}");
        ImGui.EndChild(); UiNavigation.Initial(ref _focus);
        bool done = UiIcons.Button("continue", "Continue [Enter]", "continue", new(-1, 0)) || UiNavigation.Key(ImGuiKey.Enter);
        ImGui.End(); return done;
    }
}
