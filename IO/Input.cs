using Spectre.Console;
using text_survival.Core;
using text_survival.UI;

namespace text_survival.IO
{
    public static class Input
    {
        public static string Read()
        {
            string? input = "";
            if (Output.TestMode)
            {
                TestModeIO.SignalReady();
                input = TestModeIO.ReadInput();
            }
            else if (Config.io == Config.IOType.Console)
            {
                input = Console.ReadLine();
            }
            else if (Config.io == Config.IOType.Web)
            {
                throw new NotImplementedException();
            }

            return input ?? "";
        }

        public static ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            if (Output.TestMode)
            {
                TestModeIO.SignalReady();
                string input = TestModeIO.ReadInput();

                if (string.IsNullOrEmpty(input))
                    return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);

                char keyChar = input[0];
                ConsoleKey key = char.IsLetter(keyChar)
                    ? (ConsoleKey)Enum.Parse(typeof(ConsoleKey), keyChar.ToString().ToUpper())
                    : ConsoleKey.Enter;
                return new ConsoleKeyInfo(keyChar, key, false, false, false);
            }
            else if (Config.io == Config.IOType.Console)
            {
                return Console.ReadKey(intercept);
            }
            else if (Config.io == Config.IOType.Web)
            {
                throw new NotImplementedException();
            }

