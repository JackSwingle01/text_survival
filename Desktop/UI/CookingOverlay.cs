using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Environments.Features;

namespace text_survival.Desktop.UI;

public class CookingOverlay
{
    public bool IsOpen { get; set; }

    private string? _lastActionMessage;
    private bool _lastActionSuccess;

    public void Open()
    {
        IsOpen = true;
        _lastActionMessage = null;
    }

    public CookingOverlayResult? Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen) return null;

        CookingOverlayResult? result = null;

        // Center the window
        OverlaySizes.SetupStandard();

        bool open = IsOpen;
        if (ImGui.Begin("Cooking", ref open, ImGuiWindowFlags.NoCollapse))
        {
            var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
            var inv = ctx.Inventory;

            // Fire status check
            if (fire == null || !fire.IsActive)
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "No active fire!");
                ImGui.TextDisabled("You need an active fire to cook.");
                ImGui.Spacing();

                if (ImGui.Button("Close", new Vector2(-1, 30)))
                {
                    IsOpen = false;
                }
            }
            else
            {
                // Fire info
                ImGui.Text($"Fire: {fire.GetFirePhase()}");
                ImGui.Text($"Temperature: {fire.GetCurrentFireTemperature():F0}°F");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Cook Meat section
                ImGui.Text("Cook Meat:");
                int rawMeatCount = inv.Count(Resource.RawMeat);

                if (rawMeatCount > 0)
                {
                    double rawMeatWeight = inv.Weight(Resource.RawMeat);
                    string meatLabel = $"Cook Raw Meat ({rawMeatWeight:F1}kg) - {CookingHandler.CookMeatTimeMinutes}min";

                    if (ImGui.Button(meatLabel, new Vector2(-1, 0)))
                    {
                        result = new CookingOverlayResult { Action = CookingAction.CookMeat };
                    }
                }
                else
                {
                    ImGui.TextDisabled("No raw meat available");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Melt Snow section
                ImGui.Text("Melt Snow:");
                ImGui.Text($"Current water: {inv.WaterLiters:F2}L");

                string snowLabel = $"Melt Snow (+{CookingHandler.MeltSnowWaterLiters:F1}L) - {CookingHandler.MeltSnowTimeMinutes}min";

                if (ImGui.Button(snowLabel, new Vector2(-1, 0)))
                {
                    result = new CookingOverlayResult { Action = CookingAction.MeltSnow };
                }

                // Last action message
                if (_lastActionMessage != null)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    Vector4 msgColor = _lastActionSuccess
                        ? new Vector4(0.5f, 0.9f, 0.5f, 1f)
                        : new Vector4(1f, 0.5f, 0.3f, 1f);

                    ImGui.TextColored(msgColor, _lastActionMessage);
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
        }
        ImGui.End();

        if (!open)
        {
            IsOpen = false;
        }

        return result;
    }

    public void SetActionResult(bool success, string message)
    {
        _lastActionSuccess = success;
        _lastActionMessage = message;
    }
}

public enum CookingAction
{
    CookMeat,
    MeltSnow
}

public class CookingOverlayResult
{
    public CookingAction Action { get; set; }
}
