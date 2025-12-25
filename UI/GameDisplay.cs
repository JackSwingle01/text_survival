using Spectre.Console;
using Spectre.Console.Rendering;
using text_survival.Actions;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.UI;

public static class GameDisplay
{
    private static readonly NarrativeLog _log = new();

    #region Context-aware overloads (route to WebIO when SessionId present)

    /// <summary>
    /// Add narrative with context - routes to instance log for web sessions.
    /// </summary>
    public static void AddNarrative(GameContext ctx, string text, LogLevel level = LogLevel.Normal)
    {
        if (ctx.SessionId != null)
            ctx.Log.Add(text, level);
        else
            _log.Add(text, level);
    }

    /// <summary>
    /// Add multiple narrative entries with context.
    /// </summary>
    public static void AddNarrative(GameContext ctx, IEnumerable<string> texts, LogLevel level = LogLevel.Normal)
    {
        if (ctx.SessionId != null)
            ctx.Log.AddRange(texts, level);
        else
        {
            foreach (var text in texts)
                _log.Add(text, level);
        }
    }

    public static void AddSuccess(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Success);
    public static void AddWarning(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Warning);
    public static void AddDanger(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Danger);

    public static void AddSeparator(GameContext ctx)
    {
        if (ctx.SessionId != null)
            ctx.Log.AddSeparator();
        else
            _log.AddSeparator();
    }

    public static void ClearNarrative(GameContext ctx)
    {
        if (ctx.SessionId != null)
            ctx.Log.Clear();
        else
            _log.Clear();
    }

    #endregion

    #region Static overloads (console-only, for backwards compatibility)

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


    #endregion

    /// <summary>
    /// Render the game display with optional status text.
    /// Status text style: laconic, character perspective (e.g. "Resting." "Planning." "Thinking.")
    /// </summary>
    public static void Render(
        GameContext ctx,
        bool addSeparator = true,
        string? statusText = null,
        int? progress = null,
        int? progressTotal = null)
    {
        // Route to web UI when session is active
        if (ctx.SessionId != null)
        {
            if (addSeparator)
                ctx.Log.AddSeparator();
            Web.WebIO.Render(ctx, statusText, progress, progressTotal);
            return;
        }

        // if (Output.TestMode)
        // {
        //     RenderTestMode(ctx);
        //     return;
        // }

        // if (addSeparator)
        //     _log.AddSeparator();

        // AnsiConsole.Clear();

        // // Build the 6-panel grid layout
        // // Top row: Environment info | Bottom row: Player info
        // var topRow = new Columns(
        //     BuildTimeWeatherPanel(ctx),
        //     BuildFirePanel(ctx),
        //     BuildLocationPanel(ctx)
        // ).Expand();

        // var bottomRow = new Columns(
        //     BuildTemperaturePanel(ctx),
        //     BuildSurvivalPanel(ctx),
        //     BuildInventoryPanel(ctx),
        //     BuildEffectsPanel(ctx),
        //     BuildInjuriesPanel(ctx)
        // ).Expand();

        // AnsiConsole.Write(topRow);
        // AnsiConsole.Write(bottomRow);
        // AnsiConsole.Write(BuildNarrativePanel(ctx));
        // AnsiConsole.Write(BuildStatusPanel(statusText, progress, progressTotal));
    }

    /// <summary>
    /// Render a progress loop with status panel updates. Updates game time by default.
    /// </summary>
    public static void UpdateAndRenderProgress(GameContext ctx, string statusText, int minutes, ActivityType activity, bool updateTime = true)
    {
        for (int i = 0; i < minutes; i++)
        {
            Render(ctx, addSeparator: false, statusText: statusText, progress: i, progressTotal: minutes);
            if (updateTime)
                ctx.Update(1, activity);
            Thread.Sleep(100);
        }
    }

    // private static IRenderable BuildStatusPanel(string? statusText, int? progress, int? progressTotal)
    // {
    //     string content;

    //     if (progress.HasValue && progressTotal.HasValue && progressTotal.Value > 0)
    //     {
    //         // Show progress bar with text - Spectre style
    //         int percent = progress.Value * 100 / progressTotal.Value;
    //         bool complete = progress.Value >= progressTotal.Value;
    //         string bar = CreateSpectreProgressBar(percent, 40, complete);
    //         string text = statusText ?? "Working";
    //         string color = complete ? "green" : "yellow";
    //         content = $"[{color}]{Markup.Escape(text)}[/] {bar} [white]{percent}%[/]";
    //     }
    //     else if (!string.IsNullOrEmpty(statusText))
    //     {
    //         // Just show status text
    //         content = $"[grey]{Markup.Escape(statusText)}[/]";
    //     }
    //     else
    //     {
    //         // Empty/idle state
    //         content = "[grey]—[/]";
    //     }

    //     return new Panel(new Markup(content))
    //     {
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static string CreateSpectreProgressBar(int percent, int width, bool complete)
    // {
    //     int filled = Math.Clamp(percent * width / 100, 0, width);
    //     int empty = width - filled;
    //     string color = complete ? "green" : "yellow";
    //     return $"[{color}]{new string('━', filled)}[/][grey]{new string('━', empty)}[/]";
    // }

    // #region Panel Builders

    // private static IRenderable BuildSurvivalPanel(GameContext ctx)
    // {
    //     var body = ctx.player.Body;

