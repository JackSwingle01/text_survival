using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Desktop.UI;

/// <summary>
/// ImGui overlay for fire starting and tending.
/// Two modes: Starting (no fire) and Tending (active fire or embers).
/// </summary>
public class FireOverlay
{
    public bool IsOpen { get; set; }

    private enum Mode { Starting, Tending }
    private Mode _currentMode = Mode.Starting;

    // Starting mode state
    private int _selectedToolIndex = 0;
    private int _selectedTinderIndex = 0;
    private string? _lastAttemptMessage;
    private bool _lastAttemptSuccess;

    // Tending mode state
    private string? _tendMessage;

    /// <summary>
    /// Open the fire overlay for a given fire state.
    /// </summary>
    public void Open(HeatSourceFeature? fire)
    {
        IsOpen = true;
        _lastAttemptMessage = null;
        _tendMessage = null;

        // Determine initial mode based on fire state
        if (fire != null && (fire.IsActive || fire.HasEmbers))
        {
            _currentMode = Mode.Tending;
        }
        else
        {
            _currentMode = Mode.Starting;
        }
    }

    /// <summary>
    /// Render the fire overlay.
    /// Returns an action result if the user performed an action.
    /// </summary>
    public FireOverlayResult? Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen) return null;

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        FireOverlayResult? result = null;

        // Center the window
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f, io.DisplaySize.Y * 0.5f),
            ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(450, 500), ImGuiCond.FirstUseEver);

        bool open = IsOpen;
        if (ImGui.Begin("Fire Management", ref open, ImGuiWindowFlags.NoCollapse))
        {
            // Mode tabs
            if (ImGui.BeginTabBar("FireTabs"))
            {
                // Start Fire tab
                bool canShowStartTab = fire == null || fire.IsCold || fire.HasEmbers;
                if (canShowStartTab && ImGui.BeginTabItem("Start Fire"))
                {
                    _currentMode = Mode.Starting;
                    result = RenderStartingMode(ctx, fire);
                    ImGui.EndTabItem();
                }

                // Tend Fire tab (only if fire exists and is active or has embers)
                if (fire != null && (fire.IsActive || fire.HasEmbers))
                {
                    if (ImGui.BeginTabItem("Tend Fire"))
                    {
                        _currentMode = Mode.Tending;
                        result = RenderTendingMode(ctx, fire);
                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }
        }
        ImGui.End();

        if (!open)
        {
            IsOpen = false;
        }

        return result;
    }

    private FireOverlayResult? RenderStartingMode(GameContext ctx, HeatSourceFeature? existingFire)
    {
        var inv = ctx.Inventory;
        var materials = FireHandler.GetFireMaterials(inv);

        // Check for ember carrier first
        if (materials.EmberCarrier != null)
        {
            ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f), "Ember Carrier Available!");
            ImGui.Text($"  {materials.EmberCarrier.Name}");
            ImGui.Text($"  {materials.EmberCarrier.EmberBurnHoursRemaining:F1}h remaining");
            ImGui.Spacing();

            if (materials.HasKindling)
            {
                if (ImGui.Button("Use Ember Carrier (100% success)", new Vector2(-1, 30)))
                {
                    return new FireOverlayResult
                    {
                        Action = FireAction.StartFromEmber,
                        EmberCarrier = materials.EmberCarrier
                    };
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Need kindling (sticks) to use ember");
            }

            ImGui.Separator();
            ImGui.Spacing();
        }

        // Check materials
        if (materials.Tools.Count == 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "No fire-making tools available!");
            ImGui.TextDisabled("Craft a hand drill, bow drill, or fire striker.");
            return null;
        }

        if (materials.Tinders.Count == 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "No tinder available!");
            ImGui.TextDisabled("Forage for birch bark, amadou, or other tinder.");
            return null;
        }

        if (!materials.HasKindling)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "No kindling available!");
            ImGui.TextDisabled("Gather sticks for kindling.");
            return null;
        }

        // Tool selection
        ImGui.Text("Fire-Starting Tool:");
        for (int i = 0; i < materials.Tools.Count; i++)
        {
            var tool = materials.Tools[i];
            double baseChance = FireHandler.GetToolBaseChance(tool);
            string label = $"{tool.Name} (base: {baseChance:P0})";

            if (ImGui.RadioButton(label, ref _selectedToolIndex, i))
            {
                // Selection changed
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Tinder selection
        ImGui.Text("Tinder:");
        for (int i = 0; i < materials.Tinders.Count; i++)
        {
            var tinder = materials.Tinders[i];
            int count = inv.Count(tinder);
            var fuelType = FireHandler.GetTinderFuelType(tinder);
            double bonus = FuelDatabase.Get(fuelType).IgnitionBonus;

            string tinderName = tinder switch
            {
                Resource.BirchBark => "Birch Bark",
                Resource.Amadou => "Amadou",
                Resource.Usnea => "Usnea",
                Resource.Chaga => "Chaga",
                _ => "Tinder"
            };

            string label = $"{tinderName} x{count} (+{bonus:P0})";

            if (ImGui.RadioButton(label, ref _selectedTinderIndex, i))
            {
                // Selection changed
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Kindling status
        int kindlingCount = inv.Count(Resource.Stick);
        ImGui.Text($"Kindling: {kindlingCount} sticks");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Calculate and display success chance
        if (_selectedToolIndex < materials.Tools.Count && _selectedTinderIndex < materials.Tinders.Count)
        {
            var selectedTool = materials.Tools[_selectedToolIndex];
            var selectedTinder = materials.Tinders[_selectedTinderIndex];
            int skillLevel = ctx.player.Skills.GetSkill("Firecraft").Level;

            double chance = FireHandler.CalculateFireChance(
                ctx.player, selectedTool, selectedTinder, skillLevel,
                ctx.CurrentLocation, ctx.GameTime.Hour);

            // Chance display with color coding
            Vector4 chanceColor = chance switch
            {
                >= 0.7 => new Vector4(0.4f, 0.9f, 0.4f, 1f),  // Green - good
                >= 0.4 => new Vector4(1f, 0.8f, 0.3f, 1f),    // Yellow - moderate
                _ => new Vector4(1f, 0.4f, 0.4f, 1f)          // Red - poor
            };

            ImGui.Text("Success Chance:");
            ImGui.SameLine();
            ImGui.TextColored(chanceColor, $"{chance:P0}");

            // Show modifiers
            ImGui.TextDisabled($"  Skill: Firecraft Lv{skillLevel}");

            var capacities = ctx.player.GetCapacities();
            if (Bodies.AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
            {
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.3f, 1f), "  Consciousness impaired (-20%)");
            }

            double dexterity = ctx.player.GetDexterity(Bodies.AbilityContext.FromFullContext(
                ctx.player, ctx.Inventory, ctx.CurrentLocation, ctx.GameTime.Hour));
            if (dexterity < 0.9)
            {
                double penalty = (1.0 - dexterity) * 0.5 * 100;
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.3f, 1f), $"  Dexterity penalty (-{penalty:F0}%)");
            }

            ImGui.Spacing();

            // Start Fire button
            if (ImGui.Button("Attempt to Start Fire", new Vector2(-1, 35)))
            {
                return new FireOverlayResult
                {
                    Action = FireAction.StartFire,
                    Tool = selectedTool,
                    Tinder = selectedTinder
                };
            }
        }

        // Show last attempt message
        if (_lastAttemptMessage != null)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            Vector4 msgColor = _lastAttemptSuccess
                ? new Vector4(0.4f, 0.9f, 0.4f, 1f)
                : new Vector4(1f, 0.5f, 0.3f, 1f);

            ImGui.TextColored(msgColor, _lastAttemptMessage);
        }

        return null;
    }

    private FireOverlayResult? RenderTendingMode(GameContext ctx, HeatSourceFeature fire)
    {
        var inv = ctx.Inventory;

        // Fire status header
        string phase = fire.GetFirePhase();
        Vector4 phaseColor = phase switch
        {
            "Roaring" => new Vector4(1f, 0.5f, 0.2f, 1f),
            "Steady" => new Vector4(1f, 0.7f, 0.3f, 1f),
            "Building" => new Vector4(1f, 0.6f, 0.3f, 1f),
            "Igniting" => new Vector4(1f, 0.5f, 0.4f, 1f),
            "Dying" => new Vector4(0.8f, 0.4f, 0.3f, 1f),
            "Embers" => new Vector4(0.7f, 0.3f, 0.2f, 1f),
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1f)
        };

        ImGui.Text("Fire Status:");
        ImGui.SameLine();
        ImGui.TextColored(phaseColor, phase);

        // Temperature
        double temp = fire.GetCurrentFireTemperature();
        ImGui.Text($"Temperature: {temp:F0}°F");

        // Time remaining
        if (fire.IsActive)
        {
            double hoursRemaining = fire.TotalHoursRemaining;
            int hours = (int)hoursRemaining;
            int minutes = (int)((hoursRemaining - hours) * 60);

            string timeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
            Vector4 timeColor = hoursRemaining switch
            {
                < 0.25 => new Vector4(1f, 0.3f, 0.3f, 1f),
                < 0.5 => new Vector4(1f, 0.6f, 0.3f, 1f),
                < 1.0 => new Vector4(1f, 0.8f, 0.4f, 1f),
                _ => new Vector4(0.7f, 0.9f, 0.7f, 1f)
            };

            ImGui.Text("Time Remaining:");
            ImGui.SameLine();
            ImGui.TextColored(timeColor, timeStr);
        }
        else if (fire.HasEmbers)
        {
            double emberMinutes = fire.EmberTimeRemaining * 60;
            ImGui.Text($"Ember Time: {emberMinutes:F0}m");
            ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f), "Add fuel to relight!");
        }

        ImGui.Spacing();

        // Fuel capacity bar
        float fuelPct = (float)(fire.TotalMassKg / fire.MaxFuelCapacityKg);
        ImGui.ProgressBar(fuelPct, new Vector2(-1, 0),
            $"Fuel: {fire.TotalMassKg:F1} / {fire.MaxFuelCapacityKg:F0} kg");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Available fuels
        ImGui.Text("Add Fuel:");

        var fuels = GetAvailableFuels(inv);
        foreach (var (resource, fuelType, count, weight) in fuels)
        {
            bool canAdd = fire.CanAddFuel(fuelType);
            var props = FuelDatabase.Get(fuelType);

            string fuelName = GetFuelDisplayName(resource);
            string buttonLabel = $"{fuelName} x{count} ({weight:F1}kg)";

            if (!canAdd)
            {
                ImGui.BeginDisabled();
                ImGui.Button(buttonLabel, new Vector2(-1, 0));
                ImGui.EndDisabled();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    ImGui.SetTooltip($"Fire needs {props.MinFireTemperature:F0}°F to add this fuel");
                }
            }
            else
            {
                if (ImGui.Button(buttonLabel, new Vector2(-1, 0)))
                {
                    return new FireOverlayResult
                    {
                        Action = FireAction.AddFuel,
                        FuelResource = resource
                    };
                }
            }
        }

        if (fuels.Count == 0)
        {
            ImGui.TextDisabled("No fuel available");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Charcoal collection
        if (fire.HasCharcoal)
        {
            ImGui.Text($"Charcoal Available: {fire.CharcoalAvailableKg:F2} kg");
            if (ImGui.Button("Collect Charcoal", new Vector2(-1, 0)))
            {
                return new FireOverlayResult { Action = FireAction.CollectCharcoal };
            }
            ImGui.Spacing();
        }

        // Torch lighting
        bool hasUnlitTorch = inv.Tools.Any(t => t.ToolType == ToolType.Torch && !t.IsTorchLit);
        if (fire.IsActive && hasUnlitTorch)
        {
            if (ImGui.Button("Light Torch", new Vector2(-1, 0)))
            {
                return new FireOverlayResult { Action = FireAction.LightTorch };
            }
        }

        // Ember carrier
        bool hasEmberCarrier = inv.Tools.Any(t => t.IsEmberCarrier && !t.IsEmberLit);
        if (fire.HasEmbers && hasEmberCarrier)
        {
            if (ImGui.Button("Collect Ember", new Vector2(-1, 0)))
            {
                return new FireOverlayResult { Action = FireAction.CollectEmber };
            }
        }

        // Show tending message
        if (_tendMessage != null)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1f, 1f), _tendMessage);
        }

        return null;
    }

    private List<(Resource resource, FuelType fuelType, int count, double weight)> GetAvailableFuels(Inventory inv)
    {
        var result = new List<(Resource, FuelType, int, double)>();

        // Check each fuel type
        void AddIfAvailable(Resource r, FuelType ft)
        {
            int count = inv.Count(r);
            if (count > 0)
            {
                double weight = inv.GetTotalWeight(r);
                result.Add((r, ft, count, weight));
            }
        }

        // Order by burn rate (slow burners first for better overnight burns)
        AddIfAvailable(Resource.Oak, FuelType.OakWood);
        AddIfAvailable(Resource.Birch, FuelType.BirchWood);
        AddIfAvailable(Resource.Pine, FuelType.PineWood);
        AddIfAvailable(Resource.Bone, FuelType.Bone);
        AddIfAvailable(Resource.Stick, FuelType.Kindling);
        AddIfAvailable(Resource.BirchBark, FuelType.BirchBark);
        AddIfAvailable(Resource.Tinder, FuelType.Tinder);

        return result;
    }

    private static string GetFuelDisplayName(Resource r) => r switch
    {
        Resource.Oak => "Oak Log",
        Resource.Birch => "Birch Log",
        Resource.Pine => "Pine Log",
        Resource.Stick => "Kindling",
        Resource.BirchBark => "Birch Bark",
        Resource.Tinder => "Tinder",
        Resource.Bone => "Bone",
        Resource.Charcoal => "Charcoal",
        _ => r.ToString()
    };

    /// <summary>
    /// Set the result message from a fire start attempt.
    /// </summary>
    public void SetAttemptResult(bool success, string message)
    {
        _lastAttemptSuccess = success;
        _lastAttemptMessage = message;

        // Switch to tending mode if successful
        if (success)
        {
            _currentMode = Mode.Tending;
        }
    }

    /// <summary>
    /// Set a message for tending mode.
    /// </summary>
    public void SetTendMessage(string message)
    {
        _tendMessage = message;
    }
}

/// <summary>
/// Actions that can be performed from the fire overlay.
/// </summary>
public enum FireAction
{
    StartFire,
    StartFromEmber,
    AddFuel,
    CollectCharcoal,
    LightTorch,
    CollectEmber
}

/// <summary>
/// Result from fire overlay interaction.
/// </summary>
public class FireOverlayResult
{
    public FireAction Action { get; set; }
    public Gear? Tool { get; set; }
    public Resource? Tinder { get; set; }
    public Gear? EmberCarrier { get; set; }
    public Resource? FuelResource { get; set; }
}
