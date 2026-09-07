using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actions.Handlers;
using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Desktop.Input;
using text_survival.Desktop.Rendering;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.Actions.Expeditions.WorkStrategies;

namespace text_survival.Desktop.UI;

public sealed class CombatPanel
{
    public CombatActions? Render(GameContext ctx, HudRect rect, text_survival.Combat.Unit? hoveredUnit)
    {
        CombatActions? clickedAction = null;

        HudWidgets.Begin("##Combat", rect);

        var combat = ctx.ActiveCombat;
        if (combat == null || combat.Player == null)
        {
            ImGui.End();
            return null;
        }

        var playerUnit = combat.Player;
        var nearest = combat.GetNearestEnemy(playerUnit);

        // Get display target: hovered unit or nearest enemy
        var displayTarget = hoveredUnit ?? nearest;

        // Determine if we're in stealth mode (target not engaged)
        bool inStealth = nearest != null && nearest.Awareness != AwarenessState.Engaged;
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting")?.Level ?? 0;

        // Combat header
        string headerText = inStealth ? "STALKING" : "COMBAT";
        UiText.Colored(new Vector4(1f, 0.5f, 0.3f, 1f), headerText);
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

            UiText.Text($"Distance: {distance:F0}m");
            UiText.Colored(zoneColor, zoneText);
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
                UiText.Colored(new Vector4(0.7f, 0.85f, 1f, 1f), targetLabel);
            }
            else
            {
                UiText.Colored(new Vector4(0.9f, 0.85f, 0.7f, 1f), targetLabel);
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
                UiText.Colored(awarenessColor, awarenessText);

                // Show activity hint for animals
                if (displayTarget.actor is Animal animal)
                {
                    var behavior = HuntingSightingSelector.MapActivityToBehavior(animal);
                    string hint = HuntingSightingSelector.GetBehaviorHint(behavior);
                    UiText.Wrapped(hint);
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
                    UiText.Colored(riskColor, $"Detection Risk: {detectionRisk:P0}");
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
            UiText.Disabled($"Movement: {CombatMovement.Allowance(displayTarget)}m/action  Strength: {displayTarget.actor.Strength:F2}");

            // Injuries section - show damaged body parts
            var damagedParts = displayTarget.actor.Body.Parts
                .Where(p => p.Condition < 1.0)
                .OrderBy(p => p.Condition)
                .Take(4)  // Limit to 4 to save space
                .ToList();

            if (damagedParts.Count > 0)
            {
                ImGui.Spacing();
                UiText.Disabled("Injuries:");
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
        UiText.Text("Actions:");
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
                        double hitChance = CombatFormulas.CalculateThrownAccuracy(distance, maxRange, baseAccuracy, isSmall);

                        if (ImGui.Button($"Throw {weapon.Name} ({hitChance:P0})", new Vector2(-1, 0)))
                            clickedAction = CombatActions.Throw;
                    }

                    // Throw stone with hit chance
                    int stones = ctx.Inventory.Count(Resource.Stone);
                    if (stones > 0)
                    {
                        double hitChance = CombatFormulas.CalculateThrownAccuracy(
                            distance, CombatScenario.STONE_RANGE_M, CombatScenario.STONE_BASE_ACCURACY, isSmall);

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
                        double hitChance = CombatFormulas.CalculateThrownAccuracy(distance, maxRange, baseAccuracy, isSmall);

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

        if (ImGui.Button("Call for help", new Vector2(-1, 0))) clickedAction = CombatActions.CallHelp;
        if (ImGui.Button("Call for retreat", new Vector2(-1, 0))) clickedAction = CombatActions.CallRetreat;

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
