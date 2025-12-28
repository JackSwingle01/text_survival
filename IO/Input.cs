using text_survival.Actions;
using text_survival.Web;

namespace text_survival.IO
{
    public static class Input
    {
        /// <summary>
        /// Selection with game context - routes to web UI if session active.
        /// </summary>
        public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices) where T : notnull
        {
            return WebIO.Select(ctx, prompt, choices, c => c.ToString()!);
        }

        /// <summary>
        /// Confirmation with game context.
        /// </summary>
        public static bool Confirm(GameContext ctx, string prompt, bool defaultValue = true)
        {
            return WebIO.Confirm(ctx, prompt);
        }

        /// <summary>
        /// Read integer with range and game context - routes to web UI if session active.
        /// </summary>
        public static int ReadInt(GameContext ctx, string prompt, int min, int max)
        {
            return WebIO.ReadInt(ctx, prompt, min, max);
        }
    }
}