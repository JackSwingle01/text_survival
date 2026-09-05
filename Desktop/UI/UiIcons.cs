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

    public static void Load()
    {
        Unload();
        string directory = Path.Combine(AssetPaths.Icons(), "ui");
        if (!Directory.Exists(directory)) return; // Labels remain usable without art.
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
        Textures.Clear();
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
        if (!Textures.ContainsKey(icon)) return ImGui.Selectable($"{label}##{id}", selected);
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
    }

    public static void LabelColored(string icon, Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        Label(icon, text);
        ImGui.PopStyleColor();
    }

    public static void Label(string icon, string text)
    {
        if (Textures.ContainsKey(icon))
        {
            var position = ImGui.GetCursorScreenPos();
            Draw(icon, position + new Vector2(0, Math.Max(0, (ImGui.GetTextLineHeight() - 16) / 2)));
            ImGui.Dummy(new Vector2(16, Math.Max(16, ImGui.GetTextLineHeight())));
            ImGui.SameLine(0, 4);
        }
        ImGui.TextUnformatted(text);
    }

    // The whole icon + label is one native button, preserving hover, keyboard, and IDs.
    public static bool Button(string icon, string label, string id, Vector2 size = default)
    {
        if (!Textures.ContainsKey(icon)) return ImGui.Button($"{label}##{id}", size);
        var textSize = ImGui.CalcTextSize(label);
        var padding = ImGui.GetStyle().FramePadding;
        float contentWidth = 16 + 6 + textSize.X;
        if (size.X == 0) size.X = contentWidth + padding.X * 2;
        if (size.Y == 0) size.Y = Math.Max(16, textSize.Y) + padding.Y * 2;
        bool clicked = ImGui.Button($"##{id}", size);
        var min = ImGui.GetItemRectMin();
        var actualSize = ImGui.GetItemRectSize();
        var start = min + new Vector2(Math.Max(padding.X, (actualSize.X - contentWidth) / 2), 0);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(min, min + actualSize, true);
        Draw(icon, start + new Vector2(0, (actualSize.Y - 16) / 2));
        drawList.AddText(start + new Vector2(22, (actualSize.Y - textSize.Y) / 2),
            ImGui.GetColorU32(ImGuiCol.Text), label);
        drawList.PopClipRect();
        return clicked;
    }
}
