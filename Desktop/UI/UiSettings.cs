using System.Text.Json;

namespace text_survival.Desktop.UI;

public enum UiScale { Compact, Comfortable, Large }

/// <summary>Presentation settings are separate from game saves.</summary>
public sealed record UiSettings(UiScale Scale = UiScale.Comfortable)
{
    public static string DefaultPath => Path.Combine(Directory.GetCurrentDirectory(), "saves", "ui-settings.json");
    public float Factor => Scale switch { UiScale.Compact => 1f, UiScale.Large => 1.4f, _ => 1.2f };
    public static UiSettings Load(string path)
    {
        try
        {
            var result = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(path));
            return result != null && Enum.IsDefined(result.Scale) ? result : new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save(string path)
    {
        if (!Enum.IsDefined(Scale)) throw new ArgumentOutOfRangeException(nameof(Scale));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, true);
    }
}
