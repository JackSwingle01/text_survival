using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;

namespace text_survival.Desktop.UI;

/// <summary>
/// Personal survival summary with independently scrolling condition details.
/// </summary>
public static class SurvivorPanel
{
    // Color constants
    private static readonly Vector4 ColorGood = new(0.4f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 ColorWarning = new(1f, 0.8f, 0.3f, 1f);
    private static readonly Vector4 ColorDanger = new(1f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 ColorCritical = new(1f, 0.1f, 0.1f, 1f);
    private static readonly Vector4 ColorMuted = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 ColorHeader = new(0.9f, 0.85f, 0.7f, 1f);
    private static readonly Vector4 ColorCold = new(0.5f, 0.7f, 1f, 1f);
    private static readonly Vector4 ColorWarm = new(1f, 0.6f, 0.3f, 1f);

    // Trend indicators (Unicode arrows merged into font in Program.cs)
    private const string ArrowUp = " ↑";
    private const string ArrowDown = " ↓";

    /// <summary>
    /// Render the stats panel.
    /// </summary>
    public static text_survival.UI.PlayerAction? Render(GameContext ctx, HudRect rect, IReadOnlyList<HudAction> actions, bool interactive)
    {
        text_survival.UI.PlayerAction? result = null;
        HudWidgets.Begin("##Survivor", rect);
        UiText.Colored(HudWidgets.Heading, "SURVIVOR");
        ImGui.Separator();
        RenderSurvivalStats(ctx.player);
        ImGui.Separator();
        var body = ctx.player.Body;
        double rate = ctx.player.LastUpdateMinutes > 0 && ctx.player.LastSurvivalDelta?.TemperatureDelta is double delta
            ? delta / ctx.player.LastUpdateMinutes * 60 : 0;
        UiIcons.LabelColored("temperature", body.BodyTemperature < 97 ? ColorWarning : ColorGood,
            $"Body {body.BodyTemperature:F1}°F{(rate < -.5 ? ArrowDown : rate > .5 ? ArrowUp : "")}");
        UiText.Disabled($"{rate:+0.0;-0.0;0.0}°F/hr · Feels {ctx.CurrentLocation.GetTemperatureBreakdown(ctx.CurrentActivity).FinalTemp:F0}°F");
        RenderInventorySummary(ctx);
        var warnings = SurvivorWarnings.Build(ctx);
        if (warnings.Count > 0)
        {
            // One fixed-height summary. Full warnings remain accessible in the details below.
            UiText.Colored(ColorDanger, warnings.Count == 1 ? warnings[0] : $"{warnings[0]} (+{warnings.Count - 1})");
            if (ImGui.IsItemHovered()) UiText.Tooltip(string.Join("\n", warnings));
        }
        ImGui.Separator();
        foreach (var action in actions.Where(a => a.Group == HudActionGroup.Personal))
            if (HudWidgets.Action(action, interactive)) result = action.Payload;
        ImGui.Separator();
        ImGui.BeginChild("survivor-details", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        UiText.Colored(HudWidgets.Heading, "CONDITION");
        foreach (var warning in warnings) { ImGui.PushTextWrapPos(0); UiText.Colored(ColorWarning, warning); ImGui.PopTextWrapPos(); }
        RenderBodyCondition(ctx, body);
        RenderEffects(ctx);
        RenderTensions(ctx);
        if (ImGui.CollapsingHeader("Temperature & clothing"))
            RenderTemperature(ctx, body, ctx.CurrentLocation, ctx.CurrentLocation.Weather);
        ImGui.EndChild();
        ImGui.End();
        return result;
    }

    private static void RenderSurvivalStats(Actor actor)
    {
        int energyPct = (int)(actor.Body.EnergyPct * 100);
        int caloriesPct = (int)(actor.Body.FullPct * 100);
        int hydrationPct = (int)(actor.Body.HydratedPct * 100);
        int vitalityPct = (int)(actor.Vitality * 100);

        if (ImGui.BeginTable("survival_stats", 2))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Bar", ImGuiTableColumnFlags.WidthStretch);

            RenderStatRow("Energy", energyPct, GetStatColor(energyPct));
            RenderStatRow("Food", caloriesPct, GetStatColor(caloriesPct));
            RenderStatRow("Water", hydrationPct, GetStatColor(hydrationPct));
            RenderStatRow("Vitality", vitalityPct, GetStatColor(vitalityPct));

            ImGui.EndTable();
        }
    }

    // Row helpers - caller manages table begin/end
    private static void RenderStatRow(string label, int percent, Vector4 color)
    {
        percent = Math.Clamp(percent, 0, 100);
        ImGui.TableNextColumn();
        UiIcons.Label(label == "Food" ? "food" : label.ToLowerInvariant(), label);
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(percent / 100f, new Vector2(-1, 18), $"{percent}%");
        ImGui.PopStyleColor();
    }

    private static void RenderEffectRow(string label, int percent, Vector4 color, string trend)
    {
        percent = Math.Clamp(percent, 0, 100);
        ImGui.TableNextColumn();
        UiText.Text($"  {label}");
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(percent / 100f, new Vector2(-1, OverlaySizes.CompactBarHeight), $"{percent}%{trend}");
        ImGui.PopStyleColor();
    }

    private static void RenderCapacityRow(string label, double value)
    {
        int pct = (int)(value * 100);
        ImGui.TableNextColumn();
        UiText.Text($"  {label}");
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, GetCapacityColor(value));
        ImGui.ProgressBar((float)value, new Vector2(-1, OverlaySizes.CompactBarHeight), $"{pct}%");
        ImGui.PopStyleColor();
    }

    private static void RenderTemperature(GameContext ctx, Body body, Environments.Location location, Weather weather)
    {
        double bodyTemp = body.BodyTemperature;
        // The player's real activity, not a constant. A structural shelter only helps while
        // you stay put, so asking at Idle always returned the sheltered figure - the panel
        // showed a snug number while the player was out foraging in it, and the gap between
        // the two runs to tens of degrees. GetTemperature has no parameterless overload for
        // exactly this reason; passing a literal was the same mistake by another route.
        var breakdown = location.GetTemperatureBreakdown(ctx.CurrentActivity);

        // Calculate trend
        double trendPerHour = 0;
        if (ctx.player.LastSurvivalDelta?.TemperatureDelta is double delta && ctx.player.LastUpdateMinutes > 0)
        {
            trendPerHour = (delta / ctx.player.LastUpdateMinutes) * 60;
        }

        string trendArrow = trendPerHour > 0.5 ? ArrowUp : trendPerHour < -0.5 ? ArrowDown : "";
        double tempPct = Math.Clamp((bodyTemp - 90) / 9.0, 0, 1);
        Vector4 tempColor = bodyTemp < 95 ? ColorCold : bodyTemp < 97 ? ColorWarning : ColorGood;
        Vector4 feelsLikeColor = breakdown.FinalTemp < 20 ? ColorCold : breakdown.FinalTemp < 40 ? ColorMuted : ColorWarm;
        int warmthPct = (int)(body.ClothingHeatBufferPct * 100);

        // Always visible: Body Temp, rate, Feels Like
        if (ImGui.BeginTable("temperature", 2))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

            // Body Temp row with trend rate
            ImGui.TableNextColumn();
            UiIcons.Label("temperature", "Body Temp");
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, tempColor);
            ImGui.ProgressBar((float)tempPct, new Vector2(-1, 18), $"{bodyTemp:F1}°F{trendArrow}");
            ImGui.PopStyleColor();

            // Show rate per hour if significant
            if (Math.Abs(trendPerHour) > 0.5)
            {
                ImGui.TableNextColumn();
                UiText.Text("");
                ImGui.TableNextColumn();
                Vector4 rateColor = trendPerHour > 0 ? ColorWarm : ColorCold;
                UiText.Colored(rateColor, $"  {trendPerHour:+0.0;-0.0}°F/hr");
            }

            // Feels Like (effective temperature) - prominent display
            ImGui.TableNextColumn();
            UiIcons.LabelColored("temperature", ColorHeader, "Feels Like");
            ImGui.TableNextColumn();
            UiText.Colored(feelsLikeColor, $"{breakdown.FinalTemp:F0}°F");

            // Clothing Warmth row
            ImGui.TableNextColumn();
            UiIcons.Label("clothing", "Clothing Warmth");
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ColorWarning);
            ImGui.ProgressBar(warmthPct / 100f, new Vector2(-1, 18), $"{warmthPct}%");
            ImGui.PopStyleColor();

