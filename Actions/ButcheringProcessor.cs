using text_survival.Actors.Animals;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Converts killed animals into usable resources (meat, bone, hide, sinew).
/// </summary>
public static class ButcheringProcessor
{
    /// <summary>
    /// Butcher a killed animal into meat and other resources.
    /// </summary>
    /// <param name="animal">The dead animal to butcher</param>
    /// <returns>FoundResources containing meat, bone, hide, and sinew yields</returns>
    public static FoundResources Butcher(Animal animal)
    {
        var result = new FoundResources();
        double bodyWeight = animal.Body.WeightKG;
        string animalName = animal.Name.ToLower();

        // Meat: ~40% of body weight
        AddMeat(result, bodyWeight * 0.4, animalName);

        // Bone: ~15% of body weight (2-4 bones depending on size)
        AddBones(result, bodyWeight * 0.15, animalName);

        // Hide: ~10% of body weight (1 hide)
        AddHide(result, bodyWeight * 0.10, animalName);

        // Sinew: ~5% of body weight (tendons for cordage)
        AddSinew(result, bodyWeight * 0.05, animalName);

        return result;
    }

    /// <summary>
    /// Butcher without a knife - reduced yield, no hide/sinew (can't process properly).
    /// </summary>
    public static FoundResources ButcherWithoutKnife(Animal animal)
    {
        var result = new FoundResources();
        double bodyWeight = animal.Body.WeightKG;
        string animalName = animal.Name.ToLower();

        // Only ~20% meat yield when tearing by hand
        AddMeat(result, bodyWeight * 0.2, animalName);

        // Can still crack bones
        AddBones(result, bodyWeight * 0.10, animalName);

        // No hide or sinew - need cutting tool

        return result;
    }

    private static void AddMeat(FoundResources result, double totalKg, string animalName)
    {
        // Split into portions (roughly 0.5-1.5kg each)
        while (totalKg > 0.3)
        {
            double portionSize = Math.Min(totalKg, 0.5 + Random.Shared.NextDouble());
            result.AddRawMeat(portionSize, $"{animalName} meat");
            totalKg -= portionSize;
        }

        // Add any remaining scraps
        if (totalKg > 0.1)
        {
            result.AddRawMeat(totalKg, $"scraps of {animalName} meat");
        }
    }

    private static void AddBones(FoundResources result, double totalKg, string animalName)
    {
        // Split into 2-4 bones depending on total weight
        int boneCount = totalKg switch
        {
            < 0.3 => 1,
            < 0.8 => 2,
            < 1.5 => 3,
            _ => 4
        };

        double perBone = totalKg / boneCount;
        for (int i = 0; i < boneCount; i++)
        {
            result.AddBone(perBone, $"{animalName} bone");
        }
    }

    private static void AddHide(FoundResources result, double totalKg, string animalName)
    {
        if (totalKg > 0.1)
        {
            result.AddHide(totalKg, $"{animalName} hide");
        }
    }

    private static void AddSinew(FoundResources result, double totalKg, string animalName)
    {
        if (totalKg > 0.05)
        {
            result.AddSinew(totalKg, $"{animalName} sinew");
        }
    }
}