    //     int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
    //     int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
    //     int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);
    //     // Ceiling ensures 0.4% shows as 1% not 0% - player shouldn't see 0% while alive
    //     int healthPercent = ctx.player.IsAlive
    //         ? Math.Max(1, (int)Math.Ceiling(ctx.player.Vitality * 100))
    //         : 0;

    //     var lines = new List<IRenderable>
    //     {
    //         new Markup($"Health {CreateColoredBar(healthPercent, 10, GetCapacityColor(healthPercent))} [grey]({healthPercent}%)[/] [white]{GetHealthStatus(healthPercent)}[/]"),
    //         new Markup($"Food   {CreateColoredBar(caloriesPercent, 10, GetFoodColor(caloriesPercent))} [grey]({caloriesPercent}%)[/] [white]{GetCaloriesStatus(caloriesPercent)}[/]"),
    //         new Markup($"Water  {CreateColoredBar(hydrationPercent, 10, GetWaterColor(hydrationPercent))} [grey]({hydrationPercent}%)[/] [white]{GetHydrationStatus(hydrationPercent)}[/]"),
    //         new Markup($"Energy {CreateColoredBar(energyPercent, 10, GetEnergyColor(energyPercent))} [grey]({energyPercent}%)[/] [white]{GetEnergyStatus(energyPercent)}[/]")
    //     };

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" SURVIVAL ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static IRenderable BuildTimeWeatherPanel(GameContext ctx)
    // {
    //     var weather = ctx.CurrentLocation.ParentZone.Weather;
    //     var startDate = new DateTime(2025, 1, 1);
    //     int dayNumber = (ctx.GameTime - startDate).Days + 1;
    //     string clockTime = ctx.GameTime.ToString("h:mm tt");
    //     string timeOfDay = ctx.GetTimeOfDay().ToString();

    //     // Daylight info
    //     string daylightLine;
    //     if (weather.IsDaytime(ctx.GameTime))
    //     {
    //         double hoursUntilSunset = weather.GetHoursUntilSunset(ctx.GameTime);
    //         if (hoursUntilSunset < 1)
    //         {
    //             int minutes = (int)(hoursUntilSunset * 60);
    //             daylightLine = $"[yellow]{minutes} min til dusk[/]";
    //         }
    //         else
    //         {
    //             daylightLine = $"[grey]{hoursUntilSunset:F1} hrs til dusk[/]";
    //         }
    //     }
    //     else
    //     {
    //         double hoursUntilSunrise = weather.GetHoursUntilSunrise(ctx.GameTime);
    //         daylightLine = $"[blue]Night[/] — [grey]{hoursUntilSunrise:F1} hrs til dawn[/]";
    //     }

    //     // Weather condition
    //     string condition = weather.GetConditionLabel();
    //     string conditionColor = condition switch
    //     {
    //         "Clear" => "white",
    //         "Cloudy" => "grey",
    //         "Misty" => "grey",
    //         "Light Snow" => "cyan",
    //         "Rain" => "blue",
    //         "Blizzard" => "red",
    //         "Storm" => "red",
    //         _ => "white"
    //     };

    //     // Weather numbers line
    //     string windLabel = weather.GetWindLabel();
    //     string precipLabel = weather.GetPrecipitationLabel();
    //     int cloudPercent = (int)(weather.CloudCover * 100);

    //     var lines = new List<IRenderable>
    //     {
    //         new Markup($"[yellow bold]Day {dayNumber}[/] — [white]{clockTime}[/] [grey]({timeOfDay})[/]"),
    //         new Markup(daylightLine),
    //         new Markup($"[grey]Cond:[/] [{conditionColor}]{condition}[/]"),
    //         new Markup($"[grey]Wind:[/] {windLabel}  [grey]Precip:[/] {precipLabel}")
    //     };

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" TIME/WEATHER ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static IRenderable BuildLocationPanel(GameContext ctx)
    // {
    //     var location = ctx.CurrentLocation;

    //     var lines = new List<IRenderable>
    //     {
    //         new Markup($"[white bold]{Markup.Escape(location.Name)}[/]"),
    //         new Markup($"[grey italic]{Markup.Escape(GetShortDescription(location))}[/]")
    //     };

    //     // Build feature summary
    //     var features = new List<string>();

    //     // Foraging
    //     var forage = location.GetFeature<ForageFeature>();
    //     if (forage != null)
    //     {
    //         var resources = forage.GetAvailableResourceTypes();
    //         if (resources.Count > 0)
    //             features.Add($"[green]Forage:[/] {string.Join(", ", resources.Take(3))}");
    //     }

    //     // Hunting/game
    //     var territory = location.GetFeature<AnimalTerritoryFeature>();
    //     if (territory != null)
    //     {
    //         features.Add($"[yellow]Game:[/] {territory.GetDescription()}");
    //     }

    //     // Water
    //     var water = location.GetFeature<WaterFeature>();
    //     if (water != null)
    //     {
    //         features.Add($"[blue]Water[/]");
    //     }

    //     // Shelter
    //     var shelter = location.GetFeature<ShelterFeature>();
    //     if (shelter != null)
    //     {
    //         features.Add($"[cyan]Shelter:[/] {shelter.Name}: ins:{shelter.TemperatureInsulation * 100:F0}, wnd:{shelter.WindCoverage * 100:F0}, cov:{shelter.OverheadCoverage * 100:F0}");
    //     }

