# UI input preview

Run `dotnet run --project tools/UiPreview` from the repository root in a desktop/OpenGL session.

This opens the production HUD and UI-size dialog on a deterministic fresh world.
It never loads player saves or executes gameplay commands. The working directory
is a dedicated `text-survival-ui-preview` folder under the OS temporary directory;
its UI-size preference and `input.log` are isolated from the game.

Close the initial settings dialog with Escape, use I for Inventory or C for
Crafting, and F1 to reopen settings. Other world actions are logged but not
executed. Use the normal window close control to quit; Escape does not exit the
application. The log records backend navigation flags before configuration and
observed actions. Screens and focus behavior are production components.

The screenshot-only companion is `tools/HudPreview`; it now captures the settings
dialog at all three UI sizes. Native ImGui navigation tests run without a desktop
window in `text_survival.Tests/Desktop/UI/UiNavigationTests.cs`.
