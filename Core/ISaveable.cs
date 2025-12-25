// using System.Text.Json;

// public interface ISaveable
// {
//     public abstract void Save();
//     public abstract void Load(string sessionId);
//     public abstract void CreateNew(string sessionId);
//     public static bool SessionExists(string sessionId)
//     {
//         return File.Exists(GetSavePath(sessionId));
//     }
//     public static readonly JsonSerializerOptions Options = new()
//     {
//         WriteIndented = true,
//         PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//     };


//     /// <summary>
//     /// Get the save directory path (cross-platform).
//     /// </summary>
//     private static string GetSaveDirectory()
//     {
//         string dir = Path.Combine(
//             Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
//             "TextSurvival"
//         );
//         Directory.CreateDirectory(dir);
//         return dir;
//     }

//     /// <summary>
//     /// Get the save file path. Uses session-specific filename if sessionId provided.
//     /// </summary>
//     public static string GetSavePath(string? sessionId = null)
//     {
//         string filename = string.IsNullOrEmpty(sessionId) ? "save.json" : $"save_{sessionId}.json";
//         return Path.Combine(GetSaveDirectory(), filename);
//     }
// }