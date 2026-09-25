# Phase 2 — Comfortable, stable HUD

Implemented the HUD phase. Everyday dialog workflows remain Phase 3; combat remains Phase 4.

## Behavior

- Food/water, treatment, fire, rest/camp, storage, people and wait occupy seven permanent slots at the top of the location panel. Fire and rest keep the same control identity when their verb changes. Selecting a destination changes only the area below these slots.
- Unavailable controls remain focusable, retain category art, and show a blocked badge and subdued text. Hover or keyboard focus explains why. Direct HUD shortcuts still use the existing input router and eligibility checks.
- Contextual work uses stable group/identity order. Work that disappears remains unavailable in the current location, preserving its native ImGui identity; leaving the location resets that retained list.
- The top bar has two predictable rows for time/weather and navigation. Panel sizing accommodates scale presets, with independent scrolling for conditions and contextual work.
- Removed the changing priority summary. Warmth and low vital values use colored labels, borders and an exclamation mark on their existing rows. Normal bars have dark fills for readable numbers. Temperature details use the same temperature severity treatment. No motion was introduced.
- Injuries and organs use name order instead of severity order. Full-width injury bars and wrapped name/severity labels avoid squeezing three columns into the survivor panel. Effects use stable kind order.
- History controls stay in place, including Latest when following. Temporary feedback has a reserved line. The existing log-follow implementation is unchanged.
- Existing icons now appear on HUD navigation, core actions, work, travel and history controls. Added person and location icons using PixelArtCli; rare work still uses category/fallback art pending Phase 5.

## Verification

- Main game and HudPreview build: zero warnings/errors.
- Desktop tests: **41 passed**. New cases check core identities across wounds/fire changes and retention/eligibility of disappearing work. Existing input, native ImGui navigation, layout/camera and event-history tests pass.
- Production HudPreview renders fresh camp, selected destination, low vitals with multiple effects and damaged body parts, developed camp, and disabled controls. The four camp scenarios are captured at 1280×720 with Compact, Comfortable and Large; regular captures use 1600×900. Reviewed actual rendered images, including the smallest viewport at Large scale.
- Visual checks led to darker vital fills, more top-bar height, a shorter clothing disclosure label and full-width injury rows. At 1280×720 Large, contextual work/destination details require scrolling below the visible core actions; the map retains a separate central region.
- New icons validated and rendered with PixelArtCli, then inspected enlarged, in a contact sheet, at 32 pixels, and inside the HUD.
- Native rendering and automated navigation checks do not constitute a physical keyboard playthrough or establish subjective comfort. That integrated verification remains in later phases.

Preview artifacts: `assets/previews/ui-phase2-hud.png`, `assets/previews/ui-phase2-warnings.png`, and `assets/previews/ui-phase2-icons.png`.