    //     // Add features to display
    //     if (features.Count > 0)
    //     {
    //         if (features.Count > 2)
    //         {
    //             lines.Add(new Markup(string.Join(" | ", features.Take(2))));
    //             lines.Add(new Markup(string.Join(" | ", features.Skip(2).Take(2))));
    //         }
    //         else
    //         {
    //             if (features.Count > 0)
    //                 lines.Add(new Markup(features[0]));
    //             if (features.Count > 1)
    //                 lines.Add(new Markup(features[1]));
    //         }
    //     }

    //     // Pad to 4 lines
    //     while (lines.Count < 4)
    //         lines.Add(new Text(""));

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" LOCATION ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static IRenderable BuildTemperaturePanel(GameContext ctx)
    // {
    //     var body = ctx.player.Body;
    //     var location = ctx.CurrentLocation;
    //     var fire = location.GetFeature<HeatSourceFeature>();

    //     double bodyTemp = body.BodyTemperature;
    //     double zoneTemp = location.ParentZone.Weather.TemperatureInFahrenheit;
    //     double locationTemp = location.GetTemperature(); // Includes all modifiers + fire
    //     double fireHeat = fire?.GetEffectiveHeatOutput(zoneTemp) ?? 0;

    //     // Line 2: Ambient breakdown
    //     string ambientLine;
    //     if (fireHeat > 0.5)
    //     {
    //         double baseTemp = locationTemp - fireHeat;
    //         ambientLine = $"[grey]Air:[/]   [blue]{baseTemp:F0}°F[/] + [yellow]Fire {fireHeat:F0}°F[/] = [white]{locationTemp:F0}°F felt[/]";
    //     }
    //     else
    //     {
    //         ambientLine = $"[grey]Air:[/]   [blue]{locationTemp:F0}°F[/]";
    //     }

    //     // Line 3: Trend (uses actual calculated delta from survival processor)
    //     var (arrow, trendDesc, trendColor) = GetHeatTrend(
    //         bodyTemp,
    //         ctx.player.LastSurvivalDelta?.TemperatureDelta,
    //         ctx.player.LastUpdateMinutes);

    //     var lines = new List<IRenderable>
    //     {
    //         new Markup($"[grey]Range:[/] {CreateTemperatureBar(bodyTemp)}"),
    //         new Markup($"[grey]Body:[/]  [white]{bodyTemp:F1}°F[/] [{GetTempColor(bodyTemp)}]{GetTemperatureStatus(bodyTemp)}[/]"),
    //         new Markup(ambientLine),
    //         new Markup($"[grey]Trend:[/] [{trendColor}]{arrow} {trendDesc}[/]"),
    //     };

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" TEMPERATURE ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static IRenderable BuildInventoryPanel(GameContext ctx)
    // {
    //     var inventory = ctx.Inventory;

    //     int weightPercent = (int)(inventory.CurrentWeightKg / inventory.MaxWeightKg * 100);
    //     int insulation = (int)(inventory.TotalInsulation * 100);
    //     double fuel = inventory.FuelWeightKg;
    //     string burnTime = inventory.TotalFuelBurnTimeHours >= 1.0 ? $"{(int)inventory.TotalFuelBurnTimeHours}hrs" : $"{(int)inventory.TotalFuelBurnTimeMinutes}min";

    //     var lines = new List<IRenderable>
    //     {
    //         new Markup($"Carry: {CreateColoredBar(weightPercent, 6, GetCapacityColor(100-weightPercent))} [grey]{weightPercent}%[/]"),
    //         new Markup($"[grey]Weight: {inventory.CurrentWeightKg:F1}/{inventory.MaxWeightKg:F0}kg[/]"),
    //         new Markup($"[grey]Insulation:[/] [blue]{insulation}%[/]"),
    //         new Markup($"[grey]Fuel:[/] [yellow]{fuel:F1}kg[/] [grey]({burnTime})[/]"),
    //     };

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" GEAR ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static IRenderable BuildEffectsPanel(GameContext ctx)
    // {
    //     var effects = ctx.player.EffectRegistry.GetAll();
    //     var lines = new List<IRenderable>();

    //     if (effects.Count > 0)
    //     {
    //         foreach (var effect in effects.Take(4))
    //         {
    //             string trend = GetEffectTrend(effect);
    //             string color = GetEffectColor(effect);
    //             int severityPercent = Math.Max(1, (int)Math.Ceiling(effect.Severity * 100));
    //             lines.Add(new Markup($"[{color}]{Markup.Escape(effect.EffectKind)} {severityPercent}%{trend}[/]"));
    //         }
    //         if (effects.Count > 4)
    //             lines.Add(new Markup($"[grey]+{effects.Count - 4} more...[/]"));
    //     }
    //     else
    //     {
    //         lines.Add(new Markup("[grey]None[/]"));
    //     }

    //     while (lines.Count < 4) lines.Add(new Text(""));

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" EFFECTS ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static IRenderable BuildInjuriesPanel(GameContext ctx)
    // {
    //     var body = ctx.player.Body;
    //     var damagedParts = body.Parts.Where(p => p.Condition < 0.95).ToList();
    //     var damagedOrgans = body.Parts
    //         .SelectMany(p => p.Organs)
    //         .Where(o => o.Condition < 0.95)
    //         .OrderBy(o => o.Condition)
    //         .ToList();
    //     var lines = new List<IRenderable>();

