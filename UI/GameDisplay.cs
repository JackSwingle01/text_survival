using Spectre.Console;
using Spectre.Console.Rendering;
using text_survival.Actions;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
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
            _log.AddSeparator();

        AnsiConsole.Clear();

        // Build the 6-panel grid layout
        var topRow = new Columns(
            BuildEnvironmentPanel(ctx),
            BuildTemperaturePanel(ctx),
            BuildSurvivalPanel(ctx)
        ).Expand();

        var bottomRow = new Columns(
            BuildFirePanel(ctx),
            BuildBodyPanel(ctx),
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
        int healthPercent = (int)(ctx.player.Vitality * 100);

        var lines = new List<IRenderable>
        {
            new Markup($"Health {CreateColoredBar(healthPercent, 12, GetCapacityColor(healthPercent))} [white]{GetHealthStatus(healthPercent)}[/]"),
            new Markup($"Food   {CreateColoredBar(caloriesPercent, 12, GetFoodColor(caloriesPercent))} [white]{GetCaloriesStatus(caloriesPercent)}[/]"),
            new Markup($"Water  {CreateColoredBar(hydrationPercent, 12, GetWaterColor(hydrationPercent))} [white]{GetHydrationStatus(hydrationPercent)}[/]"),
            new Markup($"Energy {CreateColoredBar(energyPercent, 12, GetEnergyColor(energyPercent))} [white]{GetEnergyStatus(energyPercent)}[/]")
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

    private static IRenderable BuildTemperaturePanel(GameContext ctx)
    {
        var body = ctx.player.Body;
        var location = ctx.CurrentLocation;
        var fire = location.GetFeature<HeatSourceFeature>();

        double bodyTemp = body.BodyTemperature;
        double zoneTemp = location.Parent.Weather.TemperatureInFahrenheit;
        double locationTemp = location.GetTemperature(); // Includes all modifiers + fire
        double fireHeat = fire?.GetEffectiveHeatOutput(zoneTemp) ?? 0;

        // Line 2: Ambient breakdown
        string ambientLine;
        if (fireHeat > 0.5)
        {
            double baseTemp = locationTemp - fireHeat;
            ambientLine = $"Air   [grey]{baseTemp:F0}°F[/] + [yellow]Fire {fireHeat:F0}°F[/] = [white]{locationTemp:F0}°F felt[/]";
        }
        else
        {
            ambientLine = $"Air   [grey]{locationTemp:F0}°F[/]";
        }

        // Line 3: Trend (uses actual calculated delta from survival processor)
        var (arrow, trendDesc, trendColor) = GetHeatTrend(
            bodyTemp,
            ctx.player.LastSurvivalDelta?.TemperatureDelta,
            ctx.player.LastUpdateMinutes);

        var lines = new List<IRenderable>
        {
            new Markup($"Body  {CreateTemperatureBar(bodyTemp)} [white]{bodyTemp:F1}°F[/] [{GetTempColor(bodyTemp)}]{GetTemperatureStatus(bodyTemp)}[/]"),
            new Markup(ambientLine),
            new Markup($"Trend [{trendColor}]{arrow} {trendDesc}[/]"),
            new Text("") // Padding
        };

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" TEMPERATURE ", Justify.Left),
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
                int severityPercent = (int)(effect.Severity * 100);
                lines.Add(new Markup($"[{color}]• {Markup.Escape(effect.EffectKind)} {severityPercent}% {trend}[/]"));
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

        if (fire == null)
        {
            lines.Add(new Markup("[grey]No fire pit[/]"));
            lines.Add(new Text(""));
            lines.Add(new Text(""));
            lines.Add(new Text(""));
        }
        else if (!fire.IsActive && !fire.HasEmbers)
        {
            // Fire pit exists but not burning
            lines.Add(new Markup("[grey]Cold[/]"));

            if (fire.TotalMassKg > 0)
            {
                int litPercent = (int)(fire.BurningMassKg / fire.TotalMassKg * 100);
                lines.Add(new Markup($"[grey]{fire.TotalMassKg:F1}kg fuel[/] [white]({litPercent}% lit)[/]"));
            }
            else
            {
                lines.Add(new Markup("[grey]No fuel[/]"));
            }
            lines.Add(new Text(""));
            lines.Add(new Text(""));
        }
        else
        {
            string phase = fire.GetFirePhase();

            // Use total fuel time when catching, otherwise burning time
            int minutes;
            if (fire.HasEmbers)
            {
                minutes = (int)(fire.EmberTimeRemaining * 60);
            }
            else if (fire.UnburnedMassKg > 0.1)
            {
                // Fuel is catching - show total estimated time
                minutes = (int)(fire.TotalHoursRemaining * 60);
            }
            else
            {
                minutes = (int)(fire.BurningHoursRemaining * 60);
            }

            string timeColor = GetFireTimeColor(minutes);
            string phaseColor = GetFirePhaseColor(phase);

            lines.Add(new Markup($"[{phaseColor}]{phase}[/]"));
            lines.Add(new Markup($"[{timeColor}]{minutes} min remaining[/] [grey]({fire.EffectiveBurnRateKgPerHour:F1} kg/hr)[/]"));

            // Show fuel status with unlit indicator
            string fuelStatus;
            if (fire.UnburnedMassKg > 0.1)
            {
                fuelStatus = $"[yellow]{fire.BurningMassKg:F1}kg burning[/] [grey](+{fire.UnburnedMassKg:F1}kg unlit)[/]";
            }
            else
            {
                fuelStatus = $"[grey]{fire.TotalMassKg:F1}/{fire.MaxFuelCapacityKg:F0} kg fuel[/]";
            }
            lines.Add(new Markup(fuelStatus));

            lines.Add(new Markup($"[yellow]+{fire.GetEffectiveHeatOutput(ctx.CurrentLocation.GetTemperature()):F0}°F heat[/]"));
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

        // Breakdown chart
        var chart = new BreakdownChart().HideTags().Width(20);
        if (inv.FuelWeightKg > 0) chart.AddItem("Fuel", inv.FuelWeightKg, Color.Orange1);
        if (inv.FoodWeightKg > 0) chart.AddItem("Food", inv.FoodWeightKg, Color.Green);
        if (inv.WaterWeightKg > 0) chart.AddItem("Water", inv.WaterWeightKg, Color.Blue);
        if (inv.ToolsWeightKg > 0) chart.AddItem("Tools", inv.ToolsWeightKg, Color.Grey);
        if (inv.EquipmentWeightKg > 0) chart.AddItem("Gear", inv.EquipmentWeightKg, Color.Purple);
        if (inv.SpecialWeightKg > 0) chart.AddItem("Special", inv.SpecialWeightKg, Color.Yellow);
        if (inv.RemainingCapacityKg > 0 && inv.RemainingCapacityKg < double.MaxValue)
            chart.AddItem("Free", inv.RemainingCapacityKg, Color.Grey23);

        if (inv.CurrentWeightKg > 0 || inv.MaxWeightKg > 0)
            lines.Add(chart);

        // Category breakdown text
        var parts = new List<string>();
        if (inv.FuelWeightKg > 0) parts.Add($"[orange1]Fuel {inv.FuelWeightKg:F1}kg[/]");
        if (inv.FoodWeightKg > 0) parts.Add($"[green]Food {inv.FoodWeightKg:F1}kg[/]");
        if (inv.WaterWeightKg > 0) parts.Add($"[blue]Water {inv.WaterWeightKg:F1}kg[/]");
        if (inv.ToolsWeightKg > 0) parts.Add($"[grey]Tools {inv.ToolsWeightKg:F1}kg[/]");
        if (inv.EquipmentWeightKg > 0) parts.Add($"[purple]Gear {inv.EquipmentWeightKg:F1}kg[/]");
        if (inv.SpecialWeightKg > 0) parts.Add($"[yellow]Special {inv.SpecialWeightKg:F1}kg[/]");
        if (parts.Count == 0)
            parts.Add("[grey]Empty[/]");
        lines.Add(new Columns(parts));

        double weightPercent = inv.MaxWeightKg > 0 ? inv.CurrentWeightKg / inv.MaxWeightKg * 100 : 0;
        string weightColor = GetWeightColor(weightPercent);
        lines.Add(new Markup($"[{weightColor}]{inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0}kg[/]"));

        // Pad to 4 lines
        while (lines.Count < 4)
            lines.Add(new Text(""));

        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(" INVENTORY ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0),
            Expand = true,
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
            Header = new PanelHeader(" LOGS ", Justify.Left),
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

    private static string GetCapacityColor(int percent) => percent switch
    {
        >= 80 => "green",
        >= 50 => "yellow",
        >= 25 => "orange1",
        _ => "red"
    };

    private static string CreateTemperatureBar(double bodyTemp, int width = 12)
    {
        // Range: 87°F (death) to 102°F (severe hyperthermia)
        const double minTemp = 87.0;
        const double maxTemp = 102.0;

        double position = (bodyTemp - minTemp) / (maxTemp - minTemp);
        position = Math.Clamp(position, 0, 1);

        int filled = (int)(position * width);
        int empty = width - filled;

        string color = GetTempBarColor(bodyTemp);
        return $"[{color}]{new string('█', filled)}[/][grey]{new string('░', empty)}[/]";
    }

    private static string GetTempBarColor(double temp) => temp switch
    {
        < 89.6 => "red",      // Severe hypothermia
        < 95.0 => "blue",     // Hypothermia
        < 97.0 => "cyan",     // Cool/shivering
        < 99.0 => "green",    // Normal/safe
        < 100.0 => "yellow",  // Hot/sweating
        _ => "red"            // Hyperthermia
    };

    private static (string arrow, string description, string color) GetHeatTrend(
        double bodyTemp,
        double? temperatureDeltaTotal,
        int minutes)
    {
        // No data yet = stable
        if (temperatureDeltaTotal is null || minutes <= 0)
            return ("→", "Stable", "green");

        // Convert total change to per-hour rate
        double deltaPerHour = (temperatureDeltaTotal.Value / minutes) * 60;

        if (Math.Abs(deltaPerHour) < 0.05)
            return ("→", "Stable", "green");

        bool cooling = deltaPerHour < 0;
        string arrow = cooling ? "↓" : "↑";

        string speed = Math.Abs(deltaPerHour) switch
        {
            > 2.0 => "rapidly",
            > 1.0 => "steadily",
            > 0.3 => "slowly",
            _ => "very slowly"
        };

        string action = cooling ? "Cooling" : "Warming";
        string rate = $"({deltaPerHour:+0.0;-0.0}°/hr)";

        // Color based on whether trend is concerning
        string color = (cooling && bodyTemp < 97) ? "cyan"
                     : (!cooling && bodyTemp > 99) ? "yellow"
                     : "grey";

        return (arrow, $"{action} {speed} {rate}", color);
    }

    #endregion

    #region Status Text Helpers

    private static string GetHealthStatus(int percent) => percent switch
    {
        >= 90 => "Healthy",
        >= 70 => "Fine",
        >= 50 => "Hurt",
        >= 25 => "Wounded",
        _ => "Critical"
    };

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
        return location.Description;
    }

    #endregion

    #region Inventory Screen

    public static void RenderInventoryScreen(GameContext ctx)
    {
        if (Output.TestMode)
        {
            RenderInventoryTestMode(ctx);
            return;
        }

        AnsiConsole.Clear();

        var inv = ctx.Inventory;

        // Build left column: Equipment + Tools
        var leftLines = new List<IRenderable>
        {
            new Markup("[bold underline]EQUIPPED[/]"),
            new Text("")
        };

        // Weapon
        if (inv.Weapon != null)
            leftLines.Add(new Markup($"[white]Weapon:[/] [yellow]{Markup.Escape(inv.Weapon.Name)}[/] [grey]({inv.Weapon.Damage:F0} dmg)[/]"));
        else
            leftLines.Add(new Markup("[white]Weapon:[/] [grey]—[/]"));

        // Armor slots
        AddEquipmentLine(leftLines, "Head", inv.Head);
        AddEquipmentLine(leftLines, "Chest", inv.Chest);
        AddEquipmentLine(leftLines, "Legs", inv.Legs);
        AddEquipmentLine(leftLines, "Feet", inv.Feet);
        AddEquipmentLine(leftLines, "Hands", inv.Hands);

        leftLines.Add(new Text(""));
        leftLines.Add(new Markup($"[cyan]Total Insulation:[/] [white]{inv.TotalInsulation * 100:F0}%[/]"));

        // Tools section
        leftLines.Add(new Text(""));
        leftLines.Add(new Markup("[bold underline]TOOLS[/]"));
        leftLines.Add(new Text(""));

        if (inv.Tools.Count == 0)
        {
            leftLines.Add(new Markup("[grey]No tools[/]"));
        }
        else
        {
            foreach (var tool in inv.Tools)
            {
                string weaponInfo = tool.IsWeapon ? $" [grey]({tool.Damage:F0} dmg)[/]" : "";
                leftLines.Add(new Markup($"[white]{Markup.Escape(tool.Name)}[/] [grey]({tool.Weight:F1}kg)[/]{weaponInfo}"));
            }
        }

        // Special items
        if (inv.Special.Count > 0)
        {
            leftLines.Add(new Text(""));
            leftLines.Add(new Markup("[bold underline]SPECIAL[/]"));
            leftLines.Add(new Text(""));
            foreach (var item in inv.Special)
            {
                leftLines.Add(new Markup($"[magenta]{Markup.Escape(item.Name)}[/] [grey]({item.Weight:F1}kg)[/]"));
            }
        }

        // Build right column: Resources
        var rightLines = new List<IRenderable>
        {
            new Markup("[bold underline]RESOURCES[/]"),
            new Text("")
        };

        // Fuel
        rightLines.Add(new Markup("[orange1 bold]Fuel[/]"));
        if (inv.LogCount > 0)
            rightLines.Add(new Markup($"  [white]{inv.LogCount} logs[/] [grey]({inv.Logs.Sum():F1}kg)[/]"));
        if (inv.StickCount > 0)
            rightLines.Add(new Markup($"  [white]{inv.StickCount} sticks[/] [grey]({inv.Sticks.Sum():F1}kg)[/]"));
        if (inv.TinderCount > 0)
            rightLines.Add(new Markup($"  [white]{inv.TinderCount} tinder[/] [grey]({inv.Tinder.Sum():F2}kg)[/]"));
        if (!inv.HasFuel && inv.TinderCount == 0)
            rightLines.Add(new Markup("  [grey]None[/]"));
        else
            rightLines.Add(new Markup($"  [grey italic]~{inv.TotalFuelBurnTimeHours:F1} hrs burn time[/]"));

        rightLines.Add(new Text(""));

        // Food
        rightLines.Add(new Markup("[green bold]Food[/]"));
        if (inv.CookedMeatCount > 0)
            rightLines.Add(new Markup($"  [white]{inv.CookedMeatCount} cooked meat[/] [grey]({inv.CookedMeat.Sum():F1}kg)[/]"));
        if (inv.RawMeatCount > 0)
            rightLines.Add(new Markup($"  [yellow]{inv.RawMeatCount} raw meat[/] [grey]({inv.RawMeat.Sum():F1}kg)[/]"));
        if (inv.BerryCount > 0)
            rightLines.Add(new Markup($"  [white]{inv.BerryCount} berries[/] [grey]({inv.Berries.Sum():F2}kg)[/]"));
        if (!inv.HasFood)
            rightLines.Add(new Markup("  [grey]None[/]"));

        rightLines.Add(new Text(""));

        // Water
        rightLines.Add(new Markup("[blue bold]Water[/]"));
        if (inv.HasWater)
            rightLines.Add(new Markup($"  [white]{inv.WaterLiters:F1}L[/]"));
        else
            rightLines.Add(new Markup("  [grey]None[/]"));

        // Pad columns to same height
        int maxLines = Math.Max(leftLines.Count, rightLines.Count);
        while (leftLines.Count < maxLines) leftLines.Add(new Text(""));
        while (rightLines.Count < maxLines) rightLines.Add(new Text(""));

        // Build layout
        var leftPanel = new Panel(new Rows(leftLines))
        {
            Border = BoxBorder.None,
            Padding = new Padding(1, 0, 2, 0)
        };

        var rightPanel = new Panel(new Rows(rightLines))
        {
            Border = BoxBorder.None,
            Padding = new Padding(2, 0, 1, 0)
        };

        var columns = new Columns(leftPanel, rightPanel).Expand();

        // Weight summary
        double weightPercent = inv.MaxWeightKg > 0 ? inv.CurrentWeightKg / inv.MaxWeightKg * 100 : 0;
        string weightColor = GetWeightColor(weightPercent);
        var weightLine = new Markup($"\n[{weightColor}]Weight: {inv.CurrentWeightKg:F1} / {inv.MaxWeightKg:F0} kg[/]");

        var content = new Rows(columns, weightLine);

        var mainPanel = new Panel(content)
        {
            Header = new PanelHeader(" INVENTORY ", Justify.Center),
            Border = BoxBorder.Double,
            Padding = new Padding(1, 1, 1, 1),
            Expand = true
        };

        AnsiConsole.Write(mainPanel);
    }

    private static void AddEquipmentLine(List<IRenderable> lines, string slot, Equipment? equipment)
    {
        if (equipment != null)
            lines.Add(new Markup($"[white]{slot}:[/] [cyan]{Markup.Escape(equipment.Name)}[/] [grey](+{equipment.Insulation * 100:F0}%)[/]"));
        else
            lines.Add(new Markup($"[white]{slot}:[/] [grey]—[/]"));
    }

    private static void RenderInventoryTestMode(GameContext ctx)
    {
        var inv = ctx.Inventory;
        TestModeIO.WriteOutput($"[Inventory: {inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0}kg]\n");

        if (inv.Weapon != null)
            TestModeIO.WriteOutput($"  Weapon: {inv.Weapon.Name}\n");

        if (inv.LogCount > 0) TestModeIO.WriteOutput($"  Logs: {inv.LogCount}\n");
        if (inv.StickCount > 0) TestModeIO.WriteOutput($"  Sticks: {inv.StickCount}\n");
        if (inv.TinderCount > 0) TestModeIO.WriteOutput($"  Tinder: {inv.TinderCount}\n");
        if (inv.HasFood) TestModeIO.WriteOutput($"  Food: {inv.FoodWeightKg:F1}kg\n");
        if (inv.HasWater) TestModeIO.WriteOutput($"  Water: {inv.WaterLiters:F1}L\n");
    }

    #endregion

    #region Test Mode

    private static void RenderTestMode(GameContext ctx)
    {
        var body = ctx.player.Body;
        var location = ctx.CurrentLocation;

        int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
        int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);

        TestModeIO.WriteOutput($"[Status: Food {caloriesPercent}%, Water {hydrationPercent}%, Energy {energyPercent}%]\n");

        // Temperature detail
        var fire = location.GetFeature<HeatSourceFeature>();
        double zoneTemp = location.Parent.Weather.TemperatureInFahrenheit;
        double locationTemp = location.GetTemperature();
        double fireHeat = fire?.GetEffectiveHeatOutput(zoneTemp) ?? 0;
        TestModeIO.WriteOutput($"[Temp: Body {body.BodyTemperature:F1}°F, Air {locationTemp:F0}°F, Fire +{fireHeat:F0}°F]\n");

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
