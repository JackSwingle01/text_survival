using text_survival.Actors.NPCs;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Converts killed animals into usable resources (meat, hides).
/// </summary>
public static class ButcheringProcessor
{
    /// <summary>
    /// Butcher a killed animal into meat and other resources.
    /// </summary>
    /// <param name="animal">The dead animal to butcher</param>
    /// <returns>FoundResources containing meat yield</returns>
    public static FoundResources Butcher(Animal animal)
    {
        var result = new FoundResources();

        // ~40% of body weight is usable meat
        double meatYieldKg = animal.Body.WeightKG * 0.4;

        // Split into portions (roughly 0.5-1.5kg each)
        while (meatYieldKg > 0.3)
        {
            double portionSize = Math.Min(meatYieldKg, 0.5 + Random.Shared.NextDouble());
            result.AddRawMeat(portionSize, $"{animal.Name.ToLower()} meat");
            meatYieldKg -= portionSize;
        }

        // Add any remaining scraps
        if (meatYieldKg > 0.1)
        {
            result.AddRawMeat(meatYieldKg, $"scraps of {animal.Name.ToLower()} meat");
        }

        return result;
    }
}