    //     // Blood status
    //     if (body.Blood.Condition < 0.95)
    //     {
    //         int bloodPercent = (int)(body.Blood.Condition * 100);
    //         string bloodStatus = body.Blood.Condition switch
    //         {
    //             >= 0.80 => "minor blood loss",
    //             >= 0.65 => "blood loss",
    //             >= 0.50 => "severe blood loss",
    //             _ => "critical blood loss"
    //         };
    //         string bloodColor = body.Blood.Condition switch
    //         {
    //             >= 0.80 => "yellow",
    //             >= 0.65 => "orange1",
    //             >= 0.50 => "red",
    //             _ => "red bold"
    //         };
    //         lines.Add(new Markup($"[{bloodColor}]{bloodStatus} ({bloodPercent}%)[/]"));
    //     }

    //     // Organ damage (internal failure)
    //     foreach (var organ in damagedOrgans.Take(2))
    //     {
    //         string color = GetInjuryColor(organ.Condition);
    //         int percent = (int)(organ.Condition * 100);
    //         lines.Add(new Markup($"[{color}]{Markup.Escape(organ.Name)} ({percent}%)[/]"));
    //     }
    //     if (damagedOrgans.Count > 2)
    //         lines.Add(new Markup($"[grey]+{damagedOrgans.Count - 2} more organs...[/]"));

    //     // Tissue damage (external wounds)
    //     if (damagedParts.Count == 0 && body.Blood.Condition >= 0.95 && damagedOrgans.Count == 0)
    //     {
    //         lines.Add(new Markup("[grey]None[/]"));
    //     }
    //     else if (damagedParts.Count > 0)
    //     {
    //         foreach (var part in damagedParts.Take(3))
    //         {
    //             string color = GetInjuryColor(part.Condition);
    //             int percent = (int)(part.Condition * 100);
    //             lines.Add(new Markup($"[{color}]{Markup.Escape(part.Name)} ({percent}%)[/]"));
    //         }
    //         if (damagedParts.Count > 3)
    //             lines.Add(new Markup($"[grey]+{damagedParts.Count - 3} more...[/]"));
    //     }

    //     while (lines.Count < 4) lines.Add(new Text(""));

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" INJURIES ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static IRenderable BuildFirePanel(GameContext ctx)
    // {
    //     var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

    //     var lines = new List<IRenderable>();

    //     if (fire == null)
    //     {
    //         lines.Add(new Markup("[grey]No fire pit[/]"));
    //         lines.Add(new Text(""));
    //         lines.Add(new Text(""));
    //         lines.Add(new Text(""));
    //     }
    //     else if (!fire.IsActive && !fire.HasEmbers)
    //     {
    //         // Fire pit exists but not burning
    //         lines.Add(new Markup("[grey]Cold[/]"));

    //         if (fire.TotalMassKg > 0)
    //         {
    //             int litPercent = (int)(fire.BurningMassKg / fire.TotalMassKg * 100);
    //             lines.Add(new Markup($"[grey]{fire.TotalMassKg:F1}kg fuel[/] [white]({litPercent}% lit)[/]"));
    //         }
    //         else
    //         {
    //             lines.Add(new Markup("[grey]No fuel[/]"));
    //         }
    //         lines.Add(new Text(""));
    //         lines.Add(new Text(""));
    //     }
    //     else
    //     {
    //         string phase = fire.GetFirePhase();

    //         // Use total fuel time when catching, otherwise burning time
    //         int minutes;
    //         if (fire.HasEmbers)
    //         {
    //             minutes = (int)(fire.EmberTimeRemaining * 60);
    //         }
    //         else if (fire.UnburnedMassKg > 0.1)
    //         {
    //             // Fuel is catching - show total estimated time
    //             minutes = (int)(fire.TotalHoursRemaining * 60);
    //         }
    //         else
    //         {
    //             minutes = (int)(fire.BurningHoursRemaining * 60);
    //         }

    //         string timeColor = GetFireTimeColor(minutes);
    //         string phaseColor = GetFirePhaseColor(phase);

    //         lines.Add(new Markup($"[{phaseColor}]{phase}[/]"));
    //         lines.Add(new Markup($"[{timeColor}]{minutes} min remaining[/] [grey]({fire.EffectiveBurnRateKgPerHour:F1} kg/hr)[/]"));

    //         // Show fuel status with unlit indicator
    //         string fuelStatus;
    //         if (fire.UnburnedMassKg > 0.1)
    //         {
    //             fuelStatus = $"[yellow]{fire.BurningMassKg:F1}kg burning[/] [grey](+{fire.UnburnedMassKg:F1}kg unlit)[/]";
    //         }
    //         else
    //         {
    //             fuelStatus = $"[grey]{fire.TotalMassKg:F1}/{fire.MaxFuelCapacityKg:F0} kg fuel[/]";
    //         }
    //         lines.Add(new Markup(fuelStatus));

    //         lines.Add(new Markup($"[yellow]+{fire.GetEffectiveHeatOutput(ctx.CurrentLocation.GetTemperature()):F0}°F heat[/]"));
    //     }

