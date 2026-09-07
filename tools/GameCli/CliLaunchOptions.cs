using System.Globalization;
using text_survival.Actions;
using text_survival.Persistence;

namespace text_survival.GameCli;

public sealed record CliLaunchOptions(string SessionId, int? Seed = null, string? Load = null)
{
    public static void ValidateSession(string id)
    {
        if (!id.StartsWith("cli_", StringComparison.Ordinal) || id.Length > 100 ||
            id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-'))
            throw new ArgumentException("Session must start with cli_, be at most 100 characters, and contain only ASCII letters, digits, underscores or hyphens.");
    }

    public static CliLaunchOptions Parse(string[] args)
    {
        string sessionId = "cli_" + Guid.NewGuid().ToString("N");
        int? seed = null;
        string? load = null;
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing option value.");
            switch (args[i])
            {
                case "--seed": seed = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--session": sessionId = Next(); break;
                case "--load": load = Next(); sessionId = load; break;
                default: throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }
        ValidateSession(sessionId);
        if (load != null && sessionId != load) throw new ArgumentException("--load cannot be combined with a different --session.");
        if (load != null && seed != null) throw new ArgumentException("--seed cannot be combined with --load.");
        return new(sessionId, seed, load);
    }

    public GameContext CreateContext()
    {
        ValidateSession(SessionId);
        if (Load != null)
        {
            var (loaded, error) = SaveManager.Load(Load);
            return loaded ?? throw new ArgumentException(error ?? "CLI save does not exist.");
        }
        if (SaveManager.HasSaveFile(SessionId)) throw new ArgumentException("Session already exists; use --load or a different --session.");
        return GameContext.CreateNewGame(Seed);
    }
}
