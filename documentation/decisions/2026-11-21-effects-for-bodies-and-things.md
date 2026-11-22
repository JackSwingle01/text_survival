# Effects apply to bodies and things, through one exposure calculation

Wet, frozen and spoiling are effects on bodies and things alike, run by one pure kernel from one definition table, and every effect reads one `Exposure.At` (air with fire heat, wind at it, precipitation, ground wetting, cover). Before, the body had a full exposure model and things had almost none, and wind reached the body through two separate paths that disagreed; sharing the physics makes a wet hide, wet tinder and wet clothes behave alike and removes that class of bug.

**Consequences:** carcass decay becomes the Spoiling effect; carried things take their carrier's exposure; state is per stack, merged by weight. The kernel must meet the verification bar in `emergent-world-design.md` §3.3 (property tests, a reference model, mutation testing), because effects code has been a major source of bugs.
