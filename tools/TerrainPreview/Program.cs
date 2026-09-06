using PixelArtCli;
using Raylib_cs;
using text_survival.Desktop.Rendering;
using text_survival.Environments.Grid;

// CPU-only visual QA using the same compositor as the game, with native PXA art.
string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var art = new Dictionary<TerrainType, TerrainRenderer.Art[]>();
foreach (var terrain in Enum.GetValues<TerrainType>())
{
    art[terrain] = Directory.GetFiles(Path.Combine(root, "assets/pixelart"), $"{terrain.ToString().ToLowerInvariant()}_tile*.pxa")
        .Order(StringComparer.Ordinal).Select(file =>
        {
            var doc = PxaDocument.Parse(File.ReadAllText(file), file);
            byte[] rgba = doc.ToRgba();
            var pixels = new Color[doc.Width*doc.Height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(rgba[i*4], rgba[i*4+1], rgba[i*4+2], rgba[i*4+3]);
            return new TerrainRenderer.Art(doc.Width, doc.Height, pixels);
        }).ToArray();
}

TerrainType[,] map = {
    { TerrainType.Forest, TerrainType.Forest, TerrainType.Clearing, TerrainType.Plain, TerrainType.Plain, TerrainType.Hills },
    { TerrainType.Forest, TerrainType.Forest, TerrainType.Clearing, TerrainType.Clearing, TerrainType.Hills, TerrainType.Hills },
    { TerrainType.Rock, TerrainType.Forest, TerrainType.Marsh, TerrainType.Marsh, TerrainType.Hills, TerrainType.Mountain },
    { TerrainType.Rock, TerrainType.Rock, TerrainType.Marsh, TerrainType.Water, TerrainType.Hills, TerrainType.Mountain },
    { TerrainType.Rock, TerrainType.Plain, TerrainType.Water, TerrainType.Water, TerrainType.DeepWater, TerrainType.DeepWater },
    { TerrainType.Plain, TerrainType.Plain, TerrainType.Water, TerrainType.DeepWater, TerrainType.DeepWater, TerrainType.DeepWater }
};
const int size = 576;
var player = PxaDocument.Parse(File.ReadAllText(Path.Combine(root, "assets/pixelart/player.pxa"))).ToRgba();
byte[] Render(bool blend)
{
    var result = new byte[size*size*4];
    for (int y = 0; y < 6; y++)
    for (int x = 0; x < 6; x++)
    {
        var neighbors = TerrainRenderer.Neighborhood.Create(map[y, x], (dx, dy) =>
            x+dx >= 0 && x+dx < 6 && y+dy >= 0 && y+dy < 6 ? map[y+dy, x+dx] : null);
        var tile = TerrainRenderer.Rasterize(x, y, neighbors, art, blend);
        for (int py = 0; py < 96; py++)
        for (int px = 0; px < 96; px++)
        {
            var c = tile[(py/2)*48+px/2];
            int dst = ((y*96+py)*size+x*96+px)*4;
            result[dst] = c.R; result[dst+1] = c.G; result[dst+2] = c.B; result[dst+3] = 255;
        }
    }
    const int spriteSize = 42; // 96px cell * .55 * .8, rounded.
    for (int y = 0; y < spriteSize; y++)
    for (int x = 0; x < spriteSize; x++)
    {
        int src = ((y*16/spriteSize)*16+x*16/spriteSize)*4;
        if (player[src+3] == 0) continue;
        int dst = ((144-spriteSize/2+y)*size+240-spriteSize/2+x)*4;
        Array.Copy(player, src, result, dst, 4);
    }
    return result;
}
var hard = Render(false);
var blended = Render(true);
string output = Path.Combine(root, "assets/previews/terrain-blending");
PngWriter.EncodeToFile(Path.Combine(output, "hard-borders.png"), size, size, hard);
PngWriter.EncodeToFile(Path.Combine(output, "blended.png"), size, size, blended);
const int width = size*2+16;
var comparison = new byte[width*size*4];
for (int i = 0; i < comparison.Length; i += 4)
{ comparison[i] = 36; comparison[i+1] = 51; comparison[i+2] = 54; comparison[i+3] = 255; }
for (int y = 0; y < size; y++)
{
    Array.Copy(hard, y*size*4, comparison, y*width*4, size*4);
    Array.Copy(blended, y*size*4, comparison, (y*width+size+16)*4, size*4);
}
PngWriter.EncodeToFile(Path.Combine(output, "comparison.png"), width, size, comparison);
Console.WriteLine($"Rendered runtime terrain comparison to {output}");
