# HUD migration context

- User reported unscrollable left overflow, journal overlap, obstructive top popups, confusing right-menu groups and inconsistent placement of action buttons such as Go.
- Inspected actual desktop implementation and proposed a pinned/scrollable survivor rail, permanent top navigation, selected-location inspector, and event strip beneath the map.
- User liked the generated mock and requested a migration plan plus useful backing-code modularization. User subsequently authorized implementation, which is now complete.
- Full decisions and acceptance gates are in `hud-migration-plan.md`; `hud-mock.png` is the reference visual.
- Preserve the single frame loop, async prompt scheduler, rendering/simulation separation, existing game action results and authoritative travel calculations.
- Current sources: `Desktop/DesktopUi.cs`, `Desktop/Rendering/{Camera,WorldRenderer}.cs`, `Desktop/UI/{StatsPanel,ActionPanel,TilePopup,JournalPanel,ToastManager,OverlaySizes}.cs`, `Desktop/Input/HotkeyRegistry.cs`, `UI/{PlayerAction,GameDisplay,NarrativeLog,ToastFeed}.cs`.
- Existing validation: `text_survival.Tests/Architecture/{LayeringTests,FrameSchedulerTests}.cs` and `text_survival.Tests/Desktop/Rendering/CameraTests.cs`.
- The source tree was already dirty with crafting/food/inventory and world-rendering work when planning started. The migration changes the desktop HUD and feedback presentation while preserving the pre-existing crafting/food/inventory work.
- The generated visual is a design reference, not evidence of implemented behaviors or a new multi-tile travel feature.

User refinement: weather details open from the top weather summary; People has one visible entry in the location panel; Follow Player uses G (MacBook-friendly), defined in HotkeyRegistry.

Implementation complete. Refer to plan verification notes for test counts, native screenshots, and the unresolved Computer Use click-targeting limitation.