    //     // Show torch status if active
    //     if (ctx.Inventory.HasLitTorch)
    //     {
    //         int torchMins = (int)ctx.Inventory.TorchBurnTimeRemainingMinutes;
    //         string torchColor = torchMins <= 5 ? "red" : torchMins <= 15 ? "yellow" : "orange3";
    //         double torchHeat = ctx.Inventory.GetTorchHeatBonusF();
    //         lines.Add(new Markup($"[{torchColor}]Torch: {torchMins} min[/] [grey](+{torchHeat:F0}°F)[/]"));
    //     }

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" FIRE ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // private static IRenderable BuildNarrativePanel(GameContext ctx)
    // {
    //     var entries = _log.GetVisible();
    //     var lines = new List<IRenderable>();

    //     foreach (var (text, level) in entries)
    //     {
    //         string color = GetLogColor(level);
    //         lines.Add(new Markup($"[{color}]{Markup.Escape(text)}[/]"));
    //     }

    //     // Pad to fixed height
    //     int padding = NarrativeLog.MAX_VISIBLE_LINES - entries.Count;
    //     for (int i = 0; i < padding; i++)
    //         lines.Add(new Text(""));

    //     return new Panel(new Rows(lines))
    //     {
    //         Header = new PanelHeader(" LOGS ", Justify.Left),
    //         Border = BoxBorder.Rounded,
    //         Padding = new Padding(1, 0, 1, 0),
    //         Expand = true
    //     };
    // }

    // #endregion

    // #region Color Helpers

    // private static string CreateColoredBar(int percent, int width, string color)
    // {
    //     int filled = Math.Clamp(percent * width / 100, 0, width);
    //     int empty = width - filled;
    //     return $"[grey][[[/][{color}]{new string('█', filled)}[/][grey]{new string('░', empty)}]][/]";
    // }

    // private static string GetLogColor(LogLevel level) => level switch
    // {
    //     LogLevel.Success => "green",
    //     LogLevel.Warning => "yellow",
    //     LogLevel.Danger => "red",
    //     LogLevel.System => "grey",
    //     _ => "white"
    // };

    // private static string GetFoodColor(int percent) => percent switch
    // {
    //     >= 60 => "green",
    //     >= 30 => "yellow",
    //     _ => "red"
    // };

    // private static string GetWaterColor(int percent) => percent switch
    // {
    //     >= 60 => "blue",
    //     >= 30 => "yellow",
    //     _ => "red"
    // };

    // private static string GetEnergyColor(int percent) => percent switch
    // {
    //     >= 60 => "cyan",
    //     >= 30 => "yellow",
    //     _ => "red"
    // };

    // private static string GetTempColor(double temp) => temp switch
    // {
    //     >= 100 => "red",
    //     >= 99 => "yellow",
    //     >= 97 => "green",
    //     >= 95 => "cyan",
    //     _ => "blue"
    // };

    // private static string GetFirePhaseColor(string phase) => phase switch
    // {
    //     "Roaring" => "red",
    //     "Building" or "Steady" => "yellow",
    //     "Igniting" or "Dying" => "olive",
    //     "Embers" => "maroon",
    //     _ => "grey"
    // };

    // private static string GetFireTimeColor(int minutes) => minutes switch
    // {
    //     >= 30 => "green",
    //     >= 15 => "yellow",
    //     _ => "red"
    // };

    // private static string GetWeightColor(double percent) => percent switch
    // {
    //     >= 90 => "red",
    //     >= 70 => "yellow",
    //     _ => "green"
    // };

    // private static string GetInjuryColor(double condition) => condition switch
    // {
    //     <= 0.2 => "red",
    //     <= 0.5 => "yellow",
    //     _ => "white"
    // };

    // private static string GetEffectColor(Effect effect)
    // {
    //     if (effect.HourlySeverityChange > 0) return "red";      // Worsening
    //     if (effect.HourlySeverityChange < 0) return "green";    // Improving
    //     return "grey";                                           // Stable
    // }

    // private static string GetCapacityColor(int percent) => percent switch
    // {
    //     >= 80 => "green",
    //     >= 50 => "yellow",
    //     >= 25 => "orange1",
    //     _ => "red"
    // };

    // private static string CreateTemperatureBar(double bodyTemp, int width = 10)
    // {
    //     // Range: 87°F (death) to 102°F (severe hyperthermia)
    //     const double minTemp = 87.0;
    //     const double maxTemp = 102.0;

    //     double position = (bodyTemp - minTemp) / (maxTemp - minTemp);
    //     position = Math.Clamp(position, 0, 1);

    //     int filled = (int)(position * width);
    //     int empty = width - filled;

    //     string color = GetTempBarColor(bodyTemp);
    //     return $"[grey][[[/][{color}]{new string('█', filled)}[/][grey]{new string('░', empty)}]][/]";
    // }

    // private static string GetTempBarColor(double temp) => temp switch
    // {
    //     < 89.6 => "red",      // Severe hypothermia
    //     < 95.0 => "blue",     // Hypothermia
    //     < 97.0 => "cyan",     // Cool/shivering
    //     < 99.0 => "green",    // Normal/safe
    //     < 100.0 => "yellow",  // Hot/sweating
    //     _ => "red"            // Hyperthermia
    // };

    // private static (string arrow, string description, string color) GetHeatTrend(
    //     double bodyTemp,
    //     double? temperatureDeltaTotal,
    //     int minutes)
    // {
    //     // No data yet = stable
    //     if (temperatureDeltaTotal is null || minutes <= 0)
    //         return ("→", "Stable", "green");

