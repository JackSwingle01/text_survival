using text_survival.Items;

namespace text_survival.Actors.Animals;

/// <summary>
/// Diet types that map to resource categories animals consume while grazing.
/// </summary>
public enum AnimalDiet
{
    /// <summary>
    /// Omnivore diet (bears): fungi, berries, nuts, roots, honey.
    /// Competes significantly with player foraging.
    /// </summary>
    Omnivore,

    /// <summary>
    /// Browser diet (caribou, megaloceros): lichens, branches/twigs.
    /// Partial competition with player foraging.
    /// </summary>
    Browser,

    /// <summary>
    /// Grazer diet (bison): grass only.
    /// Minimal competition - uses Tinder as grass proxy.
    /// </summary>
    Grazer,

    /// <summary>
    /// Carnivore diet (wolves): no foraging competition.
    /// </summary>
    Carnivore
}

/// <summary>
/// Extension methods for AnimalDiet to get consumed resources.
/// </summary>
public static class AnimalDietExtensions
{
    /// <summary>
    /// Get the resources consumed by this diet type.
    /// </summary>
    public static Resource[] GetConsumedResources(this AnimalDiet diet)
    {
        return diet switch
        {
            AnimalDiet.Omnivore =>
            [
                // Fungi
                Resource.BirchPolypore,
                Resource.Chaga,
                Resource.Amadou,
                // Berries
                Resource.Berries,
                Resource.RoseHip,
                Resource.JuniperBerry,
                // Other forage
                Resource.Nuts,
                Resource.Roots,
                Resource.Honey
            ],

            AnimalDiet.Browser =>
            [
                // Lichens
                Resource.Usnea,
                // Branches/twigs (represented by sticks)
                Resource.Stick,
                // Some fungi
                Resource.BirchPolypore
            ],

            AnimalDiet.Grazer =>
            [
                // Grass represented by tinder (dried plant matter)
                Resource.Tinder
            ],

            AnimalDiet.Carnivore => [], // No foraging

            _ => []
        };
    }

    /// <summary>
    /// Get diet for an animal type.
    /// </summary>
    public static AnimalDiet GetDietForAnimal(string animalType)
    {
        return animalType.ToLower() switch
        {
            "bear" or "cave bear" => AnimalDiet.Omnivore,
            "caribou" or "megaloceros" => AnimalDiet.Browser,
            "bison" or "steppe bison" => AnimalDiet.Grazer,
            "wolf" => AnimalDiet.Carnivore,
            _ => AnimalDiet.Carnivore // Default to no foraging competition
        };
    }
}
