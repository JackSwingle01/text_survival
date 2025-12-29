using text_survival.Web;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles cooking and melting actions.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class CookingHandler
{
    public static void CookMelt(GameContext ctx)
    {
        // Use web-based cooking UI
        WebIO.RunCookingUI(ctx);
    }
}
