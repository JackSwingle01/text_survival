using Raylib_cs;
using System.Numerics;
using text_survival.Actors.Animals;

namespace text_survival.Desktop.Rendering;

/// <summary>
/// Draws animals from the pixel art in assets/icons/animals/, one file per AnimalType.
/// Shared by the world map and the combat grid so an animal looks the same in both.
/// </summary>
public static class AnimalRenderer
{
    private static readonly Dictionary<AnimalType, Texture2D> _sprites = new();
    private static readonly HashSet<AnimalType> _reportedMissing = [];

    /// <summary>
    /// Load one sprite per AnimalType from assets/icons/animals/ (e.g. "wolf.png" for
    /// AnimalType.Wolf). Call after the Raylib window is initialized.
    /// </summary>
    public static void LoadSprites(string iconsAssetsPath)
    {
        string animalsPath = Path.Combine(iconsAssetsPath, "animals");
        if (!Directory.Exists(animalsPath))
            throw new DirectoryNotFoundException($"Animal sprites directory not found: {animalsPath}");

        foreach (string filePath in Directory.GetFiles(animalsPath, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            if (!Enum.TryParse(name, ignoreCase: true, out AnimalType type))
            {
                Console.Error.WriteLine($"AnimalRenderer: '{filePath}' does not match any AnimalType, skipping.");
                continue;
            }

            Texture2D texture = Raylib.LoadTexture(filePath);
            if (texture.Id == 0)
            {
                Console.Error.WriteLine($"AnimalRenderer: failed to load '{filePath}'.");
                continue;
            }

            // Pixel art must not be smoothed when scaled up.
            Raylib.SetTextureFilter(texture, TextureFilter.Point);
            _sprites[type] = texture;
        }

        var missing = Enum.GetValues<AnimalType>().Where(t => !_sprites.ContainsKey(t)).ToList();
        if (missing.Count > 0)
            Console.Error.WriteLine($"AnimalRenderer: no sprite for {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// Draw an animal centered on (x, y).
    /// </summary>
    /// <param name="sizePixels">Target height in pixels; width follows the sprite's aspect ratio.</param>
    public static void DrawAnimal(AnimalType type, float x, float y, float sizePixels)
    {
        if (!_sprites.TryGetValue(type, out Texture2D texture))
        {
            if (_reportedMissing.Add(type))
                Console.Error.WriteLine($"AnimalRenderer: no art for {type} - nothing drawn.");
            return;
        }

        float width = sizePixels * texture.Width / texture.Height;
        var source = new Rectangle(0, 0, texture.Width, texture.Height);
        var dest = new Rectangle(x - width / 2, y - sizePixels / 2, width, sizePixels);

        Raylib.DrawTexturePro(texture, source, dest, Vector2.Zero, 0f, Color.White);
    }
}
