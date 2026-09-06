# Terrain compositor preview

Run from the repository root:

```sh
dotnet run --project tools/TerrainPreview
```

This loads the native PXA sources through PixelArtCli and calls the game's
`TerrainRenderer.Rasterize` directly. No window or graphics context is needed.
Outputs are written to `assets/previews/terrain-blending/`:

- `hard-borders.png`: 2× terrain detail with blending disabled.
- `blended.png`: the same layout with the runtime border blend enabled.
- `comparison.png`: hard borders on the left, blended borders on the right.

The preview includes the original player sprite at its 20%-reduced size.
It isolates terrain rendering; rivers, trails, fog, highlights, and other
world overlays are drawn by their existing renderers in the game.

Terrain source textures span 2×2 map cells. A narrow, world-aligned blend
uses cardinal and diagonal neighbors to join different terrain types.
Unknown/off-map neighbors are excluded by the world renderer. Generated
textures are cached; changes to the known neighborhood produce a new cache
entry, so exploration reveals transitions without leaking hidden terrain.
