using Spectre.Console;
using Spectre.Console.Rendering;
using text_survival.Actions;
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
        AnsiConsole.Write(BuildStatusPanel(ctx));
        AnsiConsole.Write(BuildNarrativePanel(ctx));
    }

    private static IRenderable BuildStatusPanel(GameContext ctx)
    {
        var body = ctx.player.Body;
        var fire = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();

        int caloriesPercent = (int)(body.CalorieStore / SurvivalProcessor.MAX_CALORIES * 100);
        int hydrationPercent = (int)(body.Hydration / SurvivalProcessor.MAX_HYDRATION * 100);
        int energyPercent = (int)(body.Energy / SurvivalProcessor.MAX_ENERGY_MINUTES * 100);

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Label").Width(8))
            .AddColumn(new TableColumn("Bar").Width(22))
            .AddColumn(new TableColumn("Pct").Width(6))
            .AddColumn(new TableColumn("Status").Width(14))
            .AddColumn(new TableColumn("Extra").Width(16));

        table.AddRow(
            new Text("Food:"),
            new Markup(CreateColoredBar(caloriesPercent, 20, GetFoodColor(caloriesPercent))),
            new Text($"{caloriesPercent}%"),
            new Text(GetCaloriesStatus(caloriesPercent)),
            new Text("")
        );

        table.AddRow(
            new Text("Water:"),
            new Markup(CreateColoredBar(hydrationPercent, 20, GetWaterColor(hydrationPercent))),
            new Text($"{hydrationPercent}%"),
            new Text(GetHydrationStatus(hydrationPercent)),
            new Text("")
        );

        table.AddRow(
            new Text("Energy:"),
            new Markup(CreateColoredBar(energyPercent, 20, GetEnergyColor(energyPercent))),
            new Text($"{energyPercent}%"),
            new Text(GetEnergyStatus(energyPercent)),
            new Text("")
        );

        string tempStatus = GetTemperatureStatus(body.BodyTemperature);
        table.AddRow(
            new Text("Temp:"),
            new Text($"{body.BodyTemperature:F1}°F ({tempStatus})"),
            new Text(""),
            new Text($"Feels: {ctx.CurrentLocation.GetTemperature():F0}°F"),
            new Text("")
        );

        // Fire status if present
        if (fire != null && (fire.IsActive || fire.HasEmbers))
        {
            string phase = fire.GetFirePhase();
            int minutes = (int)(fire.HoursRemaining * 60);
            string fireColor = GetFireColor(phase);
            table.AddRow(
                new Markup($"[{fireColor}]Fire:[/]"),
                new Markup($"[{fireColor}]{phase} ({minutes} min)[/]"),
                new Text(""),
                new Markup($"[{fireColor}]{fire.GetCurrentFireTemperature():F0}°F[/]"),
                new Text("")
            );
        }

        return new Panel(table)
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
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

        string header = $" {ctx.CurrentLocation.Name} | {ctx.GetTimeOfDay()} ";
        return new Panel(new Rows(lines))
        {
            Header = new PanelHeader(header),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };
    }

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

    private static string GetFireColor(string phase) => phase switch
    {
        "Roaring" => "red",
        "Building" or "Steady" => "yellow",
        "Igniting" or "Dying" => "olive",
        "Embers" => "maroon",
        _ => "grey"
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

        foreach (var (text, _) in _log.GetVisible())
            TestModeIO.WriteOutput(text + "\n");
    }
}
