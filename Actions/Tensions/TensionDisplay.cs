namespace text_survival.Actions.Tensions;

/// <summary>
/// Single source of truth for tension display information.
/// Maps tension types to user-visible messages and UI categories.
/// </summary>
public static class TensionDisplay
{
    public record TensionInfo(string MessageTemplate, string Category);

    private static readonly Dictionary<string, TensionInfo> TensionLookup = new()
    {
        // Threats
        ["Stalked"] = new("A {animalType} is following you", "threat"),
        ["Hunted"] = new("You are being hunted", "threat"),
        ["PackNearby"] = new("A pack is nearby", "threat"),
        ["DeadlyCold"] = new("The cold is killing you", "threat"),

        // Opportunities
        ["WoundedPrey"] = new("A blood trail leads away", "opportunity"),
        ["HerdNearby"] = new("A herd of {animalType} passes nearby", "opportunity"),
        ["TrapLineActive"] = new("Snares are set", "opportunity"),

        // Conditions
        ["FeverRising"] = new("Fever burns in your blood", "condition"),
        ["WoundUntreated"] = new("Your wound needs attention", "condition"),
        ["Disturbed"] = new("Dark thoughts linger", "condition"),

        // Neutral
        ["ClaimedTerritory"] = new("Something has claimed this place", "neutral"),
        ["FoodScentStrong"] = new("The scent of meat carries", "neutral")
    };

    public static string GetMessage(ActiveTension tension)
    {
        if (TensionLookup.TryGetValue(tension.Type, out var info))
        {
            var message = info.MessageTemplate;
            if (tension.AnimalType != null)
            {
                message = message.Replace("{animalType}", tension.AnimalType.ToLower());
            }
            else
            {
                // Fallback for missing animal type
                message = message.Replace("A {animalType}", "Something");
                message = message.Replace("A herd of {animalType}", "A herd");
            }
            return message;
        }

        // Fallback for unknown tension types
        return tension.Description ?? tension.Type;
    }

    public static string GetCategory(string tensionType)
    {
        if (TensionLookup.TryGetValue(tensionType, out var info))
        {
            return info.Category;
        }
        return "neutral";
    }
}