            return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        }

        public static int ReadInt(string prompt = "Enter a number:")
        {
            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[{prompt}]\n");
                while (true)
                {
                    string? input = Read();
                    if (int.TryParse(input, out int result))
                        return result;
                    GameDisplay.AddNarrative("Invalid input. Please enter a number.");
                }
            }

            return AnsiConsole.Ask<int>(prompt);
        }

        public static int ReadInt(int low, int high, string prompt = "")
        {
            string displayPrompt = string.IsNullOrEmpty(prompt)
                ? $"Enter a number ({low}-{high}):"
                : prompt;

            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[{displayPrompt}]\n");
                while (true)
                {
                    string? input = Read();
                    if (int.TryParse(input, out int result) && result >= low && result <= high)
                        return result;
                    GameDisplay.AddNarrative($"Invalid input. Please enter a number between {low} and {high}.");
                }
            }

            return AnsiConsole.Prompt(
                new TextPrompt<int>(displayPrompt)
                    .Validate(n => n >= low && n <= high
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"Enter a number between {low} and {high}")));
        }

        public static bool ReadYesNo(string prompt = "Continue?")
        {
            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[{prompt}]\n");
                while (true)
                {
                    string? input = Read().Trim().ToLower();
                    if (input == "y" || input == "yes")
                        return true;
                    if (input == "n" || input == "no")
                        return false;
                    GameDisplay.AddNarrative("Invalid input. Please enter 'y' or 'n'.");
                }
            }

            return AnsiConsole.Confirm(prompt, defaultValue: true);
        }

        /// <summary>
        /// Prompt for yes/no with a custom question
        /// </summary>
        public static bool Confirm(string prompt, bool defaultValue = true)
        {
            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[Confirm: {prompt}]\n");
                return ReadYesNo();
            }

            return AnsiConsole.Confirm(prompt, defaultValue);
        }

        /// <summary>
        /// Arrow-key selection from a list (replaces numbered menu input)
        /// </summary>
        public static T Select<T>(string prompt, IEnumerable<T> choices) where T : notnull
        {
            if (Output.TestMode)
            {
                var list = choices.ToList();
                TestModeIO.WriteOutput($"[Select: {prompt}]\n");
                for (int i = 0; i < list.Count; i++)
                    TestModeIO.WriteOutput($"  {i + 1}. {list[i]}\n");

                TestModeIO.SignalReady();
                string input = TestModeIO.ReadInput();
                if (int.TryParse(input, out int idx) && idx >= 1 && idx <= list.Count)
                    return list[idx - 1];
                return list[0];
            }

            return AnsiConsole.Prompt(
                new SelectionPrompt<T>()
                    .Title(prompt)
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                    .AddChoices(choices));
        }

        /// <summary>
        /// Arrow-key selection with custom display converter
        /// </summary>
        public static T Select<T>(string prompt, IEnumerable<T> choices, Func<T, string> displaySelector) where T : notnull
        {
            if (Output.TestMode)
            {
                var list = choices.ToList();
                TestModeIO.WriteOutput($"[Select: {prompt}]\n");
                for (int i = 0; i < list.Count; i++)
                    TestModeIO.WriteOutput($"  {i + 1}. {displaySelector(list[i])}\n");

                TestModeIO.SignalReady();
                string input = TestModeIO.ReadInput();
                if (int.TryParse(input, out int idx) && idx >= 1 && idx <= list.Count)
                    return list[idx - 1];
                return list[0];
            }

            return AnsiConsole.Prompt(
                new SelectionPrompt<T>()
                    .Title(prompt)
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                    .UseConverter(displaySelector)
                    .AddChoices(choices));
        }

        /// <summary>
        /// Selection with optional cancel - returns null if cancelled
        /// </summary>
        public static T? SelectOrCancel<T>(string prompt, IEnumerable<T> choices, string cancelText = "Cancel") where T : class
        {
            if (Output.TestMode)
            {
                var list = choices.ToList();
                TestModeIO.WriteOutput($"[Select: {prompt}]\n");
                TestModeIO.WriteOutput($"  0. {cancelText}\n");
                for (int i = 0; i < list.Count; i++)
                    TestModeIO.WriteOutput($"  {i + 1}. {list[i]}\n");

                TestModeIO.SignalReady();
                string input = TestModeIO.ReadInput();
                if (int.TryParse(input, out int idx))
                {
                    if (idx == 0) return null;
                    if (idx >= 1 && idx <= list.Count) return list[idx - 1];
                }
                return null;
            }

            // Create wrapper type for cancel option
            var wrappedChoices = choices
                .Select(c => (Value: c, Display: c.ToString() ?? ""))
                .Prepend((Value: default(T)!, Display: $"[grey]{cancelText}[/]"))
                .ToList();

            var result = AnsiConsole.Prompt(
                new SelectionPrompt<(T? Value, string Display)>()
                    .Title(prompt)
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                    .UseConverter(x => x.Display)
                    .AddChoices(wrappedChoices));

            return result.Value;
        }

        /// <summary>
        /// Multi-select with checkboxes
        /// </summary>
        public static List<T> MultiSelect<T>(string prompt, IEnumerable<T> choices) where T : notnull
        {
            if (Output.TestMode)
            {
                var list = choices.ToList();
                TestModeIO.WriteOutput($"[MultiSelect: {prompt}]\n");
                for (int i = 0; i < list.Count; i++)
                    TestModeIO.WriteOutput($"  {i + 1}. {list[i]}\n");

                TestModeIO.SignalReady();
                string input = TestModeIO.ReadInput();
                // Parse comma-separated indices
                var indices = input.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out int i) ? i : -1)
                    .Where(i => i >= 1 && i <= list.Count)
                    .Select(i => list[i - 1]);
                return indices.ToList();
            }

            return AnsiConsole.Prompt(
                new MultiSelectionPrompt<T>()
                    .Title(prompt)
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                    .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                    .AddChoices(choices))
                .ToList();
        }

        /// <summary>
        /// Text input with prompt
        /// </summary>
        public static string Ask(string prompt)
        {
            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[Ask: {prompt}]\n");
                TestModeIO.SignalReady();
                return TestModeIO.ReadInput();
            }

            return AnsiConsole.Ask<string>(prompt);
        }

        /// <summary>
        /// Text input with default value
        /// </summary>
        public static string Ask(string prompt, string defaultValue)
        {
            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[Ask: {prompt} (default: {defaultValue})]\n");
                TestModeIO.SignalReady();
                var input = TestModeIO.ReadInput();
                return string.IsNullOrEmpty(input) ? defaultValue : input;
            }

            return AnsiConsole.Prompt(
                new TextPrompt<string>(prompt)
                    .DefaultValue(defaultValue)
                    .ShowDefaultValue());
        }

        /// <summary>
        /// Numeric input with prompt
        /// </summary>
        public static int AskInt(string prompt)
        {
            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[AskInt: {prompt}]\n");
                return ReadInt();
            }

            return AnsiConsole.Ask<int>(prompt);
        }

        /// <summary>
        /// Numeric input with range validation
        /// </summary>
        public static int AskInt(string prompt, int min, int max)
        {
            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[AskInt: {prompt} ({min}-{max})]\n");
                return ReadInt(min, max);
            }

            return AnsiConsole.Prompt(
                new TextPrompt<int>(prompt)
                    .Validate(n => n >= min && n <= max
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"Enter a number between {min} and {max}")));
        }

        public static void WaitForKey(string message = "Press any key to continue...")
        {
            if (Output.TestMode)
            {
                TestModeIO.WriteOutput($"[{message}]\n");
                TestModeIO.SignalReady();
                TestModeIO.ReadInput();
                return;
            }

            AnsiConsole.MarkupLine($"[grey]{message}[/]");
            AnsiConsole.Console.Input.ReadKey(true);
        }
    }
}