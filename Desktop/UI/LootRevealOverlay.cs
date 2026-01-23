using ImGuiNET;
using System.Numerics;
using text_survival.Items;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for revealing found loot with staged timing.
/// Items appear one at a time for a more satisfying collection feel.
/// </summary>
public class LootRevealOverlay
{
    private List<LootItem> _items = [];
    private int _revealedCount;
    private float _revealTimer;
    private float _totalWeightKg;
    private bool _allRevealed;
    private bool _shouldClose;

    // Timing constants
    private const float RevealDelaySeconds = 0.08f;  // Time between item reveals
    private const float InitialDelaySeconds = 0.2f;  // Pause before first item

    /// <summary>
    /// Initialize the overlay with items to display (all shown immediately).
    /// </summary>
    public void SetItems(List<LootItem> items)
    {
        _items = items;
        _shouldClose = false;
        _revealedCount = items.Count;
        _revealTimer = 0;
        _totalWeightKg = (float)items.Sum(i => i.WeightKg);
        _allRevealed = true;
    }

    /// <summary>
    /// Render the loot reveal overlay.
    /// Returns true when user dismisses.
    /// </summary>
    public bool Render(float deltaTime)
    {
        // Update reveal timer
        if (!_allRevealed)
        {
            _revealTimer -= deltaTime;
            if (_revealTimer <= 0)
            {
                if (_revealedCount < _items.Count)
                {
                    _totalWeightKg += (float)_items[_revealedCount].WeightKg;
                    _revealedCount++;
                    _revealTimer = RevealDelaySeconds;
                }

                if (_revealedCount >= _items.Count)
                {
                    _allRevealed = true;
                }
            }
        }

        // Set up centered window
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
            ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Always);

        ImGui.Begin("Found", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

        if (_items.Count == 0)
        {
            ImGui.TextDisabled("Nothing found.");
        }
        else
        {
            // Item grid - show revealed items as tiles
            RenderItemTiles();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Weight total
            ImGui.Text($"Total: {_totalWeightKg:F2} kg");
        }

        ImGui.Spacing();

        // Continue button - always visible but shows progress if not done
        if (_allRevealed)
        {
            if (ImGui.Button("Continue", new Vector2(-1, 30)))
            {
                _shouldClose = true;
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button($"... ({_revealedCount}/{_items.Count})", new Vector2(-1, 30));
            ImGui.EndDisabled();
        }

        ImGui.End();

        return _shouldClose;
    }

    private void RenderItemTiles()
    {
        // Calculate tile layout
        float windowWidth = ImGui.GetContentRegionAvail().X;
        float tileWidth = 100;
        float tileHeight = 50;
        float spacing = 8;
        int tilesPerRow = Math.Max(1, (int)((windowWidth + spacing) / (tileWidth + spacing)));

        int col = 0;
        for (int i = 0; i < _revealedCount; i++)
        {
            var item = _items[i];

            if (col > 0)
                ImGui.SameLine(0, spacing);

            RenderItemTile(item, tileWidth, tileHeight);

            col++;
            if (col >= tilesPerRow)
                col = 0;
        }

        // Show placeholder for next unrevealed item (anticipation)
        if (!_allRevealed && _revealedCount < _items.Count)
        {
            if (col > 0)
                ImGui.SameLine(0, spacing);

            RenderPlaceholderTile(tileWidth, tileHeight);
        }
    }

    private void RenderItemTile(LootItem item, float width, float height)
    {
        Vector4 bgColor = GetCategoryColor(item.Category);

        ImGui.PushStyleColor(ImGuiCol.Button, bgColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, bgColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, bgColor);

        // Background tile button
        ImGui.Button("##tile" + item.Name, new Vector2(width, height));

        // Get button rect for text overlay positioning
        var rectMin = ImGui.GetItemRectMin();
        var drawList = ImGui.GetWindowDrawList();

        // Draw item name at top
        uint textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f));
        drawList.AddText(rectMin + new Vector2(5, 5), textColor, TruncateName(item.Name, 12));

        // Draw count/weight at bottom
        string countWeight = item.Count > 1
            ? $"x{item.Count} ({item.WeightKg:F1}kg)"
            : $"{item.WeightKg:F2}kg";
        uint subtextColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.9f, 0.9f, 0.8f));
        drawList.AddText(rectMin + new Vector2(5, height - 20), subtextColor, countWeight);

        ImGui.PopStyleColor(3);
    }

    private static void RenderPlaceholderTile(float width, float height)
    {
        Vector4 bgColor = new(0.2f, 0.2f, 0.2f, 0.5f);

        ImGui.PushStyleColor(ImGuiCol.Button, bgColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, bgColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, bgColor);

        ImGui.Button("##placeholder", new Vector2(width, height));

        ImGui.PopStyleColor(3);
    }

    private static Vector4 GetCategoryColor(ResourceCategory? category) => category switch
    {
        ResourceCategory.Fuel => new Vector4(0.6f, 0.4f, 0.2f, 0.9f),      // Brown
        ResourceCategory.Food => new Vector4(0.3f, 0.6f, 0.3f, 0.9f),      // Green
        ResourceCategory.Medicine => new Vector4(0.5f, 0.3f, 0.6f, 0.9f),  // Purple
        ResourceCategory.Material => new Vector4(0.4f, 0.5f, 0.6f, 0.9f),  // Blue-gray
        ResourceCategory.Tinder => new Vector4(0.7f, 0.5f, 0.3f, 0.9f),    // Orange-brown
        _ => new Vector4(0.4f, 0.4f, 0.4f, 0.9f)                            // Gray
    };

    private static string TruncateName(string name, int maxLen)
    {
        if (name.Length <= maxLen) return name;
        return name[..(maxLen - 2)] + "..";
    }
}
