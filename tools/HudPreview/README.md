# HUD preview

Render production HUD components with a deterministic seed and no save loading or gameplay execution:

```sh
dotnet run --project tools/HudPreview/HudPreview.csproj -- /tmp/hud-preview
```

Requires a desktop/OpenGL session. Images cover a fresh camp, selected adjacent destination,
crowded condition/history state, 1280x720, 1280x720 at increased font scale, developed camp, combat, and a production blocking dialog. Uses the same
HudController, panels, action descriptors and WorldRenderer as the game. Preview inputs are
not persisted. The renderer exports native framebuffer images (Retina images are twice the
logical window dimensions).

The preview is for layout regression checks; tests in `text_survival.Tests/Desktop/UI` cover
geometry, selection, travel, action eligibility and retained-history following. Gameplay and
modal interactions still need verification in the running game.
