using Raylib_cs;
using System.Numerics;
using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments.Grid;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Draws footprints on the ground from the pixel art in assets/icons/tracks/, one file
/// per <see cref="TrackMaker"/>.
///
/// Prints are only legible close up. Sight range reaches twenty tiles from a vantage
/// point, but nobody reads paw prints from two kilometres away - so tracks are drawn
/// within a short radius that widens with the player's perception, which also keeps the
/// map from turning into a diagram of everything that has ever walked past.
/// </summary>
public static class TrackRenderer
{
    /// <summary>Tiles you can read prints from at zero perception, and per point of it.</summary>
    private const int BaseReadRange = 1;
    private const double RangePerPerception = 3;

    /// <summary>
    /// Art is authored heading north, as a gait line across the whole 16x16 canvas, and
    /// drawn at this fraction of a tile. Kept well under the tile so prints stay ground
    /// texture: at a 100px tile this puts a single print at roughly a quarter the size
    /// of a feature icon, which is the pecking order they should read in.
    /// </summary>
    private const float PrintScale = 0.35f;

    private static readonly Dictionary<TrackMaker, Texture2D> _sprites = new();
    private static readonly HashSet<TrackMaker> _reportedMissing = [];

    /// <summary>
    /// Load one sprite per TrackMaker from assets/icons/tracks/ (boot, paw, hoof).
    /// Call after the Raylib window is initialized.
    /// </summary>
    public static void LoadSprites(string iconsAssetsPath)
    {
        string tracksPath = Path.Combine(iconsAssetsPath, "tracks");
        if (!Directory.Exists(tracksPath))
            throw new DirectoryNotFoundException($"Track sprites directory not found: {tracksPath}");

        foreach (string filePath in Directory.GetFiles(tracksPath, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            TrackMaker? maker = name.ToLowerInvariant() switch
            {
                "boot" => TrackMaker.Human,
                "paw" => TrackMaker.Paw,
                "hoof" => TrackMaker.Hoof,
                _ => null
            };

            if (maker == null)
            {
                Console.Error.WriteLine($"TrackRenderer: '{filePath}' does not match any TrackMaker, skipping.");
                continue;
            }

            Texture2D texture = Raylib.LoadTexture(filePath);
            if (texture.Id == 0)
            {
                Console.Error.WriteLine($"TrackRenderer: failed to load '{filePath}'.");
                continue;
            }

            // Pixel art must not be smoothed when scaled up.
            Raylib.SetTextureFilter(texture, TextureFilter.Point);
            _sprites[maker.Value] = texture;
        }

        var missing = Enum.GetValues<TrackMaker>().Where(m => !_sprites.ContainsKey(m)).ToList();
        if (missing.Count > 0)
            Console.Error.WriteLine($"TrackRenderer: no art for {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// Draw every readable set of prints near the player. Runs after the tile pass:
    /// tracks show only on tiles currently in sight, so fog never has to be drawn over them.
    /// </summary>
    /// <param name="timeFactor">0 at midnight, 1 at noon; dims prints with the ground they sit on.</param>
    public static void Render(GameContext ctx, Camera camera, float timeFactor)
    {
        var map = ctx.Map ?? throw new InvalidOperationException("Cannot render without an initialized map.");

        double perception = AbilityCalculator.GetPerception(ctx.player, ctx);
        int readRange = BaseReadRange + (int)(perception * RangePerPerception);

        // Walking out from the player is far cheaper than sweeping the camera: the read
        // radius is a few tiles, the visible grid is hundreds.
        foreach (var position in map.CurrentPosition.GetPositionsInRange(readRange))
        {
            if (!map.IsValidPosition(position.X, position.Y)) continue;
            if (map.GetVisibility(position.X, position.Y) != TileVisibility.Visible) continue;

            var tracks = map.Tracks.At(position);
            if (tracks.Count == 0) continue;

            Vector2 screenPos = camera.WorldToScreen(position.X, position.Y);

            foreach (var (track, freshness) in tracks)
                DrawPrints(track, freshness, screenPos, camera.TileSize, timeFactor);
        }
    }

    private static void DrawPrints(Track track, double freshness, Vector2 tilePos, float tileSize, float timeFactor)
    {
        if (!_sprites.TryGetValue(track.Maker, out Texture2D texture))
        {
            if (_reportedMissing.Add(track.Maker))
                Console.Error.WriteLine($"TrackRenderer: no art for {track.Maker} - nothing drawn.");
            return;
        }

        // Fresh prints are crisp; old ones are barely a suggestion in the snow.
        byte alpha = (byte)(255 * (0.12 + 0.45 * freshness));
        byte brightness = (byte)(255 * (0.4f + timeFactor * 0.6f));
        var tint = new Color(brightness, brightness, brightness, alpha);

        float rotation = track.Heading switch
        {
            Direction.North => 0f,
            Direction.East => 90f,
            Direction.South => 180f,
            Direction.West => 270f,
            _ => 0f
        };

        // Several things crossing one tile shouldn't stamp on top of each other.
        float nudge = tileSize * 0.06f * (int)track.Maker;

        float size = tileSize * PrintScale;
        var source = new Rectangle(0, 0, texture.Width, texture.Height);

        // Rotate about the print's own centre, so the destination is placed centre-first.
        var dest = new Rectangle(
            tilePos.X + tileSize / 2 + nudge,
            tilePos.Y + tileSize / 2 + nudge,
            size, size);
        var origin = new Vector2(size / 2, size / 2);

        Raylib.DrawTexturePro(texture, source, dest, origin, rotation, tint);
    }
}
