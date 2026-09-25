using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using Raylib_cs;
using System.Numerics;
using text_survival.Desktop.Rendering;
using text_survival.Items;

namespace text_survival.Desktop.UI;

/// <summary>Shared pixel textures, owned by the desktop window's lifetime.</summary>
public static class UiIcons
{
    private static readonly Dictionary<string, Texture2D> Textures = new();

    private static bool _loaded;
    private static readonly HashSet<string> Missing = [];
    public static IReadOnlyCollection<string> MissingIcons => Missing;
    private static bool DrawsArt(string icon) => Textures.ContainsKey(icon) || _loaded && GameGui.Capture == null;
    public static void Load()
    {
        Unload();
        _loaded = true;
        string directory = Path.Combine(AssetPaths.Icons(), "ui");
        if (!Directory.Exists(directory)) return; // Draw reports missing assets and displays a visible placeholder.
        foreach (string path in Directory.GetFiles(directory, "*.png"))
        {
            var texture = Raylib.LoadTexture(path);
            if (texture.Id == 0) continue;
            Raylib.SetTextureFilter(texture, TextureFilter.Point);
            Textures[Path.GetFileNameWithoutExtension(path)] = texture;
        }
    }

    public static void Unload()
    {
        foreach (var texture in Textures.Values) Raylib.UnloadTexture(texture);
        Textures.Clear(); _loaded = false; Missing.Clear();
    }

    public static string ForCategory(string? category) => category?.ToLowerInvariant() switch
    {
        null or "all" or "general" => "foraging",
        "material" or "materials" => "materials",
        var name => name
    };

    public static string ForResource(Resource resource) => resource.GetIconKey();

    public static string ForGear(Gear gear) => gear.Category == GearCategory.Equipment || gear.Slot.HasValue
        ? "clothing"
        : gear.ToolType switch
        {
            ToolType.Knife or ToolType.Scraper => "knife",
            ToolType.Spear => "spear",
            ToolType.Cordage => "rope",
            ToolType.Torch or ToolType.EmberCarrier or ToolType.FireStriker
                or ToolType.HandDrill or ToolType.BowDrill => "fire",
            ToolType.Treatment => "medicine",
            ToolType.WaterContainer => "water",
            _ => "gear"
        };

    public static string ForConsumable(string id) => id == "wash_blood" ? "water"
        : Enum.TryParse<Resource>(id, true, out var resource) ? ForResource(resource) : "food";

    // Draw over a native selectable: the image and text share the same hit target.
    public static bool Selectable(string icon, string label, string id, bool selected = false)
    {
        if (!DrawsArt(icon)) return ImGui.Selectable($"{label}###{id}", selected);
        var position = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Math.Max(16, ImGui.GetTextLineHeight()));
        bool clicked = ImGui.Selectable($"##{id}", selected, ImGuiSelectableFlags.None, size);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(position, position + size, true);
        Draw(icon, position + new Vector2(0, (size.Y - 16) / 2));
        drawList.AddText(position + new Vector2(22, (size.Y - ImGui.GetTextLineHeight()) / 2),
            ImGui.GetColorU32(ImGuiCol.Text), label);
        drawList.PopClipRect();
        if (ImGui.IsItemHovered() && ImGui.CalcTextSize(label).X > size.X - 22)
            UiText.Tooltip(label);
        return clicked;
    }

    public static void Draw(string icon, Vector2 position, float size = 16)
    {
        if (Textures.TryGetValue(icon, out var texture))
        {
            position = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
            ImGui.GetWindowDrawList().AddImage((IntPtr)texture.Id, position,
                position + new Vector2(size), Vector2.Zero, Vector2.One);
        }
        else if (_loaded && GameGui.Capture == null)
        {
            if (Missing.Add(icon)) Console.Error.WriteLine($"Missing UI icon: {icon}");
            ImGui.GetWindowDrawList().AddRectFilled(position, position + new Vector2(size), ImGui.GetColorU32(UiStyle.Caution));
            ImGui.GetWindowDrawList().AddText(position + new Vector2(3, 0), ImGui.GetColorU32(new Vector4(.08f, .08f, .08f, 1)), "?");
        }
    }

    public static void LabelWrapped(string icon, Vector4 color, string text)
    {
        ImGui.PushTextWrapPos(0); LabelColored(icon, color, text); ImGui.PopTextWrapPos();
    }

    public static void LabelColored(string icon, Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        Label(icon, text);
        ImGui.PopStyleColor();
    }

    public static void Label(string icon, string text)
    {
        if (DrawsArt(icon))
        {
            float size = UiStyle.IconSize;
            var position = ImGui.GetCursorScreenPos();
            Draw(icon, position + new Vector2(0, Math.Max(0, (ImGui.GetTextLineHeight() - size) / 2)), size);
            ImGui.Dummy(new Vector2(size, Math.Max(size, ImGui.GetTextLineHeight())));
            ImGui.SameLine(0, 4 * UiStyle.Scale);
        }
        ImGui.TextUnformatted(text);
    }

    public static float ButtonWidth(string label) => UiStyle.IconSize + 6 * UiStyle.Scale + ImGui.CalcTextSize(label).X + ImGui.FramePadding.X * 2;

    // The whole icon + label is one native button, preserving hover, keyboard, and IDs.
    public static bool Button(string icon, string label, string id, Vector2 size = default, bool? selected = null)
    {
        if (!DrawsArt(icon)) return ImGui.Button($"{label}###{id}", size, selected);
        var padding = ImGui.FramePadding;
        float wrap = size.X == 0 ? -1 : Math.Max(1, (size.X < 0 ? ImGui.GetContentRegionAvail().X : size.X) - padding.X * 2 - UiStyle.IconSize - 6 * UiStyle.Scale);
        var textSize = ImGui.CalcTextSize(label, false, wrap);
        float iconSize = UiStyle.IconSize;
        float gap = 6 * UiStyle.Scale;
        float contentWidth = iconSize + gap + textSize.X;
        if (size.X == 0) size.X = contentWidth + padding.X * 2;
        if (size.Y == 0) size.Y = Math.Max(iconSize, textSize.Y) + padding.Y * 2;
        size.Y = Math.Max(size.Y, Math.Max(iconSize, textSize.Y) + padding.Y * 2);
        bool clicked = ImGui.Button($"##{id}", size, selected);
        var min = ImGui.GetItemRectMin();
        var actualSize = ImGui.GetItemRectSize();
        var start = min + new Vector2(Math.Max(padding.X, (actualSize.X - contentWidth) / 2), 0);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(min, min + actualSize, true);
        Draw(icon, start + new Vector2(0, (actualSize.Y - iconSize) / 2), iconSize);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), start + new Vector2(iconSize + gap, (actualSize.Y - textSize.Y) / 2),
            ImGui.GetColorU32(ImGuiCol.Text), label, wrap > 0 ? wrap : 0);
        drawList.PopClipRect();
        return clicked;
    }
}
