using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Desktop.Input;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for viewing NPC companions at the current location.
/// Shows stats, current action, and active effects.
/// </summary>
public class NPCOverlay
{
    public bool IsOpen { get; set; }

    /// <summary>
    /// Render the NPC overlay. Returns true if overlay should close.
    /// </summary>
    public bool Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen) return false;

        var npcsHere = ctx.NPCs.Where(n => n.CurrentLocation == ctx.CurrentLocation).ToList();
        if (npcsHere.Count == 0)
        {
            IsOpen = false;
            return true;
        }

        bool shouldClose = false;

        OverlaySizes.SetupStandard();

        bool open = IsOpen;
        if (ImGui.Begin("Companions", ref open, ImGuiWindowFlags.NoCollapse))
        {
            foreach (var npc in npcsHere)
            {
                RenderNPC(npc);
                ImGui.Separator();
            }

            ImGui.Spacing();
            if (ImGui.Button($"Close {HotkeyRegistry.GetTip(HotkeyAction.NPCs)}", new Vector2(-1, 0)))
            {
                shouldClose = true;
            }
        }
        ImGui.End();

        if (!open) shouldClose = true;
        if (shouldClose) IsOpen = false;

        return shouldClose;
    }

    private void RenderNPC(NPC npc)
    {
        // Name header
        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), npc.Name);

        // Current action + need
        string action = npc.CurrentAction?.Name ?? "Idle";
        string needText = npc.CurrentNeed.HasValue ? $" ({npc.CurrentNeed})" : "";
        ImGui.TextDisabled($"{action}{needText}");

        ImGui.Spacing();

        // Stat bars
        RenderStatBar("Warmth", (float)npc.Body.WarmPct, GetWarmthColor(npc.Body.WarmPct));
        RenderStatBar("Energy", (float)npc.Body.EnergyPct, GetStatColor(npc.Body.EnergyPct));
        RenderStatBar("Food", (float)npc.Body.FullPct, GetStatColor(npc.Body.FullPct));
        RenderStatBar("Water", (float)npc.Body.HydratedPct, GetStatColor(npc.Body.HydratedPct));
        RenderStatBar("Clothing Warmth", (float)npc.Body.ClothingHeatBufferPct, new Vector4(1f, 0.8f, 0.3f, 1f));

        // Active effects
        var effects = npc.EffectRegistry.GetAll().ToList();
        if (effects.Count > 0)
        {
            ImGui.Spacing();
            string effectNames = string.Join(", ", effects.Select(e => e.EffectKind));
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.4f, 1f), $"Effects: {effectNames}");
        }

        // Activity log
        var logs = npc.GetRecentLogs(5);
        if (logs.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Recent:");
            foreach (var entry in logs)
            {
                ImGui.Text($"  {entry}");
            }
        }

        // Inventory section
        ImGui.Spacing();
        RenderNPCInventory(npc.Inventory);
    }

    private void RenderNPCInventory(Inventory inv)
    {
        float weightPct = (float)(inv.CurrentWeightKg / inv.MaxWeightKg);
        string header = $"Inventory ({inv.CurrentWeightKg:F1} / {inv.MaxWeightKg:F1} kg)";

        if (ImGui.CollapsingHeader(header))
        {
            // Weight bar
            Vector4 weightColor = weightPct > 0.9f
                ? new Vector4(1f, 0.3f, 0.3f, 1f)
                : weightPct > 0.7f
                    ? new Vector4(1f, 0.8f, 0.3f, 1f)
                    : new Vector4(0.7f, 0.9f, 0.7f, 1f);

            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, weightColor);
            ImGui.ProgressBar(weightPct, new Vector2(-1, OverlaySizes.CompactBarHeight), "");
            ImGui.PopStyleColor();

            ImGui.Spacing();

            // Resource summary by category
            RenderResourceCategory(inv, "Food", ResourceCategory.Food);
            RenderResourceCategory(inv, "Fuel", ResourceCategory.Fuel);
            RenderResourceCategory(inv, "Medicine", ResourceCategory.Medicine);
            RenderResourceCategory(inv, "Material", ResourceCategory.Material);

            // Water
            if (inv.WaterLiters > 0)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), $"Water: {inv.WaterLiters:F1}L");
            }

            // Gear section
            bool hasGear = inv.Weapon != null || inv.Tools.Count > 0 ||
                          inv.Equipment.Values.Any(e => e != null) ||
                          inv.Accessories.Count > 0;

            if (hasGear)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Gear:");

                // Weapon
                if (inv.Weapon != null)
                {
                    RenderGearLine(inv.Weapon, "Weapon");
                }

                // Equipment
                foreach (var (slot, gear) in inv.Equipment)
                {
                    if (gear != null)
                    {
                        RenderGearLine(gear, slot.ToString());
                    }
                }

                // Tools
                foreach (var tool in inv.Tools)
                {
                    RenderGearLine(tool, "Tool");
                }

                // Accessories
                foreach (var acc in inv.Accessories)
                {
                    ImGui.Text($"  {acc.Name} (+{acc.CapacityBonusKg:F0}kg cap)");
                }
            }
        }
    }

    private static void RenderResourceCategory(Inventory inv, string label, ResourceCategory category)
    {
        int count = inv.GetCount(category);
        if (count > 0)
        {
            double weight = inv.GetWeight(category);
            ImGui.Text($"{label}: {count} items ({weight:F1}kg)");
        }
    }

    private static void RenderGearLine(Gear gear, string prefix)
    {
        Vector4 condColor = gear.ConditionPct > 0.5
            ? new Vector4(0.7f, 0.9f, 0.7f, 1f)
            : gear.ConditionPct > 0.25
                ? new Vector4(1f, 0.8f, 0.3f, 1f)
                : new Vector4(1f, 0.4f, 0.4f, 1f);

        ImGui.Text($"  {gear.Name}");
        ImGui.SameLine();
        ImGui.TextColored(condColor, $"({gear.ConditionPct * 100:F0}%)");
    }

    private void RenderStatBar(string label, float value, Vector4 color)
    {
        ImGui.Text(label);
        ImGui.SameLine(80);

        // Draw colored progress bar
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(value, new Vector2(-1, OverlaySizes.CompactBarHeight), "");
        ImGui.PopStyleColor();
    }

    private static Vector4 GetWarmthColor(double value)
    {
        if (value < 0.25) return new Vector4(0.3f, 0.5f, 1f, 1f);  // Cold blue
        if (value < 0.5) return new Vector4(0.5f, 0.7f, 1f, 1f);   // Light blue
        if (value > 0.9) return new Vector4(1f, 0.7f, 0.3f, 1f);   // Warm orange
        return new Vector4(0.7f, 0.9f, 0.7f, 1f);                   // Good green
    }

    private static Vector4 GetStatColor(double value)
    {
        if (value < 0.2) return new Vector4(1f, 0.3f, 0.3f, 1f);   // Critical red
        if (value < 0.4) return new Vector4(1f, 0.7f, 0.3f, 1f);   // Warning orange
        return new Vector4(0.7f, 0.9f, 0.7f, 1f);                   // Good green
    }
}
