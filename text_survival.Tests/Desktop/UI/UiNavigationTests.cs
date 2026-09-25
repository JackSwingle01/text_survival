using System.Numerics;
using ImGuiNET;
using text_survival.Desktop.UI;

namespace text_survival.Tests.Desktop.UI;

[CollectionDefinition("Native UI", DisableParallelization = true)]
public class NativeUiCollection { }

[Collection("Native UI")]
public class UiNavigationTests
{
    [Fact]
    public void TabAndEnterNavigateButCannotActivateUnavailableSharedControl()
    {
        var previous = ImGui.GetCurrentContext();
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO();
            unsafe { io.NativePtr->IniFilename = null; } // Never read/write the player's window layout.
            io.DisplaySize = new Vector2(800, 600);
            io.DeltaTime = 1f / 60;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            UiStyle.Apply(new UiSettings());
            io.Fonts.AddFontDefault();
            io.Fonts.GetTexDataAsRGBA32(out IntPtr _, out int _, out int _);
            bool activated = false;
            bool disabledActivated = false;
            bool disabledFocused = false;
            bool enabledFocused = false;
            var trace = new List<string>();
            void Frame(bool focus = false)
            {
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(new(20, 20));
                ImGui.SetNextWindowSize(new(600, 400));
                ImGui.Begin("Controls", ImGuiWindowFlags.NoSavedSettings);
                if (focus) ImGui.SetKeyboardFocusHere();
                disabledActivated |= UiControls.Button(new("unavailable", "Apply", "confirm", DisabledReason: "Choose another size first."));
                disabledFocused = ImGui.IsItemFocused();
                activated |= UiControls.Button(new("available", "Continue", "continue"));
                enabledFocused = ImGui.IsItemFocused();
                trace.Add($"disabled={disabledFocused} enabled={enabledFocused} activated={activated} nav={io.NavActive}/{io.NavVisible} tab={ImGui.IsKeyPressed(ImGuiKey.Tab)}");
                ImGui.End();
                ImGui.Render();
            }
            void Key(ImGuiKey key)
            {
                io.AddKeyEvent(key, true); Frame(); Frame();
                io.AddKeyEvent(key, false); Frame(); Frame();
            }
            Frame(true); Frame();
            Assert.True(disabledFocused);
            Key(ImGuiKey.Enter);
            Assert.False(disabledActivated);
            Key(ImGuiKey.Tab);
            Assert.True(enabledFocused, string.Join("\n", trace));
            Key(ImGuiKey.Enter);
            Assert.True(activated);
            Assert.False(disabledActivated);
        }
        finally { ImGui.DestroyContext(context); ImGui.SetCurrentContext(previous); }
    }
}