    //     // Convert total change to per-hour rate
    //     double deltaPerHour = (temperatureDeltaTotal.Value / minutes) * 60;

    //     if (Math.Abs(deltaPerHour) < 0.05)
    //         return ("→", "Stable", "green");

    //     bool cooling = deltaPerHour < 0;
    //     string arrow = cooling ? "↓" : "↑";

    //     string speed = Math.Abs(deltaPerHour) switch
    //     {
    //         > 2.0 => "rapidly",
    //         > 1.0 => "steadily",
    //         > 0.3 => "slowly",
    //         _ => "very slowly"
    //     };

    //     string action = cooling ? "Cooling" : "Warming";
    //     string rate = $"({deltaPerHour:+0.0;-0.0}°/hr)";

    //     // Color based on whether trend is concerning
    //     string color = (cooling && bodyTemp < 97) ? "cyan"
    //                  : (!cooling && bodyTemp > 99) ? "yellow"
    //                  : "grey";

    //     return (arrow, $"{action} {speed} {rate}", color);
    // }

    // #endregion

    // #region Status Text Helpers

    // private static string GetHealthStatus(int percent) => percent switch
    // {
    //     >= 90 => "Healthy",
    //     >= 70 => "Fine",
    //     >= 50 => "Hurt",
    //     >= 25 => "Wounded",
    //     _ => "Critical"
    // };

    // private static string GetCaloriesStatus(int percent) => percent switch
    // {
    //     >= 80 => "Well Fed",
    //     >= 60 => "Satisfied",
    //     >= 40 => "Peckish",
    //     >= 20 => "Hungry",
    //     _ => "Starving"
    // };

    // private static string GetHydrationStatus(int percent) => percent switch
    // {
    //     >= 80 => "Hydrated",
    //     >= 60 => "Fine",
    //     >= 40 => "Thirsty",
    //     >= 20 => "Parched",
    //     _ => "Dehydrated"
    // };

    // private static string GetEnergyStatus(int percent) => percent switch
    // {
    //     >= 90 => "Energized",
    //     >= 80 => "Alert",
    //     >= 40 => "Normal",
    //     >= 30 => "Tired",
    //     >= 20 => "Very Tired",
    //     _ => "Exhausted"
    // };

    // private static string GetTemperatureStatus(double temp) => temp switch
    // {
    //     >= 100 => "Feverish",
    //     >= 99 => "Hot",
    //     >= 97 => "Normal",
    //     >= 95 => "Cool",
    //     _ => "Cold"
    // };

    // private static string GetInjurySeverity(double condition) => condition switch
    // {
    //     <= 0.10 => "destroyed",
    //     <= 0.30 => "critically damaged",
    //     <= 0.50 => "badly wounded",
    //     <= 0.70 => "gashed",
    //     <= 0.85 => "cut",
    //     <= 0.95 => "scratched",
    //     _ => "minor"  // Shouldn't show if >= 0.95, but fallback
    // };

    // private static string GetEffectTrend(Effect effect)
    // {
    //     if (effect.HourlySeverityChange > 0) return "(↑)";
    //     if (effect.HourlySeverityChange < 0) return "(↓)";
    //     return "";
    // }

    // private static string GetShortDescription(Environments.Location location)
    // {
    //     return location.Tags;
    // }

    // #endregion

    // #region Inventory Screen

