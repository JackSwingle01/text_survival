using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Items;
using text_survival.UI;

public static class ButcherRunner
{
    public static Inventory ButcherAnimal(Animal animal, GameContext ctx)
    {
        Inventory result;

        if (ctx.Inventory.HasCuttingTool)
            result = Butcher(animal);
        else
        {
            GameDisplay.AddWarning(ctx, "Without a cutting tool, you tear what meat you can by hand...");
            result = ButcherWithoutKnife(animal);
        }

        // Manipulation impairment reduces yield (-20%)
        var manipulation = ctx.player.GetCapacities().Manipulation;
        if (AbilityCalculator.IsManipulationImpaired(manipulation))
        {
            GameDisplay.AddWarning(ctx, "Your unsteady hands waste some of the meat.");
            result.ApplyMultiplier(0.8);
        }

        return result;
    }

    /// <summary>
    /// Butcher a killed animal into meat and other resources.
    /// </summary>
    /// <param name="animal">The dead animal to butcher</param>
    /// <returns>Inventory containing meat, bone, hide, and sinew yields</returns>
    private static Inventory Butcher(Animal animal)
    {
        var result = new Inventory();
        double bodyWeight = animal.Body.WeightKG;
        string animalName = animal.Name.ToLower();

        // Special handling for megafauna - massive animals yield realistic amounts
        // Player can only butcher what they can process in the field
        if (animalName.Contains("mammoth"))
        {
            // Mammoth yields: trophy amounts, not proportional to body weight
            AddMeat(result, 50, animalName);  // 50kg meat (multiple trips or caching required)
            AddBones(result, 4, animalName);  // 4kg bone
            AddIvory(result, 4);              // 4kg ivory (2 tusks)
            AddMammothHide(result, 15);       // 15kg mammoth hide (trophy material)
            AddSinew(result, 4, animalName);  // 4kg sinew
            AddFat(result, 6, animalName);    // 6kg fat
        }
        else
        {
            // Normal animals: yields proportional to body weight
            // Meat: ~40% of body weight
            AddMeat(result, bodyWeight * 0.4, animalName);

            // Bone: ~15% of body weight (2-4 bones depending on size)
            AddBones(result, bodyWeight * 0.15, animalName);

            // Hide: ~10% of body weight (1 hide)
            AddHide(result, bodyWeight * 0.10, animalName);

            // Sinew: ~5% of body weight (tendons for cordage)
            AddSinew(result, bodyWeight * 0.05, animalName);

            // Fat: ~8% of body weight (for rendering into tallow)
            AddFat(result, bodyWeight * 0.08, animalName);
        }

        return result;
    }

    /// <summary>
    /// Butcher without a knife - reduced yield, no hide/sinew (can't process properly).
    /// </summary>
    private static Inventory ButcherWithoutKnife(Animal animal)
    {
        var result = new Inventory();
        double bodyWeight = animal.Body.WeightKG;
        string animalName = animal.Name.ToLower();

        // Only ~20% meat yield when tearing by hand
        AddMeat(result, bodyWeight * 0.2, animalName);

        // Can still crack bones
        AddBones(result, bodyWeight * 0.10, animalName);

        // No hide or sinew - need cutting tool

        return result;
    }

    private static void AddMeat(Inventory result, double totalKg, string animalName)
    {
        // Split into portions (roughly 0.5-1.5kg each)
        while (totalKg > 0.3)
        {
            double portionSize = Math.Min(totalKg, 0.5 + Random.Shared.NextDouble());
            result.Add(Resource.RawMeat, portionSize);
            totalKg -= portionSize;
        }

        // Add any remaining scraps
        if (totalKg > 0.1)
        {
            result.Add(Resource.RawMeat, totalKg);
        }
    }

    private static void AddBones(Inventory result, double totalKg, string animalName)
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
            result.Add(Resource.Bone, perBone);
        }
    }

    private static void AddHide(Inventory result, double totalKg, string animalName)
    {
        if (totalKg > 0.1)
        {
            result.Add(Resource.Hide, totalKg);
        }
    }

    private static void AddMammothHide(Inventory result, double totalKg)
    {
        // Mammoth hide is trophy material - split into large pieces
        // 15kg total, split into 2-3 pieces for easier handling
        int pieces = totalKg switch
        {
            < 5 => 1,
            < 10 => 2,
            _ => 3
        };

        double perPiece = totalKg / pieces;
        for (int i = 0; i < pieces; i++)
        {
            result.Add(Resource.MammothHide, perPiece);
        }
    }

    private static void AddIvory(Inventory result, double totalKg)
    {
        // Ivory from mammoth tusks - split into 2 tusks
        double perTusk = totalKg / 2;
        result.Add(Resource.Ivory, perTusk);
        result.Add(Resource.Ivory, perTusk);
    }

    private static void AddSinew(Inventory result, double totalKg, string animalName)
    {
        if (totalKg > 0.05)
        {
            result.Add(Resource.Sinew, totalKg);
        }
    }

    private static void AddFat(Inventory result, double totalKg, string animalName)
    {
        // Fat comes in chunks - split into 2-3 pieces
        if (totalKg < 0.1) return;

        int chunks = totalKg switch
        {
            < 0.3 => 1,
            < 0.6 => 2,
            _ => 3
        };

        double perChunk = totalKg / chunks;
        for (int i = 0; i < chunks; i++)
        {
            result.Add(Resource.RawFat, perChunk);
        }
    }
}