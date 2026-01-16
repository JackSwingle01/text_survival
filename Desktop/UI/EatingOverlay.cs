using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Survival;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for eating and drinking.
/// Shows available consumables with calories and hydration values.
/// </summary>
public class EatingOverlay
{
    public bool IsOpen { get; set; }

    private string? _lastConsumeMessage;
    private bool _lastConsumeWarning;

    /// <summary>
    /// Open the eating overlay.
    /// </summary>
    public void Open()
    {
        IsOpen = true;
        _lastConsumeMessage = null;
    }

    /// <summary>
    /// Render the eating overlay.
    /// Returns the consumable ID if the user consumed something.
    /// </summary>
    public string? Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen) return null;

        string? consumedId = null;

        // Center the window
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
            ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(400, 450), ImGuiCond.FirstUseEver);

        bool open = IsOpen;
        if (ImGui.Begin("Eat & Drink", ref open, ImGuiWindowFlags.NoCollapse))
        {
            // Current stats
            var body = ctx.player.Body;

            // Calories bar
            float calPct = (float)(body.CalorieStore / 2000.0);
            Vector4 calColor = calPct switch
            {
                < 0.15f => new Vector4(1f, 0.3f, 0.3f, 1f),
                < 0.3f => new Vector4(1f, 0.6f, 0.3f, 1f),
                < 0.5f => new Vector4(1f, 0.8f, 0.4f, 1f),
                _ => new Vector4(0.5f, 0.8f, 0.5f, 1f)
            };
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, calColor);
            ImGui.ProgressBar(calPct, new Vector2(-1, 0), $"Calories: {body.CalorieStore:F0} / 2000");
            ImGui.PopStyleColor();

            // Hydration bar
            float hydPct = (float)(body.Hydration / SurvivalProcessor.MAX_HYDRATION);
            Vector4 hydColor = hydPct switch
            {
                < 0.2f => new Vector4(1f, 0.3f, 0.3f, 1f),
                < 0.4f => new Vector4(1f, 0.6f, 0.3f, 1f),
                < 0.6f => new Vector4(1f, 0.8f, 0.4f, 1f),
                _ => new Vector4(0.4f, 0.7f, 1f, 1f)
            };
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, hydColor);
            ImGui.ProgressBar(hydPct, new Vector2(-1, 0), $"Hydration: {body.Hydration:F1}L / {SurvivalProcessor.MAX_HYDRATION:F0}L");
            ImGui.PopStyleColor();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Available consumables
            var consumables = ConsumptionHandler.GetAvailableConsumables(ctx);

            if (consumables.Count == 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.3f, 1f), "No food or water available!");
                ImGui.TextDisabled("Forage for berries, hunt for meat, or find water.");
            }
            else
            {
                // Food section
                var foods = consumables.Where(c => c.Calories.HasValue).ToList();
                if (foods.Count > 0)
                {
                    ImGui.Text("Food:");
                    ImGui.Spacing();

                    foreach (var food in foods)
                    {
                        RenderConsumableButton(food, ref consumedId);
                    }

                    ImGui.Spacing();
                }

                // Drink section
                var drinks = consumables.Where(c => c.Id == "water" || c.Id == "wash_blood").ToList();
                if (drinks.Count > 0)
                {
                    ImGui.Separator();
                    ImGui.Spacing();
                    ImGui.Text("Water:");
                    ImGui.Spacing();

                    foreach (var drink in drinks)
                    {
                        RenderConsumableButton(drink, ref consumedId);
                    }
                }
            }

            // Last consumption message
            if (_lastConsumeMessage != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                Vector4 msgColor = _lastConsumeWarning
                    ? new Vector4(1f, 0.6f, 0.3f, 1f)
                    : new Vector4(0.5f, 0.9f, 0.5f, 1f);

                ImGui.TextColored(msgColor, _lastConsumeMessage);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Close button
            if (ImGui.Button("Done", new Vector2(-1, 30)))
            {
                IsOpen = false;
            }
        }
        ImGui.End();

        if (!open)
        {
            IsOpen = false;
        }

        return consumedId;
    }

    private void RenderConsumableButton(ConsumptionHandler.ConsumableInfo item, ref string? consumedId)
    {
        // Build button label
        string label = item.Name;

        // Add amount info
        if (item.Id == "water")
        {
            label += $" ({item.Amount:F2}L)";
        }
        else if (item.Id == "wash_blood")
        {
            label += $" (uses {item.Amount:F1}L)";
        }
        else
        {
            label += $" ({item.Amount * 1000:F0}g)";
        }

        // Add stats
        var stats = new List<string>();
        if (item.Calories.HasValue)
        {
            stats.Add($"+{item.Calories}cal");
        }
        if (item.Hydration.HasValue)
        {
            if (item.Hydration > 0)
                stats.Add($"+{item.Hydration}ml");
            else
                stats.Add($"{item.Hydration}ml");
        }
        if (stats.Count > 0)
        {
            label += $" [{string.Join(", ", stats)}]";
        }

        // Warning color
        if (item.Warning != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.3f, 0.2f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.4f, 0.3f, 1f));
        }

        if (ImGui.Button(label, new Vector2(-1, 0)))
        {
            consumedId = item.Id;
        }

        if (item.Warning != null)
        {
            ImGui.PopStyleColor(2);

            // Show warning on hover
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(item.Warning);
            }
        }
    }

    /// <summary>
    /// Set the result message from consuming an item.
    /// </summary>
    public void SetConsumeResult(string message, bool isWarning)
    {
        _lastConsumeMessage = message;
        _lastConsumeWarning = isWarning;
    }
}