            ImGui.EndTable();
        }

        // Collapsible breakdown section - default collapsed
        if (ImGui.CollapsingHeader("Details##temp", ImGuiTreeNodeFlags.None))
        {
            if (ImGui.BeginTable("temp_breakdown", 2))
            {
                ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

                // Base temp
                ImGui.TableNextColumn();
                UiText.Disabled("Base");
                ImGui.TableNextColumn();
                UiText.Disabled($"{breakdown.BaseTemp:F0}°F");

                // Location modifier
                if (Math.Abs(breakdown.LocationMod) > 1)
                {
                    ImGui.TableNextColumn();
                    UiText.Disabled("Location");
                    ImGui.TableNextColumn();
                    UiText.Disabled($"{breakdown.LocationMod:+0;-0}°F");
                }

                // Wind chill (negative)
                if (breakdown.WindChill < -1)
                {
                    ImGui.TableNextColumn();
                    UiIcons.LabelColored("wind", ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled], "Wind");
                    ImGui.TableNextColumn();
                    UiText.Colored(ColorCold, $"{breakdown.WindChill:F0}°F");
                }

                // Sun warming (positive)
                if (breakdown.SunWarming > 1)
                {
                    ImGui.TableNextColumn();
                    UiIcons.LabelColored("sun", ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled], "Sun");
                    ImGui.TableNextColumn();
                    UiText.Colored(ColorWarm, $"+{breakdown.SunWarming:F0}°F");
                }

                // Precipitation cooling (negative)
                if (breakdown.PrecipCooling > 1)
                {
                    ImGui.TableNextColumn();
                    UiIcons.LabelColored("precipitation", ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled], "Precip");
                    ImGui.TableNextColumn();
                    UiText.Colored(ColorCold, $"-{breakdown.PrecipCooling:F0}°F");
                }

                // Shelter bonus (positive)
                if (breakdown.ShelterBonus > 1)
                {
                    ImGui.TableNextColumn();
                    UiIcons.LabelColored("shelter", ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled], "Shelter");
                    ImGui.TableNextColumn();
                    UiText.Colored(ColorWarm, $"+{breakdown.ShelterBonus:F0}°F");
                }

                // Fire bonus (positive)
                if (breakdown.FireBonus > 1)
                {
                    ImGui.TableNextColumn();
                    UiIcons.LabelColored("fire", ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled], "Fire");
                    ImGui.TableNextColumn();
                    UiText.Colored(ColorWarm, $"+{breakdown.FireBonus:F0}°F");
                }

                ImGui.EndTable();
            }
        }
    }

    private static void RenderBodyCondition(GameContext ctx, Body body)
    {
        var capacities = ctx.player.GetCapacities();
        bool hasBloodIssue = body.Blood.Condition < 0.95;
        bool hasCapacityIssues = capacities.Moving < 0.9 || capacities.Manipulation < 0.9 || capacities.Consciousness < 0.9;
        var injuredParts = body.Parts
            .Where(p => p.Condition <= 0.995 || p.Organs.Any(o => o.Condition <= 0.995))
            .OrderBy(p => p.Condition)
            .ToList();
        bool hasInjuries = injuredParts.Count > 0;

        if (!hasBloodIssue && !hasCapacityIssues && !hasInjuries) return;

        ImGui.Separator();

        // Injuries section - show damaged body parts and blood loss
        if (hasInjuries || hasBloodIssue)
        {
            UiIcons.LabelColored("bandage", ColorHeader, "Injuries");

            if (ImGui.BeginTable("injuries", 3))
            {
                ImGui.TableSetupColumn("Part", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Bar", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Severity", ImGuiTableColumnFlags.WidthFixed, 60);

                foreach (var part in injuredParts)
                {
                    RenderInjuryRow(part.Name, part.Condition);
                    var damagedOrgans = part.Organs.Where(o => o.Condition <= 0.995).OrderBy(o => o.Condition);
                    foreach (var organ in damagedOrgans)
                    {
                        RenderInjuryRow(organ.Name, organ.Condition);
                    }
                }

                // Blood loss display - shown with injuries since bleeding is injury-related
                if (hasBloodIssue)
                {
                    int bloodPct = (int)(body.Blood.Condition * 100);
                    ImGui.TableNextColumn();
                    UiText.Text("  Blood");
                    ImGui.TableNextColumn();
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, GetCapacityColor(body.Blood.Condition));
                    ImGui.ProgressBar((float)body.Blood.Condition, new Vector2(-1, OverlaySizes.CompactBarHeight), $"{bloodPct}%");
                    ImGui.PopStyleColor();
                    ImGui.TableNextColumn();
                    UiText.Text("");  // Empty third column to match table format
                }

                ImGui.EndTable();
            }
        }

        // Capacities section
        if (hasCapacityIssues)
        {
            if (hasInjuries || hasBloodIssue)
                ImGui.Spacing();
            UiText.Colored(ColorHeader, "Capacities");

            if (ImGui.BeginTable("body_condition", 2))
            {
                ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Bar", ImGuiTableColumnFlags.WidthStretch);

                if (capacities.Moving < 0.9)
                    RenderCapacityRow("Moving", capacities.Moving);
                if (capacities.Manipulation < 0.9)
                    RenderCapacityRow("Manipulate", capacities.Manipulation);
                if (capacities.Consciousness < 0.9)
                    RenderCapacityRow("Conscious", capacities.Consciousness);

                ImGui.EndTable();
            }
        }
    }

    private static void RenderInjuryRow(string partName, double condition)
    {
        double damage = 1 - condition;
        int damagePct = (int)(damage * 100);
        string severity = GetDamageDescription(condition);
        Vector4 color = GetInjuryColor(condition);

        ImGui.TableNextColumn();
        UiText.Text($"  {partName}");

        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar((float)damage, new Vector2(-1, OverlaySizes.CompactBarHeight), $"{damagePct}%");
        ImGui.PopStyleColor();

        ImGui.TableNextColumn();
        UiText.Colored(color, severity);
    }

    private static string GetDamageDescription(double condition)
    {
        return condition switch
        {
            <= 0 => "Destroyed",
            < 0.2 => "Critical",
            < 0.4 => "Severe",
            < 0.6 => "Moderate",
            < 0.8 => "Light",
            _ => "Minor"
        };
    }

    private static Vector4 GetInjuryColor(double condition)
    {
        if (condition < 0.2) return ColorCritical;
        if (condition < 0.4) return ColorDanger;
        if (condition < 0.6) return ColorWarning;
        return ColorMuted;
    }

    private static void RenderEffects(GameContext ctx)
    {
        var effects = ctx.player.EffectRegistry.GetAll().ToList();
        if (effects.Count == 0) return;

        ImGui.Separator();
        UiIcons.LabelColored("medicine", ColorHeader, "Active Effects");

        if (ImGui.BeginTable("effects", 2))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Bar", ImGuiTableColumnFlags.WidthStretch);

            foreach (var effect in effects)
            {
                int severity = (int)(effect.Severity * 100);
                // Calculate actual trend from severity delta
                double? severityDelta = effect.GetSeverityChangeSinceSnapshot();
                string trend = severityDelta switch
                {
                    null => "",                    // No snapshot yet
                    > 0.001 => ArrowUp,           // Worsening
                    < -0.001 => ArrowDown,        // Improving
                    _ => ""                        // Stable
                };
                Vector4 color = GetEffectColor(effect);
                RenderEffectRow(effect.EffectKind, severity, color, trend);

                // Add tooltip on hover
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    UiText.Colored(ColorHeader, effect.EffectKind);
                    ImGui.Separator();

                    // Show capacity modifiers
                    var modifiers = effect.CapacityModifiers.ToDictionary();
                    bool hasModifiers = false;
                    foreach (var (capacity, modifier) in modifiers)
                    {
                        if (Math.Abs(modifier) > 0.01)
                        {
                            hasModifiers = true;
                            int pctChange = (int)(modifier * 100);
                            Vector4 modColor = pctChange >= 0 ? ColorGood : ColorDanger;
                            UiText.Colored(modColor, $"{capacity}: {pctChange:+0;-0}%");
                        }
                    }

                    // Show damage over time if present
                    if (effect.Damage != null)
                    {
                        hasModifiers = true;
                        UiText.Colored(ColorDanger, $"Damage: {effect.Damage.PerHour:F0}/hr ({effect.Damage.Type})");
                    }

                    // Show if treatment required
                    if (effect.RequiresTreatment)
                    {
                        hasModifiers = true;
                        UiText.Colored(ColorWarning, "Requires treatment");
                    }

                    if (!hasModifiers)
                    {
                        UiText.Disabled("No direct capacity effects");
                    }

                    ImGui.EndTooltip();
                }
            }

            ImGui.EndTable();
        }
    }

    private static void RenderTensions(GameContext ctx)
    {
        var tensions = ctx.Tensions.GetAllTensions().ToList();
        if (tensions.Count == 0) return;

        ImGui.Separator();
        UiIcons.LabelColored("spear", ColorHeader, "Threats");

        foreach (var tension in tensions)
        {
            Vector4 color = tension.Severity > 0.7 ? ColorDanger :
                           tension.Severity > 0.4 ? ColorWarning : ColorMuted;
            UiText.Colored(color, $"  {tension.Type}");
        }
    }

    private static void RenderInventorySummary(GameContext ctx)
    {
        var inv = ctx.Inventory;
        double current = inv.CurrentWeightKg;
        double max = inv.MaxWeightKg;
        double pct = max > 0 ? current / max : 0;
        Vector4 weightColor = pct > 0.9 ? ColorDanger : pct > 0.7 ? ColorWarning : ColorGood;
        double fuelKg = inv.GetWeight(ResourceCategory.Fuel);

        if (ImGui.BeginTable("inventory", 2))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

            // Carry row
            ImGui.TableNextColumn();
            UiIcons.Label("backpack", "Carry");
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, weightColor);
            ImGui.ProgressBar((float)pct, new Vector2(-1, 18), $"{current:F1}/{max:F1} kg");
            ImGui.PopStyleColor();

            ImGui.EndTable();
        }
    }

    private static Vector4 GetStatColor(int percent)
    {
        if (percent <= 15) return ColorCritical;
        if (percent <= 30) return ColorDanger;
        if (percent <= 50) return ColorWarning;
        return ColorGood;
    }

    private static Vector4 GetCapacityColor(double capacity)
    {
        if (capacity <= 0.3) return ColorCritical;
        if (capacity <= 0.5) return ColorDanger;
        if (capacity <= 0.7) return ColorWarning;
        return ColorMuted;
    }

    private static Vector4 GetEffectColor(Effect effect)
    {
        float severity = (float)Math.Clamp(effect.Severity, 0, 1);

        if (effect.IsBeneficial)
        {
            // Positive effects: green at high severity (strong buff), yellow at low (fading)
            return Vector4.Lerp(ColorWarning, ColorGood, severity);
        }
        else
        {
            // Negative effects: red at high severity (dangerous), green at low (resolving)
            return Vector4.Lerp(ColorGood, ColorDanger, severity);
        }
    }

}
