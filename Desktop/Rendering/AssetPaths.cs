namespace text_survival.Desktop.Rendering;

/// <summary>
/// Where the rendering layer finds its art. One resolution, used by every loader,
/// so a published build and a development run cannot disagree about the path.
/// </summary>
public static class AssetPaths
{
    /// <summary>
    /// assets/icons - next to the executable when published, next to the working
    /// directory when run from the repo. Throws if neither exists: without art there
    /// is nothing to draw, and a blank map is a worse report than a crash.
    /// </summary>
    public static string Icons()
    {
        string published = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icons");
        if (Directory.Exists(published))
            return published;

        string development = Path.Combine(Directory.GetCurrentDirectory(), "assets", "icons");
        if (Directory.Exists(development))
            return development;

        throw new DirectoryNotFoundException(
            $"Icon assets not found. Looked in '{published}' and '{development}'.");
    }
}
