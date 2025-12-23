using System.Text.Json;
using text_survival.Actions;

namespace text_survival.Persistence;

/// <summary>
/// Handles save file I/O operations.
/// Supports session-specific saves for web mode via sessionId parameter.
/// </summary>
public static class SaveManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Get the save directory path (cross-platform).
    /// </summary>
    private static string GetSaveDirectory()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TextSurvival"
        );
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Get the save file path. Uses session-specific filename if sessionId provided.
    /// </summary>
    private static string GetSavePath(string? sessionId = null)
    {
        string filename = string.IsNullOrEmpty(sessionId) ? "save.json" : $"save_{sessionId}.json";
        return Path.Combine(GetSaveDirectory(), filename);
    }

    /// <summary>
    /// Check if a save file exists for the given session (or default).
    /// </summary>
    public static bool HasSaveFile(string? sessionId = null)
    {
        return File.Exists(GetSavePath(sessionId));
    }

    /// <summary>
    /// Save the game context to disk. Uses ctx.SessionId for web sessions.
    /// </summary>
    public static void Save(GameContext ctx)
    {
        try
        {
            var saveData = SaveDataConverter.ToSaveData(ctx);
            string json = JsonSerializer.Serialize(saveData, Options);
            File.WriteAllText(GetSavePath(ctx.SessionId), json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving game: {ex.Message}");
        }
    }

    /// <summary>
    /// Load save data from disk for the given session (or default).
    /// </summary>
    public static GameSaveData? Load(string? sessionId = null)
    {
        string path = GetSavePath(sessionId);
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
    /// Delete the save file for the given session (or default).
    /// </summary>
    public static void DeleteSave(string? sessionId = null)
    {
        string path = GetSavePath(sessionId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Get the save file location for display.
    /// </summary>
    public static string GetSaveLocation(string? sessionId = null)
    {
        return GetSavePath(sessionId);
    }
}