    public static void RenderInventoryScreen(GameContext ctx, Inventory? inventory = null, string? title = null)
    {
        var inv = inventory ?? ctx.Inventory;
        var headerTitle = title ?? "INVENTORY";

        // Route to web UI when session is active
        if (ctx.SessionId != null)
        {
            Web.WebIO.RenderInventory(ctx, inv, headerTitle);
            return;
        }

        // if (Output.TestMode)
        // {
        //     RenderInventoryTestMode(inv, headerTitle);
        //     return;
        // }

        // AnsiConsole.Clear();

        // Column 1: Gear (Equipment + Tools)
        // var gearLines = new List<IRenderable>
        // {
        //     new Markup("[bold underline]GEAR[/]"),
        //     new Text("")
        // };

        // Weapon
        // gearLines.Add(new Markup("[white bold]Weapon:[/]"));
        // if (inv.Weapon != null)
        //     gearLines.Add(new Markup($"  [yellow]{Markup.Escape(inv.Weapon.Name)}[/] [grey]({inv.Weapon.Damage:F0} dmg)[/]"));
        // else
        //     gearLines.Add(new Markup("  [grey]None[/]"));

        // Armor slots
        // gearLines.Add(new Markup("[white bold]Armor:[/]"));
        // if (inv.Head != null || inv.Chest != null || inv.Legs != null || inv.Feet != null || inv.Hands != null)
        // {
        //     AddEquipmentLine(gearLines, "  Head", inv.Head);
        //     AddEquipmentLine(gearLines, "  Chest", inv.Chest);
        //     AddEquipmentLine(gearLines, "  Legs", inv.Legs);
        //     AddEquipmentLine(gearLines, "  Feet", inv.Feet);
        //     AddEquipmentLine(gearLines, "  Hands", inv.Hands);
        //     if (inv.TotalInsulation > 0)
        //         gearLines.Add(new Markup($"  [cyan]+{inv.TotalInsulation * 100:F0}% insulation[/]"));
        // }
        // else
        // {
        //     gearLines.Add(new Markup("  [grey]None[/]"));
        // }

        // Tools
        // gearLines.Add(new Markup("[white bold]Tools:[/]"));
        // if (inv.Tools.Count > 0)
        // {
        //     foreach (var tool in inv.Tools)
        //     {
        //         string weaponInfo = tool.IsWeapon ? $" [grey]({tool.Damage:F0} dmg)[/]" : "";
        //         gearLines.Add(new Markup($"  [white]{Markup.Escape(tool.Name)}[/]{weaponInfo}"));
        //     }
        // }
        // else
        // {
        //     gearLines.Add(new Markup("  [grey]None[/]"));
        // }

        // Special items
        // if (inv.Special.Count > 0)
        // {
        //     gearLines.Add(new Markup("[white bold]Special:[/]"));
        //     foreach (var item in inv.Special)
        //     {
        //         gearLines.Add(new Markup($"  [magenta]{Markup.Escape(item.Name)}[/]"));
        //     }
        // }

        // Column 2: Fuel
        // var fuelLines = new List<IRenderable>
        // {
        //     new Markup("[orange1 bold underline]FUEL[/]"),
        //     new Text("")
        // };

        // if (inv.Logs.Count > 0)
        //     fuelLines.Add(new Markup($"[white]{inv.Logs.Count} logs[/] [grey]({inv.Logs.Sum():F1}kg)[/]"));
        // if (inv.Sticks.Count > 0)
        //     fuelLines.Add(new Markup($"[white]{inv.Sticks.Count} sticks[/] [grey]({inv.Sticks.Sum():F1}kg)[/]"));
        // if (inv.Tinder.Count > 0)
        //     fuelLines.Add(new Markup($"[white]{inv.Tinder.Count} tinder[/] [grey]({inv.Tinder.Sum():F2}kg)[/]"));
        // if (!inv.HasFuel)
        //     fuelLines.Add(new Markup("[grey]None[/]"));
        // else
        //     fuelLines.Add(new Markup($"[grey italic]~{inv.TotalFuelBurnTimeHours:F1} hrs[/]"));

        // Column 3: Food/Water
        // var foodLines = new List<IRenderable>
        // {
        //     new Markup("[green bold underline]FOOD/WATER[/]"),
        //     new Text("")
        // };

        // Food subcategory
        // foodLines.Add(new Markup("[white bold]Food:[/]"));
        // if (inv.HasFood)
        // {
        //     if (inv.CookedMeat.Count > 0)
        //         foodLines.Add(new Markup($"  [white]{inv.CookedMeat.Count} cooked meat[/] [grey]({inv.CookedMeat.Sum():F1}kg)[/]"));
        //     if (inv.RawMeat.Count > 0)
        //         foodLines.Add(new Markup($"  [yellow]{inv.RawMeat.Count} raw meat[/] [grey]({inv.RawMeat.Sum():F1}kg)[/]"));
        //     if (inv.Berries.Count > 0)
        //         foodLines.Add(new Markup($"  [white]{inv.Berries.Count} berries[/] [grey]({inv.Berries.Sum():F2}kg)[/]"));
        // }
        // else
        // {
        //     foodLines.Add(new Markup("  [grey]None[/]"));
        // }

        // Water subcategory
        // foodLines.Add(new Markup("[white bold]Water:[/]"));
        // if (inv.HasWater)
        //     foodLines.Add(new Markup($"  [blue]{inv.WaterLiters:F1}L[/]"));
        // else
        //     foodLines.Add(new Markup("  [grey]None[/]"));

        // Column 4: Materials
        // var matLines = new List<IRenderable>
        // {
        //     new Markup("[purple bold underline]MATERIALS[/]"),
        //     new Text("")
        // };

        // if (inv.Stone.Count > 0)
        //     matLines.Add(new Markup($"[white]{inv.Stone.Count} stone[/] [grey]({inv.Stone.Sum():F1}kg)[/]"));
        // if (inv.Bone.Count > 0)
        //     matLines.Add(new Markup($"[white]{inv.Bone.Count} bone[/] [grey]({inv.Bone.Sum():F1}kg)[/]"));
        // if (inv.Hide.Count > 0)
        //     matLines.Add(new Markup($"[white]{inv.Hide.Count} hide[/] [grey]({inv.Hide.Sum():F1}kg)[/]"));
        // if (inv.PlantFiber.Count > 0)
        //     matLines.Add(new Markup($"[white]{inv.PlantFiber.Count} plant fiber[/] [grey]({inv.PlantFiber.Sum():F2}kg)[/]"));
        // if (inv.Sinew.Count > 0)
        //     matLines.Add(new Markup($"[white]{inv.Sinew.Count} sinew[/] [grey]({inv.Sinew.Sum():F2}kg)[/]"));
        // if (!inv.HasCraftingMaterials)
        //     matLines.Add(new Markup("[grey]None[/]"));

        // Pad columns to same height
        // int maxLines = Math.Max(Math.Max(gearLines.Count, fuelLines.Count), Math.Max(foodLines.Count, matLines.Count));
        // while (gearLines.Count < maxLines) gearLines.Add(new Text(""));
        // while (fuelLines.Count < maxLines) fuelLines.Add(new Text(""));
        // while (foodLines.Count < maxLines) foodLines.Add(new Text(""));
        // while (matLines.Count < maxLines) matLines.Add(new Text(""));

        // Build layout
        // var gearPanel = new Panel(new Rows(gearLines)) { Border = BoxBorder.None, Padding = new Padding(1, 0, 1, 0) };
        // var fuelPanel = new Panel(new Rows(fuelLines)) { Border = BoxBorder.None, Padding = new Padding(1, 0, 1, 0) };
        // var foodPanel = new Panel(new Rows(foodLines)) { Border = BoxBorder.None, Padding = new Padding(1, 0, 1, 0) };
        // var matPanel = new Panel(new Rows(matLines)) { Border = BoxBorder.None, Padding = new Padding(1, 0, 1, 0) };

        // var columns = new Columns(gearPanel, fuelPanel, foodPanel, matPanel).Expand();

        // Weight summary
        // double weightPercent = inv.MaxWeightKg > 0 ? inv.CurrentWeightKg / inv.MaxWeightKg * 100 : 0;
        // string weightColor = GetWeightColor(weightPercent);
        // var weightLine = new Markup($"\n[{weightColor}]Weight: {inv.CurrentWeightKg:F1} / {inv.MaxWeightKg:F0} kg[/]");

        // var content = new Rows(columns, weightLine);

        // var mainPanel = new Panel(content)
        // {
        //     Header = new PanelHeader($" {headerTitle} ", Justify.Center),
        //     Border = BoxBorder.Double,
        //     Padding = new Padding(1, 1, 1, 1),
        //     Expand = true
        // };

        // AnsiConsole.Write(mainPanel);
    }

