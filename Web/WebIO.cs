using text_survival.Actions;
using text_survival.Actions.Variants;
using text_survival.Crafting;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Web.Dto;

namespace text_survival.Web;

public static class WebIO
{
    private const string NotImplementedMessage = "WebIO is being replaced by stateless REST API. Port this logic.";

    // Rendering methods
    public static void Render(GameContext ctx, string statusText) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void RenderWithDuration(GameContext ctx, string statusText, int elapsedMinutes) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void RenderCrafting(GameContext ctx, NeedCraftingSystem crafting, string headerTitle) =>
        throw new NotImplementedException(NotImplementedMessage);

    // Event methods
    public static void RenderEvent(GameContext ctx, EventDto data) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void WaitForEventContinue(GameContext ctx) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void ClearEvent(GameContext ctx) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static string GenerateSemanticId(string label, int index) =>
        throw new NotImplementedException(NotImplementedMessage);

    // Input methods - used by Input.cs facade
    public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices, Func<T, string> formatter) where T : notnull =>
        throw new NotImplementedException(NotImplementedMessage);

    public static bool Confirm(GameContext ctx, string prompt) =>
        throw new NotImplementedException(NotImplementedMessage);

    // Hunt methods
    public static string WaitForHuntChoice(GameContext ctx, HuntDto data) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void WaitForHuntContinue(GameContext ctx) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void RenderHunt(GameContext ctx, HuntDto data) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void ClearHunt(GameContext ctx) =>
        throw new NotImplementedException(NotImplementedMessage);

    // Encounter methods
    public static string WaitForEncounterChoice(GameContext ctx, EncounterDto data) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void RenderEncounter(GameContext ctx, EncounterDto data) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void WaitForEncounterContinue(GameContext ctx) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void ClearEncounter(GameContext ctx) =>
        throw new NotImplementedException(NotImplementedMessage);

    // Combat methods
    public static string WaitForCombatChoice(GameContext ctx, CombatDto data) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void WaitForCombatContinue(GameContext ctx) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void RenderCombat(GameContext ctx, CombatDto data) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void ClearCombat(GameContext ctx) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void ShowWorkResult(GameContext ctx, string title, string message, List<string> collected) =>
        throw new NotImplementedException(NotImplementedMessage);


    // Butcher methods
    public static string? SelectButcherOptions(GameContext ctx, ButcherDto data) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static void RunTransferUI(GameContext ctx, Inventory storage, string name) =>
        throw new NotImplementedException(NotImplementedMessage);

    public static (ForageFocus?, int) SelectForageOptions(GameContext ctx, ForageDto data) =>
        throw new NotImplementedException(NotImplementedMessage);
}
