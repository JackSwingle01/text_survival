using ImGuiNET;
using System.Numerics;

namespace text_survival.Desktop.UI;

/// <summary>Consequential yes/no prompts always begin on the safe answer.</summary>
public sealed class ConfirmationDialog(string prompt)
{
    private bool _focus = true;
    public bool? Render()
    {
        bool? answer = null;
        OverlaySizes.SetupSmallDialog();
        ImGui.Begin("Confirm", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
        UiText.Wrapped(prompt);
        UiText.Wrapped("Tab: choose · Enter: answer · Esc: No");
        ImGui.Separator();
        UiNavigation.Initial(ref _focus);
        if (UiIcons.Button("cancel", "No [Esc]", "no", new Vector2(-1, 0)) || UiNavigation.Key(ImGuiKey.Escape)) answer = false;
        if (UiIcons.Button("confirm", "Yes", "yes", new Vector2(-1, 0))) answer = true;
        ImGui.End();
        return answer;
    }
}
