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

        // Weather info
        string weatherLabel = weather.GetConditionLabel();
        string windLabel = weather.GetWindLabel();
        ImGui.TextDisabled($"{weather.GetSeasonLabel()} | {weatherLabel} | {windLabel}");
    }

    private static void RenderSurvivalStats(Body body)
    {
        // Calculate percentages
        int energyPct = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);
        int caloriesPct = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPct = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);

        RenderStatBar("Energy", energyPct, GetStatColor(energyPct));
        RenderStatBar("Food", caloriesPct, GetStatColor(caloriesPct));
        RenderStatBar("Water", hydrationPct, GetStatColor(hydrationPct));
    }

    private static void RenderStatBar(string label, int percent, Vector4 color)
    {
        percent = Math.Clamp(percent, 0, 100);

        ImGui.Text(label);
        ImGui.SameLine(70);

        // Draw progress bar
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(percent / 100f, new Vector2(140, 14), $"{percent}%");
        ImGui.PopStyleColor();
    }

    private static void RenderTemperature(GameContext ctx, Body body, Environments.Location location, Weather weather)
    {
        double bodyTemp = body.BodyTemperature;
        double airTemp = location.GetTemperature();

        // Calculate trend
        double trendPerHour = 0;
        if (ctx.player.LastSurvivalDelta?.TemperatureDelta is double delta && ctx.player.LastUpdateMinutes > 0)
        {
            trendPerHour = (delta / ctx.player.LastUpdateMinutes) * 60;
        }

        // Trend arrow and color
        string trendArrow = trendPerHour > 0.5 ? "^" : trendPerHour < -0.5 ? "v" : "-";
        Vector4 tempColor = bodyTemp < 95 ? ColorDanger : bodyTemp < 97 ? ColorWarning : ColorGood;

        ImGui.Text("Body Temp");
        ImGui.SameLine(70);
        ImGui.TextColored(tempColor, $"{bodyTemp:F1}F {trendArrow}");

        ImGui.Text("Air Temp");
        ImGui.SameLine(70);
        Vector4 airColor = airTemp < 20 ? ColorCold : airTemp < 40 ? ColorMuted : ColorWarm;
        ImGui.TextColored(airColor, $"{airTemp:F0}F");

        // Clothing warmth
        int warmthPct = (int)(body.ClothingHeatBufferPct * 100);
        if (warmthPct < 100)
        {
            ImGui.Text("Insulation");
            ImGui.SameLine(70);
            ImGui.TextColored(warmthPct < 30 ? ColorDanger : warmthPct < 60 ? ColorWarning : ColorMuted,
                $"{warmthPct}%");
        }
    }

    private static void RenderBodyCondition(GameContext ctx, Body body)
    {
        bool hasIssues = false;

        // Blood level (if low)
        if (body.Blood.Condition < 0.95)
        {
            if (!hasIssues) { ImGui.Separator(); hasIssues = true; }
            int bloodPct = (int)(body.Blood.Condition * 100);
            ImGui.TextColored(bloodPct < 50 ? ColorCritical : bloodPct < 70 ? ColorDanger : ColorWarning,
                $"Blood: {bloodPct}%");
        }

        // Impaired capacities
        var capacities = ctx.player.GetCapacities();
        if (capacities.Moving < 0.9)
        {
            if (!hasIssues) { ImGui.Separator(); hasIssues = true; }
            ImGui.TextColored(GetCapacityColor(capacities.Moving),
                $"Moving: {(int)(capacities.Moving * 100)}%");
        }
        if (capacities.Manipulation < 0.9)
        {
            if (!hasIssues) { ImGui.Separator(); hasIssues = true; }
            ImGui.TextColored(GetCapacityColor(capacities.Manipulation),
                $"Manipulation: {(int)(capacities.Manipulation * 100)}%");
        }
        if (capacities.Consciousness < 0.9)
        {
            if (!hasIssues) { ImGui.Separator(); hasIssues = true; }
            ImGui.TextColored(GetCapacityColor(capacities.Consciousness),
                $"Consciousness: {(int)(capacities.Consciousness * 100)}%");
        }
    }

    private static void RenderEffects(GameContext ctx)
    {
        var effects = ctx.player.EffectRegistry.GetAll().Take(6).ToList();
        if (effects.Count == 0) return;

        ImGui.Separator();
        ImGui.TextColored(ColorHeader, "Active Effects");

        foreach (var effect in effects)
        {
            int severity = (int)(effect.Severity * 100);
            string trend = effect.HourlySeverityChange > 0 ? "^" :
                          effect.HourlySeverityChange < 0 ? "v" : "";

            Vector4 color = GetEffectColor(effect);
            ImGui.TextColored(color, $"  {effect.EffectKind} {severity}% {trend}");
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

        // Weight
        double current = inv.CurrentWeightKg;
        double max = inv.MaxWeightKg;
        double pct = max > 0 ? current / max : 0;

        Vector4 weightColor = pct > 0.9 ? ColorDanger : pct > 0.7 ? ColorWarning : ColorMuted;

        ImGui.Text("Carry");
        ImGui.SameLine(70);
        ImGui.TextColored(weightColor, $"{current:F1}/{max:F1} kg");

        // Fuel summary
        double fuelKg = inv.GetWeight(ResourceCategory.Fuel);
        if (fuelKg > 0)
        {
            ImGui.Text("Fuel");
            ImGui.SameLine(70);
            ImGui.Text($"{fuelKg:F1} kg");
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
        ImGui.SameLine(70);
        ImGui.TextColored(fireColor, $"{phase} ({FormatTime(minutes)})");
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
        // Categorize by effect type
        string kind = effect.EffectKind.ToLowerInvariant();

        if (kind.Contains("bleed") || kind.Contains("hypother") || kind.Contains("frostbite"))
            return ColorDanger;
        if (kind.Contains("pain") || kind.Contains("hungry") || kind.Contains("thirst") || kind.Contains("tired"))
            return ColorWarning;
        if (kind.Contains("warm") || kind.Contains("rest") || kind.Contains("focus"))
            return ColorGood;

        return ColorMuted;
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
