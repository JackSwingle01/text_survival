using System.Globalization;
using System.Text.Json;
using text_survival.GameCli;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
var output = Console.Out;
Console.SetOut(Console.Error); // Keep game diagnostics off the JSON protocol stream.
void Write(object value) { output.WriteLine(JsonSerializer.Serialize(value, CliProtocol.Json)); output.Flush(); }
try
{
    if (args.Length == 4 && args[0] == "--worker")
    {
        Environment.ExitCode = new BackgroundCli(args[1]).RunWorker(args[2], args[3]);
        return;
    }
    if (args.Length > 0 && args[0] is "start" or "send" or "poll" or "peek" or "stop")
    {
        Write(new { ok = true, result = new BackgroundCli(BackgroundCli.DefaultRoot).Dispatch(args) });
        return;
    }
    if (args.Length == 0 || args.Contains("--help")) { Write(CliSession.Help); return; }
    var options = CliLaunchOptions.Parse(args[0] == "interactive" ? args[1..] : args);
    using var session = new CliSession(options.CreateContext(), options.SessionId);
    Write(new { ok = true, result = session.Look() });
    while (!session.Exiting && Console.ReadLine() is { } line)
    {
        try
        {
            var (command, value) = CliProtocol.Parse(line);
            Write(new { ok = true, result = session.Execute(command, value) });
        }
        catch (Exception ex) when (CliProtocol.IsInputError(ex))
        { Write(new { ok = false, error = ex.Message, screen = session.Ui.Screen }); }
    }
}
catch (Exception ex)
{
    if (CliProtocol.IsInputError(ex))
    {
        Write(new { ok = false, error = ex.Message });
        Environment.ExitCode = 2;
    }
    else
    {
        Write(new { ok = false, fatal = true, error = ex.Message });
        Console.Error.WriteLine(ex);
        Environment.ExitCode = 1;
    }
}
