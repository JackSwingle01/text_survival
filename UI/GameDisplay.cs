using Spectre.Console;
using Spectre.Console.Rendering;
using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Survival;

namespace text_survival.UI;

public static class GameDisplay
{
    private static readonly NarrativeLog _log = new();

    public static void AddNarrative(string text, LogLevel level = LogLevel.Normal)
    {
        _log.Add(text, level);
    }

    public static void AddNarrative(IEnumerable<string> texts)
    {
        foreach (var text in texts)
            AddNarrative(text);
    }

    public static void AddSuccess(string text) => AddNarrative(text, LogLevel.Success);
    public static void AddWarning(string text) => AddNarrative(text, LogLevel.Warning);
    public static void AddDanger(string text) => AddNarrative(text, LogLevel.Danger);
    public static void AddSeparator() => AddNarrative("", LogLevel.Normal);

    public static void ClearNarrative() => _log.Clear();

    public static void Render(GameContext ctx, bool addSeparator = true)
    {
        if (Output.TestMode)
        {
            RenderTestMode(ctx);
            return;
        }

        if (addSeparator)
            _log.Add("· · ·", LogLevel.System);

        AnsiConsole.Clear();

        // Build the 4-panel grid layout
        var topRow = new Columns(
            BuildSurvivalPanel(ctx),
            BuildEnvironmentPanel(ctx)
        ).Expand();

        var bottomRow = new Columns(
            BuildBodyPanel(ctx),
            BuildFirePanel(ctx),
            BuildInventoryPanel(ctx)
        ).Expand();

        AnsiConsole.Write(topRow);
        AnsiConsole.Write(bottomRow);
        AnsiConsole.Write(BuildNarrativePanel(ctx));
    }

    #region Panel Builders

    private static IRenderable BuildSurvivalPanel(GameContext ctx)
    {
        var body = ctx.player.Body;

        int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
        int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);

