# Save compatibility starts at the first playable release

Until the rebuild has a playable release, its save format changes freely and old saves may stop loading; there are no save migrations or migration tests. Nobody plays rebuild saves yet, and versioning plus migrations now would slow every step that touches saved state. From the first playable release on, saves are versioned and each format change ships with a migration and a test against real-shaped saves.
