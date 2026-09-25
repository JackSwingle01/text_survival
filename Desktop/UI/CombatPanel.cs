using ImGui = text_survival.Desktop.UI.GameGui;
using ImGuiNET;
using text_survival.UI;
using text_survival.Environments.Grid;
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
    public CombatInput? Render(GameContext ctx, HudRect rect, text_survival.Combat.Unit? hoveredUnit, CombatUiState state, bool interactive)
    {
        CombatInput? result = null;

        HudWidgets.Begin("##Combat", rect);

        var combat = ctx.ActiveCombat;
        if (combat == null || combat.Player == null)
        {
            ImGui.End();
            return null;
        }

        var playerUnit = combat.Player;
        state.Update(combat);
        bool commands = interactive && UiInput.Context == InputContext.Combat && !UiInput.HudFocused;
        bool Key(ImGuiKey key) => commands && GameGui.Capture == null && UiNavigation.Key(key);
        if (Key(ImGuiKey.Escape)) state.Cancel();
        if (Key(ImGuiKey.X)) state.Inspect(combat);
        if (Key(ImGuiKey.Q)) state.Cycle(combat, -1);
        if (Key(ImGuiKey.E)) state.Cycle(combat, 1);
        if (Key(ImGuiKey.W)) state.Step(combat, 0, -1);
        if (Key(ImGuiKey.S)) state.Step(combat, 0, 1);
        if (Key(ImGuiKey.A)) state.Step(combat, -1, 0);
        if (Key(ImGuiKey.D)) state.Step(combat, 1, 0);
        if (Key(ImGuiKey.Enter) && state.Cursor != null)
        {
            state.Feedback = state.MoveReason(combat);
            if (state.Feedback == null) { result = new(null, state.Cursor); state.Cancel(); }
        }
        var nearest = state.Target;

        // Inspection is separate from the explicitly selected attack target.
        var displayTarget = state.Inspecting ? combat.Units.FirstOrDefault(u => u.actor.IsAlive && u.Position == state.Cursor) : hoveredUnit ?? nearest;

        // Determine if we're in stealth mode (target not engaged)
        bool inStealth = nearest != null && nearest.Awareness != AwarenessState.Engaged;
        int huntingSkill = ctx.player.Skills.GetSkill("Hunting")?.Level ?? 0;

        ImGui.BeginChild("combat-status", new Vector2(0, Math.Min(ImGui.GetFontSize() * 10, rect.Height * .32f)));
        string headerText = inStealth ? "STALKING" : "COMBAT";
        double targetDistance = nearest == null ? 0 : playerUnit.Position.DistanceTo(nearest.Position);
        var targetZone = CombatScenario.GetZone(targetDistance);
        UiText.Colored(targetZone is Zone.close or Zone.near ? UiStyle.Caution : HudWidgets.Heading,
            nearest == null ? headerText : $"{headerText} · {targetDistance:F0}m · {targetZone.ToString().ToUpperInvariant()}");
        UiText.Wrapped("WASD → Enter move · Q/E target · X inspect · Esc cancel");
        UiIcons.LabelColored(state.Inspecting ? "inspect" : "target", HudWidgets.Heading,
            state.Inspecting ? "INSPECTING · No time passes" : $"TARGET: {nearest?.actor.Name ?? "None"}");
        UiText.Wrapped(state.Cursor is { } cursor
            ? $"({cursor.X}, {cursor.Y}) · {state.MoveReason(combat) ?? $"Move: 1 action · allowance {CombatMovement.Allowance(playerUnit)}m"}"
            : "Choose a target or preview a move.");
        UiText.Wrapped(state.Feedback ?? " ");
        ImGui.EndChild();
        bool openThrow = false;
        ImGui.BeginChild("combat-actions", new Vector2(0, Math.Max(ImGui.GetFontSize() * 10, 4 * (UiStyle.IconSize + 16 * UiStyle.Scale) + 12)));

        void Action(CombatActions action, string label, string icon, ImGuiKey key, bool shift = false)
        {
            string? reason = CombatUiState.Reason(ctx, nearest, action);
            bool clicked = UiControls.Button(new($"combat-{action}", label, icon, DisabledReason: reason, Tone: action == CombatActions.Flee ? UiTone.Caution : UiTone.Normal), interactive, new(-1, 0));
            bool pressed = Key(key) && ImGuiNET.ImGui.GetIO().KeyShift == shift && !state.Inspecting;
            if (pressed && reason != null) state.Feedback = reason;
            if ((clicked || pressed) && reason == null && result == null) { result = new(action, null, nearest); state.Cancel(); }
        }
        Action(CombatActions.Attack, "Attack [F]", ctx.Inventory.Weapon is { } weapon ? UiIcons.ForGear(weapon) : "shove", ImGuiKey.F);
        Action(CombatActions.Block, "Block [R]", "block", ImGuiKey.R);
        Action(CombatActions.Dodge, "Dodge [C]", "dodge", ImGuiKey.C);
        Action(CombatActions.Flee, "Flee [Shift+F]", "flee", ImGuiKey.F, true);
        Action(CombatActions.Shove, "Shove [V]", "shove", ImGuiKey.V);
        if (UiControls.Button(new("combat-throw", "Throw… [T]", "throw"), interactive, new(-1, 0)) || Key(ImGuiKey.T))
        {
            openThrow = true;
            if (GameGui.Capture != null) state.Throwing = true;
        }
        Action(CombatActions.Wait, "Wait [Space]", "clock", ImGuiKey.Space);
        Action(CombatActions.Assess, "Assess [Z]", "inspect", ImGuiKey.Z);
        Action(CombatActions.Intimidate, "Intimidate [B]", "intimidate", ImGuiKey.B);
        Action(CombatActions.CallHelp, "Call for help [H]", "call-help", ImGuiKey.H);
        Action(CombatActions.CallRetreat, "Call for retreat [J]", "call-retreat", ImGuiKey.J);
        ImGui.EndChild();
        if (openThrow && GameGui.Capture == null) ImGuiNET.ImGui.OpenPopup("Choose throwable");
        if (GameGui.Capture != null ? state.Throwing : ImGuiNET.ImGui.BeginPopup("Choose throwable"))
        {
            if (GameGui.Capture == null && ImGuiNET.ImGui.IsKeyPressed(ImGuiKey.Escape, false)) ImGuiNET.ImGui.CloseCurrentPopup();
            UiText.Wrapped("Select a throwable · Tab / arrows · Enter · Esc cancel");
            foreach (var action in new[] { CombatActions.Throw, CombatActions.ThrowStone })
            {
                string label = action == CombatActions.Throw ? ctx.Inventory.Weapon?.Name ?? "Weapon" : $"Stone ×{ctx.Inventory.Count(Resource.Stone)}";
                double distance = nearest == null ? 0 : playerUnit.Position.DistanceTo(nearest.Position);
                double chance = CombatFormulas.CalculateThrownAccuracy(distance,
                    action == CombatActions.ThrowStone ? CombatScenario.STONE_RANGE_M : ctx.Inventory.Weapon?.Name.Contains("Stone") == true ? 25 : 20,
                    action == CombatActions.ThrowStone ? CombatScenario.STONE_BASE_ACCURACY : ctx.Inventory.Weapon?.Name.Contains("Stone") == true ? .75 : .70,
                    nearest?.actor is Animal animal && animal.Size == AnimalSize.Small);
                if (UiControls.Button(new($"throw-{action}", $"{label} · {chance:P0} hit", "throw", DisabledReason: CombatUiState.Reason(ctx, nearest, action)), interactive, new(-1, 0)))
                { result = new(action, null, nearest); state.Cancel(); if (GameGui.Capture == null) ImGuiNET.ImGui.CloseCurrentPopup(); }
                if (action == CombatActions.Throw && GameGui.Capture == null) ImGuiNET.ImGui.SetItemDefaultFocus();
            }
            if (UiControls.Button(new("throw-cancel", "Cancel [Esc]", "back"), interactive, new(-1, 0))) { state.Throwing = false; if (GameGui.Capture == null) ImGuiNET.ImGui.CloseCurrentPopup(); }
            if (GameGui.Capture == null) ImGuiNET.ImGui.EndPopup();
        }
        ImGui.BeginChild("combat-details", new Vector2(0, 0));
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
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, UiStyle.BarFill(vitalityColor));
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
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, UiStyle.BarFill(boldnessColor));
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
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, UiStyle.BarFill(threatColor));
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
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, UiStyle.BarFill(aggressionColor));
                ImGui.ProgressBar(aggressionDisplay, new Vector2(-1, 0), $"Aggression: {aggressionVal * 100:F0}%");
                ImGui.PopStyleColor();
            }

            // Speed and Strength as numbers
            ImGui.Spacing();
            UiText.Disabled($"Movement: {CombatMovement.Allowance(displayTarget)}m/action  Strength: {displayTarget.actor.Strength:F2}");

            // Injuries section - show damaged body parts
            var damagedParts = displayTarget.actor.Body.Parts
                .Where(p => p.Condition < 1.0)
                .OrderBy(p => p.Name)
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
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, UiStyle.BarFill(injuryColor));
                    UiText.Text(part.Name);
                    ImGui.ProgressBar(1 - condition, new Vector2(-1, 0), $"Damage: {(1 - condition) * 100:F0}%");
                    ImGui.PopStyleColor();
                }
            }
        }

        ImGui.EndChild();
        ImGui.End();
        return result;
    }
}