        var lines = new List<IRenderable>
        {
            new Markup($"Food   {CreateColoredBar(caloriesPercent, 12, GetFoodColor(caloriesPercent))} [white]{GetCaloriesStatus(caloriesPercent)}[/]"),
            new Markup($"Water  {CreateColoredBar(hydrationPercent, 12, GetWaterColor(hydrationPercent))} [white]{GetHydrationStatus(hydrationPercent)}[/]"),
            new Markup($"Energy {CreateColoredBar(energyPercent, 12, GetEnergyColor(energyPercent))} [white]{GetEnergyStatus(energyPercent)}[/]"),
            new Markup($"Temp   [white]{body.BodyTemperature:F1}°F[/] [{GetTempColor(body.BodyTemperature)}]{GetTemperatureStatus(body.BodyTemperature)}[/]")
        };

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" SURVIVAL ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
            Expand = true
        };
    }

    private static IRenderable BuildEnvironmentPanel(GameContext ctx)
    {
        var location = ctx.CurrentLocation;
        double ambientTemp = location.GetTemperature();

        // Time display - Day X — HH:MM AM/PM (TimeOfDay)
        var startDate = new DateTime(2025, 1, 1);
        int dayNumber = (ctx.GameTime - startDate).Days + 1;
        string clockTime = ctx.GameTime.ToString("h:mm tt");
        string timeOfDay = ctx.GetTimeOfDay().ToString();

        var lines = new List<IRenderable>
        {
            new Markup($"[yellow bold]Day {dayNumber}[/] — [white]{clockTime}[/] [grey]({timeOfDay})[/]"),
            new Markup($"[white bold]{Markup.Escape(location.Name)}[/] — [grey]{ambientTemp:F0}°F[/]"),
            new Markup($"[grey italic]{Markup.Escape(GetShortDescription(location))}[/]")
        };

        // Show available features (foraging, harvestables)
        string gatherSummary = location.GetGatherSummary();
        if (!string.IsNullOrEmpty(gatherSummary))
        {
            lines.Add(new Markup($"[green]{Markup.Escape(gatherSummary)}[/]"));
        }

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" ENVIRONMENT ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
            Expand = true
        };
    }

    private static IRenderable BuildBodyPanel(GameContext ctx)
    {
        var body = ctx.player.Body;
        var effects = ctx.player.EffectRegistry.GetAll();
        var damagedParts = body.Parts.Where(p => p.Condition < 1.0).ToList();

        var lines = new List<IRenderable>();

        // Injuries
        if (damagedParts.Count == 0)
        {
            lines.Add(new Markup("[grey]No injuries[/]"));
        }
        else
        {
            foreach (var part in damagedParts.Take(3)) // Limit to 3 to avoid overflow
            {
                string severity = GetInjurySeverity(part.Condition);
                string color = GetInjuryColor(part.Condition);
                lines.Add(new Markup($"[{color}]{Markup.Escape(part.Name)} — {severity}[/]"));
            }
            if (damagedParts.Count > 3)
                lines.Add(new Markup($"[grey]+{damagedParts.Count - 3} more...[/]"));
        }

        // Effects
        if (effects.Count > 0)
        {
            lines.Add(new Text("")); // Spacer
            foreach (var effect in effects.Take(3)) // Limit to 3
            {
                string trend = GetEffectTrend(effect);
                string color = GetEffectColor(effect);
                lines.Add(new Markup($"[{color}]• {Markup.Escape(effect.EffectKind)} {trend}[/]"));
            }
            if (effects.Count > 3)
                lines.Add(new Markup($"[grey]+{effects.Count - 3} more...[/]"));
        }

        // Pad to minimum height for consistent layout
        while (lines.Count < 4)
            lines.Add(new Text(""));

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" BODY ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
            Expand = true
        };
    }

    private static IRenderable BuildFirePanel(GameContext ctx)
    {
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        var lines = new List<IRenderable>();

        if (fire == null || (!fire.IsActive && !fire.HasEmbers))
        {
            lines.Add(new Markup("[grey]No fire[/]"));
            lines.Add(new Text(""));
            lines.Add(new Text(""));
            lines.Add(new Text(""));
        }
        else
        {
            string phase = fire.GetFirePhase();
            int minutes = (int)(fire.HoursRemaining * 60);
            string timeColor = GetFireTimeColor(minutes);
            string phaseColor = GetFirePhaseColor(phase);

            lines.Add(new Markup($"[{phaseColor}]{phase}[/]"));
            lines.Add(new Markup($"[{timeColor}]{minutes} min remaining[/]"));
            lines.Add(new Markup($"[grey]+{fire.GetEffectiveHeatOutput(ctx.CurrentLocation.GetTemperature()):F0}°F heat[/]"));
            lines.Add(new Text(""));
        }

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" FIRE ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
            Expand = true
        };
    }

    private static IRenderable BuildInventoryPanel(GameContext ctx)
    {
        var inv = ctx.Inventory;
        var lines = new List<IRenderable>();

        // Weight bar
        double weightPercent = inv.MaxWeightKg > 0 ? inv.CurrentWeightKg / inv.MaxWeightKg * 100 : 0;
        string weightColor = GetWeightColor(weightPercent);
        lines.Add(new Markup($"{CreateColoredBar((int)weightPercent, 12, weightColor)} [{weightColor}]{inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0}kg[/]"));

        // Category breakdown
        var parts = new List<string>();
        if (inv.FuelWeightKg > 0) parts.Add($"[orange1]Fuel {inv.FuelWeightKg:F1}[/]");
        if (inv.FoodWeightKg > 0) parts.Add($"[green]Food {inv.FoodWeightKg:F1}[/]");
        if (inv.WaterWeightKg > 0) parts.Add($"[blue]Water {inv.WaterWeightKg:F1}[/]");
        if (inv.ToolsWeightKg > 0) parts.Add($"[grey]Tools {inv.ToolsWeightKg:F1}[/]");

        if (parts.Count > 0)
            lines.Add(new Markup(string.Join(" ", parts)));
        else
            lines.Add(new Markup("[grey]Empty[/]"));

        // Pad to match other panels
        while (lines.Count < 4)
            lines.Add(new Text(""));

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" INVENTORY ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
            Expand = true
        };
    }

    private static IRenderable BuildNarrativePanel(GameContext ctx)
    {
        var entries = _log.GetVisible();
        var lines = new List<IRenderable>();

        foreach (var (text, level) in entries)
        {
            string color = GetLogColor(level);
            lines.Add(new Markup($"[{color}]{Markup.Escape(text)}[/]"));
        }

        // Pad to fixed height
        int padding = NarrativeLog.MAX_VISIBLE_LINES - entries.Count;
        for (int i = 0; i < padding; i++)
            lines.Add(new Text(""));

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" NARRATIVE ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
            Expand = true
        };
    }

    #endregion

    #region Color Helpers

    private static string CreateColoredBar(int percent, int width, string color)
    {
        int filled = Math.Clamp(percent * width / 100, 0, width);
        int empty = width - filled;
        return $"[{color}]{new string('█', filled)}[/][grey]{new string('░', empty)}[/]";
    }

    private static string GetLogColor(LogLevel level) => level switch
    {
        LogLevel.Success => "green",
        LogLevel.Warning => "yellow",
        LogLevel.Danger => "red",
        LogLevel.System => "grey",
        _ => "white"
    };

    private static string GetFoodColor(int percent) => percent switch
    {
        >= 60 => "green",
        >= 30 => "yellow",
        _ => "red"
    };

    private static string GetWaterColor(int percent) => percent switch
    {
        >= 60 => "blue",
        >= 30 => "yellow",
        _ => "red"
    };

    private static string GetEnergyColor(int percent) => percent switch
    {
        >= 60 => "cyan",
        >= 30 => "yellow",
        _ => "red"
    };

    private static string GetTempColor(double temp) => temp switch
    {
        >= 100 => "red",
        >= 99 => "yellow",
        >= 97 => "green",
        >= 95 => "cyan",
        _ => "blue"
    };

    private static string GetFirePhaseColor(string phase) => phase switch
    {
        "Roaring" => "red",
        "Building" or "Steady" => "yellow",
        "Igniting" or "Dying" => "olive",
        "Embers" => "maroon",
        _ => "grey"
    };

    private static string GetFireTimeColor(int minutes) => minutes switch
    {
        >= 30 => "green",
        >= 15 => "yellow",
        _ => "red"
    };

    private static string GetWeightColor(double percent) => percent switch
    {
        >= 90 => "red",
        >= 70 => "yellow",
        _ => "green"
    };

    private static string GetInjuryColor(double condition) => condition switch
    {
        <= 0.2 => "red",
        <= 0.5 => "yellow",
        _ => "white"
    };

    private static string GetEffectColor(Effect effect)
    {
        if (effect.HourlySeverityChange > 0) return "red";      // Worsening
        if (effect.HourlySeverityChange < 0) return "green";    // Improving
        return "grey";                                           // Stable
    }

    #endregion

    #region Status Text Helpers

    private static string GetCaloriesStatus(int percent) => percent switch
    {
        >= 80 => "Well Fed",
        >= 60 => "Satisfied",
        >= 40 => "Peckish",
        >= 20 => "Hungry",
        _ => "Starving"
    };

    private static string GetHydrationStatus(int percent) => percent switch
    {
        >= 80 => "Hydrated",
        >= 60 => "Fine",
        >= 40 => "Thirsty",
        >= 20 => "Parched",
        _ => "Dehydrated"
    };

    private static string GetEnergyStatus(int percent) => percent switch
    {
        >= 90 => "Energized",
        >= 80 => "Alert",
        >= 40 => "Normal",
        >= 30 => "Tired",
        >= 20 => "Very Tired",
        _ => "Exhausted"
    };

    private static string GetTemperatureStatus(double temp) => temp switch
    {
        >= 100 => "Feverish",
        >= 99 => "Hot",
        >= 97 => "Normal",
        >= 95 => "Cool",
        _ => "Cold"
    };

    private static string GetInjurySeverity(double condition) => condition switch
    {
        <= 0 => "Destroyed",
        <= 0.2 => "Critical",
        <= 0.4 => "Severe",
        <= 0.6 => "Moderate",
        <= 0.8 => "Light",
        _ => "Minor"
    };

    private static string GetEffectTrend(Effect effect)
    {
        if (effect.HourlySeverityChange > 0) return "(↑)";
        if (effect.HourlySeverityChange < 0) return "(↓)";
        return "";
    }

    private static string GetShortDescription(Environments.Location location)
    {
        // Get environment feature for terrain description
        var env = location.GetFeature<EnvironmentFeature>();
        if (env != null)
        {
            return env.GetDescription();
        }

        // Fallback to terrain type
        return location.Terrain switch
        {
            TerrainType.Rough => "Rough, uneven ground",
            TerrainType.Snow => "Snow-covered terrain",
            TerrainType.Steep => "Steep incline",
            TerrainType.Water => "Near water",
            TerrainType.Hazardous => "Dangerous area",
            _ => "Open ground"
        };
    }

    #endregion

    #region Test Mode

    private static void RenderTestMode(GameContext ctx)
    {
        var body = ctx.player.Body;

        int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
        int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);

        TestModeIO.WriteOutput($"[Status: Food {caloriesPercent}%, Water {hydrationPercent}%, Energy {energyPercent}%, Temp {body.BodyTemperature:F1}°F]\n");

        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire != null && (fire.IsActive || fire.HasEmbers))
        {
            int minutes = (int)(fire.HoursRemaining * 60);
            TestModeIO.WriteOutput($"[Fire: {fire.GetFirePhase()} - {minutes} min]\n");
        }

        // Add body/effects info to test mode
        var effects = ctx.player.EffectRegistry.GetAll();
        if (effects.Count > 0)
        {
            var effectNames = effects.Select(e => e.EffectKind);
            TestModeIO.WriteOutput($"[Effects: {string.Join(", ", effectNames)}]\n");
        }

        foreach (var (text, _) in _log.GetVisible())
            TestModeIO.WriteOutput(text + "\n");
    }

    #endregion
}
