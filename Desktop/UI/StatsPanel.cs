using ImGuiNET;
using System.Numerics;
using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.Survival;

namespace text_survival.Desktop.UI;

/// <summary>
/// Comprehensive stats panel displaying full survival state.
/// Matches the web version's GameStateDto content.
/// </summary>
public static class StatsPanel
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
    public static void Render(GameContext ctx)
    {
        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(280, 0), ImGuiCond.Always);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                                  ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;

        if (ImGui.Begin("Survival Status", flags))
        {
            var body = ctx.player.Body;
            var location = ctx.CurrentLocation;
            var weather = location.Weather;

            // Time Section
            RenderTimeSection(ctx, weather);

            ImGui.Separator();

            // Survival Stats
            RenderSurvivalStats(body);

            ImGui.Separator();

            // Temperature
            RenderTemperature(ctx, body, location, weather);

            // Body Condition (if any issues)
            RenderBodyCondition(ctx, body);

            // Active Effects
            RenderEffects(ctx);

            // Tensions
            RenderTensions(ctx);

            ImGui.Separator();

            // Inventory Summary
            RenderInventorySummary(ctx);

            // Fire Status (if at location with fire)
            RenderFireStatus(location);

            // Camp Info (if at camp)
            RenderCampInfo(ctx);
        }
        ImGui.End();
    }

    private static void RenderTimeSection(GameContext ctx, Weather weather)
    {
        var startDate = new DateTime(2025, 1, 1);
        int dayNumber = (ctx.GameTime - startDate).Days + 1;
        bool isDaytime = weather.IsDaytime(ctx.GameTime);
        string timeOfDay = ctx.GetTimeOfDay().ToString();

        ImGui.TextColored(ColorHeader, $"Day {dayNumber}");
        ImGui.SameLine();
        ImGui.Text($"  {ctx.GameTime:h:mm tt}");

        // Weather info - expanded
        ImGui.TextDisabled(weather.GetSeasonLabel());
        ImGui.SameLine();
        ImGui.TextDisabled($"| {weather.GetConditionLabel()}");

        // Weather details
        if (ImGui.BeginTable("weather_details", 2))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

            // Base temp
            ImGui.TableNextColumn();
            ImGui.TextDisabled("Base Temp");
            ImGui.TableNextColumn();
            ImGui.TextDisabled($"{weather.BaseTemperature:F0}°F");

            // Wind
            ImGui.TableNextColumn();
            ImGui.TextDisabled("Wind");
            ImGui.TableNextColumn();
            ImGui.TextDisabled($"{weather.WindSpeed:F0} mph {weather.CurrentWindDirection}");

            // Precipitation
            ImGui.TableNextColumn();
            ImGui.TextDisabled("Precip");
            ImGui.TableNextColumn();
            ImGui.TextDisabled(GetPrecipitationLabel(weather.Precipitation));

            // Weather front
            string frontLabel = weather.GetFrontLabel();
            if (!string.IsNullOrEmpty(frontLabel))
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled("Front");
                ImGui.TableNextColumn();
                ImGui.TextDisabled(frontLabel);
            }

            ImGui.EndTable();
        }
    }

    private static string GetPrecipitationLabel(double precipitation)
    {
        if (precipitation < 0.1) return "None";
        if (precipitation < 0.3) return "Light";
        if (precipitation < 0.6) return "Moderate";
        return "Heavy";
    }

    private static void RenderSurvivalStats(Body body)
    {
        int energyPct = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);
        int caloriesPct = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPct = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
        int vitalityPct = (int)(AbilityCalculator.CalculateVitality(body) * 100);

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
        ImGui.Text(label);
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(percent / 100f, new Vector2(-1, 18), $"{percent}%");
        ImGui.PopStyleColor();
    }

    private static void RenderEffectRow(string label, int percent, Vector4 color, string trend)
    {
        percent = Math.Clamp(percent, 0, 100);
        ImGui.TableNextColumn();
        ImGui.Text($"  {label}");
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(percent / 100f, new Vector2(-1, OverlaySizes.CompactBarHeight), $"{percent}%{trend}");
        ImGui.PopStyleColor();
    }

    private static void RenderCapacityRow(string label, double value)
    {
        int pct = (int)(value * 100);
        ImGui.TableNextColumn();
        ImGui.Text($"  {label}");
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, GetCapacityColor(value));
        ImGui.ProgressBar((float)value, new Vector2(-1, OverlaySizes.CompactBarHeight), $"{pct}%");
        ImGui.PopStyleColor();
    }

    private static void RenderTemperature(GameContext ctx, Body body, Environments.Location location, Weather weather)
    {
        double bodyTemp = body.BodyTemperature;
        var breakdown = location.GetTemperatureBreakdown();

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

        if (ImGui.BeginTable("temperature", 2))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

            // Body Temp row with trend rate
            ImGui.TableNextColumn();
            ImGui.Text("Body Temp");
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, tempColor);
            ImGui.ProgressBar((float)tempPct, new Vector2(-1, 18), $"{bodyTemp:F1}°F{trendArrow}");
            ImGui.PopStyleColor();

            // Show rate per hour if significant
            if (Math.Abs(trendPerHour) > 0.5)
            {
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.TableNextColumn();
                Vector4 rateColor = trendPerHour > 0 ? ColorWarm : ColorCold;
                ImGui.TextColored(rateColor, $"  {trendPerHour:+0.0;-0.0}°F/hr");
            }

            // Feels Like (effective temperature) - prominent display
            ImGui.TableNextColumn();
            ImGui.TextColored(ColorHeader, "Feels Like");
            ImGui.TableNextColumn();
            ImGui.TextColored(feelsLikeColor, $"{breakdown.FinalTemp:F0}°F");

            // Temperature breakdown - only show significant modifiers (> 1°F effect)
            // Base temp
            ImGui.TableNextColumn();
            ImGui.TextDisabled("  Base");
            ImGui.TableNextColumn();
            ImGui.TextDisabled($"{breakdown.BaseTemp:F0}°F");

            // Location modifier
            if (Math.Abs(breakdown.LocationMod) > 1)
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled("  Location");
                ImGui.TableNextColumn();
                ImGui.TextDisabled($"{breakdown.LocationMod:+0;-0}°F");
            }

            // Wind chill (negative)
            if (breakdown.WindChill < -1)
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled("  Wind");
                ImGui.TableNextColumn();
                ImGui.TextColored(ColorCold, $"{breakdown.WindChill:F0}°F");
            }

            // Sun warming (positive)
            if (breakdown.SunWarming > 1)
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled("  Sun");
                ImGui.TableNextColumn();
                ImGui.TextColored(ColorWarm, $"+{breakdown.SunWarming:F0}°F");
            }

            // Precipitation cooling (negative)
            if (breakdown.PrecipCooling > 1)
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled("  Precip");
                ImGui.TableNextColumn();
                ImGui.TextColored(ColorCold, $"-{breakdown.PrecipCooling:F0}°F");
            }

            // Shelter bonus (positive)
            if (breakdown.ShelterBonus > 1)
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled("  Shelter");
                ImGui.TableNextColumn();
                ImGui.TextColored(ColorWarm, $"+{breakdown.ShelterBonus:F0}°F");
            }

            // Fire bonus (positive)
            if (breakdown.FireBonus > 1)
            {
                ImGui.TableNextColumn();
                ImGui.TextDisabled("  Fire");
                ImGui.TableNextColumn();
                ImGui.TextColored(ColorWarm, $"+{breakdown.FireBonus:F0}°F");
            }

            // Clothing Warmth row
            ImGui.TableNextColumn();
            ImGui.Text("Clothing Warmth");
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ColorWarning);
            ImGui.ProgressBar(warmthPct / 100f, new Vector2(-1, 18), $"{warmthPct}%");
            ImGui.PopStyleColor();

            ImGui.EndTable();
        }
    }

    private static void RenderBodyCondition(GameContext ctx, Body body)
    {
        var capacities = ctx.player.GetCapacities();
        bool hasBloodIssue = body.Blood.Condition < 0.95;
        bool hasCapacityIssues = capacities.Moving < 0.9 || capacities.Manipulation < 0.9 || capacities.Consciousness < 0.9;
        var injuredParts = body.Parts.Where(p => p.Condition < 1.0).OrderBy(p => p.Condition).ToList();
        bool hasInjuries = injuredParts.Count > 0;

        if (!hasBloodIssue && !hasCapacityIssues && !hasInjuries) return;

        ImGui.Separator();

        // Injuries section - show damaged body parts and blood loss
        if (hasInjuries || hasBloodIssue)
        {
            ImGui.TextColored(ColorHeader, "Injuries");

            if (ImGui.BeginTable("injuries", 3))
            {
                ImGui.TableSetupColumn("Part", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Bar", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Severity", ImGuiTableColumnFlags.WidthFixed, 60);

                foreach (var part in injuredParts)
                {
                    RenderInjuryRow(part.Name, part.Condition);
                }

                // Blood loss display - shown with injuries since bleeding is injury-related
                if (hasBloodIssue)
                {
                    int bloodPct = (int)(body.Blood.Condition * 100);
                    ImGui.TableNextColumn();
                    ImGui.Text("  Blood");
                    ImGui.TableNextColumn();
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, GetCapacityColor(body.Blood.Condition));
                    ImGui.ProgressBar((float)body.Blood.Condition, new Vector2(-1, OverlaySizes.CompactBarHeight), $"{bloodPct}%");
                    ImGui.PopStyleColor();
                    ImGui.TableNextColumn();
                    ImGui.Text("");  // Empty third column to match table format
                }

                ImGui.EndTable();
            }
        }

        // Capacities section
        if (hasCapacityIssues)
        {
            if (hasInjuries || hasBloodIssue)
                ImGui.Spacing();
            ImGui.TextColored(ColorHeader, "Capacities");

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
        ImGui.Text($"  {partName}");

        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar((float)damage, new Vector2(-1, OverlaySizes.CompactBarHeight), $"{damagePct}%");
        ImGui.PopStyleColor();

        ImGui.TableNextColumn();
        ImGui.TextColored(color, severity);
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
        var effects = ctx.player.EffectRegistry.GetAll().Take(6).ToList();
        if (effects.Count == 0) return;

        ImGui.Separator();
        ImGui.TextColored(ColorHeader, "Active Effects");

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
                    ImGui.TextColored(ColorHeader, effect.EffectKind);
                    ImGui.Separator();

                    // Show capacity modifiers
                    var modifiers = effect.CapacityModifiers.ToDictionary();
                    bool hasModifiers = false;
                    foreach (var (capacity, modifier) in modifiers)
                    {
                        if (Math.Abs(modifier) > 0.01)
                        {
                            hasModifiers = true;
                            int pctChange = (int)((modifier - 1.0) * 100);
                            Vector4 modColor = pctChange >= 0 ? ColorGood : ColorDanger;
                            ImGui.TextColored(modColor, $"{capacity}: {pctChange:+0;-0}%%");
                        }
                    }

                    // Show damage over time if present
                    if (effect.Damage != null)
                    {
                        hasModifiers = true;
                        ImGui.TextColored(ColorDanger, $"Damage: {effect.Damage.PerHour:F0}/hr ({effect.Damage.Type})");
                    }

                    // Show if treatment required
                    if (effect.RequiresTreatment)
                    {
                        hasModifiers = true;
                        ImGui.TextColored(ColorWarning, "Requires treatment");
                    }

                    if (!hasModifiers)
                    {
                        ImGui.TextDisabled("No direct capacity effects");
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
        ImGui.TextColored(ColorHeader, "Threats");

        foreach (var tension in tensions)
        {
            Vector4 color = tension.Severity > 0.7 ? ColorDanger :
                           tension.Severity > 0.4 ? ColorWarning : ColorMuted;
            ImGui.TextColored(color, $"  {tension.Type}");
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
            ImGui.Text("Carry");
            ImGui.TableNextColumn();
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, weightColor);
            ImGui.ProgressBar((float)pct, new Vector2(-1, 18), $"{current:F1}/{max:F1} kg");
            ImGui.PopStyleColor();

            // Fuel row (if any)
            if (fuelKg > 0)
            {
                ImGui.TableNextColumn();
                ImGui.Text("Fuel");
                ImGui.TableNextColumn();
                ImGui.Text($"{fuelKg:F1} kg");
            }

            ImGui.EndTable();
        }
    }

    private static void RenderFireStatus(Environments.Location location)
    {
        var fire = location.GetFeature<HeatSourceFeature>();
        if (fire == null || (!fire.IsActive && !fire.HasEmbers)) return;

        ImGui.Separator();

        string phase;
        int minutes;

        if (fire.HasEmbers)
        {
            phase = "Embers";
            minutes = (int)(fire.EmberTimeRemaining * 60);
        }
        else
        {
            phase = fire.GetFirePhase();
            minutes = fire.UnburnedMassKg > 0.1
                ? (int)(fire.TotalHoursRemaining * 60)
                : (int)(fire.BurningHoursRemaining * 60);
        }

        Vector4 fireColor = minutes <= 5 ? ColorCritical :
                           minutes <= 15 ? ColorDanger :
                           minutes <= 30 ? ColorWarning : ColorWarm;

        ImGui.TextColored(ColorHeader, "Fire");
        ImGui.SameLine();
        ImGui.TextColored(fireColor, $"{phase} ({FormatTime(minutes)})");
    }

    private static void RenderCampInfo(GameContext ctx)
    {
        // Only show when at camp
        if (ctx.Camp == null || ctx.CurrentLocation != ctx.Camp) return;

        ImGui.Separator();
        ImGui.TextColored(ColorHeader, "Camp");

        // Shelter info
        var shelter = ctx.Camp.GetFeature<ShelterFeature>();
        if (shelter != null)
        {
            int insulation = (int)Math.Round(shelter.TemperatureInsulation * 100);
            int wind = (int)Math.Round(shelter.WindCoverage * 100);
            int overhead = (int)Math.Round(shelter.OverheadCoverage * 100);
            ImGui.Text($"  Shelter: {insulation}%% insulation");
            ImGui.TextDisabled($"    Wind: {wind}%% | Overhead: {overhead}%%");
        }

        // Bedding info
        var bedding = ctx.Camp.GetFeature<BeddingFeature>();
        if (bedding != null)
        {
            ImGui.Text($"  Bedding: {bedding.Quality} quality");
        }

        // Storage summary
        var cache = ctx.Camp.GetFeature<CacheFeature>();
        if (cache != null && cache.Storage.CurrentWeightKg > 0)
        {
            ImGui.Text($"  Storage: {cache.Storage.CurrentWeightKg:F1} kg stored");
        }

        // Curing rack status
        var rack = ctx.Camp.GetFeature<CuringRackFeature>();
        if (rack != null && rack.ItemCount > 0)
        {
            if (rack.HasReadyItems)
            {
                ImGui.TextColored(ColorGood, $"  Curing Rack: items ready!");
            }
            else
            {
                ImGui.Text($"  Curing Rack: {rack.ItemCount} curing");
            }
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
}
