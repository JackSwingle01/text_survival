using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Environments.Features;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui panel showing available actions at the current location.
/// </summary>
public class ActionPanel
{
    private string? _lastMessage;
    private float _messageTimer;

    /// <summary>
    /// Show a temporary message (e.g., "The way is blocked").
    /// </summary>
    public void ShowMessage(string message)
    {
        _lastMessage = message;
        _messageTimer = 3.0f; // Show for 3 seconds
    }

    /// <summary>
    /// Render the action panel. Returns the action ID if one was clicked.
    /// </summary>
    public string? Render(GameContext ctx, float deltaTime)
    {
        string? clickedAction = null;

        ImGui.SetNextWindowPos(new Vector2(960, 50), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(300, 620), ImGuiCond.FirstUseEver);

        ImGui.Begin("Actions");

        // Location header
        var location = ctx.CurrentLocation;
        ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), location?.Name ?? "Unknown");
        if (location != null)
        {
            ImGui.TextDisabled($"Terrain: {location.Terrain}");
        }
        ImGui.Separator();

        // Show temporary message
        if (_messageTimer > 0)
        {
            _messageTimer -= deltaTime;
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), _lastMessage ?? "");
            ImGui.Separator();
        }

        // Fire status
        RenderFireStatus(ctx);

        // Movement info
        ImGui.TextDisabled("Movement: WASD or click adjacent tile");
        ImGui.Separator();

        // Quick actions
        ImGui.Text("Quick Actions:");

        if (ImGui.Button("Wait (5 min) [Space]", new Vector2(-1, 0)))
            clickedAction = "wait";

        // Fire actions
        var fire = location?.GetFeature<HeatSourceFeature>();
        if (fire != null && (fire.IsActive || fire.HasEmbers))
        {
            if (ctx.Inventory.HasFuel)
            {
                if (ImGui.Button("Tend Fire [F]", new Vector2(-1, 0)))
                    clickedAction = "tend_fire";
            }
        }
        else if (CanStartFire(ctx))
        {
            if (ImGui.Button("Start Fire [F]", new Vector2(-1, 0)))
                clickedAction = "start_fire";
        }

        // Eat/Drink
        if (ctx.Inventory.HasFood || ctx.Inventory.HasWater)
        {
            if (ImGui.Button("Eat/Drink", new Vector2(-1, 0)))
                clickedAction = "eat_drink";
        }

        // Cook/Melt (if fire active)
        if (fire != null && fire.IsActive)
        {
            if (ImGui.Button("Cook/Melt", new Vector2(-1, 0)))
                clickedAction = "cook";
        }

        ImGui.Separator();

        // Location work options
        if (location != null)
        {
            var workOptions = location.GetWorkOptions(ctx).ToList();
            if (workOptions.Count > 0)
            {
                ImGui.Text("Work:");
                foreach (var opt in workOptions)
                {
                    if (ImGui.Button(opt.Label, new Vector2(-1, 0)))
                        clickedAction = $"work:{opt.Id}";
                }
                ImGui.Separator();
            }
        }

        // Menu actions
        ImGui.Text("Menus:");

        if (ImGui.Button("Inventory [I]", new Vector2(-1, 0)))
            clickedAction = "inventory";

        if (ImGui.Button("Crafting [C]", new Vector2(-1, 0)))
            clickedAction = "crafting";

        if (ImGui.Button("Discovery Log", new Vector2(-1, 0)))
            clickedAction = "discovery_log";

        // Camp storage (if at camp)
        var storage = ctx.Camp?.GetFeature<CacheFeature>();
        if (location == ctx.Camp && storage != null)
        {
            if (ImGui.Button("Camp Storage", new Vector2(-1, 0)))
                clickedAction = "storage";
        }

        // Curing rack (if available)
        var rack = location?.GetFeature<CuringRackFeature>();
        if (rack != null)
        {
            string rackLabel = rack.HasReadyItems
                ? "Curing Rack (items ready!)"
                : rack.ItemCount > 0
                    ? $"Curing Rack ({rack.ItemCount} curing)"
                    : "Curing Rack";
            if (ImGui.Button(rackLabel, new Vector2(-1, 0)))
                clickedAction = "curing_rack";
        }

        ImGui.Separator();

        // Rest options
        var bedding = location?.GetFeature<BeddingFeature>();
        if (bedding != null)
        {
            if (ImGui.Button("Sleep", new Vector2(-1, 0)))
                clickedAction = "sleep";
        }
        else
        {
            if (ImGui.Button("Make Camp", new Vector2(-1, 0)))
                clickedAction = "make_camp";
        }

        // Treatment (if wounds)
        if (CanTreatWounds(ctx))
        {
            if (ImGui.Button("Treat Wounds", new Vector2(-1, 0)))
                clickedAction = "treat_wounds";
        }

        ImGui.End();

        return clickedAction;
    }

    /// <summary>
    /// Render fire status if there's a fire at current location.
    /// </summary>
    private void RenderFireStatus(GameContext ctx)
    {
        var fire = ctx.CurrentLocation?.GetFeature<HeatSourceFeature>();
        if (fire == null) return;

        if (fire.IsActive)
        {
            string phase = fire.GetFirePhase();
            int minutes = (int)(fire.BurningHoursRemaining * 60);
            float tempC = (float)fire.GetCurrentFireTemperature();

            Vector4 fireColor = minutes <= 5
                ? new Vector4(1f, 0.3f, 0.3f, 1f)  // Red - critical
                : minutes <= 15
                    ? new Vector4(1f, 0.7f, 0.3f, 1f)  // Orange - warning
                    : new Vector4(1f, 0.6f, 0.2f, 1f); // Normal fire color

            ImGui.TextColored(fireColor, $"Fire: {phase}");
            ImGui.Text($"  {tempC:F0}°C - {FormatTime(minutes)} remaining");

            if (minutes <= 5)
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "  Add fuel now!");

            ImGui.Separator();
        }
        else if (fire.HasEmbers)
        {
            int emberMinutes = (int)(fire.EmberTimeRemaining * 60);
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.2f, 1f), "Embers glowing");
            ImGui.Text($"  {FormatTime(emberMinutes)} until cold");
            ImGui.Separator();
        }
    }

    private static string FormatTime(int minutes)
    {
        if (minutes >= 60)
        {
            int hours = minutes / 60;
            int mins = minutes % 60;
            return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
        }
        return $"{minutes}m";
    }

    private static bool CanStartFire(GameContext ctx)
    {
        var fire = ctx.CurrentLocation?.GetFeature<HeatSourceFeature>();
        bool noFire = fire == null || (!fire.IsActive && !fire.HasEmbers);
        if (!noFire) return false;

        bool hasTool = ctx.Inventory.Tools.Any(t =>
            t.ToolType == Items.ToolType.FireStriker ||
            t.ToolType == Items.ToolType.HandDrill ||
            t.ToolType == Items.ToolType.BowDrill);
        return hasTool && ctx.Inventory.CanStartFire;
    }

    private static bool CanTreatWounds(GameContext ctx)
    {
        // Check if player has treatable conditions and treatment items
        var effects = ctx.player.EffectRegistry;
        bool hasTreatableWounds = effects.GetAll().Any(e =>
            e.EffectKind == "Bleeding" || e.EffectKind == "Burn" || e.EffectKind == "Infected");

        bool hasTreatmentItems = ctx.Inventory.GetCount(ResourceCategory.Medicine) > 0;

        return hasTreatableWounds && hasTreatmentItems;
    }
}
