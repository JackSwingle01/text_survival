using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Environments.Features;
using text_survival.Survival;
using text_survival.Desktop.Input;
using text_survival.UI;

namespace text_survival.Desktop.UI;

/// <summary>
/// Combined overlay for eating, drinking, and cooking.
/// Uses a two-column layout with item list on left and details on right.
/// </summary>
public class FoodOverlay
{
    public bool IsOpen { get; set; }

    /// <summary>
    /// Pending cooking/melting action to be processed outside the ImGui frame.
    /// </summary>
    public PendingFoodAction? PendingAction { get; private set; }

    public void ClearPendingAction() => PendingAction = null;

    private FoodItem? _selectedItem;
    private string? _lastMessage;
    private bool _lastMessageIsWarning;
    private float _messageTimer;

    public void Open()
    {
        IsOpen = true;
        _selectedItem = null;
        _lastMessage = null;
        PendingAction = null;
    }

    public void Render(GameContext ctx, float deltaTime)
    {
        if (!IsOpen) return;

        // Update message timer
        if (_messageTimer > 0)
        {
            _messageTimer -= deltaTime;
            if (_messageTimer <= 0)
                _lastMessage = null;
        }

        OverlaySizes.SetupWide();

        bool open = IsOpen;
        if (ImGui.Begin("Food & Water", ref open, ImGuiWindowFlags.NoCollapse))
        {
            // Stats bars at top
            RenderStatsBars(ctx);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Show message if any
            if (_lastMessage != null)
            {
                Vector4 msgColor = _lastMessageIsWarning
                    ? new Vector4(1f, 0.6f, 0.3f, 1f)
                    : new Vector4(0.5f, 0.9f, 0.5f, 1f);
                UiText.Colored(msgColor, _lastMessage);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            // Main content area - two columns
            float contentHeight = ImGui.GetContentRegionAvail().Y - 35;

            // Left: Item list
            ImGui.BeginChild("ItemList", new Vector2(280, contentHeight), ImGuiChildFlags.Borders);
            RenderItemList(ctx);
            ImGui.EndChild();

            ImGui.SameLine();

            // Right: Details panel
            ImGui.BeginChild("Details", new Vector2(0, contentHeight), ImGuiChildFlags.Borders);
            RenderDetailsPanel(ctx);
            ImGui.EndChild();

            // Close button
            if (ImGui.Button($"Done {HotkeyRegistry.GetTip(HotkeyAction.Cancel)}", new Vector2(-1, 30)))
            {
                IsOpen = false;
            }
        }
        ImGui.End();

        if (!open)
        {
            IsOpen = false;
        }
    }

    private void RenderStatsBars(GameContext ctx)
    {
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
        // Hydration is stored in millilitres. Printing it with an L suffix read "3000.0L / 4000L"
        // - three tonnes of water. Converted through the same constant that turns a litre drunk
        // into hydration gained, so the display cannot drift from the thing it is displaying.
        double hydrationLiters = body.Hydration / ConsumptionHandler.WaterHydrationPerLiter;
        double maxHydrationLiters = SurvivalProcessor.MAX_HYDRATION / ConsumptionHandler.WaterHydrationPerLiter;
        ImGui.ProgressBar(hydPct, new Vector2(-1, 0), $"Hydration: {hydrationLiters:F1}L / {maxHydrationLiters:F1}L");
        ImGui.PopStyleColor();
    }

    private void RenderItemList(GameContext ctx)
    {
        var consumables = ConsumptionHandler.GetAvailableConsumables(ctx);
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool hasActiveFire = fire != null && fire.IsActive;

        if (consumables.Count == 0 && !hasActiveFire)
        {
            UiText.Colored(new Vector4(1f, 0.5f, 0.3f, 1f), "No food or water available!");
            UiText.Disabled("Forage for berries, hunt for meat,");
            UiText.Disabled("or melt snow at a fire.");
            return;
        }

        // Food section
        var foods = consumables.Where(c => c.Calories.HasValue).ToList();
        if (foods.Count > 0)
        {
            RenderSectionHeader("Food");

            foreach (var food in foods)
            {
                RenderConsumableItem(food);
            }
        }

        // Water section
        var drinks = consumables.Where(c => c.Id == "water" || c.Id == "wash_blood").ToList();
        if (drinks.Count > 0)
        {
            if (foods.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Spacing();
            }

            RenderSectionHeader("Water");

            foreach (var drink in drinks)
            {
                RenderConsumableItem(drink);
            }
        }

        // Fire actions section (only when fire active)
        if (hasActiveFire)
        {
            if (foods.Count > 0 || drinks.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Spacing();
            }

            RenderSectionHeader("Fire Actions");

            // Melt snow
            var meltItem = new FoodItem.MeltSnow();
            bool isSelected = _selectedItem is FoodItem.MeltSnow;

            if (UiIcons.Selectable("water", $"Melt Snow (+{CookingHandler.MeltSnowWaterLiters:F1}L)", "melt_snow", isSelected))
            {
                _selectedItem = meltItem;
            }
        }
    }

    private static void RenderSectionHeader(string title)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.8f, 0.9f, 1f));
        UiIcons.Label(title == "Fire Actions" ? "fire" : UiIcons.ForCategory(title), title);
        ImGui.PopStyleColor();
        ImGui.Separator();
    }

    private void RenderConsumableItem(ConsumptionHandler.ConsumableInfo item)
    {
        bool isSelected = _selectedItem is FoodItem.Consumable c && c.Info.Id == item.Id;

        // Build display string
        string label = item.Name;
        if (item.Id == "water")
            label += $" ({item.Amount:F2}L)";
        else if (item.Id == "wash_blood")
            label += $" (uses {item.Amount:F1}L)";
        else
            label += $" ({item.Amount * 1000:F0}g)";

        // Color for warnings
        if (item.Warning != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.5f, 1f));
        }

        if (UiIcons.Selectable(UiIcons.ForConsumable(item.Id), label, item.Id, isSelected))
        {
            _selectedItem = new FoodItem.Consumable(item);
        }

        if (item.Warning != null)
        {
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                UiText.Tooltip(item.Warning);
            }
        }
    }

    private void RenderDetailsPanel(GameContext ctx)
    {
        if (_selectedItem == null)
        {
            UiText.Disabled("Select an item from the list.");
            return;
        }

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        bool hasActiveFire = fire != null && fire.IsActive;

        switch (_selectedItem)
        {
            case FoodItem.Consumable consumable:
                RenderConsumableDetails(ctx, consumable.Info, hasActiveFire);
                break;

            case FoodItem.MeltSnow:
                RenderMeltSnowDetails(ctx);
                break;
        }
    }

    private void RenderConsumableDetails(GameContext ctx, ConsumptionHandler.ConsumableInfo item, bool hasActiveFire)
    {
        // Item name
        UiText.Colored(new Vector4(0.9f, 0.85f, 0.7f, 1f), item.Name);
        ImGui.Separator();
        ImGui.Spacing();

        // Amount
        if (item.Id == "water")
            UiText.Text($"Amount: {item.Amount:F2}L");
        else if (item.Id == "wash_blood")
            UiText.Text($"Uses: {item.Amount:F1}L of water");
        else
            UiText.Text($"Amount: {item.Amount * 1000:F0}g ({item.Amount:F2}kg)");

        ImGui.Spacing();

        // Nutritional info
        if (item.Calories.HasValue)
        {
            UiText.Text($"Calories: +{item.Calories}");
        }
        if (item.Hydration.HasValue)
        {
            if (item.Hydration > 0)
                UiText.Text($"Hydration: +{item.Hydration}ml");
            else
                UiText.Text($"Hydration: {item.Hydration}ml");
        }

        // Warning
        if (item.Warning != null)
        {
            ImGui.Spacing();
            UiText.Colored(new Vector4(1f, 0.6f, 0.3f, 1f), item.Warning);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Action buttons
        if (item.Id == "water")
        {
            if (ImGui.Button("Drink", new Vector2(-1, 30)))
            {
                var result = ConsumptionHandler.Consume(ctx, item.Id);
                SetMessage(result.Message, result.IsWarning);
                _selectedItem = null; // Deselect after consuming
            }
        }
        else if (item.Id == "wash_blood")
        {
            if (ImGui.Button("Wash", new Vector2(-1, 30)))
            {
                var result = ConsumptionHandler.Consume(ctx, item.Id);
                SetMessage(result.Message, result.IsWarning);
                _selectedItem = null;
            }
        }
        else
        {
            // Food item - can eat, and maybe cook
            bool isRaw = item.Id == "RawMeat" || item.Id == "RawFish";

            if (isRaw && item.Warning != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.3f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.4f, 0.3f, 1f));
            }

            string eatLabel = isRaw ? "Eat (risky)" : "Eat";
            if (ImGui.Button(eatLabel, new Vector2(-1, 30)))
            {
                var result = ConsumptionHandler.Consume(ctx, item.Id);
                SetMessage(result.Message, result.IsWarning);
                _selectedItem = null;
            }

            if (isRaw && item.Warning != null)
            {
                ImGui.PopStyleColor(2);
            }

            // Cook button for raw food (only when fire active)
            if (isRaw && hasActiveFire)
            {
                ImGui.Spacing();

                int cookTime = item.Id == "RawMeat"
                    ? CookingHandler.CookMeatTimeMinutes
                    : CookingHandler.CookFishTimeMinutes;

                FoodAction cookAction = item.Id == "RawMeat"
                    ? FoodAction.CookMeat
                    : FoodAction.CookFish;

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.4f, 1f));

                if (ImGui.Button($"Cook ({cookTime} min)", new Vector2(-1, 30)))
                {
                    // Store pending action and close overlay
                    PendingAction = new PendingFoodAction
                    {
                        Action = cookAction,
                        ItemId = item.Id,
                        Minutes = cookTime
                    };
                    IsOpen = false;
                }

                ImGui.PopStyleColor(2);
            }
            else if (isRaw && !hasActiveFire)
            {
                ImGui.Spacing();
                UiText.Disabled("Start a fire to cook");
            }
        }
    }

    private void RenderMeltSnowDetails(GameContext ctx)
    {
        UiText.Colored(new Vector4(0.9f, 0.85f, 0.7f, 1f), "Melt Snow");
        ImGui.Separator();
        ImGui.Spacing();

        UiText.Text($"Produces: {CookingHandler.MeltSnowWaterLiters:F1}L of water");
        UiText.Text($"Time: {CookingHandler.MeltSnowTimeMinutes} minutes");

        ImGui.Spacing();
        UiText.Disabled("Snow is freely available in this");
        UiText.Disabled("frozen landscape.");

        ImGui.Spacing();

        // Current water
        UiText.Text($"Current water: {ctx.Inventory.Weight(Resource.Water):F2}L");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.6f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.7f, 1f));

        if (ImGui.Button($"Melt ({CookingHandler.MeltSnowTimeMinutes} min)", new Vector2(-1, 30)))
        {
            // Store pending action and close overlay
            PendingAction = new PendingFoodAction
            {
                Action = FoodAction.MeltSnow,
                ItemId = "snow",
                Minutes = CookingHandler.MeltSnowTimeMinutes
            };
            IsOpen = false;
        }

        ImGui.PopStyleColor(2);
    }

    private void SetMessage(string message, bool isWarning)
    {
        _lastMessage = message;
        _lastMessageIsWarning = isWarning;
        _messageTimer = 3.0f;
    }
}

/// <summary>
/// Represents a selectable item in the food overlay.
/// </summary>
public abstract record FoodItem
{
    public record Consumable(ConsumptionHandler.ConsumableInfo Info) : FoodItem;
    public record MeltSnow() : FoodItem;
}