    // private static void AddEquipmentLine(List<IRenderable> lines, string slot, Equipment? equipment)
    // {
    //     if (equipment != null)
    //         lines.Add(new Markup($"[white]{slot}:[/] [cyan]{Markup.Escape(equipment.Name)}[/] [grey](+{equipment.Insulation * 100:F0}%)[/]"));
    //     else
    //         lines.Add(new Markup($"[white]{slot}:[/] [grey]—[/]"));
    // }

    // private static void RenderInventoryTestMode(Inventory inv, string title)
    // {
    //     TestModeIO.WriteOutput($"[{title}: {inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0}kg]\n");

    //     if (inv.Weapon != null)
    //         TestModeIO.WriteOutput($"  Weapon: {inv.Weapon.Name}\n");

    //     if (inv.Logs.Count > 0) TestModeIO.WriteOutput($"  Logs: {inv.Logs.Count}\n");
    //     if (inv.Sticks.Count > 0) TestModeIO.WriteOutput($"  Sticks: {inv.Sticks.Count}\n");
    //     if (inv.Tinder.Count > 0) TestModeIO.WriteOutput($"  Tinder: {inv.Tinder.Count}\n");
    //     if (inv.HasFood) TestModeIO.WriteOutput($"  Food: {inv.FoodWeightKg:F1}kg\n");
    //     if (inv.HasWater) TestModeIO.WriteOutput($"  Water: {inv.WaterLiters:F1}L\n");
    // }

    // #endregion

    // #region Test Mode

    // private static void RenderTestMode(GameContext ctx)
    // {
    //     var body = ctx.player.Body;
    //     var location = ctx.CurrentLocation;

    //     int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
    //     int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
    //     int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);

    //     TestModeIO.WriteOutput($"[Status: Food {caloriesPercent}%, Water {hydrationPercent}%, Energy {energyPercent}%]\n");

    //     // Temperature detail
    //     var fire = location.GetFeature<HeatSourceFeature>();
    //     double zoneTemp = location.ParentZone.Weather.TemperatureInFahrenheit;
    //     double locationTemp = location.GetTemperature();
    //     double fireHeat = fire?.GetEffectiveHeatOutput(zoneTemp) ?? 0;
    //     TestModeIO.WriteOutput($"[Temp: Body {body.BodyTemperature:F1}°F, Air {locationTemp:F0}°F, Fire +{fireHeat:F0}°F]\n");

    //     if (fire != null && (fire.IsActive || fire.HasEmbers))
    //     {
    //         int minutes = (int)(fire.HoursRemaining * 60);
    //         TestModeIO.WriteOutput($"[Fire: {fire.GetFirePhase()} - {minutes} min]\n");
    //     }

    //     // Show torch status if active
    //     if (ctx.Inventory.HasLitTorch)
    //     {
    //         int torchMins = (int)ctx.Inventory.TorchBurnTimeRemainingMinutes;
    //         TestModeIO.WriteOutput($"[Torch: {torchMins} min remaining]\n");
    //     }

    //     // Add blood status to test mode
    //     if (body.Blood.Condition < 0.95)
    //     {
    //         int bloodPercent = (int)(body.Blood.Condition * 100);
    //         TestModeIO.WriteOutput($"[Blood: {bloodPercent}%]\n");
    //     }

    //     // Add body/effects info to test mode
    //     var effects = ctx.player.EffectRegistry.GetAll();
    //     if (effects.Count > 0)
    //     {
    //         var effectNames = effects.Select(e => e.EffectKind);
    //         TestModeIO.WriteOutput($"[Effects: {string.Join(", ", effectNames)}]\n");
    //     }

    //     foreach (var (text, _) in _log.GetVisible())
    //         TestModeIO.WriteOutput(text + "\n");
    // }

    // #endregion
}
