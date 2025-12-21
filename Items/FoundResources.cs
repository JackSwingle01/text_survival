namespace text_survival.Items;

/// <summary>
/// Transfer object for resources found during foraging, harvesting, hunting, etc.
/// Returned by resource generation features, consumed by Inventory.Add().
/// </summary>
public class FoundResources
{
    // Fire supplies - each entry is item weight in kg
    public List<double> Logs { get; set; } = [];
    public List<double> Sticks { get; set; } = [];
    public List<double> Tinder { get; set; } = [];

    // Food - each entry is portion weight in kg
    public List<double> CookedMeat { get; set; } = [];
    public List<double> RawMeat { get; set; } = [];
    public List<double> Berries { get; set; } = [];

    // Water in liters
    public double WaterLiters { get; set; }

    // Crafting materials - each entry is item weight in kg
    public List<double> Stone { get; set; } = [];
    public List<double> Bone { get; set; } = [];
    public List<double> Hide { get; set; } = [];
    public List<double> PlantFiber { get; set; } = [];
    public List<double> Sinew { get; set; } = [];

    // Discrete items
    public List<Tool> Tools { get; set; } = [];
    public List<Item> Special { get; set; } = [];

    // Narrative descriptions for expedition log ("a heavy log", "some berries")
    public List<string> Descriptions { get; set; } = [];

    // Convenience check
    public bool IsEmpty =>
        Logs.Count == 0 &&
        Sticks.Count == 0 &&
        Tinder.Count == 0 &&
        CookedMeat.Count == 0 &&
        RawMeat.Count == 0 &&
        Berries.Count == 0 &&
        WaterLiters == 0 &&
        Stone.Count == 0 &&
        Bone.Count == 0 &&
        Hide.Count == 0 &&
        PlantFiber.Count == 0 &&
        Sinew.Count == 0 &&
        Tools.Count == 0 &&
        Special.Count == 0;

    public double TotalWeightKg =>
        Logs.Sum() + Sticks.Sum() + Tinder.Sum() +
        CookedMeat.Sum() + RawMeat.Sum() + Berries.Sum() +
        WaterLiters +
        Stone.Sum() + Bone.Sum() + Hide.Sum() + PlantFiber.Sum() + Sinew.Sum() +
        Tools.Sum(t => t.Weight) +
        Special.Sum(i => i.Weight);

    // Builder methods for fluent resource creation

    public FoundResources AddLog(double weightKg, string? description = null)
    {
        Logs.Add(weightKg);
        Descriptions.Add(description ?? DescribeLog(weightKg));
        return this;
    }

    public FoundResources AddStick(double weightKg, string? description = null)
    {
        Sticks.Add(weightKg);
        Descriptions.Add(description ?? DescribeStick(weightKg));
        return this;
    }

    public FoundResources AddTinder(double weightKg, string? description = null)
    {
        Tinder.Add(weightKg);
        Descriptions.Add(description ?? "some dry tinder");
        return this;
    }

    public FoundResources AddBerries(double weightKg, string? description = null)
    {
        Berries.Add(weightKg);
        Descriptions.Add(description ?? DescribeBerries(weightKg));
        return this;
    }

    public FoundResources AddRawMeat(double weightKg, string? description = null)
    {
        RawMeat.Add(weightKg);
        Descriptions.Add(description ?? DescribeMeat(weightKg));
        return this;
    }

    public FoundResources AddWater(double liters, string? description = null)
    {
        WaterLiters += liters;
        Descriptions.Add(description ?? $"{liters:F1}L of water");
        return this;
    }

    public FoundResources AddTool(Tool tool, string? description = null)
    {
        Tools.Add(tool);
        Descriptions.Add(description ?? $"a {tool.Name}");
        return this;
    }

    public FoundResources AddSpecial(Item item, string? description = null)
    {
        Special.Add(item);
        Descriptions.Add(description ?? $"a {item.Name}");
        return this;
    }

    public FoundResources AddStone(double weightKg, string? description = null)
    {
        Stone.Add(weightKg);
        Descriptions.Add(description ?? DescribeStone(weightKg));
        return this;
    }

    public FoundResources AddBone(double weightKg, string? description = null)
    {
        Bone.Add(weightKg);
        Descriptions.Add(description ?? DescribeBone(weightKg));
        return this;
    }

    public FoundResources AddHide(double weightKg, string? description = null)
    {
        Hide.Add(weightKg);
        Descriptions.Add(description ?? DescribeHide(weightKg));
        return this;
    }

    public FoundResources AddPlantFiber(double weightKg, string? description = null)
    {
        PlantFiber.Add(weightKg);
        Descriptions.Add(description ?? "a bundle of plant fibers");
        return this;
    }

    public FoundResources AddSinew(double weightKg, string? description = null)
    {
        Sinew.Add(weightKg);
        Descriptions.Add(description ?? "some animal sinew");
        return this;
    }

    // Description helpers
    private static string DescribeLog(double kg) => kg switch
    {
        < 1.5 => "a small log",
        < 2.5 => "a decent log",
        < 3.5 => "a heavy log",
        _ => "a massive log"
    };

    private static string DescribeStick(double kg) => kg switch
    {
        < 0.2 => "some twigs",
        < 0.4 => "a stick",
        _ => "a thick branch"
    };

    private static string DescribeBerries(double kg) => kg switch
    {
        < 0.1 => "a handful of berries",
        < 0.3 => "some berries",
        _ => "a good amount of berries"
    };

    private static string DescribeMeat(double kg) => kg switch
    {
        < 0.3 => "a small cut of meat",
        < 0.6 => "a portion of meat",
        _ => "a large piece of meat"
    };

    private static string DescribeStone(double kg) => kg switch
    {
        < 0.2 => "a small stone",
        < 0.4 => "a good-sized stone",
        _ => "a heavy stone"
    };

    private static string DescribeBone(double kg) => kg switch
    {
        < 0.2 => "a small bone",
        < 0.5 => "a sturdy bone",
        _ => "a large bone"
    };

    private static string DescribeHide(double kg) => kg switch
    {
        < 0.5 => "a small hide",
        < 1.0 => "a decent hide",
        _ => "a large hide"
    };
}
