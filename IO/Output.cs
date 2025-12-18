using System.Runtime.CompilerServices;
using Spectre.Console;
using text_survival.Actions;
using text_survival.Actors.NPCs;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.IO
{
    public static class Output
    {
        // Test mode: set TEST_MODE=1 to skip sleeps and use file I/O
        public static bool TestMode = Environment.GetEnvironmentVariable("TEST_MODE") == "1";
        public static int SleepTime = 200;

        // Message batching for deduplication during long actions
        private static bool IsBatching = false;
        private static List<string> MessageBuffer = new List<string>();

        #region Color Mapping

        public static string GetMarkupColor(object x) => x switch
        {
            string => "white",
            int or float or double => "green",
            Npc => "red",
            Item => "cyan",
            Container => "yellow",
            Player => "green",
            Zone => "blue",
            Location => "navy",
            Enum => "grey",
            null => "red",
            _ => "red",
        };

        // Legacy support for test mode
        public static ConsoleColor DetermineTextColor(object x) => x switch
        {
            string => ConsoleColor.White,
            int or float or double => ConsoleColor.Green,
            Npc => ConsoleColor.Red,
            Item => ConsoleColor.Cyan,
            Container => ConsoleColor.Yellow,
            Player => ConsoleColor.Green,
            Zone => ConsoleColor.Blue,
            Location => ConsoleColor.DarkBlue,
            Enum => ConsoleColor.Gray,
            null => ConsoleColor.Red,
            _ => ConsoleColor.Red,
        };

        #endregion

        #region Core Write Methods

        public static void Write(params object[] args)
        {
            foreach (var arg in args)
            {
                string text = GetFormattedText(arg);

                if (TestMode)
                {
                    TestModeIO.WriteOutput(text);
                }
                else
                {
                    string color = GetMarkupColor(arg);
                    // Use MarkupInterpolated for auto-escaping (best practice from docs)
                    AnsiConsole.Markup($"[{color}]{Markup.Escape(text)}[/]");
                    Thread.Sleep(SleepTime);
                }
            }
        }

        /// <summary>
        /// Write markup text directly (caller responsible for escaping)
        /// </summary>
        public static void WriteMarkup(string markup)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput(markup + "\n");
                return;
            }
            AnsiConsole.MarkupLine(markup);
        }

        public static void WriteLine(params object[] args)
        {
            if (IsBatching)
            {
                string message = GetFormattedText(args);
                MessageBuffer.Add(message);
                return;
            }

            string text = GetFormattedText(args);

            // Add to narrative log so text persists across screen clears
            if (!string.IsNullOrWhiteSpace(text))
                GameDisplay.AddNarrative(text);

            Write(args);
            Write("\n");
        }

        /// <summary>
        /// Write without pacing delays - use for status updates, UI chrome
        /// </summary>
        public static void WriteImmediate(params object[] args)
        {
            foreach (var arg in args)
            {
                string text = GetFormattedText(arg);

                if (TestMode)
                {
                    TestModeIO.WriteOutput(text);
                }
                else
                {
                    string color = GetMarkupColor(arg);
                    AnsiConsole.Markup($"[{color}]{Markup.Escape(text)}[/]");
                }
            }
        }

        public static void WriteLineImmediate(params object[] args)
        {
            WriteImmediate(args);
            if (TestMode)
                TestModeIO.WriteOutput("\n");
            else
                AnsiConsole.WriteLine();
        }

        public static void WriteColored(ConsoleColor color, params object[] args)
        {
            string text = GetFormattedText(args);

            // Add to narrative log with appropriate level
            if (!string.IsNullOrWhiteSpace(text))
            {
                var level = color switch
                {
                    ConsoleColor.Red => LogLevel.Danger,
                    ConsoleColor.Yellow => LogLevel.Warning,
                    ConsoleColor.Green => LogLevel.Success,
                    _ => LogLevel.Normal
                };
                GameDisplay.AddNarrative(text, level);
            }

            if (TestMode)
            {
                Write(args);
                return;
            }

            string spectreColor = ConsoleColorToSpectre(color);
            foreach (var arg in args)
            {
                string argText = GetFormattedText(arg);
                AnsiConsole.Markup($"[{spectreColor}]{Markup.Escape(argText)}[/]");
                Thread.Sleep(SleepTime);
            }
        }

        public static void WriteLineColored(ConsoleColor color, params object[] args)
        {
            WriteColored(color, args);

            if (TestMode)
                TestModeIO.WriteOutput("\n");
            else
                AnsiConsole.WriteLine();
        }

        public static void WriteAll(List<string> lines)
        {
            lines.ForEach(l => WriteLine(l));
        }

        public static void WriteWarning(string str) => WriteLineColored(ConsoleColor.Yellow, str);
        public static void WriteDanger(string str) => WriteLineColored(ConsoleColor.Red, str);
        public static void WriteSuccess(string str) => WriteLineColored(ConsoleColor.Green, str);

        #endregion

        #region Screen Control

        public static void Clear()
        {
            if (!TestMode)
                AnsiConsole.Clear();
        }

        public static void Rule(string? title = null)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput(new string('─', 40) + "\n");
                if (title != null)
                    TestModeIO.WriteOutput($"  {title}\n");
                return;
            }

            if (title != null)
                AnsiConsole.Write(new Rule(title).LeftJustified());
            else
                AnsiConsole.Write(new Rule());
        }

        #endregion

        #region Status and Progress

        /// <summary>
        /// Show a spinner while work executes.
        /// WARNING: Do not use prompts (Input.Select, etc.) inside Status - not supported by Spectre.Console.
        /// </summary>
        public static void Status(string message, Action work)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput($"[Status: {message}]\n");
                work();
                return;
            }

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start(message, ctx => work());
        }

        /// <summary>
        /// Show a spinner with updatable status text.
        /// WARNING: Do not use prompts inside Status - not supported by Spectre.Console.
        /// </summary>
        public static void Status(string message, Action<StatusContext> work)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput($"[Status: {message}]\n");
                work(null!);
                return;
            }

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start(message, work);
        }

        /// <summary>
        /// Async spinner.
        /// WARNING: Do not use prompts inside Status - not supported by Spectre.Console.
        /// </summary>
        public static async Task StatusAsync(string message, Func<StatusContext, Task> work)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput($"[Status: {message}]\n");
                await work(null!);
                return;
            }

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(message, work);
        }

        /// <summary>
        /// Show a progress bar for a known-duration task.
        /// WARNING: Do not use prompts inside Progress - not supported by Spectre.Console.
        /// </summary>
        public static void Progress(string description, int totalSteps, Action<ProgressTask> work)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput($"[Progress: {description} ({totalSteps} steps)]\n");
                work(null!);
                return;
            }

            AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new SpinnerColumn(),
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn()
                )
                .Start(ctx =>
                {
                    var task = ctx.AddTask(description, maxValue: totalSteps);
                    work(task);
                });
        }

        /// <summary>
        /// Simple progress bar that increments automatically with delays
        /// </summary>
        public static void ProgressSimple(string description, int steps, int delayMs = 100)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput($"[Progress: {description}]\n");
                return;
            }

            AnsiConsole.Progress()
                .AutoClear(true)
                .HideCompleted(true)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn()
                )
                .Start(ctx =>
                {
                    var task = ctx.AddTask(description, maxValue: steps);
                    while (!task.IsFinished)
                    {
                        task.Increment(1);
                        Thread.Sleep(delayMs);
                    }
                });
        }

        #endregion

        #region Tables

        /// <summary>
        /// Create and display a simple table
        /// </summary>
        public static void Table(string title, string[] columns, IEnumerable<string[]> rows)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput($"[Table: {title}]\n");
                TestModeIO.WriteOutput(string.Join(" | ", columns) + "\n");
                foreach (var row in rows)
                    TestModeIO.WriteOutput(string.Join(" | ", row) + "\n");
                return;
            }

            var table = new Table()
                .Title(title)
                .Border(TableBorder.Rounded);

            foreach (var col in columns)
                table.AddColumn(col);

            foreach (var row in rows)
                table.AddRow(row);

            AnsiConsole.Write(table);
        }

        /// <summary>
        /// Create a table with full customization
        /// </summary>
        public static Table CreateTable(string? title = null)
        {
            var table = new Table().Border(TableBorder.Rounded);
            if (title != null)
                table.Title(title);
            return table;
        }

        public static void WriteTable(Table table)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput("[Table output]\n");
                return;
            }

            AnsiConsole.Write(table);
        }

        #endregion

        #region Panels

        /// <summary>
        /// Display content in a bordered panel
        /// </summary>
        public static void Panel(string content, string? header = null)
        {
            if (TestMode)
            {
                if (header != null)
                    TestModeIO.WriteOutput($"[{header}]\n");
                TestModeIO.WriteOutput(content + "\n");
                return;
            }

            var panel = new Panel(content)
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0, 1, 0)
            };

            if (header != null)
                panel.Header = new PanelHeader(header);

            AnsiConsole.Write(panel);
        }

        /// <summary>
        /// Display a status panel (fire, vitals, etc.)
        /// </summary>
        public static void StatusPanel(string header, params (string label, string value)[] stats)
        {
            if (TestMode)
            {
                TestModeIO.WriteOutput($"[{header}]\n");
                foreach (var (label, value) in stats)
                    TestModeIO.WriteOutput($"  {label}: {value}\n");
                return;
            }

            var content = string.Join("\n", stats.Select(s => $"[grey]{Markup.Escape(s.label)}:[/] {Markup.Escape(s.value)}"));
            var panel = new Panel(content)
            {
                Header = new PanelHeader(header),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0, 1, 0)
            };

            AnsiConsole.Write(panel);
        }

        #endregion

        #region Helpers

        private static string GetFormattedText(params object[] args)
        {
            string result = string.Empty;

            foreach (var arg in args)
            {
                result += arg switch
                {
                    float f => $"{f:F1}",
                    double d => $"{d:F1}",
                    null => "[NULL]",
                    _ => arg.ToString()
                };
            }
            return result;
        }

        private static string ConsoleColorToSpectre(ConsoleColor color) => color switch
        {
            ConsoleColor.Black => "black",
            ConsoleColor.DarkBlue => "navy",
            ConsoleColor.DarkGreen => "green",
            ConsoleColor.DarkCyan => "teal",
            ConsoleColor.DarkRed => "maroon",
            ConsoleColor.DarkMagenta => "purple",
            ConsoleColor.DarkYellow => "olive",
            ConsoleColor.Gray => "silver",
            ConsoleColor.DarkGray => "grey",
            ConsoleColor.Blue => "blue",
            ConsoleColor.Green => "lime",
            ConsoleColor.Cyan => "aqua",
            ConsoleColor.Red => "red",
            ConsoleColor.Magenta => "fuchsia",
            ConsoleColor.Yellow => "yellow",
            ConsoleColor.White => "white",
            _ => "white"
        };

        // Batching control for message deduplication
        public static void StartBatching() => IsBatching = true;

        public static List<string> StopBatching()
        {
            IsBatching = false;
            var messages = MessageBuffer.ToList();
            MessageBuffer.Clear();
            return messages;
        }

        #endregion
    }
}