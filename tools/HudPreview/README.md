# HUD preview

Render production HUD components with a deterministic seed and no save loading or gameplay execution:

```sh
dotnet run --project tools/HudPreview/HudPreview.csproj -- /tmp/hud-preview
```

Requires a desktop/OpenGL session. Images cover a fresh camp, selected adjacent destination,
crowded condition/history state, 1280x720, 1280x720 at increased font scale, developed camp, combat, a production blocking dialog, and the UI-size dialog at Compact, Comfortable and Large settings. Uses the same
HudController, panels, action descriptors and WorldRenderer as the game. Preview inputs are
not persisted. The renderer exports native framebuffer images (Retina images are twice the
logical window dimensions).

The preview is for layout regression checks; tests in `text_survival.Tests/Desktop/UI` cover
geometry, selection, travel, action eligibility and retained-history following. Gameplay and
modal interactions still need verification in the running game.

Phase 2 also captures fresh camp (0), destination (1), low vitals/effects (2), and developed camp (5) at 1280×720 in every scale preset: `hud-{scenario}-720-{preset}.png`. Low-vital captures include reserved feedback.

Phase 3 also captures production food, fire, inventory, crafting, transfer, quantity and confirmation dialogs at 1600×900 Comfortable and 1280×720 Large (`phase3-*.png`). A connected graphical display is required.

Phase 4 adds the combat HUD to the 1280×720 Compact/Comfortable/Large matrix. The Phase 4 render attempt also failed during GLFW monitor detection; it produced no screenshots.

Phase 5 preview rendering now succeeds on the available monitor. Run
`dotnet tools/HudPreview/bin/Debug/net10.0/HudPreview.dll /tmp/ui-phase5-preview --phase5-only`
for companions, discoveries, event choices, structured results, quantities and
expanded history at 1600×900 Comfortable and 1280×720 Large. Output is captured
at Retina resolution. Fixtures use fresh in-memory worlds and disable ImGui INI
persistence. The earlier monitor failures remain historical notes.
