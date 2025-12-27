using System.Text.Json;
using System.Text.Json.Serialization;

using text_survival.Actions;

namespace text_survival.Persistence;

/// <summary>
/// Handles save file I/O operations.
/// Supports session-specific saves for web mode via sessionId parameter.
/// </summary>
public static class SaveManager
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter(), new StackConverterFactory() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        ReferenceHandler = ReferenceHandler.Preserve
    };

    /// <summary>
    /// Get the save directory path (project directory for easier access).
    /// </summary>
    private static string GetSaveDirectory()
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "saves");
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
    /// Returns (success, error) tuple - caller can handle errors appropriately.
    /// </summary>
    public static (bool success, string? error) Save(GameContext ctx)
    {
        try
        {
            string json = JsonSerializer.Serialize(ctx, Options);
            File.WriteAllText(GetSavePath(ctx.SessionId), json);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Load save data from disk for the given session (or default).
    /// Returns (context, error) tuple - context is null if load failed.
    /// Note: With ReferenceHandler.Preserve, circular references are handled automatically.
    /// </summary>
    public static (GameContext? ctx, string? error) Load(string? sessionId = null)
    {
        string path = GetSavePath(sessionId);
        if (!File.Exists(path))
            return (null, null); // No save file is not an error

        try
        {
            string json = File.ReadAllText(path);
            var ctx = JsonSerializer.Deserialize<GameContext>(json, Options);
            return (ctx, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
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

    /// <summary>
    /// Test serialization and return result (throws on error).
    /// </summary>
    public static string TestSerialization()
    {
        var ctx = GameContext.CreateNewGame();

        // This will throw if serialization fails
        string json = JsonSerializer.Serialize(ctx, Options);

        // Try deserialization too
        var deserialized = JsonSerializer.Deserialize<GameContext>(json, Options);

        int locationCount = 0;
        if (deserialized?.Map != null)
        {
            foreach (var loc in deserialized.Map.NamedLocations)
                locationCount++;
        }

        return $"SUCCESS: Serialized {json.Length} characters, deserialized OK. " +
               $"Locations: {locationCount}";
    }
}
