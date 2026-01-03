using text_survival.Actions;

namespace text_survival.Persistence;

/// <summary>
/// Handles game initialization for both new games and loaded saves.
/// </summary>
public static class GameInitializer
{
    /// <summary>
    /// Load game from save file. Returns null if no save exists or load fails.
    /// </summary>
    public static GameContext? LoadGame(string? sessionId = null)
    {
        var (ctx, error) = SaveManager.Load(sessionId);
        if (error != null)
        {
            Console.WriteLine($"Failed to load save: {error}");
        }

        // Post-load: recreate non-serialized data
        ctx?.Herds.RecreateAllMembers(ctx.Map);

        return ctx;
    }

    /// <summary>
    /// Try to load a saved game, falling back to a new game if none exists.
    /// </summary>
    public static GameContext LoadOrCreateNew(string? sessionId = null)
    {
        if (SaveManager.HasSaveFile(sessionId))
        {
            var loaded = LoadGame(sessionId);
            if (loaded != null)
                return loaded;
        }

        return GameContext.CreateNewGame();
    }
}
