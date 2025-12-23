using System.Text.Json;
using text_survival.Actions;

namespace text_survival.Persistence;

/// <summary>
/// Handles save file I/O operations.
/// </summary>
public static class SaveManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Get the save file path (cross-platform).
    /// </summary>
    private static string GetSavePath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TextSurvival"
        );
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "save.json");
    }

    /// <summary>
    /// Check if a save file exists.
    /// </summary>
    public static bool HasSaveFile()
    {
        return File.Exists(GetSavePath());
    }

    /// <summary>
    /// Save the game context to disk.
    /// </summary>
    public static void Save(GameContext ctx)
    {
        try
        {
            var saveData = SaveDataConverter.ToSaveData(ctx);
            string json = JsonSerializer.Serialize(saveData, Options);
            File.WriteAllText(GetSavePath(), json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving game: {ex.Message}");
        }
    }

    /// <summary>
    /// Load save data from disk.
    /// </summary>
    public static GameSaveData? Load()
    {
        string path = GetSavePath();
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameSaveData>(json, Options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading save: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Delete the save file.
    /// </summary>
    public static void DeleteSave()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Get the save file location for display.
    /// </summary>
    public static string GetSaveLocation()
    {
        return GetSavePath();
    }
}
