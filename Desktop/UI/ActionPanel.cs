using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Desktop.Input;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Actions.Expeditions.WorkStrategies;

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
    /// Render the action panel. Returns typed camp action, work strategy, or combat action.
    /// </summary>
    public (CampAction? CampAction, IWorkStrategy? WorkStrategy, CombatActions? CombatAction) Render(GameContext ctx, float deltaTime)
    {
        if (ctx.ActiveCombat != null)
        {
            return (null, null, RenderCombatActions(ctx, deltaTime));
        }
        else
        {
            var (action, workStrategy) = RenderLocationActions(ctx, deltaTime);
            return (action, workStrategy, null);
        }
    }

    private (CampAction? action, IWorkStrategy? workStrategy) RenderLocationActions(GameContext ctx, float deltaTime)
    {
        CampAction? clickedAction = null;
        IWorkStrategy? workStrategy = null;

        // Position relative to grid if WorldRenderer is available
        int panelX = 960; // Default fallback
        if (DesktopRuntime.WorldRenderer != null)
        {
            panelX = DesktopRuntime.WorldRenderer.Camera.GetRightPanelX();
        }

        ImGui.SetNextWindowPos(new Vector2(panelX, 50), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(300, 620), ImGuiCond.Always);

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

        if (ImGui.Button($"Wait (5 min) {HotkeyRegistry.GetTip(HotkeyAction.Wait)}", new Vector2(-1, 0)))
            clickedAction = CampAction.Wait;

        // Fire actions
        var fire = location?.GetFeature<HeatSourceFeature>();
        if (fire != null && (fire.IsActive || fire.HasEmbers))
        {
            if (ctx.Inventory.HasFuel)
            {
                if (ImGui.Button($"Tend Fire {HotkeyRegistry.GetTip(HotkeyAction.Fire)}", new Vector2(-1, 0)))
                    clickedAction = CampAction.TendFire;
            }
        }
        else if (CanStartFire(ctx))
        {
            if (ImGui.Button($"Start Fire {HotkeyRegistry.GetTip(HotkeyAction.Fire)}", new Vector2(-1, 0)))
                clickedAction = CampAction.StartFire;
        }

        // Eat/Drink
        if (ctx.Inventory.HasFood || ctx.Inventory.HasWater)
        {
            if (ImGui.Button("Eat/Drink", new Vector2(-1, 0)))
                clickedAction = CampAction.EatDrink;
        }

        // Cook/Melt (if fire active)
        if (fire != null && fire.IsActive)
        {
            if (ImGui.Button("Cook/Melt", new Vector2(-1, 0)))
                clickedAction = CampAction.Cook;
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
                        workStrategy = opt.Strategy;
                }
                ImGui.Separator();
            }
        }

        // Menu actions
        ImGui.Text("Menus:");

        if (ImGui.Button($"Inventory {HotkeyRegistry.GetTip(HotkeyAction.Inventory)}", new Vector2(-1, 0)))
            clickedAction = CampAction.Inventory;

        if (ImGui.Button($"Crafting {HotkeyRegistry.GetTip(HotkeyAction.Crafting)}", new Vector2(-1, 0)))
            clickedAction = CampAction.Crafting;

        if (ImGui.Button($"Discovery Log {HotkeyRegistry.GetTip(HotkeyAction.DiscoveryLog)}", new Vector2(-1, 0)))
            clickedAction = CampAction.DiscoveryLog;

        // Camp storage (if at camp)
        var storage = ctx.Camp?.GetFeature<CacheFeature>();
        if (location == ctx.Camp && storage != null)
        {
            if (ImGui.Button($"Camp Storage {HotkeyRegistry.GetTip(HotkeyAction.Storage)}", new Vector2(-1, 0)))
                clickedAction = CampAction.Storage;
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
                clickedAction = CampAction.CuringRack;
        }

        ImGui.Separator();

        // Rest options
        var bedding = location?.GetFeature<BeddingFeature>();
        if (bedding != null)
        {
            if (ImGui.Button("Sleep", new Vector2(-1, 0)))
                clickedAction = CampAction.Sleep;
        }
        else
        {
            if (ImGui.Button("Make Camp", new Vector2(-1, 0)))
                clickedAction = CampAction.MakeCamp;
        }

        // Treatment (if wounds)
        if (CanTreatWounds(ctx))
        {
            if (ImGui.Button("Treat Wounds", new Vector2(-1, 0)))
                clickedAction = CampAction.TreatWounds;
        }

        ImGui.End();

        return (clickedAction, workStrategy);
    }

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
            ImGui.Text($"  {tempC:F0}°F - {FormatTime(minutes)} remaining");

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

    /// <summary>
    /// Render combat actions when in combat mode.
    /// </summary>
    private CombatActions? RenderCombatActions(GameContext ctx, float deltaTime)
    {
        CombatActions? clickedAction = null;

        // Position relative to grid if WorldRenderer is available
        int panelX = 960; // Default fallback
        if (DesktopRuntime.WorldRenderer != null)
        {
            panelX = DesktopRuntime.WorldRenderer.Camera.GetRightPanelX();
        }

        ImGui.SetNextWindowPos(new Vector2(panelX, 50), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(300, 620), ImGuiCond.Always);

        ImGui.Begin("Combat");

        var combat = ctx.ActiveCombat;
        if (combat == null || combat.Player == null)
        {
            ImGui.End();
            return null;
        }

        var playerUnit = combat.Player;
        var nearest = combat.GetNearestEnemy(playerUnit);

        // Get display target: hovered unit or nearest enemy
        var displayTarget = DesktopRuntime.WorldRenderer?.HoveredCombatUnit ?? nearest;

        // Determine if we're in stealth mode (target not engaged)
        bool inStealth = nearest != null && nearest.Awareness != AwarenessState.Engaged;
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting")?.Level ?? 0;

        // Combat header
        string headerText = inStealth ? "STALKING" : "COMBAT";
        ImGui.TextColored(new Vector4(1f, 0.5f, 0.3f, 1f), headerText);
        ImGui.Separator();

        // Distance display (to nearest enemy for tactical relevance)
        if (nearest != null)
        {
            double distance = playerUnit.Position.DistanceTo(nearest.Position);
            var zone = CombatScenario.GetZone(distance);

            string zoneText = zone switch
            {
                Zone.close => "CLOSE (0-1m)",
                Zone.near => "NEAR (1-3m)",
                Zone.mid => "MID (3-15m)",
                Zone.far => "FAR (15-25m)",
                _ => "UNKNOWN"
            };

            Vector4 zoneColor = zone switch
            {
                Zone.close => new Vector4(1f, 0.2f, 0.2f, 1f),   // Red - danger
                Zone.near => new Vector4(1f, 0.5f, 0.2f, 1f),    // Orange
                Zone.mid => new Vector4(1f, 0.8f, 0.3f, 1f),     // Yellow
                Zone.far => new Vector4(0.6f, 0.8f, 0.6f, 1f),   // Green - safer
                _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
            };

            ImGui.Text($"Distance: {distance:F0}m");
            ImGui.TextColored(zoneColor, zoneText);
        }

        // Target stats section
        if (displayTarget != null)
        {
            ImGui.Separator();

            // Target header with indicator if hovering different unit
            bool isHoveredUnit = displayTarget != nearest && displayTarget != playerUnit;
            string targetLabel = displayTarget == playerUnit ? "YOU" : displayTarget.actor.Name.ToUpper();
            if (isHoveredUnit)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.85f, 1f, 1f), targetLabel);
            }
            else
            {
                ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.7f, 1f), targetLabel);
            }
            ImGui.Separator();

            // Show awareness state in stealth for non-player units
            if (displayTarget != playerUnit && displayTarget.Awareness != AwarenessState.Engaged)
            {
                string awarenessText = displayTarget.Awareness == AwarenessState.Unaware
                    ? "Unaware"
                    : "Alert";
                Vector4 awarenessColor = displayTarget.Awareness == AwarenessState.Unaware
                    ? new Vector4(0.4f, 0.9f, 0.4f, 1f)  // Green - good
                    : new Vector4(1f, 0.8f, 0.3f, 1f);   // Yellow - caution
                ImGui.TextColored(awarenessColor, awarenessText);

                // Show activity hint for animals
                if (displayTarget.actor is Animal animal)
                {
                    var behavior = HuntingSightingSelector.MapActivityToBehavior(animal);
                    string hint = HuntingSightingSelector.GetBehaviorHint(behavior);
                    ImGui.TextWrapped(hint);
                }
                ImGui.Spacing();

                // Detection risk display (stealth only)
                if (inStealth && nearest != null)
                {
                    double detectionRisk = combat.CalculateDetectionRisk(playerUnit, nearest, huntingSkill);
                    Vector4 riskColor = detectionRisk switch
                    {
                        >= 0.7 => new Vector4(1f, 0.3f, 0.3f, 1f),   // Red - high risk
                        >= 0.4 => new Vector4(1f, 0.7f, 0.3f, 1f),   // Orange - medium
                        _ => new Vector4(0.4f, 0.9f, 0.4f, 1f)        // Green - low
                    };
                    ImGui.TextColored(riskColor, $"Detection Risk: {detectionRisk:P0}");
                    ImGui.Spacing();
                }
            }

            // Vitality bar
            float vitalityVal = (float)Math.Clamp(displayTarget.actor.Vitality, 0, 1);
            Vector4 vitalityColor = vitalityVal switch
            {
                >= 0.7f => new Vector4(0.4f, 0.9f, 0.4f, 1f),
                >= 0.4f => new Vector4(1f, 0.8f, 0.3f, 1f),
                _ => new Vector4(1f, 0.3f, 0.3f, 1f)
            };
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, vitalityColor);
            ImGui.ProgressBar(vitalityVal, new Vector2(-1, 0), $"Vitality: {vitalityVal * 100:F0}%");
            ImGui.PopStyleColor();

            // Only show combat stats for non-player units
            if (displayTarget != playerUnit)
            {
                // Boldness bar
                float boldnessVal = (float)Math.Clamp(displayTarget.Boldness, 0, 3.0);
                float boldnessDisplay = boldnessVal / 3.0f; // Normalize for display (animals start at 2.3-3.0)
                Vector4 boldnessColor = boldnessVal switch
                {
                    >= 0.7f => new Vector4(1f, 0.4f, 0.3f, 1f),   // Red - aggressive
                    >= 0.5f => new Vector4(1f, 0.7f, 0.3f, 1f),   // Orange - bold
                    >= 0.3f => new Vector4(1f, 1f, 0.4f, 1f),     // Yellow - wary
                    _ => new Vector4(0.6f, 0.6f, 0.6f, 1f)         // Gray - cautious
                };
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, boldnessColor);
                ImGui.ProgressBar(boldnessDisplay, new Vector2(-1, 0), $"Boldness: {boldnessVal * 100:F0}%");
                ImGui.PopStyleColor();

                // Threat bar
                float threatVal = (float)Math.Clamp(displayTarget.Threat, 0, 1.5);
                float threatDisplay = threatVal / 1.5f;
                Vector4 threatColor = threatVal switch
                {
                    >= 0.6f => new Vector4(0.8f, 0.2f, 0.2f, 1f), // Dark red - high threat
                    >= 0.3f => new Vector4(0.9f, 0.5f, 0.3f, 1f), // Orange
                    _ => new Vector4(0.6f, 0.7f, 0.6f, 1f)         // Muted green - low threat
                };
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, threatColor);
                ImGui.ProgressBar(threatDisplay, new Vector2(-1, 0), $"Threat: {threatVal * 100:F0}%");
                ImGui.PopStyleColor();

                // Aggression bar
                float aggressionVal = (float)Math.Clamp(displayTarget.Aggression, 0, 1.5);
                float aggressionDisplay = aggressionVal / 1.5f;
                Vector4 aggressionColor = aggressionVal switch
                {
                    >= 0.7f => new Vector4(1f, 0.2f, 0.2f, 1f),   // Bright red
                    >= 0.4f => new Vector4(1f, 0.6f, 0.2f, 1f),   // Orange
                    _ => new Vector4(0.7f, 0.7f, 0.5f, 1f)         // Muted
                };
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, aggressionColor);
                ImGui.ProgressBar(aggressionDisplay, new Vector2(-1, 0), $"Aggression: {aggressionVal * 100:F0}%");
                ImGui.PopStyleColor();
            }

            // Speed and Strength as numbers
            ImGui.Spacing();
            ImGui.TextDisabled($"Speed: {displayTarget.actor.Speed:F2}  Strength: {displayTarget.actor.Strength:F2}");

            // Injuries section - show damaged body parts
            var damagedParts = displayTarget.actor.Body.Parts
                .Where(p => p.Condition < 1.0)
                .OrderBy(p => p.Condition)
                .Take(4)  // Limit to 4 to save space
                .ToList();

            if (damagedParts.Count > 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Injuries:");
                foreach (var part in damagedParts)
                {
                    float condition = (float)part.Condition;
                    Vector4 injuryColor = condition switch
                    {
                        >= 0.7f => new Vector4(0.7f, 0.9f, 0.4f, 1f),  // Light damage
                        >= 0.4f => new Vector4(1f, 0.7f, 0.3f, 1f),    // Moderate
                        _ => new Vector4(1f, 0.3f, 0.3f, 1f)            // Severe
                    };
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, injuryColor);
                    ImGui.ProgressBar(condition, new Vector2(-1, 12), $"{part.Name}: {condition * 100:F0}%");
                    ImGui.PopStyleColor();
                }
            }
        }

        ImGui.Separator();

        // Combat actions based on zone and stealth state
        ImGui.Text("Actions:");
        ImGui.Spacing();

        if (nearest != null)
        {
            double distance = playerUnit.Position.DistanceTo(nearest.Position);
            var zone = CombatScenario.GetZone(distance);
            var weapon = ctx.Inventory.Weapon;
            bool hasWeapon = weapon != null;
            bool isSmall = nearest.actor is Animal a && a.Size == AnimalSize.Small;

            // Stealth actions (Wait and Assess) when target is unaware/alert
            if (inStealth)
            {
                if (ImGui.Button("Wait", new Vector2(-1, 0)))
                    clickedAction = CombatActions.Wait;
                if (ImGui.Button("Assess", new Vector2(-1, 0)))
                    clickedAction = CombatActions.Assess;
                ImGui.Separator();
            }

            switch (zone)
            {
                case Zone.close:
                    if (ImGui.Button("Attack", new Vector2(-1, 0)))
                        clickedAction = CombatActions.Attack;
                    if (ImGui.Button("Block", new Vector2(-1, 0)))
                        clickedAction = CombatActions.Block;
                    if (ImGui.Button("Shove", new Vector2(-1, 0)))
                        clickedAction = CombatActions.Shove;
                    break;

                case Zone.near:
                    if (ImGui.Button("Attack", new Vector2(-1, 0)))
                        clickedAction = CombatActions.Attack;
                    if (ImGui.Button("Dodge", new Vector2(-1, 0)))
                        clickedAction = CombatActions.Dodge;
                    if (ImGui.Button("Block", new Vector2(-1, 0)))
                        clickedAction = CombatActions.Block;
                    break;

                case Zone.mid:
                    // Throw weapon with hit chance
                    if (hasWeapon)
                    {
                        double maxRange = weapon!.Name.Contains("Stone") ? 25.0 : 20.0;
                        double baseAccuracy = weapon.Name.Contains("Stone") ? 0.75 : 0.70;
                        double hitChance = HuntingCalculator.CalculateThrownAccuracy(distance, maxRange, baseAccuracy, isSmall);

                        if (ImGui.Button($"Throw {weapon.Name} ({hitChance:P0})", new Vector2(-1, 0)))
                            clickedAction = CombatActions.Throw;
                    }

                    // Throw stone with hit chance
                    int stones = ctx.Inventory.Count(Resource.Stone);
                    if (stones > 0)
                    {
                        double hitChance = HuntingCalculator.CalculateThrownAccuracy(
                            distance, HuntHandler.GetStoneRange(), HuntHandler.GetStoneBaseAccuracy(), isSmall);

                        if (ImGui.Button($"Throw Stone x{stones} ({hitChance:P0})", new Vector2(-1, 0)))
                            clickedAction = CombatActions.ThrowStone;
                    }

                    if (ImGui.Button("Intimidate", new Vector2(-1, 0)))
                        clickedAction = CombatActions.Intimidate;
                    break;

                case Zone.far:
                    // Throw weapon at far range
                    if (hasWeapon)
                    {
                        double maxRange = weapon!.Name.Contains("Stone") ? 25.0 : 20.0;
                        double baseAccuracy = weapon.Name.Contains("Stone") ? 0.75 : 0.70;
                        double hitChance = HuntingCalculator.CalculateThrownAccuracy(distance, maxRange, baseAccuracy, isSmall);

                        if (hitChance > 0)
                        {
                            if (ImGui.Button($"Throw {weapon.Name} ({hitChance:P0})", new Vector2(-1, 0)))
                                clickedAction = CombatActions.Throw;
                        }
                    }

                    if (ImGui.Button("Intimidate", new Vector2(-1, 0)))
                        clickedAction = CombatActions.Intimidate;
                    break;
            }
        }

        // Flee option - available when near any edge
        if (CombatScenario.CanFlee(playerUnit.Position))
        {
            ImGui.Separator();
            int dist = CombatScenario.GetDistanceFromEdge(playerUnit.Position);
            string label = dist == 0 ? "Flee! (at edge)" : $"Flee ({dist}m to edge)";
            if (ImGui.Button(label, new Vector2(-1, 0)))
                clickedAction = CombatActions.Flee;
        }

        ImGui.End();

        return clickedAction;
    }
}
